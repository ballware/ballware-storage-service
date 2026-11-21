# API surface

The service exposes the same two resources (`attachment`, `temporary`) on **two documents** with
different audiences and tenant resolution:

| Document | Title (OpenAPI `info.title`) | Audience | Tenant source | Operations |
| --- | --- | --- | --- | --- |
| `storage` | ballware Storage User API | end users (browser clients, Swagger UI) | `tenant` claim (except anonymous downloads) | 6 → [user-api.md](user-api.md) |
| `service` | ballware Storage Service API | backend services (client-credentials tokens) | explicit `{tenantId}` path segment | 12 → [service-api.md](service-api.md) |

The operation inventory below was extracted from the committed snapshots and matches them exactly
([openapi/storage.json](../openapi/storage.json), [openapi/service.json](../openapi/service.json)):

## `storage` document (6 operations)

| Operation | Method | Path | Policy | Responses |
| --- | --- | --- | --- | --- |
| AttachmentAllForEntityAndOwner | GET | `/storage/attachment/allforentityandowner/{entity}/{ownerId}` | `storageApi` | 200, 401 |
| AttachmentDownloadForEntityAndOwnerById | GET | `/storage/attachment/downloadforentityandownerbyid/{tenantId}/{entity}/{ownerId}/{id}` | *none (anonymous)* | 200, 404 |
| AttachmentUploadForEntityAndOwner | POST | `/storage/attachment/uploadforentityandowner/{entity}/{ownerId}` | `storageApi` | 201, 401 |
| AttachmentDropForEntityAndOwnerById | DELETE | `/storage/attachment/dropforentityandownerbyid/{entity}/{ownerId}/{id}` | `storageApi` | 200, 401 |
| AttachmentDropAllForEntityAndOwner | DELETE | `/storage/attachment/dropallforentityandowner/{entity}/{ownerId}` | `storageApi` | 200, 401 |
| TemporaryDownloadById | GET | `/storage/temporary/downloadbyid/{tenantId}/{id}` | *none (anonymous)* | 200, 404 |

## `service` document (12 operations)

| Operation | Method | Path | Policy | Responses |
| --- | --- | --- | --- | --- |
| AttachmentAllForTenantEntityAndOwner | GET | `/storage/attachment/allfortenantentityandowner/{tenantId}/{entity}/{ownerId}` | `serviceApi` | 200, 401 |
| AttachmentDownloadForTenantEntityAndOwnerById | GET | `/storage/attachment/downloadfortenantentityandownerbyid/{tenantId}/{entity}/{ownerId}/{id}` | *none (anonymous)* | 200, 404 |
| AttachmentDownloadForTenantEntityAndOwnerByFilename | GET | `/storage/attachment/downloadfortenantentityandownerbyfilename/{tenantId}/{entity}/{ownerId}/{filename}` | *none (anonymous)* | 200, 404 |
| AttachmentUploadForTenantEntityAndOwnerBehalfOfUser | POST | `/storage/attachment/uploadfortenantentityandownerbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}` | `serviceApi` | 201, 401 |
| AttachmentDropForTenantEntityAndOwnerByIdBehalfOfUser | DELETE | `/storage/attachment/dropfortenantentityandownerbyidbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}/{id}` | `serviceApi` | 200, 401 |
| AttachmentDropForTenantEntityAndOwnerByFilenameBehalfOfUser | DELETE | `/storage/attachment/dropfortenantentityandownerbyfilenamebehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}/{filename}` | `serviceApi` | 200, 401 |
| AttachmentDropAllForTenantEntityAndOwnerBehalfOfUser | DELETE | `/storage/attachment/dropallfortenantentityandownerbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}` | `serviceApi` | 200, 401 |
| AttachmentDropAllForTenantAndEntityBehalfOfUser | DELETE | `/storage/attachment/dropallfortenantandentitybehalfofuser/{tenantId}/{userId}/{entity}` | `serviceApi` | 200, 401 |
| AttachmentDropAllForTenantBehalfOfUser | DELETE | `/storage/attachment/dropallfortenantbehalfofuser/{tenantId}/{userId}` | `serviceApi` | 200, 401 |
| TemporaryDownloadForTenantById | GET | `/storage/temporary/downloadfortenantbyid/{tenantId}/{id}` | `serviceApi` | 200, 401, 404 |
| TemporaryUploadForTenantAndIdBehalfOfUser | POST | `/storage/temporary/uploadfortenantandidbehalfofuser/{tenantId}/{userId}/{id}` | `serviceApi` | 201, 401 |
| TemporaryDropForTenantAndIdBehalfOfUser | DELETE | `/storage/temporary/dropfortenantandidbehalfofuser/{tenantId}/{userId}/{id}` | `serviceApi` | 200, 401 |

