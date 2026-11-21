# Storage providers and jobs

## Provider selection

`Storage.Provider` selects the blob backend; only the provider whose section is **both** selected
and configured is registered (`src/Ballware.Storage.Service/Startup.cs:171-178`,
`Configuration/StorageOptions.cs:5-8`):

- `"azure"` → `AddBallwareAzureBlobStorage` (`Provider.Azure/ServiceCollectionExtensions.cs:10`)
  reading the `AzureStorage` section: `ConnectionString`, `ContainerName`
  (`Provider.Azure/Configuration/AzureStorageOptions.cs`).
- `"minio"` → `AddBallwareMinioBlobStorage` (`Provider.Minio/ServiceCollectionExtensions.cs:11`)
  reading the `MinioStorage` section: `Endpoint`, `AccessKey`, `SecretKey`, `BucketName`,
  `UseSSL` (default `true`) (`Provider.Minio/Configuration/MinioStorageOptions.cs:5-9`).

Both register implementations of `IAttachmentStorageProvider` and `ITemporaryStorageProvider`
(`src/Ballware.Storage.Metadata/`), which is all the endpoint layer depends on.

## Provider behavior (Azure and MinIO are intentionally symmetric)

- **Container/bucket provisioning per call.** Azure: `CreateIfNotExistsAsync` before every
  operation (`Provider.Azure/Internal/AzureAttachmentStorageProvider.cs:12-19`; temporary provider
  likewise `AzureTemporaryStorageProvider.cs:12-19`). MinIO: `BucketExistsAsync` →
  `MakeBucketAsync` (`EnsureBucketExistsAsync`, `Provider.Minio/Internal/MinioAttachmentStorageProvider.cs:14-20`).
  No upfront provisioning step and no failure if the container exists.
- **Content type is set on the blob** at upload (`Azure…:38-43` sets `BlobHttpHeaders.ContentType`;
  `Minio…:60-67` `PutObjectArgs.WithContentType`) — and the endpoint **additionally** persists the
  `ContentType` in the metadata row, which is what the download actually returns (see deviation 4 in
  [api/known-deviations.md](../api/known-deviations.md)).
- **Existence check before download/delete.** Azure: `blobClient.ExistsAsync()` guard
  (`AzureAttachmentStorageProvider.cs:55`, `:71`). MinIO: `StatObjectAsync` wrapped into a boolean,
  swallowing `ObjectNotFoundException` (`MinioAttachmentStorageProvider.cs:22-36`).
- **Missing blob ⇒ `null` ⇒ HTTP 404.** Providers return `null` from download; the handler maps it
  to `Results.NotFound("File not found …")` (`AttachmentEndpoint.cs:290-293`, `309-312`;
  `TemporaryEndpoint.cs:86-89`).
- **Object keys** follow the layout in [storage-model.md](storage-model.md):
  `{tenantId}/{entity}/{ownerId}/{fileName}` and `{tenantId}/temporary/{temporaryId}/{fileName}`
  (`MinioAttachmentStorageProvider.cs:38-41`, `MinioTemporaryStorageProvider.cs:38-40`,
  `AzureAttachmentStorageProvider.cs:21-23`, `AzureTemporaryStorageProvider.cs:21-23`).

## Streaming (or rather: buffering)

Uploads/downloads are buffered through `MemoryStream` in the MinIO provider
(`MinioAttachmentStorageProvider.cs:49-69` upload, `:71-93` download; same in
`MinioTemporaryStorageProvider.cs:49-71`, `:73-93`). The Azure provider passes the request stream to
`UploadAsync` and returns the blob download stream (`AzureAttachmentStorageProvider.cs:31-47`,
`:49-63`), but the handler materializes the download via `Results.File(stream, …)` per request
(`AttachmentEndpoint.cs:295`). There is **no chunked/multipart upload of a single file** and no
resumable protocol — the practical ceiling for one upload is the Kestrel request limit of 40 MB
(`appsettings.json:35-39`).

## Temporary cleanup job

- Job: `TemporaryCleanupJob`, Quartz `JobKey("cleanup", "temporary")`
  (`src/Ballware.Storage.Jobs/Internal/TemporaryCleanupJob.cs:10`).
- Trigger: cron from `Trigger:TemporaryCleanupCron`, default `0 0/10 * ? * * *` — every 10 minutes
  (`src/Ballware.Storage.Jobs/Configuration/TriggerOptions.cs:5`; trigger wiring in
  `src/Ballware.Storage.Jobs/ServiceCollectionExtensions.cs:13-24`). The Quartz server waits for
  running jobs on shutdown (`ServiceCollectionExtensions.cs:26-29`).
- Selection: `ExpiryDate <= UtcNow` across **all** tenants
  (`src/Ballware.Storage.Data.Ef/Repository/TemporaryBaseRepository.cs:19-24`).
- Action per expired entry: delete the blob via `DropByPathAsync`, then remove the metadata row
  with `userId = null` and empty claims — the cleanup is **not attributed to a user and leaves no
  audit user** (`TemporaryCleanupJob.cs:27-33`).

## Quartz configuration

The base `appsettings.json` ships clustered AdoJobStore on PostgreSQL with JSON serialization and
`instanceId=AUTO` (`src/Ballware.Storage.Service/appsettings.json:28-32`). Clustered setups need a
persistent Quartz store; configure it via the optional `ConnectionStrings:QuartzConnection` plus the
`quartz.dataSource.ballware.*` keys (repository `README.md` shows both the persistent and the RAM
variant). The Swagger environment switches to `Quartz.Simpl.RAMJobStore`
(`appsettings.Swagger.json:24-26`) so snapshot generation never needs a Quartz database.
