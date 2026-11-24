# Storage model

The service persists two kinds of file records — **attachments** (long-lived, attached to a domain
record) and **temporaries** (time-boxed exchange payloads, e.g. imports/exports). Each record exists
in two shapes: a **public DTO** (what the API sends/receives) and a **persistable** (what the
database stores). Blobs themselves live in the provider backend, addressed by `StoragePath`.

## Public DTOs vs. persistables

### Attachment

Public `Ballware.Storage.Data.Public.Attachment` (`src/Ballware.Storage.Data/Public/Attachment.cs`):

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | public identity = persistable `uuid` (see mapping below) |
| `Entity` | `string` (`required`) | identifier of the attached domain entity (e.g. `document`, `mlmodel`) |
| `OwnerId` | `Guid` (`required`) | identity of the owning domain record |
| `FileName` | `string` (`required`) | original file name |
| `ContentType` | `string` (`required`) | MIME type taken from the upload part and replayed on download |
| `FileSize` | `long` | bytes |
| `StoragePath` | `string` (`required`) | provider object key — see below |

Persistable `Ballware.Storage.Data.Persistables.Attachment` extends abstract `Blob`
(`src/Ballware.Storage.Data/Persistables/Blob.cs`, `…/Attachment.cs`) → table `attachment`
(`src/Ballware.Storage.Data.Ef.Postgres/Migrations/20250707155847_Initial.cs:15-37`):

| Column | Type | Notes |
| --- | --- | --- |
| `id` | bigint identity | internal surrogate key (`Blob.cs:10-12`, `JsonIgnore`) |
| `uuid` | uuid | public `Id` (`Blob.cs:14-16`, mapped via `[JsonPropertyName(nameof(Id))]`) |
| `tenant_id` | uuid | tenant (`JsonIgnore`) |
| `creator_id`, `create_stamp`, `last_changer_id`, `last_change_stamp` | uuid / timestamptz | audit (`JsonIgnore`) |
| `file_name`, `content_type` | text NOT NULL | |
| `file_size` | bigint | |
| `storage_path` | text NOT NULL | provider object key |
| `entity`, `owner_id` | text / uuid | attachment-specific (`Persistables/Attachment.cs:9-15`) |

All internal/audit columns are `[JsonIgnore]` and **never exposed** in API payloads
(`Blob.cs:10-37`); only `uuid` is serialized as `Id`.

### Temporary

Public `Temporary` (`src/Ballware.Storage.Data/Public/Temporary.cs`): `Id`, `FileName`,
`ContentType`, `ExpiryDate`, `FileSize`, `StoragePath` (all `required` except `Id`).

Persistable → table `temporary` (`Postgres/Migrations/…Initial.cs:39-56`): same columns as
`attachment` plus `expiry_date` (timestamptz NOT NULL); no `entity`/`owner_id`.

### Mapping

Mapster (`src/Ballware.Storage.Data.Ef/Internal/StorageMappingProfile.cs:9-21`) maps
public `Id` ⇄ persistable `Uuid` and **ignores** the internal `Id` on the write direction — public
DTOs never carry the bigint key.

## Indexes and the unique file-name rule

Model customizer `StorageModelBaseCustomizer`
(`src/Ballware.Storage.Data.Ef/Model/StorageModelBaseCustomizer.cs:36-43`):

- unique `(tenant_id, uuid)` on both tables — public identity is unique per tenant
- `(tenant_id)` — tenant scans
- unique `(tenant_id, entity, owner_id, file_name)` on `attachment` — **one blob per file name per
  entity+owner**

Consequence: re-uploading a file with the same name for the same `entity`/`ownerId` is a **replace**,
not a second row — the handler looks up the existing row by file name, uploads the new blob (same
`StoragePath`), updates the row's fields and saves it; the public `Id` stays unchanged
(`src/Ballware.Storage.Api/Endpoints/AttachmentEndpoint.cs:181-195`, service variant `259-273`).

## UTC everywhere

A `DateTime`/`DateTime?` value converter is applied to **all** properties in the EF model
(`StorageModelBaseCustomizer.cs:17-33`): writes convert to UTC, reads re-tag as UTC. Timestamps in
payloads (`ExpiryDate`, implicitly the audit stamps in other services' DTOs) are therefore UTC —
the public `Attachment`/`Temporary` DTOs only expose `ExpiryDate` as a time value.

## `identifier` = `"primary"` is a label

Shared repositories take a query `identifier` parameter. In this service it is always the constant
`"primary"` (`AttachmentPrimaryQuery` `AttachmentEndpoint.cs:15`, `TemporaryPrimaryQuery`
`TemporaryEndpoint.cs:14`). It selects no alternate query set and derives no rights — a pure label
at the 1.2.0 shared-repository API.

## `StoragePath`

`StoragePath` is the **provider object key** (blob/container path or bucket object name), not a
downloadable URL and not meant to be interpreted by clients. Layout is built by the providers:

- Attachment: `{tenantId}/{entity}/{ownerId}/{fileName}`
  (`src/Ballware.Storage.Provider.S3/Internal/S3AttachmentStorageProvider.cs:41-44`,
  `Provider.Azure/Internal/AzureAttachmentStorageProvider.cs:21-23`)
- Temporary: `{tenantId}/temporary/{temporaryId}/{fileName}`
  (`S3TemporaryStorageProvider.cs:41-44`, `AzureTemporaryStorageProvider.cs:21-23`)

Because the `fileName` is embedded, a file name containing `/` or `..` would alter the key layout —
the S3 provider normalizes the file name segment (collapses leading/duplicate slashes, drops `.`
segments, rejects `..` segments with `ArgumentException`), the Azure provider embeds the raw name —
see best practices and deviation 9 in [api/known-deviations.md](../api/known-deviations.md).

## Temporary expiry

Expiry is hard-coded: `ExpiryDate = DateTime.UtcNow.AddMinutes(30)` at upload
(`src/Ballware.Storage.Api/Endpoints/TemporaryEndpoint.cs:112`). Only the cleanup **cron** is
configurable (`Trigger:TemporaryCleanupCron`); the 30-minute lifetime is not
(deviation 8 in [api/known-deviations.md](../api/known-deviations.md)). Expiry semantics: the
cleanup job removes rows where `ExpiryDate <= UtcNow`
(`src/Ballware.Storage.Data.Ef/Repository/TemporaryBaseRepository.cs:19-24`) — downloads are not
blocked before that point.
