# Plan: ballware-storage-service — .NET 8 → .NET 10, AutoMapper → Mapster, Shared-Pakete

## Kontext & Referenzen

- Referenz-Migration in ballware-meta-service: Commits `9a05968` ("Update to .net 10, replaced mappings from AutoMapper with Mapster #56") + `1daa39a`/`870943e`/`ca2e8dd` (Pipeline/Client-Generierung für net10).
- Shared-Pakete (ballware-shared-dotnet): `BaseRepository`/`TenantableBaseRepository` nutzen ab v1.x `MapsterMapper.IMapper` (Commit `299cfdf`); Klassenumbenennung `TenantableRepository` → `TenantableBaseRepository` (Commit `4c174c0`). Alle Shared-Pakete sind net10-only.
- Betroffene Projekte: 11 src- + 6 test-.csproj, `Startup.cs`, `MapperConfigurationExtensions.cs`, `StorageMappingProfile.cs`, 2 Base-Repositories, Dockerfile, 2 Workflows, nswag.json + generierter Client.

## Getroffene Entscheidungen

1. **TFM:** alle Projekte `net10.0`.
2. **Shared-Pakete:** stabile `1.2.0` bevorzugen; Fallback `1.2.0-alpha.14` falls der GitHub-Feed (nuget.pkg.github.com/ballware) keine stabilen liefert. `Ballware.Shared.Authentication` wird NICHT aufgenommen (Folgearbeit, analog meta-Commit 425a100).
3. **Mapper:** Mapster 7.4.0 + Mapster.DependencyInjection 1.0.1, Muster exakt wie meta (`TypeAdapterConfig` Singleton + `IMapper`/`ServiceMapper` Scoped).
4. **JSON:** Newtonsoft-Pipeline bleibt (`AddNewtonsoftJson` + `Swashbuckle.AspNetCore.Newtonsoft`), Versionen 10.1.3 / Mvc.NewtonsoftJson 10.0.x.
5. EF-Migrationsdateien bleiben unverändert (kein Modellwechsel); Client `StorageServiceClient.cs` wird in der Pipeline mit `runtime: Net100` neu generiert.
6. Semver wie meta: Minor-Bump über GitVersion (`+semver: minor`-Konvention beibehalten).

## Versionstabelle (Versions-parität mit ballware-meta-service)

| Paket | neu |
|---|---|
| Ballware.Shared.Authorization / Data / Data.Ef | 1.2.0 (Fallback 1.2.0-alpha.14) |
| Mapster | 7.4.0 |
| Mapster.DependencyInjection (Service), Mapster (Data.Ef) | 1.0.1 / 7.4.0 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.3 |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 10.0.x (aktuell verfügbar, z.B. 10.0.13) |
| Microsoft.EntityFrameworkCore / .Abstractions / .Relational / .Design | 10.0.3 (Design wie meta auch 10.0.2 ok) |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.2 |
| Microsoft.Data.SqlClient | 6.1.4 |
| Npgsql | 10.0.1 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 |
| Microsoft.Extensions.Hosting | 10.0.3 |
| Microsoft.Extensions.Configuration.Abstractions / .DependencyInjection(.Abstractions) / .Logging.Abstractions / .Options / .Caching.Memory | 10.0.3 (bzw. 10.0.2) |
| Newtonsoft.Json | 13.0.4 |
| Serilog 4.3.0 (bleibt), Serilog.AspNetCore | 10.0.0 |
| Serilog.Sinks.Console | 6.1.1 |
| Quartz, Quartz.AspNetCore, Quartz.Extensions.DependencyInjection, Quartz.Serialization.Json | 3.8.1 (bleibt) |
| Swashbuckle.AspNetCore + .Annotations + .Newtonsoft | 10.1.3 |
| Microsoft.AspNetCore.Mvc.Testing / TestHost | 10.0.3 |
| Microsoft.NET.Test.Sdk, NUnit, NUnit3TestAdapter, coverlet, Moq, Bogus, Testcontainers.* | unverändert (meta-parität, funktioniert unter net10) |
| AWSSDK.S3 4.1.2, Azure.Storage.Blobs 12.24.1 | bleiben |
| AutoMapper 13.0.1, MimeMapping 3.1.0 | entfernen (MimeMapping ist ungenutzt) |

