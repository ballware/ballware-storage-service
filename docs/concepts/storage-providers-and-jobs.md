# Storage providers and jobs

## Provider selection

`Storage.Provider` selects the blob backend; only the provider whose section is **both** selected
and configured is registered (`src/Ballware.Storage.Service/Startup.cs:174-181`,
`Configuration/StorageOptions.cs:5-8`):

- `"azure"` → `AddBallwareAzureBlobStorage` (`Provider.Azure/ServiceCollectionExtensions.cs:10`)
  reading the `AzureStorage` section: `ConnectionString`, `ContainerName`
  (`Provider.Azure/Configuration/AzureStorageOptions.cs`).
- `"s3"` → `AddBallwareS3BlobStorage` (`Provider.S3/ServiceCollectionExtensions.cs:13`)
  reading the `S3Storage` section: `Endpoint` (host:port **without** scheme — the scheme is derived
  from `UseSSL`), `AccessKey`, `SecretKey`, `BucketName`, `UseSSL` (default `true`),
  `Region` (default `us-east-1`), `ForcePathStyle` (default `true`)
  (`Provider.S3/Configuration/S3StorageOptions.cs:8-14`). The client is built manually from these
  options on top of the AWS SDK for .NET (`AWSSDK.S3` v4), targeting self-hosted S3-compatible
  backends (primarily SeaweedFS). The former `minio` provider is gone without replacement.

Both register implementations of `IAttachmentStorageProvider` and `ITemporaryStorageProvider`
(`src/Ballware.Storage.Metadata/`), which is all the endpoint layer depends on.

## Provider behavior (Azure and S3 are intentionally symmetric)

- **Container/bucket provisioning.** Azure: `CreateIfNotExistsAsync` before every operation
  (`Provider.Azure/Internal/AzureAttachmentStorageProvider.cs:12-19`; temporary provider likewise
  `AzureTemporaryStorageProvider.cs:12-19`). S3: **ensure-once with cache** — the singleton
  `S3BucketProvisioner` performs `HeadBucket` and, on 404, `PutBucket` exactly once per process
  (`Provider.S3/Internal/S3BucketProvisioner.cs:23-61`); success is cached, failures are **not**
  cached (the next call retries), `BucketAlreadyOwnedByYou`/`BucketAlreadyExists` count as success.
  Both scoped providers call `EnsureAsync()` before every object operation. No upfront provisioning
  step on startup.
- **Content type is set on the blob** at upload (`Azure…:38-43` sets `BlobHttpHeaders.ContentType`;
  `S3…` `PutObjectRequest.ContentType`, `S3AttachmentStorageProvider.cs:78-86`) — and the endpoint
  **additionally** persists the
  `ContentType` in the metadata row, which is what the download actually returns (see deviation 4 in
  [api/known-deviations.md](../api/known-deviations.md)).
- **Existence check before download/delete.** Azure: `blobClient.ExistsAsync()` guard
  (`AzureAttachmentStorageProvider.cs:55`, `:71`). S3: `GetObjectMetadataAsync` wrapped into a
  boolean, catching `AmazonS3Exception` with HTTP 404 (`S3AttachmentStorageProvider.cs:23-37`).
- **Missing blob ⇒ `null` ⇒ HTTP 404.** Providers return `null` from download; the handler maps it
  to `Results.NotFound("File not found …")` (`AttachmentEndpoint.cs:290-293`, `309-312`;
  `TemporaryEndpoint.cs:86-89`).
- **Object keys** follow the layout in [storage-model.md](storage-model.md):
  `{tenantId}/{entity}/{ownerId}/{fileName}` and `{tenantId}/temporary/{temporaryId}/{fileName}`
  (`S3AttachmentStorageProvider.cs:41-44`, `S3TemporaryStorageProvider.cs:41-44`,
  `AzureAttachmentStorageProvider.cs:21-23`, `AzureTemporaryStorageProvider.cs:21-23`).
  The S3 provider additionally normalizes the `fileName` key segment (leading/duplicate slashes
  collapse, `.` segments drop, `..` segments throw `ArgumentException`) — unnormalized keys would
  break SigV4 signature verification on S3-compatible backends (`SignatureDoesNotMatch`) and could
  let object keys escape their bucket prefix
  (`S3AttachmentStorageProvider.cs:46-58`).

## Streaming (or rather: buffering)

Uploads/downloads are buffered through `MemoryStream` in the S3 provider
(`S3AttachmentStorageProvider.cs:67-89` upload, `:91-114` download; same in
`S3TemporaryStorageProvider.cs:67-90`, `:92-114`). Two hard compatibility rules apply to the S3
provider: `UseChunkEncoding = false` is mandatory (AWS SDK v4 defaults to `aws-chunked` content
encoding, which S3-compatible backends like SeaweedFS do not decode on upload — objects would land
corrupted), and uploads never pass the raw request stream (payload signing over plain HTTP requires
a seekable stream). The Azure provider passes the request stream to
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
