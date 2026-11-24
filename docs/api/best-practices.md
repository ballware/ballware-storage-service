# Best practices (for callers)

Conventions derived from the implemented behavior — all statements traceable to this repo's code.
(Platform-observed naming kept generic; no tenant-specific metadata.)

## Entity and owner identifiers

- **Entity identifiers are path segments** and act as a shared namespace per tenant. Observed
  platform conventions: `document`, `mlmodel`, `origin`, `image`. Pick short, stable, lowercase
  identifiers — they are part of the unique index (`tenant_id, entity, owner_id, file_name`) and of
  `StoragePath` ([storage-model.md](../concepts/storage-model.md)).
- **`ownerId` = the UUID of the owning domain record** (the meta/generic entity row the files
  belong to), not the user id. The service never validates it — consistency is a caller contract.

## File names

- **Use fixed file names per purpose** (`report.xml`, `model.zip`) if you rely on
  `download…byfilename` round-trips — the fixed name *is* the pointer, and the unique index means
  the slot holds exactly one blob per owner record.
- **No `/` and no `..` in file names.** They are embedded raw into `StoragePath`
  (`{tenantId}/{entity}/{ownerId}/{fileName}`) and into URL segments. A `/` shifts the object key
  layout, breaks by-filename routes, and can push keys outside the intended namespace prefix
  (deviation 9 in [known-deviations.md](known-deviations.md)).
- **URL-encode `{filename}`** in path segments (`encodeURIComponent` / `Uri.EscapeDataString`).
- **Always set an explicit per-part `Content-Type`** on uploads: it is persisted verbatim and
  replayed on downloads (`AttachmentEndpoint.cs:191`, `269`; `TemporaryEndpoint.cs:109`).

## Upload strategy

- Re-upload of the same `(entity, ownerId, fileName)` **replaces** the blob and row — treat uploads
  as upserts; don't pre-delete, and don't expect a duplicate to fail or to create a second record
  (`AttachmentEndpoint.cs:181-195`).
- 201 responses are empty: **query before you link**. If you need the `Id` after upload, call
  `allforentityandowner…` (or use a caller-chosen temporary id, below).

## Temporaries

- **Generate the temporary id yourself** (`Guid.NewGuid()`), upload under it, and hand out
  `{tenantId}/{id}` — ids are stable across re-upload of the same id
  (`TemporaryEndpoint.cs:100-107`).
- **Consume within 30 minutes.** Expiry is fixed at upload + 30 min (re-armed per re-upload); the
  cleanup cron only determines *when* expired rows are swept (`TemporaryEndpoint.cs:112`,
  `TriggerOptions.cs:5`). For longer-lived exchange, use attachments instead.

## Download URLs

- Download routes are anonymous: **treat the URL as a capability granted by unguessable GUIDs,
  not as a secret channel** (deviation 1 in [known-deviations.md](known-deviations.md)). Don't put
  download URLs in logs shared across tenants, and don't encode authorization meaning into them.
- `StoragePath` is not a download URL — clients must use the API download operations; the raw key
  exposes tenant/owner GUID structure and the file name ([storage-model.md](../concepts/storage-model.md#storagepath)).

## Bulk operations

- `dropallfortenant…` / `dropallfortenantandentity…` are tenant-wide; restrict them to lifecycle
  flows (tenant deletion, entity deprecation) and prefer the owner-scoped variants for normal
  cleanup ([service-api.md](service-api.md)).
