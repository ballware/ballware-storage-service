# Schemas

## `Attachment` (component schema in both snapshots)

The only component schema in `storage.json` and `service.json`. It serializes PascalCase (the
generated DTOs in other languages mirror this via the NSwag/`DefaultContractResolver` config):

| Field | Type | `required`? | `nullable`? | Meaning |
| --- | --- | --- | --- | --- |
| `Id` | string (uuid) | no | false | public identity = DB `uuid` ([storage-model.md](../concepts/storage-model.md)) |
| `Entity` | string | **yes** | **true** | domain entity identifier (`document`, `mlmodel`, …) |
| `OwnerId` | string (uuid) | yes | false | owning record id |
| `FileName` | string | **yes** | **true** | original file name |
| `ContentType` | string | **yes** | **true** | persisted MIME type |
| `FileSize` | integer (int64) | no | false | bytes |
| `StoragePath` | string | **yes** | **true** | provider object key (not a URL) |

**`required` + `nullable: true` tension:** `Entity`, `FileName`, `ContentType`, `StoragePath` are
declared *required* yet *nullable* — an artifact of mapping C# `required` properties
(`Data/Public/Attachment.cs`) where Swashbuckle sees the Newtonsoft-contracted type. Strict
OpenAPI validators (e.g. some codegen linters) flag this; it is a known deviation (6 in
[known-deviations.md](known-deviations.md)), not a runtime problem: the API always sends non-null
values for these fields in practice.

The response of the query operations (`allforentityandowner…`) is a JSON array of this schema.

## `Temporary` (code-only — **not in any snapshot**)

There is **no `Temporary` component schema** in either snapshot: the only temporary operations that
touch data are downloads (binary) and, in the service document, upload (no response body) and drop
(no body) — so nothing ever serializes a `Temporary` object. Fields per the public DTO
(`src/Ballware.Storage.Data/Public/Temporary.cs`):

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | caller-chosen at upload; = DB `uuid` |
| `FileName` | `string` required | |
| `ContentType` | `string` required | persisted, replayed on download |
| `ExpiryDate` | `DateTime` (UTC) required | upload time + 30 min, hard-coded (`TemporaryEndpoint.cs:112`) |
| `FileSize` | `long` required | |
| `StoragePath` | `string` required | `{tenantId}/temporary/{id}/{fileName}` |

If uploads ever return bodies (recommended change R2 in
[known-deviations.md](known-deviations.md)), this schema would appear in the snapshots.

## Multipart upload body

Both upload operations declare (`storage.json` / `service.json`):

```json
"requestBody": {
  "content": {
    "multipart/form-data": {
      "schema": {
        "type": "object",
        "required": ["files"],
        "properties": {
          "files": { "type": "array", "items": { "type": "string", "format": "binary" } }
        }
      },
      "encoding": { "files": { "style": "form" } }
    }
  }
}
```

Part name `files` (repeatable); the Angular client uses `files[]` — both bind to
`IFormFileCollection` ([README.md](README.md#conventions)). Each part's `Content-Type` becomes the
persisted `ContentType` — set it explicitly.

## `ProblemDetails` (500 only)

Unhandled exceptions produce RFC 7807-style JSON (`Startup.cs:242-253`):

```json
{
  "type": "https://httpstatuses.com/500",
  "title": "An unexpected error occurred.",
  "status": 500,
  "detail": "<exception text — Development environment only>",
  "instance": "/storage/attachment/…"
}
```

Content type `application/problem+json`. **404s do not use this shape** — they are bare JSON
strings (`Results.NotFound("…")` — deviation 5).
