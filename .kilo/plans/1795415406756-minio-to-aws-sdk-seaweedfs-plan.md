# MinIO → AWS SDK v4 (S3-Provider) + SeaweedFS-Testcontainer

## Kontext

Der Blob-Provider `src/Ballware.Storage.Provider.Minio` nutzt das NuGet-Paket `Minio` 6.0.4, die
Integrationstests `Testcontainers.Minio` 4.6.0 mit Image `minio/minio:latest`.

Recherche-Ergebnis (Stand 2026-11-23):
- `minio/minio` (Server): GitHub-Repo **archived**, Community-Edition nur noch als Source, keine
  gepflegten Binaries/Images → `minio/minio`-Image ist als Testcontainer nicht mehr brauchbar.
- `minio/minio-dotnet`: **nicht** archived, `Minio` 7.0.0 (2025-11) auf NuGet nicht als deprecated
  markiert. Die Prämisse "NuGet-Paket abgekündigt" stimmt also formal nicht — der Exchange wird
  trotzdem gemacht (AWS SDK v3 ist end-of-support, `AWSSDK.S3` v4 ist der de-facto Standard für
  S3-kompatible Endpoints und läuft gegen SeaweedFS, AWS S3, Ceph, R2 …).
- Testcontainers for .NET listet **kein** SeaweedFS-Modul → GenericContainer.
- SeaweedFS S3: Port **8333**, unterstützt CreateBucket/HeadBucket/HeadObject/GetObject/PutObject/
  DeleteObject vollständig; Image `chrislusf/seaweedfs` hat versionierte Tags (z. B. `4.55`) und
  unterstützt die `mini`-All-in-One-Variante (master+volume+filer+s3) via Entrypoint.

## Getroffene Entscheidungen

1. Client: **AWSSDK.S3 v4** (4.1.2), kein `AWSSDK.Extensions.NETCore.Setup` — Client wird manuell
   aus den Options gebaut.
2. **Harter Schnitt ohne Alias**: neues Projekt `Ballware.Storage.Provider.S3`,
   Extension `AddBallwareS3BlobStorage`, Options `S3StorageOptions`, Konfig-Sektion `S3Storage`,
   `Storage.Provider = "s3"`. `minio`/`MinioStorage` entfällt ersatzlos, altes Projekt und altes
   Testprojekt werden gelöscht.
3. Produktiver Backend: self-hosted, primär **SeaweedFS** → path-style + Region `us-east-1` als Default.
4. Bucket-Bereitstellung: **Ensure-once mit Cache** (Singleton statt pro Aufruf).
5. Options-Shape: `Endpoint` bleibt `host:port`, `UseSSL` (default true) bleibt; neu: `Region`
   (default `us-east-1`), `ForcePathStyle` (default true).
6. Tests: **Testcontainers GenericContainer** (`Testcontainers` 4.17.0) mit
   `chrislusf/seaweedfs:4.55` (Tag pinnt, kein `latest`).
7. CI/Test-Zugangsdaten: GitHub-Secrets `TEST_MINIO_ACCESSKEY/SECRETKEY` →
   `TEST_S3_ACCESSKEY/TEST_S3_SECRETKEY`; Env-Keys `TestSettings__AccessKey/SecretKey` bleiben.

## Aufgaben (Reihenfolge)

### 1. Neues Provider-Projekt `src/Ballware.Storage.Provider.S3`

`Ballware.Storage.Provider.S3.csproj` als Kopie der Minio-csproj (net8.0, ImplicitUsings, Nullable,
`InternalsVisibleTo` = `$(MSBuildProjectName).Tests`, ProjectReference `Ballware.Storage.Metadata`)
mit:

```xml
<PackageReference Include="AWSSDK.S3" Version="4.1.2" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
```

`Configuration/S3StorageOptions.cs`:
```csharp
public class S3StorageOptions
{
    [Required] public required string Endpoint { get; set; }   // host:port, kein Schema
    [Required] public required string AccessKey { get; set; }
    [Required] public required string SecretKey { get; set; }
    [Required] public required string BucketName { get; set; }
    public bool UseSSL { get; set; } = true;
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
}
```

`ServiceCollectionExtensions.cs` — `AddBallwareS3BlobStorage(this IServiceCollection, S3StorageOptions)`:
- `services.AddSingleton(options)`
- `services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new AmazonS3Config {
      ServiceURL = (options.UseSSL ? "https://" : "http://") + options.Endpoint,
      AuthenticationRegion = options.Region,
      ForcePathStyle = options.ForcePathStyle,
      RequestChecksumCalculation = RequestChecksumCalculation.WhenRequired,
      MaxErrorRetry = 3
  }, new BasicAWSCredentials(options.AccessKey, options.SecretKey)))`
