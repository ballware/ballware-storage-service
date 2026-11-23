# Service API (`service` document)

Twelve operations for backend-to-backend calls. The tenant is **always** the `{tenantId}` path
segment; `behalf-of-user` operations additionally take `{userId}`, which is used **only to fill
audit columns** (`creator_id`, `last_changer_id`) — it does not authorize, filter, or validate
anything (`Startup.cs:107-123` gates on the `serviceApi` scope; repository audit per
`Persistables/Blob.cs:22-35`). Snapshot:
[openapi/service.json](../openapi/service.json) (12 paths, schema `Attachment`).

## Attachment operations

### GET /storage/attachment/allfortenantentityandowner/{tenantId}/{entity}/{ownerId}

Query list, same payload as the user API. Policy `serviceApi`
(`AttachmentEndpoint.cs:81-88`, handler `248-251`).

### GET /storage/attachment/downloadfortenantentityandownerbyid/{tenantId}/{entity}/{ownerId}/{id}

**Anonymous** download by attachment id (`AttachmentEndpoint.cs:90-96`, handler `279-296`). 200
stream (persisted `ContentType`, `Content-Disposition`), 404 JSON string on missing row/blob.

### GET /storage/attachment/downloadfortenantentityandownerbyfilename/{tenantId}/{entity}/{ownerId}/{filename}

**Anonymous** download by file name — the fixed-file-name convention (`report.xml`, `model.zip`)
makes this the workhorse for backend round-trips
(`AttachmentEndpoint.cs:98-104`, handler `298-315`). `{filename}` is a single URL segment:
URL-encode it; a name containing `/` would not match the route (and would corrupt `StoragePath` —
deviation 9).

### POST /storage/attachment/uploadfortenantentityandownerbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}

Upload, multipart part `files`, replace-on-same-name semantics identical to the user API
(`AttachmentEndpoint.cs:106-114`, handler `253-277`). 201 empty body (`:276`). Audit stamps record
`userId`; repository calls pass **empty claims** (`ImmutableDictionary<string, object>.Empty` —
`:263`, `:273`), which is accepted (no claims to carry, no rejection).

```bash
curl -X POST "$BASE/storage/attachment/uploadfortenantentityandownerbehalfofuser/$TENANT_ID/$USER_ID/document/$OWNER_ID" \
  -H "Authorization: Bearer $SERVICE_TOKEN" \
  -F "files=@report.xml;type=application/xml"
```

### DELETE /storage/attachment/dropfortenantentityandownerbyidbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}/{id}

Single drop by id (`AttachmentEndpoint.cs:116-123`, handler `317-334`). 200 empty / 404 JSON string.

### DELETE /storage/attachment/dropfortenantentityandownerbyfilenamebehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}/{filename}

Single drop by file name (`AttachmentEndpoint.cs:125-132`, handler `336-353`).

### DELETE /storage/attachment/dropallfortenantentityandownerbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}

Bulk drop of **one owner record**: lists all attachments for `(tenant, entity, ownerId)` and drops
each blob + row (`AttachmentEndpoint.cs:134-141`, handler `355-373`). Idempotent (empty list ⇒ 200).

### DELETE /storage/attachment/dropallfortenantandentitybehalfofuser/{tenantId}/{userId}/{entity}

Bulk drop of **all attachments of one entity across the whole tenant** — e.g. wiping every
document of every owner when the entity is deprecated (`AttachmentEndpoint.cs:143-150`, handler
`375-393`: `AllByEntityAsync(tenantId, entity)` then per-item drop). This is tenant-wide — call it
only for entity lifecycle events.

### DELETE /storage/attachment/dropallfortenantbehalfofuser/{tenantId}/{userId}

**Tenant-level wipe**: all attachment rows *and* blobs of a tenant
(`AttachmentEndpoint.cs:152-159`, handler `395-413`: `AllAsync(tenantId)` then per-item drop).
Intended for tenant deletion flows; destructive and unscoped by entity/owner.

Bulk drops are sequential per item (no bulk SQL), 200 even for empty sets, and each removed row
audits `userId` while blobs are deleted provider-side.

## Temporary operations

### GET /storage/temporary/downloadfortenantbyid/{tenantId}/{id}

Download a temporary — **this one is protected** by `serviceApi`
(`TemporaryEndpoint.cs:43-51`, handler `75-92`), unlike the anonymous user-API twin. 200 stream /
401 / 404 JSON string.

### POST /storage/temporary/uploadfortenantandidbehalfofuser/{tenantId}/{userId}/{id}

Upload one or more files under a **caller-chosen** temporary id `{id}`. If the id already exists,
the same row is overwritten (upload → lookup by id → update, `TemporaryEndpoint.cs:94-118`, esp.
`:100-107`), with `ExpiryDate = now + 30 min` re-set on every upload (`:112`). 201 empty body
(`:117`). Generate the id client-side (`Guid.NewGuid()`) so the subsequent download link is
predictable — see [examples.md](examples.md#3-export-round-trip-with-a-preset-id) and
[best-practices.md](best-practices.md).

```bash
curl -X POST "$BASE/storage/temporary/uploadfortenantandidbehalfofuser/$TENANT_ID/$USER_ID/$TEMPORARY_ID" \
  -H "Authorization: Bearer $SERVICE_TOKEN" \
  -F "files=@export.zip;type=application/zip"
```

### DELETE /storage/temporary/dropfortenantandidbehalfofuser/{tenantId}/{userId}/{id}

Remove blob + row for one temporary (`TemporaryEndpoint.cs:63-70`, handler `120-137`). 200 empty /
404 JSON string. Explicit drops are optional — expiry + cleanup job reclaim automatically — but
consumers typically drop after a successful download to free space early.
