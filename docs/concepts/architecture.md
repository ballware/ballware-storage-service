# Architecture

The Storage Service is a .NET 8 ASP.NET Core minimal-API service that stores **file blobs** in a
provider backend (Azure Blob Storage or MinIO) and their **metadata rows** in a relational database
(EF Core on PostgreSQL or SQL Server). Two logical API surfaces — User API and Service API — share
the same resources but differ in how the tenant is resolved and which authorization policy applies
(see [tenancy-and-authorization.md](tenancy-and-authorization.md)).

## Projects and responsibilities

| Project | Responsibility |
| --- | --- |
| `src/Ballware.Storage.Service` | Web host: `Program.cs` (configuration sources, Kestrel), `Startup.cs` (auth, policies, JSON, Swagger, routing), `appsettings*.json`, `Dockerfile` |
| `src/Ballware.Storage.Api` | HTTP endpoint definitions for attachments and temporaries (`Endpoints/AttachmentEndpoint.cs`, `Endpoints/TemporaryEndpoint.cs`) |
| `src/Ballware.Storage.Data` | Public DTOs (`Public/Attachment.cs`, `Public/Temporary.cs`), persistables (`Persistables/Blob.cs`, `…/Attachment.cs`, `…/Temporary.cs`), repository interfaces |
| `src/Ballware.Storage.Data.Ef` | EF Core plumbing: repository base classes, model customizer (indexes, UTC conversion), mapping profile |
| `src/Ballware.Storage.Data.Ef.Postgres` | Npgsql wiring, migrations, `InitializationWorker` (hosted EF migration runner) |
| `src/Ballware.Storage.Data.Ef.SqlServer` | SQL Server wiring and migrations |
| `src/Ballware.Storage.Provider.Azure` | Azure Blob Storage provider |
| `src/Ballware.Storage.Provider.Minio` | MinIO (S3-compatible) provider |
| `src/Ballware.Storage.Provider` | Shared provider contracts project (metadata interfaces live in `Ballware.Storage.Metadata`) |
| `src/Ballware.Storage.Metadata` | Provider interfaces `IAttachmentStorageProvider`, `ITemporaryStorageProvider` |
| `src/Ballware.Storage.Jobs` | Quartz registration and the `TemporaryCleanupJob` |
| `src/Ballware.Storage.Service.Client` | NSwag-generated `StorageServiceClient` (built from the `service` OpenAPI document in CI, `.github/workflows/publish-packages.yml:49-62`) |

Dependency direction: `Service` → `Api` → (`Metadata` contracts, `Data` repository interfaces) →
`Data.Ef*` implementations, `Provider.*` implementations, `Jobs`. The shared tenancy/repository
behavior comes from the private `Ballware.Shared.* 0.0.2` packages (GitHub Packages).

## Configuration order

`Program.cs` clears the default sources and rebuilds them in this order (`src/Ballware.Storage.Service/Program.cs:18-22`):

1. `appsettings.json`
2. `appsettings.{Environment}.json` (e.g. `Development`, `Swagger`, `IntegrationTesting`)
3. `appsettings.local.json` (developer-local, git-ignored, **overrides** the environment file)
4. Environment variables (`AddEnvironmentVariables` — highest precedence)

This order matters for snapshot generation — see [openapi/README.md](../openapi/README.md) — and lets
containers override everything without `appsettings.local.json`.

## JSON conventions

All serializers are configured **PascalCase** (no camel-casing):

- `JsonOptions` / HTTP `JsonOptions` with `PropertyNamingPolicy = null`
  (`src/Ballware.Storage.Service/Startup.cs:136-144`)
- MVC core with `AddJsonOptions(… PropertyNamingPolicy = null)` **and**
  `AddNewtonsoftJson(… DefaultContractResolver)` (`Startup.cs:148-151`) — Newtonsoft support is also
  wired into Swagger generation via `AddSwaggerGenNewtonsoftSupport()` (`Startup.cs:219`).

Response fields therefore appear as `Id`, `Entity`, `OwnerId`, `FileName`, `ContentType`, `FileSize`,
`StoragePath` (see [storage-model.md](storage-model.md)).

## Pipeline

`Startup.InitializeServices()` wires, in order (`Startup.cs:32-221`):

- options binding with validate-on-start for `Authorization`, `Meta`, `Storage`, `Trigger`,
  `Swagger`, and optionally `AzureStorage`/`MinioStorage` (`Startup.cs:45-77`); a missing
  `Authorization`/`Meta`/`Storage` section aborts startup with `ConfigurationException`
  (`Startup.cs:79-82`)
- JWT bearer authentication against `Authorization.Authority`/`Audience`, with
  `MapInboundClaims = false` (`Startup.cs:89-103`)