- `services.AddSingleton<S3BucketProvisioner>()`
- `services.AddScoped<IAttachmentStorageProvider, S3AttachmentStorageProvider>()`
- `services.AddScoped<ITemporaryStorageProvider, S3TemporaryStorageProvider>()`

Validierung: `Endpoint` darf kein `://` enthalten (sonst `OptionsValidationException`) — Schema kommt
ausschließlich aus `UseSSL`.

### 2. Bucket-Ensure-once (Cache)

`Internal/S3BucketProvisioner.cs` (internal, Singleton): einmal pro Prozess
`GetBucketMetadataAsync` (HeadBucket) → bei 404 `PutBucketAsync`; Ergebnis als
`Lazy<Task>`/`SemaphoreSlim(1,1)` + Completed-Flag cached, Fehler werden **nicht** gecacht
(nächster Aufruf versucht erneut). `BucketAlreadyOwnedByYou`/`BucketAlreadyExists` ⇒ Erfolg.
Beide Provider (scoped) nehmen den Provisioner per CTor und rufen `await _provisioner.EnsureAsync()`
vor jeder Objektoperation.

### 3. Provider-Implementierungen (`Internal/`)

Schlüssel/Layout unverändert: `{tenantId}/{entity}/{ownerId}/{fileName}` bzw.
`{tenantId}/temporary/{temporaryId}/{fileName}` (siehe `docs/concepts/storage-model.md`).

Mapping der bisherigen Minio-Aufrufe:

| bisher (Minio) | neu (AWSSDK.S3 v4) |
|---|---|
| `BucketExistsAsync`/`MakeBucketAsync` | `S3BucketProvisioner` (Task 2) |
| `StatObjectAsync` + `ObjectNotFoundException` | `GetObjectMetadataAsync` → `catch (Amazon.S3Exception ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)` |
| `PutObjectArgs.WithStreamData/WithObjectSize/WithContentType` | `PutObjectAsync(new PutObjectRequest { BucketName, Key, InputStream = ms, ContentType = contentType, UseChunkEncoding = false, AutoCloseStream = false })` |
| `GetObjectArgs.WithCallbackStream` | `GetObjectAsync` → `Response.ResponseStream` in `MemoryStream` kopieren, `Position = 0`, zurückgeben (`GetObjectResponse` nicht vor dem Kopieren dispo­nieren) |
| `RemoveObjectAsync` | `DeleteObjectAsync(new DeleteObjectRequest { BucketName, Key })` |

Zwei harte Compat-Regeln (beide dokumentiert in den Code-Kommentaren der Provider-Klassen **nicht**
erforderlich, aber in Doku nötig):
- `UseChunkEncoding = false` zwingend: AWS SDK v4 sendet per Default `Content-Encoding: aws-chunked`,
  und SeaweedFS dekodiert das beim Upload **nicht** → Objekt landet corrupt (55 Bytes größer,
  dokumentiert für boto3/SeaweedFS).
- Object-Keys dürfen keinen führenden Slash und keine doppelten Slashes enthalten (SigV4-Path wird
  vom SDK anders signiert als von SeaweedFS geprüft → `SignatureDoesNotMatch`). Deshalb in
  `GenerateBlobPath` den `fileName` normalisieren: führende `/` und `//` kollabieren, `..`-Segmente
  verwerfen. (Behandlungsentscheidung: Provider wirft `ArgumentException` bei `..`; alles andere wird
  normalisiert.)

Streaming-Verhalten bleibt bewusst gepuffert (`MemoryStream`), identisch zum bisherigen
Minio-Provider — keine Änderung am 40-MB-Kestrel-Limit, kein Multipart-Upload.

### 4. Service-Integration

- `src/Ballware.Storage.Service/Ballware.Storage.Service.csproj`: ProjectReference Minio → S3.
- `Startup.cs`: `S3StorageOptions? s3StorageOptions = Configuration.GetSection("S3Storage")
  .Get<S3StorageOptions>();` + `AddOptionsWithValidateOnStart<S3StorageOptions>()`-Block +
  Provider-Zweig `"s3"` (`Startup.cs:171-178`), `AddBallwareS3BlobStorage(s3StorageOptions)`.
  Kein Fallback auf `minio`.
- `src/Ballware.Storage.Service/Dockerfile`: `COPY ./Ballware.Storage.Provider.Minio …` →
  `COPY ./Ballware.Storage.Provider.S3 …`.
- `ballware-storage-service.sln`: altes Provider-/Testprojekt entfernen, neue per
  `dotnet sln add` ergänzen.

### 5. Alt lasten entfernen

