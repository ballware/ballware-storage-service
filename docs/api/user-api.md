# User API (`storage` document)

Six operations for end-user contexts. The tenant comes from the caller's `tenant` claim — **not**
from the URL — except on the anonymous download routes. Snapshot:
[openapi/storage.json](../openapi/storage.json) (6 paths, schema `Attachment`).

## GET /storage/attachment/allforentityandowner/{entity}/{ownerId}

Query all attachments of one owner's record.

- Policy `storageApi`; tenant from `tenant` claim (`AttachmentEndpoint.cs:26-33`, handler `164-169`).
- Path params: `entity` (string identifier, e.g. `document`), `ownerId` (uuid — record id the
  attachments belong to).
- 200: `Attachment[]` (PascalCase):

```json
[
  {
    "Id": "4f6d2c1e-8a3b-4c9d-9e0f-1a2b3c4d5e6f",
    "Entity": "document",
    "OwnerId": "9b1de2f4-5c6a-4b8e-a7d3-0e1f2a3b4c5d",
    "FileName": "report.xml",
    "ContentType": "application/xml",
    "FileSize": 18432,
    "StoragePath": "0b8a…f0/document/9b1d…c5d/report.xml"
  }
]
```

## POST /storage/attachment/uploadforentityandowner/{entity}/{ownerId}

Upload one or more files. `multipart/form-data` with part name `files`; antiforgery disabled
(`AttachmentEndpoint.cs:43-51`, handler `171-199`).

- Same `(entity, ownerId, fileName)` as an existing attachment ⇒ **replace**: the blob and the
  row fields are overwritten, `Id` unchanged (unique index + lookup/merge in
  `AttachmentEndpoint.cs:181-195`).
- 201 with **empty body** (`Results.Created()`, `AttachmentEndpoint.cs:198`) — the created/updated
  `Attachment` is *not* returned (deviation 3 in [known-deviations.md](known-deviations.md)). Query
  it afterwards via `allforentityandowner` or download by filename.

```bash
curl -X POST "$BASE/storage/attachment/uploadforentityandowner/document/$OWNER_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -F "files=@report.xml;type=application/xml"
```

## GET /storage/attachment/downloadforentityandownerbyid/{tenantId}/{entity}/{ownerId}/{id}

Download one attachment as a binary stream.

- **Anonymous** — no policy, no token required (`AttachmentEndpoint.cs:35-41`); tenant comes from
  the `{tenantId}` path segment. Gateway mirrors this as an explicitly anonymous route
  (`ballware-apigateway … appsettings.json:130-136`). Treat the URL as capability-by-GUID, not as
  secret — see deviations 1 and 9.
- 200: stream with the record's persisted `ContentType` and a `Content-Disposition` file name
  (`AttachmentEndpoint.cs:279-296`); 404 with a JSON **string** body when the row or the blob is
  missing (`:285`, `:292`).

## DELETE /storage/attachment/dropforentityandownerbyid/{entity}/{ownerId}/{id}

Delete one attachment (blob + metadata row).

- Policy `storageApi`; handler `AttachmentEndpoint.cs:201-222`.
- 200 empty body on success; 404 (JSON string) when the row is missing or has no `StoragePath`
  (`:211`). Note: the lookup is by `id` within the tenant — `entity`/`ownerId` are not part of the
  repository lookup, only of the 404 message and the blob-path call.

## DELETE /storage/attachment/dropallforentityandowner/{entity}/{ownerId}

Delete **all** attachments of one owner record (blob + row for each).

- Policy `storageApi`; handler `AttachmentEndpoint.cs:224-246`. Idempotent: zero matches ⇒ 200.

## GET /storage/temporary/downloadbyid/{tenantId}/{id}

Download a temporary file by caller-chosen id.

- **Anonymous** (`TemporaryEndpoint.cs:25-33`, handler `75-92`); tenant from path.
- 200: stream with persisted `ContentType` + file name (`:91`); 404 JSON string when the row, its
  path, or the blob is missing (`:81`, `:88`).
- Expired temporaries are removed by the cleanup job (30-minute expiry,
  [storage-model.md](../concepts/storage-model.md)) — a download after expiry is a 404.
- Uploads/drops for temporaries exist **only** on the service API (see
  [service-api.md](service-api.md)); user-side clients obtain temporary URLs only (typically
  received from another service, e.g. an export link).

```bash
curl -o export.zip "$BASE/storage/temporary/downloadbyid/$TENANT_ID/$TEMPORARY_ID"
```