- the two authorization policies `storageApi` / `serviceApi` (`Startup.cs:105-121`)
- optional CORS default policy from the `Cors` section (`Startup.cs:123-134`)
- PascalCase JSON (above), AutoMapper mappings, `Ballware.Shared.Authorization` utilities with the
  configured `TenantClaim`/`UserIdClaim`/`RightClaim` (`Startup.cs:155-160`)
- EF provider selection (`Meta.Provider` = `postgres` | `mssql` +
  `ConnectionStrings:MetaStorageConnection`, `Startup.cs:162-169`)
- storage provider selection (`Storage.Provider` = `azure` | `minio`, `Startup.cs:171-178`)
- Quartz background jobs (`Startup.cs:180`, see [storage-providers-and-jobs.md](storage-providers-and-jobs.md))
- Swagger generation with the two documents and an `oidc` OpenIdConnect security scheme
  (`Startup.cs:184-220`)

`Startup.InitializeApp()` (`Startup.cs:223-290`):

- developer exception page in Development (`Startup.cs:225-229`)
- custom exception handler (below) (`Startup.cs:231-256`)
- `UseCors` → `UseRouting` → `UseAuthorization` (`Startup.cs:258-261`)
- four route mappings: `MapAttachmentUserApi("storage/attachment")`,
  `MapAttachmentServiceApi("storage/attachment")`, `MapTemporaryUserApi("storage/temporary")`,
  `MapTemporaryServiceApi("storage/temporary")` (`Startup.cs:263-266`)
- `MapSwagger` + `UseSwagger`, and with `Swagger.EnableClient` the Swagger UI at `/swagger` with both
  documents, OIDC client id/secret and **PKCE** (`Startup.cs:271-288`)

## Error contract

Two different shapes — important for clients:

1. **Unhandled exceptions → 500 as `application/problem+json`.** The exception handler builds a
   `ProblemDetails` object (`Type=https://httpstatuses.com/500`,
   `Title="An unexpected error occurred."`, `Detail` only in Development) and writes it with
   content type `application/problem+json` (`Startup.cs:242-253`).
2. **Endpoint-caught misses (404 and similar) → plain JSON string body, no ProblemDetails.**
   Handlers return `Results.NotFound("<message>")`, which serializes the message as a bare JSON
   string — there is no `Message` field and no `problem+json` content type
   (`AttachmentEndpoint.cs:211`, `285`, `292`, `304`, `311`, `323`, `342`;
   `TemporaryEndpoint.cs:81`, `88`, `126`). See deviation 5 in
   [api/known-deviations.md](../api/known-deviations.md).

## Request limits

Kestrel limits the request body to **40 000 000 bytes** (~38 MiB) via
`Kestrel:Limits:MaxRequestBodySize` (`src/Ballware.Storage.Service/appsettings.json:35-39`). Uploads
are buffered through provider-side streams (see
[storage-providers-and-jobs.md](storage-providers-and-jobs.md)); oversized uploads fail at the
transport layer, not with an application-level 404/500 contract.

## Initialization and jobs wiring

- EF migration bootstrap: `InitializationWorker` (hosted service registered by the Postgres and
  SQL Server packages) runs `Database.MigrateAsync` at startup **when `Meta.AutoMigrations` is
  true**   (`src/Ballware.Storage.Data.Ef.Postgres/Internal/InitializationWorker.cs:23-27`, registered
  in `Data.Ef.Postgres/ServiceCollectionExtensions.cs:35`).
- Quartz: `AddBallwareStorageBackgroundJobs` registers the `TemporaryCleanupJob` with a cron trigger
  from `Trigger.TemporaryCleanupCron` and a Quartz server that waits for jobs to complete on shutdown
  (`src/Ballware.Storage.Jobs/ServiceCollectionExtensions.cs:11-32`). The default job store in
  `appsettings.json` is clustered AdoJobStore/PostgreSQL (`appsettings.json:27-33`); local setups
  commonly switch to the RAM store (see repository `README.md`). Details in
  [storage-providers-and-jobs.md](storage-providers-and-jobs.md).

## Version bracket (maintenance note)

This service pins `Ballware.Shared.*` **0.0.2** on net8.0 (e.g.
`src/Ballware.Storage.Service/Ballware.Storage.Service.csproj:11`), with AutoMapper 13.0.1 and
Swashbuckle 7.2.0. The current shared basis in `ballware-shared-dotnet` uses newer names — e.g.
`TenantableBaseRepository` instead of `TenantableRepository` — so upgrading this service to the
current shared packages is a rename-and-retarget task, not a drop-in update. This is a maintenance
note, not a defect in the pinned combination.
