# OpenAPI snapshots

The committed files in this folder are the source of truth the API pages were written against:

| File | Content |
| --- | --- |
| [storage.json](storage.json) | `ballware Storage User API` — 6 paths, 1 component schema (`Attachment`), OpenAPI `3.0.1` |
| [service.json](service.json) | `ballware Storage Service API` — 12 paths, 1 component schema (`Attachment`), OpenAPI `3.0.1` |
| [manifest.json](manifest.json) | Provenance: generator tool + version, environment, git describe reference, `fetchedAt`, per-document openapi/title/pathCount/schemaCount |
| [fetch-spec.sh](fetch-spec.sh) | Generator script |

## How the snapshot is produced

Generation runs **offline from the built assembly** — no database, no IdP. The CI pipeline uses the
same mechanism for the client build (`.github/workflows/publish-packages.yml:49-57`); this folder
extends it to also export the `storage` (user) document, which CI does not currently generate.

Run from the repository root (idempotent — it rebuilds and rewrites the three files):

```bash
docs/openapi/fetch-spec.sh
```

What it does:

1. `dotnet build src/Ballware.Storage.Service --configuration Release`
2. Ensures the pinned tool via the local tool manifest (what CI does):
   `dotnet new tool-manifest || true` + `dotnet tool update Swashbuckle.AspNetCore.Cli --version 7.2.0`.
   If the repository had no tool manifest before the run, the one created by the script is removed
   again afterwards — the working tree stays clean.
3. Invokes the tool. Because host SDKs newer than the tool's highest bundled target framework make
   `dotnet tool run swagger` pick a tool variant that may need a runtime which is not installed
   (the 7.2.0 package bundles `net6.0`, `net8.0` and `net9.0` variants), the script invokes the
   variant from the NuGet package cache that matches the service's target framework
   (`net8.0`) — it always binds against the same framework as the service assembly:
   `~/.nuget/packages/swashbuckle.aspnetcore.cli/7.2.0/tools/net8.0/any/dotnet-swagger.dll`
4. Exports both documents with the Swagger environment:

   ```bash
   export ASPNETCORE_ENVIRONMENT=Swagger
   export Authorization__Authority=https://localhost
   cd src/Ballware.Storage.Service
   swagger tofile --output ../../docs/openapi/storage.json bin/Release/net8.0/Ballware.Storage.Service.dll storage
   swagger tofile --output ../../docs/openapi/service.json bin/Release/net8.0/Ballware.Storage.Service.dll service
   ```

5. Validates each file (parses as JSON, `openapi` starts with `3.`, `paths` non-empty) and writes
   `manifest.json` with `pathCount`/`schemaCount` per document.

The `swagger tofile` tool resolves the host from the assembly **without starting it**, so the EF
initialization worker (`src/Ballware.Storage.Data.Ef.Postgres/Internal/InitializationWorker.cs:23-28`)
and the Quartz server never run — no Postgres, no Keycloak, no blob storage needed.

## Why `ASPNETCORE_ENVIRONMENT=Swagger`

`appsettings.Swagger.json` (`src/Ballware.Storage.Service/appsettings.Swagger.json`) supplies **fake**
Authority/clients (`https://localhost`, `fake` client id). Consequently the `oidc` security definition
in the snapshots points at a non-existent endpoint:

```json
"oidc": { "type": "openIdConnect", "openIdConnectUrl": "https://localhost/.well-known/openid-configuration" }
```

That URL is a placeholder, **not** a real IdP discovery endpoint. Do not wire clients to it.

> **Leak warning:** configuration order is
> `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.local.json` → environment
> variables (`src/Ballware.Storage.Service/Program.cs:18-22`). A developer
> `appsettings.local.json` overrides the Swagger values — including the Authority — and its real IdP
> URL would be baked into the committed snapshot. The script therefore pins
> `Authorization__Authority=https://localhost` as an environment override. When regenerating by hand
> (or in CI), generate from a clean checkout without `appsettings.local.json`.

## Tool version pin

Keep `Swashbuckle.AspNetCore.Cli` at **7.2.0** — it matches the service's Swashbuckle packages
(`src/Ballware.Storage.Service/Ballware.Storage.Service.csproj:27-29`) and the CI tool pin
(`.github/workflows/publish-packages.yml:32`). Sibling services use Swashbuckle 10.x; **this repo
stays on 7.2.0** — other versions reformat the documents (e.g. OpenAPI `3.0.4` instead of `3.0.1`)
and create spurious diffs.

## Private feed (restore requirements)

The build needs the private `Ballware.Shared.* 0.0.2` packages from GitHub Packages. Locally they
resolve from the `~/.nuget/packages` cache; in a clean CI environment a GitHub source with a PAT is
required (CI does this via `dotnet nuget add source https://nuget.pkg.github.com/ballware/...` with
the workflow token, `.github/workflows/publish-packages.yml:38-46`).

## Live-server fallback

If the service runs locally (`dotnet run --project src/Ballware.Storage.Service` in the Development
environment, ports per `appsettings.Development.json`), the raw documents can also be fetched from
the running service:

```bash
curl http://localhost:5005/swagger/storage/swagger.json
curl http://localhost:5005/swagger/service/swagger.json
```

Caution: in the Development environment the `oidc` `openIdConnectUrl` reflects the *configured*
`Authorization:Authority` (possibly a real IdP from `appsettings.local.json`) — use this only for
inspection, never commit the result unfiltered.

## Refresh cadence and drift

The snapshot is a manual artifact: regenerate after endpoint or DTO changes (new/renamed routes,
schema changes, Swagger config). A re-run should only change `manifest.json`'s `fetchedAt`; if
`storage.json`/`service.json` also change, review the diff — it is an API surface change and the API
pages in [../api/](../api/README.md) must be reviewed against it (expected counts: `storage` 6/1,
`service` 12/1). Regenerating does not require touching any source code.

## Known snapshot quirks

The snapshots describe the **implemented** behavior, including its deviations — e.g. the global
`security` block uses the `storageApi` scope even in the `service` document, and upload operations
answer `201` with an empty body. See [../api/known-deviations.md](../api/known-deviations.md).