`src/Ballware.Storage.Provider.Minio/` und `test/Ballware.Storage.Provider.Minio.Tests/`
inkl. `MinioBackedBaseTest.cs` und der ungenutzten `DelayWaitStrategy.cs` löschen. (Die
`DelayWaitStrategy`-Kopien in `Ballware.Storage.Provider.Azure.Tests` /
`Data.Ef.SqlServer.Tests` bleiben unangetastet.)

### 6. Neues Testprojekt `test/Ballware.Storage.Provider.S3.Tests`

csproj wie die bisherige Minio-Test-csproj (NUnit 4.3.0, NUnit3TestAdapter, Microsoft.NET.Test.Sdk,
Mvc.Testing, coverlet, `SonarQubeExclude`), aber:
- `Testcontainers.Minio` 4.6.0 **raus**, `Testcontainers` **4.17.0** rein.
- ProjectReference auf `Ballware.Storage.Provider.S3`.

`Utils/SeaweedFsBackedBaseTest.cs` (Analogon zu `AzuriteBackedBaseTest`):
```csharp
const string Image = "chrislusf/seaweedfs:4.55";   // Tag pinnen, kein latest

_container = new ContainerBuilder(Image)
    .WithCommand("mini", "-dir=/data", "-s3.port=8333")
    .WithEnvironment("AWS_ACCESS_KEY_ID", accessKey)
    .WithEnvironment("AWS_SECRET_ACCESS_KEY", secretKey)
    .WithEnvironment("S3_BUCKET", "storage")
    .WithPortBinding(8333, true)
    .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
        .ForPort(8333)
        .ForPath("/")
        .ForStatusCodeMatching(code => (int)code >= 200 && (int)code < 500)))
    .Build();
```
- `accessKey`/`secretKey` aus `TestSettings:AccessKey` / `TestSettings:SecretKey`
  (`appsettings.json` → `appsettings.additional.json` → `appsettings.local.json` → Env, gleiche
  Kette wie bisher). Fehlen beide → sofortige, eindeutige Fehlermeldung statt stiller
  "Allow All"-Modus.
- Warte-Strategie akzeptiert bewusst 403: Sind Credentials gesetzt, ist Signaturpflicht aktiv und
  `GET /` liefert `AccessDenied` (403), nicht 200. Falls `ForStatusCodeMatching` in der gepinnten
  Version nicht verfügbar ist: Fallback `.UntilInternalTcpPortIsAvailable(8333)` plus
  `AddCustomWaitStrategy(new DelayWaitStrategy(TimeSpan.FromSeconds(3)))`.
- `Options = new S3StorageOptions { Endpoint = $"{c.Hostname}:{c.GetMappedPublicPort(8333)}",
  BucketName = "storage", AccessKey = …, SecretKey = …, UseSSL = false,
  Region = "us-east-1", ForcePathStyle = true }`.
- `appsettings.local.json` mit `TestSettings:AccessKey`/`SecretKey` (Testwerte, z. B.
  `s3test`/`s3test123`) — keine echten Credentials, und **nicht** in
  `src/Ballware.Storage.Service/appsettings.local.json` erweitern (dort liegen bereits reale
  Produktions-Zugangsdaten; das ist ein bestehendes Problem, hier nicht vergrößern).

Tests 1:1 portieren (Namen/Assertions identisch):
- `Attachment/AttachmentStorageProviderTest.cs` (Upload → Download → Drop → Download == null)
- `Temporary/TemporaryStorageProviderTest.cs`
Ein zusätzlicher Test, damit der Ensure-once-Pfad gegen SeaweedFS abgedeckt ist:
- `BucketProvisioningTest`: Provider mit `BucketName = "storage-auto"` (nicht via `S3_BUCKET`
  vorgegeben) → Upload/Download erfolgreich, und zweiter Upload ohne erneuten HeadBucket
  (Cache-Wirksamkeit über zwei Scopes hinweg).

### 7. CI

`.github/workflows/sonarqube-latest.yml` Zeilen 52-53:
```yaml
TestSettings__AccessKey: ${{ secrets.TEST_S3_ACCESSKEY }}
TestSettings__SecretKey: ${{ secrets.TEST_S3_SECRETKEY }}
```
**Vor dem Merge** in GitHub `TEST_S3_ACCESSKEY`/`TEST_S3_SECRETKEY` anlegen (die alten
`TEST_MINIO_*` danach löschen), sonst laufen die S3-Tests im Sonar-Job mit leeren Credentials.
Runner `ubuntu-latest` stellt Docker für Testcontainers bereit; Image-Layer-Cache nicht verfügbar →
erster Pull ~300 MB (`chrislusf/seaweedfs` amd64), das ist akzeptiert.

