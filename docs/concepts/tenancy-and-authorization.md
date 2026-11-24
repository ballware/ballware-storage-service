# Tenancy and authorization

The Storage Service authorizes at the **scope/policy layer only** and isolates data at the
**repository layer** via a tenant filter. It performs **no** entity- or record-level rights checks —
unlike Meta/Generic/Document, where `meta.<entity>.<right>`-style checks happen in the endpoint
layer of those services.

## Policies

Two named policies are built from the `Authorization` options
(`src/Ballware.Storage.Service/Startup.cs:107-123`):

| Policy | Satisfied when | Config knob (default) |
| --- | --- | --- |
| `storageApi` | the JWT `scope` claim (split on spaces) contains any value from `Authorization:RequiredUserScopes` (`storageApi`) | `RequiredUserScopes`, `Configuration/AuthorizationOptions.cs:14` |
| `serviceApi` | the JWT `scope` claim contains any value from `Authorization:RequiredServiceScopes` (`serviceApi`) | `RequiredServiceScopes`, `AuthorizationOptions.cs:15` |

Authentication is JWT bearer against `Authorization:Authority` + `Authorization:Audience` with
`MapInboundClaims = false`, so claim types are taken verbatim (`Startup.cs:91-105`).

There is **no fallback policy**: only routes explicitly decorated with
`.RequireAuthorization("storageApi"|"serviceApi")` are protected. Every route *without* it is
anonymous — currently all four attachment/temporary download routes **except** the service-API
temporary download: `AttachmentEndpoint.cs:35-41` (user byId), `90-104` (service byId + byFilename),
`TemporaryEndpoint.cs:25-33` (user temporary byId); the service temporary download **is** protected
(`TemporaryEndpoint.cs:43-51`) — see deviation 1 in
[api/known-deviations.md](../api/known-deviations.md).

## Tenant resolution differs per API surface

| Surface | Tenant source | User identification |
| --- | --- | --- |
| User API (`storage` document) | `tenant` claim via `IPrincipalUtils.GetUserTenandId` (`AttachmentEndpoint.cs:166`, `173`, `203`, `226`) | authenticated user (`sub` claim) |
| User temporary download | `{tenantId}` **from the path** (route is anonymous) (`TemporaryEndpoint.cs:25`, `75`) | none |
| Service API (`service` document) | `{tenantId}` **from the path** (e.g. `AttachmentEndpoint.cs:248`, `253`) | `{userId}` path parameter — recorded "behalf of user" |

Consequences for URL/payload design: User API paths do **not** contain a tenant segment for
query/upload/drop operations (the claim decides it); Service API paths always carry an explicit
`{tenantId}`, and `behalf-of-user` operations additionally carry `{userId}`.

The claims used are configurable (`Configuration/AuthorizationOptions.cs:18-24`): `TenantClaim`
(default `tenant`), `UserIdClaim` (default `sub`), `RightClaim` (default `right`). Note that while
`RightClaim` is configured and passed around, it is **never evaluated** in this service (see below).

## Repository-level behavior

Repositories derive from `TenantableBaseRepository` in `Ballware.Shared.Data.Ef` **1.2.0**
(e.g. `src/Ballware.Storage.Data.Ef/Repository/AttachmentBaseRepository.cs:9`) and are registered
by the EF provider packages (`Data.Ef.Postgres/ServiceCollectionExtensions.cs:30-34`). Per shared
repository behavior at this version:

- **Tenant filter:** reads/writes are scoped to the tenant (`WHERE tenant_id = @tenantId AND
  uuid = @id` for id lookups) plus the storage-specific filters
  (`AttachmentBaseRepository.cs:18-45`, `TemporaryBaseRepository.cs:19-24`).
- **Audit fields:** `creator_id`/`create_stamp` and `last_changer_id`/`last_change_stamp` are
  populated from the `userId` passed to `SaveAsync`/`RemoveAsync` (persistable columns
  `Persistables/Blob.cs:22-35`; endpoint call sites e.g. `AttachmentEndpoint.cs:195`, `216`).
- **No rights check.** The `claims` dictionary passed to repository methods is forwarded but not
  evaluated for authorization in this service. The Service API handlers pass an empty dictionary
  (`ImmutableDictionary<string, object>.Empty` — `AttachmentEndpoint.cs:263`, `273`, `281`, `328`,
  `347`, `366`, `386`, `406`; `TemporaryEndpoint.cs:77`, `100`, `104`, `114`, `122`, `131`), which
  causes no rejection: it simply means "no extra claims to carry".
- **Repository hooks are inert here.** `ITenantableRepositoryHook` is a constructor parameter with a
  null default (`AttachmentBaseRepository.cs:13`, `TemporaryBaseRepository.cs:14`) and no
  implementation is ever registered in this service, so the hook is always `null`
  (registration without hook: `Data.Ef.Postgres/ServiceCollectionExtensions.cs:30-34`). Hooks are
  where other services attach schema/rights behavior — the Storage Service has none.

## Keycloak side (realm used by ballware-docker-compose)

In `ballware-docker-compose/realms/ballware-realm.tpl`:

- Browser client **`ballwareweb`** (`ballware-realm.tpl:609-610`) lists `storageApi` among its
  **optional** client scopes (`ballware-realm.tpl:716`) — Swagger UI and the Angular app request it
  interactively.
- Machine client **`storage`** (`ballware-realm.tpl:1201-1213`) has `serviceApi` as a **default**
  client scope (`ballware-realm.tpl:1292`), i.e. every client-credentials token issued for it
  already carries `serviceApi`. `serviceApi` is defined as a realm client scope at
  `ballware-realm.tpl:1375`.

The scope values issued by Keycloak must match the service's configured
`RequiredUserScopes`/`RequiredServiceScopes` strings, because the policies compare the `scope`
claim values token-verbatim after splitting on spaces (`Startup.cs:111-115`, `120-123`). A mismatch
(e.g. compose setting only `Authorization__RequiredScopes`) yields 401s on every protected route —
see deviation 10 in [api/known-deviations.md](../api/known-deviations.md).

## Anonymous downloads and the gateway

The download routes are anonymous by design so browsers can open them directly (`window.open`), and
the API gateway mirrors this: the route `storage-anonymous-download` maps
`/storage/attachment/downloadforentityandownerbyid/{**catch-all}` with
`"AuthorizationPolicy": "Anonymous"`
(`ballware-apigateway/src/Ballware.ApiGateway.Service/appsettings.json:130-136`). Access control for
anonymous downloads therefore rests on unguessable GUIDs in the path (`{tenantId}`, `{id}`) —
explicitly **not** a strong authorization mechanism; see deviations 1 and 9 in
[api/known-deviations.md](../api/known-deviations.md).

## What this service does **not** do

- No `meta.<entity>.<right>` evaluation, no rights scripts, no entity permission tables — a caller
  with `storageApi`/`serviceApi` scope can access **any** owner's attachments **within the resolved
  tenant**.
- No validation that `{ownerId}` references an existing record — `ownerId` is just a GUID column
  (`Persistables/Attachment.cs:15`).
- No per-entity scope differentiation — the scope gates the whole surface, not individual entities.