## Aufgaben (Reihenfolge)

### 1. csproj-Umstellung (alle 17 Projekte)
- `<TargetFramework>` → `net10.0` in src/Ballware.Storage.{Data, Metadata, Data.Ef, Data.Ef.Postgres, Data.Ef.SqlServer, Api, Jobs, Provider.Azure, Provider.S3, Service, Service.Client} und allen 6 test-Projekten.
- Paketversionen gemäß Tabelle ersetzen; `SuppressTfmSupportBuildWarnings` in Service/Provider.S3/S3.Tests behalten (AWSSDK-Hashing-Flag gilt weiterhin).
- Data.Ef: `AutoMapper` → `Mapster`. Service: `Mapster` + `Mapster.DependencyInjection` hinzufügen, `MimeMapping` entfernen.
- Service.Client.csproj + `nswag.json`: `"runtime": "Net80"` → `"Net100"`.

### 2. Ballware.Storage.Data.Ef — Mapster-Migration
- `Internal/StorageMappingProfile.cs`: AutoMapper `Profile` → Mapster `IRegister` nach meta-Muster (`src/Ballware.Meta.Data.Ef/Mapping/StorageMappingProfile.cs`):
  ```csharp
  config.NewConfig<Public.Attachment, Persistables.Attachment>()
      .Ignore(dest => dest.Id!).Map(dest => dest.Uuid, src => src.Id);
  config.NewConfig<Persistables.Attachment, Public.Attachment>()
      .Map(dest => dest.Id, src => src.Uuid);
  // Temporary analog
  ```
- `MapperConfigurationExtensions.cs`: Signatur `TypeAdapterConfig AddBallwareMetaStorageMappings(this TypeAdapterConfig configuration)` mit `new StorageMappingProfile().Register(configuration);` (meta-Muster).
- `Repository/AttachmentBaseRepository.cs`, `Repository/TemporaryBaseRepository.cs`:
  - `using AutoMapper;` → `using MapsterMapper;`
  - Basisklasse `TenantableRepository<...>` → `TenantableBaseRepository<...>` (Umbenennung im Shared-Paket).

### 3. Ballware.Storage.Service/Startup.cs
- `Services.AddAutoMapper(...)` (Zeile 155) ersetzen durch:
  ```csharp
  var mapsterConfig = new TypeAdapterConfig().AddBallwareMetaStorageMappings();
  Services.AddSingleton(mapsterConfig);
  Services.AddScoped<IMapper, ServiceMapper>();
  ```
  (`using Mapster; using MapsterMapper;`)
- Swashbuckle 10 / Microsoft.OpenApi 2.x Breaking Changes (meta-Startup 292–383 als Vorlage):
  - `using Microsoft.OpenApi;` statt `Microsoft.OpenApi.Models`; `OpenApiInfo`/`SecuritySchemeType` direkt verwenden.
  - `AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = ... } ... } })` ersetzen durch dokumentbasierte Variante:
    `c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement { { new OpenApiSecuritySchemeReference("oidc", document), authorizationOptions.RequiredUserScopes.Split(" ") } });`
  - `AddSwaggerGenNewtonsoftSupport()` bleibt.

### 4. Tests (4 Stellen)
- `test/Ballware.Storage.Data.Ef.{Postgres,SqlServer}.Tests/EfMigrationsTest.cs` und `.../Repository/RepositoryBaseTest.cs`: `AddAutoMapper(...)` ersetzen durch
  ```csharp
  var mapsterConfig = new TypeAdapterConfig().AddBallwareMetaStorageMappings();
  PreparedBuilder.Services.AddSingleton(mapsterConfig);
  PreparedBuilder.Services.AddScoped<IMapper, ServiceMapper>();
  ```
  (meta-Muster: RepositoryBaseTest.cs:95–99).
- Api.Tests: nur Versionsbumps (IPrincipalUtils-API unverändert; kein Mapper im Einsatz).