### 8. Doku

- `README.md` Zeilen 12, 18, 31, 67, 98, 105-108, 161, 187-193, 232, 291: Provider `azure` | `s3`,
  Section `S3Storage` (`Endpoint`, `AccessKey`, `SecretKey`, `BucketName`, `UseSSL`, `Region`,
  `ForcePathStyle`), Beispiel-Konfiguration mit SeaweedFS (`endpoint: localhost:8333`,
  `region: us-east-1`), Hinweis dass MinIO-Section nicht mehr unterstützt wird.
- `docs/concepts/storage-providers-and-jobs.md`: Provider-Auswahl (Zeilen 9-14),
  Bucket-Verhalten (Zeilen 19-25: jetzt Ensure-once, nicht pro Aufruf), Objekt-Keys/Existenz-Check
  (Zeilen 26-39), Streaming-Abschnitt (Zeilen 41-50).
- `docs/concepts/storage-model.md` Zeilen 91-94 und `docs/api/known-deviations.md` Zeile 133:
  Dateipfade auf `Provider.S3/Internal/S3*StorageProvider.cs` aktualisieren (Zeilennummern neu setzen).
- `docs/README.md` Zeilen 19, 68-69: Azure/S3.

## Verifikation

```bash
dotnet restore ballware-storage-service.sln
dotnet build ballware-storage-service.sln --no-incremental -c Release
dotnet test test/Ballware.Storage.Provider.S3.Tests -c Release          # braucht Docker
dotnet test ballware-storage-service.sln --no-build -c Release          # Gesamt-Suite
dotnet pack ballware-storage-service.sln -c Release --output ./nupkgs   # neuer Package-Id
```
Manueller Smoke gegen SeaweedFS (kein NuGet-Code nötig):
```bash
docker run --rm -d --name weed -p 8333:8333 \
  -e AWS_ACCESS_KEY_ID=s3test -e AWS_SECRET_ACCESS_KEY=s3test123 -e S3_BUCKET=storage \
  chrislusf/seaweedfs:4.55 mini -dir=/data -s3.port=8333
```
Prüfen: Upload/Download byte-identisch (kein `aws-chunked`-Corruption), HeadObject 404 → Download
`null` → HTTP 404 im Endpoint-Verhalten unverändert.

GitVersion: Konfigurations-Bruch → Commit-Marker `+semver: major` (Repo nutzt
`commit-message-incrementing`).

## Risiken / Fallstricke

- **aws-chunked**: `UseChunkEncoding = false` ist Pflicht, sonst corrupt Objekte auf SeaweedFS
  (und teils auf anderen S3-Implimentationen). Kein Problem auf echtem AWS S3.
- **Payload-Signing über HTTP**: `DisablePayloadSigning` ist nur mit HTTPS erlaubt — über
  `http://` (Tests/interne Devs) signiert das SDK die Payload und braucht einen seekbaren Stream;
  deshalb Upload weiterhin über `MemoryStream` (wie bisher), nicht direkt über
  `file.OpenReadStream()`.
- **`RequestChecksumCalculation`**: v4 calculating checksums per Default kann von
  Dritt-Backends abgelehnt werden → `WhenRequired` setzen.
- **Key-Hygiene**: unsanitized `file.FileName` (Endpoint übergibt ihn unverändert) kann `/` oder
  `..` enthalten → SigV4-Mismatch und Path-Traversal im Bucket. Normalisierung im Provider (Task 3)
  ist Bewusst **mit** aufgenommen; echte Endpoint-seitige Validierung bleibt separate Aufgabe.
- **Breaking Change für Deployments**: bestehende `MinioStorage`-Konfigurationen starten nicht mehr
  (bewusst, Entscheidung 2). Deployments müssen gleichzeitig umgestellt werden.
- **Package-Publication**: `dotnet pack` veröffentlicht künftig
  `Ballware.Storage.Provider.S3` auf GitHub Packages; `Ballware.Storage.Provider.Minio` verschwindet.

## Out of scope / offen

- Ob fremde ballware-Repos `Ballware.Storage.Provider.Minio` als NuGet-Paket referenzieren — wenn
  ja, dort parallele Umstellung nötig (nicht Teil dieses Plans).
- Austausch der in `src/Ballware.Storage.Service/appsettings.local.json` liegenden echten
  Credentials (Postgres/Azure) — separates Sicherheits-Thema.
- Produktiver Rollout einer SeaweedFS-Instanz (Deployment/Operatoren-Doku) — nicht Teil dieses Plans.
- Echte S3-Backend-AWS-Produktivnutzung (Virtual-host-style, IAM) — bewusst nicht unterstützt.
