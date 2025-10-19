# Ballware Storage Service

[![Build & Publish (main)](https://github.com/ballware/ballware-storage-service/actions/workflows/publish-packages.yml/badge.svg?branch=main)](https://github.com/ballware/ballware-storage-service/actions/workflows/publish-packages.yml)
[![CI Tests (main)](https://github.com/ballware/ballware-storage-service/actions/workflows/sonarqube-latest.yml/badge.svg?branch=main)](https://github.com/ballware/ballware-storage-service/actions/workflows/sonarqube-latest.yml)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=ballware_ballware-storage-service&metric=coverage)](https://sonarcloud.io/summary/overall?id=ballware_ballware-storage-service)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=ballware_ballware-storage-service&metric=bugs)](https://sonarcloud.io/summary/overall?id=ballware_ballware-storage-service)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=ballware_ballware-storage-service&metric=vulnerabilities)](https://sonarcloud.io/summary/overall?id=ballware_ballware-storage-service)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=ballware_ballware-storage-service&metric=security_rating)](https://sonarcloud.io/summary/overall?id=ballware_ballware-storage-service)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=ballware_ballware-storage-service&metric=reliability_rating)](https://sonarcloud.io/summary/overall?id=ballware_ballware-storage-service)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=ballware_ballware-storage-service&metric=sqale_rating)](https://sonarcloud.io/summary/overall?id=ballware_ballware-storage-service)

This repository contains the Ballware Storage Service – the component responsible for binary file storage (attachments) and temporary storage in the Ballware ecosystem. It exposes HTTP APIs, stores file metadata in a relational database (EF Core), and persists file blobs in a provider backend (Azure Blob Storage or MinIO).

For a full multi‑service setup (Keycloak, Storage, Schema, Meta, …) please use the umbrella repo: https://github.com/ballware/ballware-docker-compose. This README focuses solely on the Storage Service.

- Tech stack: .NET 8, ASP.NET Core, EF Core (PostgreSQL or SQL Server), Serilog, Quartz, AutoMapper
- AuthN/Z: OAuth2/OIDC with JWT Bearer, scope‑based (storageApi, serviceApi)
- Storage providers: Azure Blob Storage or MinIO (S3‑compatible)
- API docs: Swagger/OpenAPI (two docs: “storage” and “service”)


## Repository structure

- `src/Ballware.Storage.Service/` – Web service host (Program, Startup, appsettings, Dockerfile)
- `src/Ballware.Storage.Api/*` – API endpoints/mappers, mapped in `Startup`
- `src/Ballware.Storage.Data/*` – data models, repositories, mappings
- `src/Ballware.Storage.Data.Ef/*` – EF Core integration and configuration
- `src/Ballware.Storage.Data.Ef.Postgres/*` – PostgreSQL‑specific implementation/migrations
- `src/Ballware.Storage.Data.Ef.SqlServer/*` – SQL Server‑specific implementation/migrations
- `src/Ballware.Storage.Provider.Azure/*` – Azure Blob Storage provider
- `src/Ballware.Storage.Provider.Minio/*` – MinIO provider
- `src/Ballware.Storage.Jobs/*` – background jobs (Quartz)
- `test/*` – unit/integration tests


## What this service provides

The service hosts two logical API surfaces with the same resources but different target audiences/policies:

- Storage (User) API – user‑context endpoints, policy `storageApi`
- Service API – service‑to‑service endpoints, policy `serviceApi`

Mounted routes (prefix → resource):

- `/storage/attachment` – file attachments (upload, download, delete, …)
- `/storage/temporary` – temporary file storage (time‑boxed; cleaned up by a Quartz job)

Use Swagger UI to explore the concrete operations and models.


## Quick start (local development)

Prerequisites:

- .NET SDK 8.0+
- Metadata database: PostgreSQL or SQL Server
- Blob storage: Azure Blob Storage or MinIO
- An OIDC provider (e.g., Keycloak) reachable by the service

Steps:

1) Trust the local ASP.NET Core development certificate

- macOS/Linux/Windows:
  - `dotnet dev-certs https --trust`

2) Create `appsettings.local.json` (in `src/Ballware.Storage.Service/`)

- This file is not committed and overrides local settings. Adjust authority, scopes, storage provider config, and connection strings to your setup (see Configuration below). Example:

```
{
  "Authorization": {
    "Authority": "https://localhost:3001/realms/ballware",
    "Audience": "storageApi",
    "RequireHttpsMetadata": true,
    "RequiredUserScopes": "openid storageApi",
    "RequiredServiceScopes": "serviceApi",
    "TenantClaim": "tenant",
    "UserIdClaim": "sub",
    "RightClaim": "right"
  },
  "Meta": {
    "Provider": "postgres",           // "postgres" or "mssql"
    "AutoMigrations": true
  },
  "Storage": {
    "Provider": "azure"               // "azure" or "minio"
  },
  "AzureStorage": {
    "ConnectionString": "UseDevelopmentStorage=true", // or a real Azure connection
    "ContainerName": "storage"
  },
  // Alternative to Azure:
  // "MinioStorage": {
  //   "Endpoint": "https://localhost:9000",
  //   "AccessKey": "minioadmin",
  //   "SecretKey": "minioadmin",
  //   "BucketName": "storage",
  //   "UseSSL": true
  // },
  "Swagger": {
    "EnableClient": true,
    "ClientId": "ballwareweb"
  },
  "Cors": {
    "AllowedOrigins": "*",
    "AllowedMethods": "*",
    "AllowedHeaders": "*"
  },
  // In‑memory job store for local tests (optional):
  "Quartz": {
    "quartz.jobStore.type": "Quartz.Simpl.RAMJobStore, Quartz"
  },
  "ConnectionStrings": {
    "MetaStorageConnection": "Host=localhost;Port=5432;Database=storage;Username=storage;Password=***",
    // Optional: persistent Quartz store (e.g., Postgres)
    // "QuartzConnection": "Host=localhost;Port=5432;Database=quartz;Username=quartz;Password=***"
  }
}
```

Important: Do not commit secrets to the repository. For local development, prefer `appsettings.local.json`, environment variables, or .NET User Secrets.

3) Start the service

- In the repo root:
  - `dotnet restore`
  - `dotnet build`
  - `dotnet run --project src/Ballware.Storage.Service`

Default ports (Development – see `appsettings.Development.json`):

- HTTP: `http://localhost:5005`
- HTTPS: `https://localhost:6005`

4) Open Swagger UI