(Policy column = `RequireAuthorization` on the route — `AttachmentEndpoint.cs:26-69`, `81-160`,
`TemporaryEndpoint.cs:25-71`; anonymous downloads per `AttachmentEndpoint.cs:35-41`, `90-104`,
`TemporaryEndpoint.cs:25-33`.)

## Conventions

- **Path segments.** GUID parameters bind as `Guid` (`format: uuid` in the snapshots): use
  lowercase canonical form (`d`, e.g. `6c3f…-…`, no braces/hyphens added — .NET round-trips either,
  lowercase is what the generated clients emit). `entity`, `filename` are plain strings — URL-encode
  them client-side (`encodeURIComponent` in JS, `Uri.EscapeDataString` in .NET); `/` inside a file
  name would break the segment ([best-practices.md](best-practices.md)).
- **Field casing.** All JSON is PascalCase (`Startup.cs:136-151`).
- **Multipart uploads.** Part name is `files` (OpenAPI declaration, .NET clients) — the Angular
  client sends `files[]`; both bind to `IFormFileCollection files` because the ASP.NET binder
  accepts the indexed variant (`AttachmentEndpoint.cs:171`, `253`; `TemporaryEndpoint.cs:94`), and
  antiforgery is disabled on upload routes (`DisableAntiforgery()`, `AttachmentEndpoint.cs:45`,
  `108`; `TemporaryEndpoint.cs:55`).
- **Upload request form.** `multipart/form-data`, any number of `files` parts. The per-part
  `Content-Type` header is **persisted** as `ContentType` and replayed on downloads — always set it
  explicitly (`AttachmentEndpoint.cs:191`, `269`; `TemporaryEndpoint.cs:109`). A part without a
  content type persists `null`/empty semantics into a schema field that is declared `required`.
- **Downloads.** Respond with the **persisted** `ContentType` of the record (not `octet-stream` as
  declared) plus `Content-Disposition` carrying the file name (`Results.File(content, contentType,
  fileName)` — `AttachmentEndpoint.cs:295`, `314`; `TemporaryEndpoint.cs:91`; deviation 4 in
  [known-deviations.md](known-deviations.md)).

## Status-code matrix

| Code | When | Body |
| --- | --- | --- |
| 200 | query (JSON `Attachment[]`), download (binary stream), drop operations (empty) | JSON or stream; drops return empty body |
| 201 | uploads (`Results.Created()`) | **empty body** — created metadata is **not** returned (deviation 3) |
| 401 | protected routes without a matching scope | empty |
| 404 | metadata row missing, or `StoragePath` empty, or blob missing in provider | **JSON string** message — not ProblemDetails (deviation 5) |
| 500 | unhandled exception | `application/problem+json` (`Startup.cs:242-253`) |

## Consumers (observed in the sibling services)

- **Meta / Generic services** use only the temporary trio
  (`TemporaryUploadForTenantAndIdBehalfOfUser`, `TemporaryDownloadForTenantById`,
  `TemporaryDropForTenantAndIdBehalfOfUser`) for import/export hand-off
  (e.g. `ballware-generic-service/src/Ballware.Generic.Service/Adapter/StorageServiceFileStorageAdapter.cs:18`,
  `ballware-meta-service/src/Ballware.Meta.Service/Adapter/StorageServiceFileStorageAdapter.cs:18`).
- **Document service** stores generated documents as attachments under entity `document` with fixed
  file name `report.xml`
  (`ballware-document-service/src/Ballware.Document.Service/Extensions/DocumentImportExportRepositoryHook.cs:19,43,50`).
- **ML service** stores models under entity `mlmodel` with fixed name `model.zip`
  (`ballware-ml-service/src/Ballware.Ml.Engine.AutoMl/Internal/AutoMlExecutor.cs:106,200`).
- **LLM services** attach source/image payloads under entities like `origin` and `image` (platform
  convention; not wired in this repo).
- **Angular** (`ballware-angular`, `ngrx-meta-services`) uses the **User API** only, and opens
  anonymous download URLs via `window.open`
  (`libs/ngrx-meta-services/src/lib/attachment/attachment.store.ts:108`) — the download URL design
  depends on this staying anonymous.
- **.NET backend clients** use the generated `StorageServiceClient` package (this repo's client
  project; release line 9.3.x). Consumers configure it via
  `StorageClient__ServiceUrl`, `StorageClient__TokenEndpoint`, `StorageClient__ClientId`,
  `StorageClient__ClientSecret`, `StorageClient__Scopes` and obtain client-credentials tokens with
  scope `serviceApi` (compose wiring: `ballware-docker-compose/docker-compose.yml:258-262`).

See [examples.md](examples.md) for copy-paste sequences and the .NET client snippet.
