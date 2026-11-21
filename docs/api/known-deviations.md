# Known deviations

Implemented behavior that deviates from what a conventional API design would prescribe. Each entry
states the **status quo** (with code evidence), its **implication**, and a **recommendation**. Fix
proposals are documented only — nothing here has been changed in code; the grouped change plan is
at the end.

## 1. User-API downloads (and service attachment downloads) are anonymous

**Status quo.** The attachment download routes — user-API `downloadforentityandownerbyid`
(`AttachmentEndpoint.cs:35-41`), service-API `download…byid`/`…byfilename` (`AttachmentEndpoint.cs:90-104`)
— and the user-API temporary `downloadbyid` (`TemporaryEndpoint.cs:25-33`) carry **no**
`RequireAuthorization`. The tenant arrives via the `{tenantId}` path segment, so access control is
only the unguessability of `{tenantId}`/`{id}` GUIDs. The API gateway encodes this explicitly with
an anonymous route (`storage-anonymous-download`, `ballware-apigateway/src/Ballware.ApiGateway.Service/appsettings.json:130-136`).
The service-API temporary download is the only protected download
(`TemporaryEndpoint.cs:43-51`).

**Implication.** Anyone who learns a tenant id + attachment/temporary id can fetch the file without
any token. For 30-min temporaries this is the intended UX (browser download links); for
attachments it weakens tenant isolation to "GUID secrecy". Angular's `window.open` flow
(`ballware-angular … attachment.store.ts:108`) and the gateway route **depend** on this staying
unauthenticated.

**Recommendation.** Do not simply add `RequireAuthorization` — it breaks browser downloads and the
gateway route. If stronger control is wanted, introduce short-lived signed download URLs (or a
gateway-issued one-time redirect) *together with* the Angular client and gateway rollout. See
proposals R4/H below.

## 2. The global `security` block names `storageApi` in both documents

**Status quo.** SwaggerGen adds one global security requirement built from
`RequiredUserScopes.Split(" ")` (`Startup.cs:208-216`), so the `service` document — whose routes all
enforce `serviceApi` — declares `oidc: ["storageApi"]`
(snapshots: [openapi/service.json](../openapi/service.json) `security`; also
`src/Ballware.Storage.Service.Client/swagger.json:745-751`).

**Implication.** OpenAPI-derived clients/authorizations pre-select the wrong scope for service
operations; Swagger UI "Authorize" won't request `serviceApi`; documentation misstates the required
scope for 12 of 18 operations. Runtime authorization is unaffected (policies are route-level).

**Recommendation.** Specification-only fix (no behavior change): per-document `IDocumentFilter`s
that rewrite the document's `security` array — `storage` → `RequiredUserScopes`, `service` →
`RequiredServiceScopes`.

## 3. Uploads return 201 with an empty body; no `Temporary` schema

**Status quo.** Both upload handlers end in `Results.Created()` — no location header, no body
(`AttachmentEndpoint.cs:198`, `276`; `TemporaryEndpoint.cs:117`). `Temporary` is never serialized,
so no snapshot contains a `Temporary` schema (schemaCount 1 in both documents).