- `https://localhost:6005/swagger` (contains the two docs “storage” and “service”; use the “Authorize” button for OIDC login).


## Configuration

Configuration order: `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.local.json` → environment variables.

Key sections:

- Authorization
- Meta (EF Core provider for metadata)
- Storage (selects the blob provider)
- AzureStorage or MinioStorage (provider‑specific settings)
- Trigger (schedules cleanup job)
- Swagger
- Cors
- Quartz
- ConnectionStrings (at least `MetaStorageConnection`, optionally `QuartzConnection`)

Example (trim to your needs):

```
{
  "Authorization": {
    "Authority": "https://localhost:3001/realms/ballware",
    "Audience": "storageApi",
    "RequireHttpsMetadata": true,
    "RequiredUserScopes": "openid storageApi",
    "RequiredServiceScopes": "serviceApi",
    "TenantClaim": "tenant",
    "UserIdClaim": "sub",
    "RightClaim": "right"
  },
  "Meta": {
    "Provider": "postgres",
    "AutoMigrations": true
  },
  "Storage": {
    "Provider": "azure"               // or "minio"
  },
  "AzureStorage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "ContainerName": "storage"
  },
  "MinioStorage": {
    "Endpoint": "https://localhost:9000",
    "AccessKey": "<key>",
    "SecretKey": "<secret>",
    "BucketName": "storage",
    "UseSSL": true
  },
  "Trigger": {
    "TemporaryCleanupCron": "0 0/10 * ? * * *" // default: every 10 minutes
  },
  "Swagger": {
    "EnableClient": true,
    "ClientId": "ballwareweb"
  },
  "Cors": {
    "AllowedOrigins": "*",
    "AllowedMethods": "*",
    "AllowedHeaders": "*"
  },
  "Quartz": {
    // Example: persistent job store via PostgreSQL
    "quartz.scheduler.instanceName": "ballware-local-storage",
    "quartz.jobStore.dataSource": "ballware",
    "quartz.dataSource.ballware.provider": "Npgsql",
    "quartz.dataSource.ballware.connectionStringName": "QuartzConnection"

    // For demos/tests use the RAM store instead:
    // "quartz.jobStore.type": "Quartz.Simpl.RAMJobStore, Quartz"
  },
  "ConnectionStrings": {
    "MetaStorageConnection": "Host=localhost;Port=5432;Database=storage;Username=storage;Password=***",
    "QuartzConnection": "Host=localhost;Port=5432;Database=quartz;Username=quartz;Password=***"
  }
}
```