### 5. Deployment/Pipeline
- `src/Ballware.Storage.Service/Dockerfile`: `aspnet:8.0-bookworm-slim` → `10.0-bookworm-slim`, `sdk:8.0-bookworm-slim` → `10.0-bookworm-slim`.
- `.github/workflows/publish-packages.yml`: Image `sdk:8.0` → `sdk:10.0`; `Swashbuckle.AspNetCore.Cli --version 7.2.0` → `10.1.3`; Swagger-Pfad `./bin/Release/net8.0/...` → `net10.0`.
- `src/Ballware.Storage.Service.Client/nswag.json` Net100 + `StorageServiceClient.cs` im PR neu generieren (Pipeline-Schritt laufen lassen bzw. lokal reproduzieren).
- `sonarqube-latest.yml` ist bereits auf dotnet 10 — unverändert.

### 6. Branch/Kommunikation
- Feature-Branch analog meta: `feature/update-to-net-10-replace-automapper-with-mapster`.
- Repo-Konvention: Issue-Referenz `#NN` in Commits — Issue-Numme im Storage-Repo anlegen/bestätigen (meta hatte #56).
- Kein Commit ohne explizite Aufforderung durch den Nutzer.

## Risiken / Prüfpunkte

- **Shared-Feed:** Verfügbarkeit stabiler `1.2.0`-Pakete auf nuget.pkg.github.com/ballware ist nicht verifizierbar ohne Token; bei Restriktion auf `1.2.0-alpha.14` zurückfallen und im PR vermerken.
- **Mapster-Null-Semantik:** `Mapper.Map<Public.Attachment>(t.Result)` in `SingleByEntityOwnerAndFileNameAsync` (FirstOrDefault kann null liefern) — Mapster statt AutoMapper-Null-Verhalten verhalten; durch Postgres-/SqlServer-Repository-Tests absichern.
- **Mapster-Mapping:** `Ignore(dest => dest.Id!)` auf `long?` (Blob.Id) und `required`-Properties in Public-Modellen prüfen (Mapster erzeugt Zielinstanzen; `required`-Initialisierung testen).
- **EF Core 10 / Npgsql 10 / SqlClient 6:** UTC-DateTime-ValueConverter im `StorageModelBaseCustomizer`, Migrationsapplied (`InitializationWorker`) und snake_case-Column-Attribute gegen Testcontainers-Tests verifizieren.
- **Client-Breaking:** `Ballware.Storage.Service.Client` wird net10-only (verbraucht Newtonsoft.Json 13.0.4) — Abwärtskompatibilität zu net8-Consumern endet; meta nutzt den Client bereits (9.3.1) auf net10.
- **NSwag-Client-Reexport:** generierte Datei muss eingecheckt werden; Swagger-Gruppenname `service` und ASPNETCORE_ENVIRONMENT=Swagger bleiben unverändert.

## Validierung

1. `dotnet restore ballware-storage-service.sln` (benötigt konfigurierten GitHub-NuGet-Source mit PAT lokal).
2. `dotnet build ballware-storage-service.sln -c Release` — keine `net8.0`-Artefakte mehr in `obj/bin`.
3. `dotnet test ballware-storage-service.sln` (Docker für Testcontainers nötig; S3-/Secret-Tests ggf. via appsettings.local).
4. Swagger-Check: `dotnet tool run swagger tofile ... ASPNETCORE_ENVIRONMENT=Swagger` mit CLI 10.1.3 auf `bin/Release/net10.0/Ballware.Storage.Service.dll`; anschließend `nswag run nswag.json` und Diff gegen `StorageServiceClient.cs`.
5. Start-Smoke: Service mit `appsettings.Development.json`/`Swagger`-Env starten; /health-artige Swagger-Endpunkte erreichbar.

## Offene Punkte (nicht blockierend)

- Issue-Nummer für Commit-Referenz im Storage-Repo.
- Follow-up (explizit NICHT Teil dieses Plans): `ballware.shared.authentication` 1.3.0-alpha.1 UserInfo-Enrichment, analog meta-Commit 425a100.