**Implication.** Callers cannot learn the created record (`Id` of an attachment upload; effective
expiry of a temporary) without a follow-up query — this is why consumers invent temporary ids
(see [examples.md](examples.md#3-export-round-trip-with-a-preset-id)).

**Recommendation.** Additive fix: return the produced `Attachment`/`Temporary` as the 201 body
plus matching `Produces<…>` declarations. This pulls the `Temporary` schema into the snapshots —
snapshot regeneration and `ballware-docs` ingestion should follow.

## 4. Downloads declare `application/octet-stream` but send the persisted content type

**Status quo.** Routes declare `Produces<FileStream>(200, contentType: "application/octet-stream")`
(`AttachmentEndpoint.cs:36`, `91`, `99`; `TemporaryEndpoint.cs:26`, `45`), but the actual response
uses the metadata row's `ContentType` (`Results.File(fileContent, attachment.ContentType, …)` —
`AttachmentEndpoint.cs:295`, `314`; `TemporaryEndpoint.cs:91`).

**Implication.** The OpenAPI documents understate the real media type; generated clients treat
every download as opaque binary (harmless today — all consumers just stream — but wrong for strict
clients that trust the declared type).

**Recommendation.** Specification-only fix: declare `Produces` per operation with the meaningful
type — or document "the type equals the uploaded part's type" and keep `octet-stream` only as a
fallback declaration.

## 5. 404 bodies are bare JSON strings, not ProblemDetails

**Status quo.** `Results.NotFound("…")` serializes a JSON *string* (`AttachmentEndpoint.cs:211`,
`285`, `292`, `304`, `311`, `323`, `342`; `TemporaryEndpoint.cs:81`, `88`, `126`); only 500 goes
through ProblemDetails (`Startup.cs:242-253`).

**Implication.** Clients using a `payload?.Message` ProblemDetails fallback silently lose the 404
message — e.g. "is this a missing attachment or a transient error?" distinctions degrade. Note the
cosmetic bug in one message: `Temporary with ID ${id} …` renders a literal `$`
(`TemporaryEndpoint.cs:81`).

**Recommendation.** Align 404s with `Results.Problem(...)`/ProblemDetails as an additive response
shape change (bodies only — status codes stay 404); fix the stray `$` while touching that line.

## 6. `Attachment` fields are simultaneously `required` and `nullable: true`

**Status quo.** `Entity`, `FileName`, `ContentType`, `StoragePath` carry both `required` and
`nullable: true` in both snapshots (see [schemas.md](schemas.md#attachment-component-schema-in-both-snapshots)).

**Implication.** Strict OpenAPI validators/codegen linters flag the schema; hand-written clients may
generate needless null-checks or reject valid payloads.

**Recommendation.** Specification-only fix: DTO contracts without the nullability annotation (or a
Swashbuckle schema processor that drops `nullable` for required string fields).

## 7. No entity/tenant rights evaluation anywhere

**Status quo.** Authorization = scope policy + tenant filter only. `claims` are passed through to
repositories but never evaluated (empty dictionaries throughout the service handlers —
`AttachmentEndpoint.cs:263`, `273`, `281`, `328`, `347`, `366`, `386`, `406`);
`ITenantableRepositoryHook` is never registered (`Data.Ef.Postgres/ServiceCollectionExtensions.cs:30-34`).
`AuthorizationOptions.RightClaim` (`AuthorizationOptions.cs:24`) is configured but unused.

**Implication.** Any caller with the scope reaches **all** owners' attachments in the resolved
tenant; there is no per-entity or per-record permission concept — unlike Meta/Generic/Document,
where rights checks run in those services' endpoint layers before storage calls. The Storage
Service is a trusted internal utility, not an authorization boundary.

**Recommendation.** Keep as designed but document it loudly (done here and in
[concepts/tenancy-and-authorization.md](../concepts/tenancy-and-authorization.md)). If a rights
boundary is ever desired, the repository hook seam (`ITenantableRepositoryHook`) is the designed
insertion point.

## 8. Temporary expiry is hard-coded to 30 minutes

**Status quo.** `ExpiryDate = UtcNow.AddMinutes(30)` (`TemporaryEndpoint.cs:112`); only the cleanup
cron is configurable (`Trigger:TemporaryCleanupCron`, `TriggerOptions.cs:5`).

**Implication.** Larger exports consumed on slow connections can expire mid-download window; no
tenant- or use-case-specific tuning is possible without a code change.

**Recommendation.** Promote the expiry to a `Trigger` option (e.g.
`TemporaryExpiryMinutes`, default 30) — configurable default, no behavior change for existing
deployments.

## 9. `StoragePath` embeds the raw file name

**Status quo.** `GenerateBlobPath` concatenates `{fileName}` without sanitization
(`MinioAttachmentStorageProvider.cs:38-41`, `AzureAttachmentStorageProvider.cs:21-23`), and the
unique index includes the raw name (`StorageModelBaseCustomizer.cs:39`).

**Implication.** A file name containing `/` or `..` reshapes the object key outside the intended
`{tenant}/{entity}/{owner}/` prefix, and such names can't round-trip via by-filename URL segments
anyway (route matching + encoding). Today callers (backend services with fixed names) never send
such names, so the exposure is latent rather than active.

**Recommendation.** Harden only with coordinated rollout (it can orphan existing blobs keyed by
old names): sanitize before `GenerateBlobPath` (strip path separators and `..`) **and** decide
whether legacy rows with affected keys are migrated or grandfathered.

## 10. Configuration-key drift around scope settings (cross-repo)

**Status quo.** The service binds `Authorization:RequiredUserScopes` / `RequiredServiceScopes`
(`AuthorizationOptions.cs:14-15`), but surrounding configs use the legacy key
`Authorization__RequiredScopes`: `ballware-docker-compose/docker-compose.yml:216` and
`appsettings.Swagger.json:5` (`"RequiredScopes": "openid"`). Observed in a developer working copy of
a consumer repo: `ballware-generic-service`'s uncommitted `appsettings.local.json` requests scope
`storageApi` where the service API expects `serviceApi`.

**Implication.** Those settings are silently ignored by this service — compose and the Swagger
environment run on the built-in defaults (`storageApi`/`serviceApi`), so things *accidentally* work;
if defaults or realms drift, consumers will 401 with no obvious cause. Mis-scoped consumer configs
fail with 401 on every service-API call.

**Recommendation.** This repo: rename `RequiredScopes` → `RequiredUserScopes` in
`appsettings.Swagger.json` (unused key today, misleading for readers). Cross-repo (separate
tickets): compose key renames and consumer scope values.

---

## Recommended changes (proposals, grouped by risk)

None implemented — this is the change plan for follow-up work.

**R1 — Specification-only (no runtime behavior change).**

- Per-document `IDocumentFilter` setting the document's `security` array
  (`storage` → `RequiredUserScopes`, `service` → `RequiredServiceScopes`) instead of the single
  global requirement (`Startup.cs:208-216`) — fixes deviation 2.
- Extend CI/snapshot generation to also export the `storage` document
  (currently only `service` is generated, `publish-packages.yml:49-57`) — `docs/openapi/fetch-spec.sh`
  already does both locally.
- Rename `RequiredScopes` → `RequiredUserScopes` in `appsettings.Swagger.json` (deviation 10).
- Drop the misleading `nullable: true` or the `required` flags consistently (deviation 6); declare
  realistic `Produces` content types (deviation 4).

**R2 — Additive (response bodies).**

- Uploads return the created/updated `Attachment`/`Temporary` in the 201 body
  (`AttachmentEndpoint.cs:198`, `276`; `TemporaryEndpoint.cs:117`) — fixes deviation 3 and pulls the
  `Temporary` schema into snapshots (bump API pages + `ballware-docs` ingestion accordingly).
- 404s as ProblemDetails bodies (deviation 5), status codes unchanged.

**R3 — Configurability.**

- Temporary expiry as an option (`Trigger:TemporaryExpiryMinutes`, default 30) replacing the
  hard-coded value at `TemporaryEndpoint.cs:112` (deviation 8).

**R4 — Hardening (behavior change; only with coordinated rollout).**

- File-name sanitization before `GenerateBlobPath` (deviation 9), with a decision on legacy blobs.
- Signed/short-lived download URLs instead of anonymous routes (deviation 1). Must be rolled out
  **together with** `ballware-angular` (`window.open` flow, `attachment.store.ts:108`) and the
  gateway's `storage-anonymous-download` route — adding `RequireAuthorization` alone would break
  both and is explicitly **not** recommended.

**R5 — Foreign repos (separate tickets).**

- `ballware-docker-compose`: `Authorization__RequiredScopes` →
  `Authorization__Required*Scopes` (deviation 10).
- `ballware-generic-service` consumer config: request `serviceApi` for Storage Service-API calls.