Notes:

- `Meta.Provider`: `postgres` or `mssql`; the matching EF configuration is applied automatically if `ConnectionStrings:MetaStorageConnection` is present.
- `Storage.Provider`: `azure` or `minio`; configure the corresponding section (`AzureStorage` or `MinioStorage`).
- Swagger: with `EnableClient=true` the service exposes two Swagger docs ("storage" and "service") with OIDC security.
- Background jobs: cleanup for temporary files is scheduled via Quartz using `Trigger.TemporaryCleanupCron`.


## Authentication and authorization

- JWT Bearer authentication against `Authorization.Authority` and `Authorization.Audience`.
- Scope‑based policies:
  - Policy `storageApi` requires one of the scopes defined in `Authorization.RequiredUserScopes` (default: `storageApi`).
  - Policy `serviceApi` requires one of the scopes defined in `Authorization.RequiredServiceScopes` (default: `serviceApi`).
- Swagger UI uses an OpenID Connect definition (`/.well-known/openid-configuration` of the authority server).


## Docker

A Dockerfile is available at `src/Ballware.Storage.Service/Dockerfile`.

Build:

```
# The build consumes GitHub Packages for some dependencies.
# Provide your GitHub username and a PAT with package read permission.

docker build \
  --build-arg GITHUB_USERNAME=<username> \
  --build-arg GITHUB_PAT=<github_pat_with_packages_read> \
  -t ballware-storage-service:local \
  src/Ballware.Storage.Service
```

Run (ports exposed by the Dockerfile: 5000/5001):

```
docker run --rm -p 5000:5000 -p 5001:5001 \
  -e ASPNETCORE_URLS="http://+:5000;https://+:5001" \
  -e Authorization__Authority="https://localhost:3001/realms/ballware" \
  -e Authorization__Audience="storageApi" \
  -e Storage__Provider="azure" \
  -e AzureStorage__ConnectionString="UseDevelopmentStorage=true" \
  -e AzureStorage__ContainerName="storage" \
  -e Meta__Provider="postgres" \
  -e ConnectionStrings__MetaStorageConnection="Host=host.docker.internal;Port=5432;Database=storage;Username=storage;Password=***" \
  ballware-storage-service:local
```

Alternative: mount a prepared `appsettings.json`/`appsettings.local.json` into the container workdir (`/app`).


## Tests

Run all tests:

```
dotnet test
```

Notes:

- Some integration tests require reachable infrastructure (e.g., PostgreSQL, optionally MinIO/Azure emulator). Adjust the test configuration or provide test containers accordingly.
- Pure unit tests do not require external infrastructure.


## Troubleshooting

- Certificate/HTTPS: ensure the local development certificate is trusted (`dotnet dev-certs https --trust`).
- 401/403 in Swagger: check authority/audience and the configured required scopes. Use the Swagger “Authorize” button to sign in.
- CORS: adjust the `Cors` section in `appsettings.local.json` if browser requests are blocked.
- Ports: Development defaults to 5005/6005; the Docker image exposes 5000/5001.


## License

MIT License — Copyright (c) 2025 ballware Software & Consulting, Frank Ballmeyer (https://www.ballware.de)

See the `LICENSE` file in this repository.
