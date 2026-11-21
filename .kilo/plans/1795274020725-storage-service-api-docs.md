# docs/ für ballware-storage-service (API- und Funktions-Dokumentation)

Ziel: Ein `docs/`-Ordner im Service-Repository, der API-Fläche, Request-/Response-Payloads,
Datenmodell, Provider-Verhalten und Autorisierung erklärt — gespiegelt an den bereits existierenden
Docs von `ballware-meta-service`, `ballware-document-service` und `ballware-generic-service`, damit
`ballware-docs` die Seiten später übernehmen kann. **Keine Quellcode-Änderungen** (Fixes nur als
Vorschläge, siehe Task 8).

## Kontext (verifiziert)

- Zwei Swagger-Dokumente: `storage` ("ballware Storage User API") und `service`
  ("ballware Storage Service API") — `src/Ballware.Storage.Service/Startup.cs:186-198`; gemappte Routen
  `Startup.cs:263-266`. Pfade/Anzahl: **6 Pfade** im User-Dokument, **12 Pfade** im Service-Dokument,
  je **1 Schema** (`Attachment`). Referenz: `src/Ballware.Storage.Service.Client/swagger.json`
  (service-Dokument) und `ballware-docs/openapi/storage/{storage,service}.json`.
- CI erzeugt bisher **nur** das `service`-Dokument (`.github/workflows/publish-packages.yml:50-58`,
  `ASPNETCORE_ENVIRONMENT=Swagger`, `swagger tofile … service`), danach NSwag-Client
  (`nswag.json`, `StorageServiceClient.cs`).
- Swashbuckle.AspNetCore **7.2.0** (`src/Ballware.Storage.Service/Ballware.Storage.Service.csproj:27-29`),
  CI-Toolpin `Swashbuckle.AspNetCore.Cli 7.2.0` (`publish-packages.yml:32`). Die Sibling-Repos nutzen
  10.x — für dieses Repo gilt **7.2.0**, um Diff-Noise zu vermeiden.
- Config-Reihenfolge `Program.cs:18-22`; Dev-Ports 5005/6005 (`appsettings.Development.json:8-20`);
  Compose-publish 5005 (`ballware-docker-compose/docker-compose.yml:201-204`); Body-Limit
  40 000 000 B (`appsettings.json:35-39`); JSON PascalCase (`Startup.cs:136-151`).

## Getroffene Entscheidungen

1. Doku beschreibt den **Ist-Zustand** inkl. eigener Known-Deviations-Seite; Fix-Vorschläge stehen im
   Plan (Task 8) und in `docs/api/known-deviations.md`, **ohne** Codeänderung.
2. `docs/openapi/` enthält beide Snapshots (`storage.json`, `service.json`), `manifest.json` und
   `fetch-spec.sh`, offline generiert aus der Build-Assembly, Toolpin **Swashbuckle.AspNetCore.Cli 7.2.0**,
   `ASPNETCORE_ENVIRONMENT=Swagger` plus `Authorization__Authority=https://localhost` (Override).
3. Struktur = Spiegel der Sibling-Services (`concepts/`, `api/`, `openapi/`) + zusätzliche
   `api/known-deviations.md`.
4. Konsumenten-Kontext nur als **Kurzabschnitt** in `docs/api/README.md` (kein eigenes Kapitel).
5. Sprache: **Englisch** (wie `README.md` und alle Sibling-Docs).
6. Inhalte aus `Documents/GitHub/ballware-fwpro/**` (Tenant-/Entity-Metadaten) werden **nicht**
   übernommen; sie dienten nur der Recherche. Nur anonymisierte Konventions-Templates fließen in
   `docs/api/best-practices.md`.
7. `README.md` der Repository-Wurzel bekommt nur einen kurzen „Documentation“-Abschnitt mit Link auf
   `docs/README.md` (Setup-Teil bleibt unangetastet).

## Task 1 — OpenAPI-Snapshot-Erzeugung

Neu: `docs/openapi/fetch-spec.sh` (Vorbild: `ballware-meta-service/docs/openapi/fetch-spec.sh`),
angepasst auf dieses Repo:

- `TOOL_VERSION="7.2.0"`, `CONFIG_PROJECT="src/Ballware.Storage.Service"`,
  Dll `bin/Release/net8.0/Ballware.Storage.Service.dll`, Dokument-Schleife `for doc in storage service`.
- `dotnet build src/Ballware.Storage.Service --configuration Release`, Tool-Installation über
  PATH-Check mit Fallback auf Tool-Manifest (`dotnet tool run swagger …`) wie in der CI.
- Export mit `export ASPNETCORE_ENVIRONMENT=Swagger` und
  `export Authorization__Authority="https://localhost"` (Zwang! `appsettings.local.json` liegt in dieser
  Arbeitskopie und würde sonst echte IdP-/Azure-Werte in den Snapshot backen — Leak-Grenze analog
  `ballware-meta-service/docs/openapi/README.md:60-66`).
- Validierung + `manifest.json` (Felder identisch zu den Siblings: `source`, `toolVersion`,
  `environment`, `serviceVersion` via `git describe --tags --always --dirty`, `fetchedAt`,
  `documents.{storage,service}.{file,openapi,title,pathCount,schemaCount}`).
- Live-Fallback im README dokumentieren (`curl http://localhost:5005/swagger/{storage|service}/swagger.json`).

Artefakte (committen): `docs/openapi/storage.json`, `docs/openapi/service.json`,
`docs/openapi/manifest.json`. Erwartete Counts: **storage 6/1**, **service 12/1**.
`.gitignore` blockiert die Dateien nicht (kein pauschales `*.json`; nur
`appsettings.local.json`, Zeile 488).

## Task 2 — `docs/README.md`

Index mit Inhalts-Tabelle (Page → Topic), Quick facts und Request-Flow-Diagramm (Textblock):

- zwei Dokumente + Counts + OpenAPI `3.0.1`, Swagger-UI `https://localhost:6005/swagger`
  (`Startup.cs:279-288`), Raw-Dokumente `/swagger/{storage|service}/swagger.json`
- Ports: Dev `http://localhost:5005` / `https://localhost:6005`, Container 5000/5001 (README),
  Compose `5005`
- Auth: JWT Bearer + Policies `storageApi` / `serviceApi` (`Startup.cs:105-121`), Claims-Konfiguration
  `tenant` / `sub` / `right` (`Configuration/AuthorizationOptions.cs:14-24`)
- JSON PascalCase; Multipart-Feld `files` (OpenAPI) bzw. `files[]` (Angular)
- Scope-Hinweis: diese Doku ist die Backend-Sicht; Setup/Konfiguration bleibt im Repo-`README.md`;
  Frontend-Metadaten-Vertrags liegt in `ballware-angular/docs/metadata` (nicht duplizieren)

Flow-Block: `client → JWT/Policy → Endpoint (User: Tenant aus „tenant“-Claim, Service: {tenantId}
aus Pfad) → Provider (Azure/MinIO, Blob-Pfad) + Tenantable-Repository (Tenant-Filter, Audit) →
EF Core (postgres|mssql) → Quartz-Cleanup für temporäre Dateien`.

## Task 3 — `docs/concepts/architecture.md`

- Projekt-/Layer-Layout analog Repo-README, mit Zuständigkeit je Projekt
  (`Api`, `Data`, `Data.Ef`, `Data.Ef.Postgres`, `Data.Ef.SqlServer`, `Provider.Azure`,
  `Provider.Minio`, `Jobs`, `Metadata`, `Service`, `Service.Client`)
- Pipeline: `Program.cs:18-22` (Config-Reihenfolge inkl. `appsettings.local.json`),
  `Startup.cs:136-151` (PascalCase, Newtonsoft), `Startup.cs:231-256` (Fehlervertrag),
  `Startup.cs:258-266` (Routing), `Startup.cs:271-288` (Swagger/OIDC-PKCE)
- Fehlervertrag explizit: **500** als `application/problem+json`
  (`Startup.cs:242-253`); **404**/andere als `Results.NotFound("<Text>")` → JSON-String als Body,
  **kein** ProblemDetails (`AttachmentEndpoint.cs:211`, `285`, `304`; `TemporaryEndpoint.cs:81`, `88`)
- Request-Limit 40 MB (`appsettings.json:35-39`), Quartz-Setup
  (`Ballware.Storage.Jobs/ServiceCollectionExtensions.cs:11-32`), initialisierung via
  EF-Migrations-HostedService (`Data.Ef.Postgres/Internal/InitializationWorker.cs:23-28`)
- Versions-Klammer: Service pinnt `Ballware.Shared.* 0.0.2` (net8.0/AutoMapper), die aktuelle
  Shared-Basis nutzt neuere Namen (`TenantableBaseRepository` statt `TenantableRepository`) — nur als
  Wartungshinweis, nicht als Fehler behaupten

## Task 4 — `docs/concepts/tenancy-and-authorization.md`

- Policies: `storageApi` ← `Authorization:RequiredUserScopes` (Default `storageApi`),
  `serviceApi` ← `RequiredServiceScopes` (Default `serviceApi`); Scope-Split auf Leerzeichen
  (`Startup.cs:105-121`). Kein Fallback-Policy → Routen ohne `RequireAuthorization` sind anonym.
- Tenanzaufösung: **User-API** = `tenant`-Claim via `IPrincipalUtils.GetUserTenandId`
  (`AttachmentEndpoint.cs:166`, `173`; `Temporary`-Download aus Pfad), **Service-API** = explizites
  `{tenantId}` (+ `{userId}` als „behalf of user“) — Konsequenz für Payload-/URL-Design
- Repository-Ebene (geteilt, via `Ballware.Shared.Data.Ef` 0.0.2): Tenant-Filter
  (`WHERE tenant_id = @tenantId AND uuid = @id`), Audit-Felder aus `userId`, **keine** Rights-Prüfung;
  `claims` wird nur weitergereicht, leere `claims` (Service-Handler, `ImmutableDictionary<…>.Empty` —
  `AttachmentEndpoint.cs:263,273,281,328,347,366,386,406`) führen zu keiner Ablehnung;
  `ITenantableRepositoryHook` ist im Storage-Service nie registriert (Hook immer `null`,
  `Data.Ef.Postgres/ServiceCollectionExtensions.cs:30-34`)
- Keycloak-Seite: Client `ballwareweb` (Browser, `storageApi` optional), Maschinen-Client `storage`
  (`serviceApi` als default client scope) — Quelle
  `ballware-docker-compose/realms/ballware-realm.tpl:1201-1213,1292,1375,1739`; Scope-Werte müssen zu
  `RequiredUserScopes`/`RequiredServiceScopes` passen
- Explizit: diese Services kennt **keine** `meta.<entity>.<right>`-Rechteprüfung (Unterschied zu
  Meta/Generic/Document — dort passiert Rights-Check im Endpoint-Layer, nicht hier)

## Task 5 — `docs/concepts/storage-model.md`

- Öffentliche DTOs vs. Persistables:
  - `Attachment` (public) `Id, Entity, OwnerId, FileName, ContentType, FileSize, StoragePath`
    (`Data/Public/Attachment.cs`) ↔ `attachment`-Tabelle `id` (bigint Identity), `uuid`, `tenant_id`,
    `creator_id`, `create_stamp`, `last_changer_id`, `last_change_stamp`, `file_name`, `content_type`,
    `file_size`, `storage_path`, `entity`, `owner_id` (`Data/Persistables/Blob.cs`, `…/Attachment.cs`)
  - `Temporary` (public) `Id, FileName, ContentType, ExpiryDate, FileSize, StoragePath`
    (`Data/Public/Temporary.cs`) ↔ `temporary`-Tabelle + `expiry_date`
  - Public `Id` = Persistable `uuid` (AutoMapper-Profil `Data.Ef/Internal/StorageMappingProfile.cs`);
    interne `id`/`tenant_id`/Audit-Spalten sind `JsonIgnore` und werden nie exponiert
- Indizes (`Data.Ef/Model/StorageModelBaseCustomizer.cs:36-43`): unique `(tenant_id, uuid)`,
  `(tenant_id)`, unique `(tenant_id, entity, owner_id, file_name)` → **ein Blob pro Dateiname pro
  Besitzer**; Re-Upload ersetzt Blob und aktualisiert die bestehende Zeile (Id bleibt gleich,
  `AttachmentEndpoint.cs:179-195`)
- UTC-ValueConverter für alle `DateTime` (Zeilen 20-32) — Zeitstempel/Payloads sind UTC
- `identifier` ist im Storage-Service stets `"primary"` und nur ein Label (keine Query-Auswahl,
  keine Rechteableitung)
- `StoragePath` = Provider-Objekt-Key, **nicht** downloadbar/interpretierbar vom Client; Layout:
  Attachment `{tenantId}/{entity}/{ownerId}/{fileName}`, Temporary
  `{tenantId}/temporary/{temporaryId}/{fileName}`
  (`Provider.Minio/Internal/Minio*StorageProvider.cs:38-41`,
  `Provider.Azure/Internal/Azure*StorageProvider.cs:21-24`)
- Expiry: `ExpiryDate = UtcNow + 30 min` hart kodiert (`TemporaryEndpoint.cs:112`)

## Task 6 — `docs/concepts/storage-providers-and-jobs.md`

- Provider-Auswahl `Storage:Provider` = `azure` | `minio` (`Startup.cs:171-178`,
  `Configuration/StorageOptions.cs`); Sektionen `AzureStorage` (`ConnectionString`, `ContainerName`)
  bzw. `MinioStorage` (`Endpoint`, `AccessKey`, `SecretKey`, `BucketName`, `UseSSL`)
- Verhalten: Container/Bucket wird bei jedem Aufruf `CreateIfNotExists`/`BucketExists`
  (`Azure…:12-19`, `Minio…:14-20`); Content-Type wird am Blob gesetzt; Existenz-Check vor
  Download/Delete; fehlender Blob ⇒ `null` ⇒ HTTP 404
- Streaming: Upload/Download über `MemoryStream` (kein Chunking/Multipart-Upload der Datei selbst);
  Dateigröße durch Kestrel-Limit gedeckelt
- Cleanup-Job: `TemporaryCleanupJob` (JobKey `cleanup/temporary`), Cron
  `Trigger:TemporaryCleanupCron` Default `0 0/10 * ? * * *` (`Jobs/Configuration/TriggerOptions.cs:5`,
  `Jobs/ServiceCollectionExtensions.cs:17-23`), Selektion `ExpiryDate <= UtcNow`
  (`Data.Ef/Repository/TemporaryBaseRepository.cs:19-24`), Löschen von Blob + Metazeile
  ohne User/Audit (`Jobs/Internal/TemporaryCleanupJob.cs:27-33`)
- Quartz-Konfig (Cluster/RAM-Store) wie im Repo-README; `QuartzConnection` optional

## Task 7 — API-Seiten (`docs/api/`)

`api/README.md`
- Überblick beider Dokumente, Operations-Tabellen (Name → Methode → Pfad → Policy → Response),
  Konventionen: GUID „D“ lowercase in Pfadsegmenten, `encodeURIComponent`/`EscapeDataString`,
  PascalCase-Felder, Multipart `files` (OpenAPI/.NET-Client) vs. `files[]` (Angular) — beide werden
  von `IFormFileCollection files` gebunden (`AttachmentEndpoint.cs:171,253`, `TemporaryEndpoint.cs:94`)
  mit `DisableAntiforgery()`
- Statuscodes-Matrix: 200 (Query/Download/Drop), 201 (Upload, **leerer Body** — `Results.Created()`),
  401 (Policy), 404 (Metadatei oder Blob fehlt)
- Upload-Requestform: `multipart/form-data`, Part-Name `files`, pro-Part `Content-Type` muss gesetzt
  sein (wird als `ContentType` persistiert und beim Download zurückgegeben)
- Kurzabschnitt „Consumers“ (max. 1 Bildschirm): Meta/Generic (nur temporary-Trio für Import/Export),
  Document (`document`/`report.xml` attachment), ML (`mlmodel`/`model.zip`), LLM (`origin`, `image`),
  Angular (nur User-API, Download per `window.open`), `StorageServiceClient` 9.3.1 via
  `StorageClient__{ServiceUrl,TokenEndpoint,ClientId,ClientSecret,Scopes}` + client-credentials
  Scope `serviceApi`

`api/user-api.md` — die 6 `storage`-Operationen mit Parametern, Pfadsegmenten und Beispielpayloads
(`GET allforentityandowner` → `Attachment[]`; `POST uploadforentityandowner` → multipart;
`GET downloadforentityandownerbyid/{tenantId}/…` → binär + `Content-Disposition`;
`DELETE dropforentityandownerbyid`, `DELETE dropallforentityandowner`; `GET
/storage/temporary/downloadbyid/{tenantId}/{id}`)

`api/service-api.md` — die 12 `service`-Operationen (Attachment: `allfortenantentityandowner`,
`download…byid`, `download…byfilename`, `upload…behalfofuser`, `drop…byid…behalfofuser`,
`drop…byfilename…behalfofuser`, `dropall…behalfofuser`, `dropallfortenantandentity…`,
`dropallfortenant…`; Temporary: `downloadfortenantbyid`, `uploadfortenantandidbehalfofuser`,
`dropfortenantandidbehalfofuser`) inkl. Semantik der „bulk“ Drops
(`AttachmentEndpoint.cs:375-413`) und Hinweis, dass `{userId}` nur die Audit-Felder füllt

`api/schemas.md` — Feldtabellen zu `Attachment` (aus Snapshot, inkl. `required` +
`nullable: true`-Spannung) und `Temporary` **aus dem Code** (`Data/Public/Temporary.cs`) mit explizitem
Vermerk, dass `Temporary` in keinem Snapshot als Schema vorkommt; dazu Multipart-Body-Schema und
ProblemDetails-Form (nur 500)

`api/examples.md` — copy-paste curl-Sequenzen, jeder Pfad existiert im Snapshot:
1. User-Flow: Token → Upload (`-F "files=@report.pdf"`) → Query → Download → Drop
2. Service-Flow: client-credentials-Token (`serviceApi`) → Upload behalf of user →
   Download by filename → Drop behalf of user
3. Export-Round-Trip mit vorgewählter Temporary-Id (identische Id in Upload und Download) +
   Hinweis 30-min-Expiry
4. .NET-Client-Ausschnitt (`StorageServiceClient` mit `HttpClient.BaseAddress`,
   `FileParameter`-Liste, `FileResponse.Stream`)

`api/best-practices.md` — anonymisierte Templates/Konventionen (keine Tenant-Metadaten aus
`ballware-fwpro`): Entity-Identifikatoren als Pfadsegment (`document`, `mlmodel`, `origin`, `image`
als beobachtete Konvention), `ownerId` = Record-UUID des besitzenden Datensatzes, feste Dateinamen
pro Nutzung (`report.xml`, `model.zip`), Temporary-Id vom Aufrufer erzeugen (`Guid.NewGuid()`) und
innerhalb 30 min verbrauchen, Dateinamen ohne `/` bzw. `..` (wegen StoragePath/URL-Segment und Unique
Index), Re-Upload als Replace-Strategie, Download-URL nicht als geheimes Geheimnis behandeln
(GUID-only), `ContentType` immer explizit setzen

`api/known-deviations.md` — Ist-Zustand + jeweils „Implikation“ und „Empfehlung“:
1. User-API-Downloads anonym (kein `RequireAuthorization`, `AttachmentEndpoint.cs:35-41`,
   `TemporaryEndpoint.cs:25-33`), gateway-seitig explizit als anonyme Route abgebildet
   (`ballware-apigateway/src/Ballware.ApiGateway.Service/appsettings.json:130-136`);
   `{tenantId}` kommt aus dem Pfad ⇒ Gültigkeit nur über schwer rätbare GUIDs
2. Globaler `security`-Block `oidc: ["storageApi"]` gilt auch für die 12 `serviceApi`-Operationen des
   `service`-Dokuments (`Startup.cs:208-216`; Snapshot
   `src/Ballware.Storage.Service.Client/swagger.json:745-751`)
3. Kein `Temporary`-Schema; Upload antwortet 201 ohne Body → erzeugte Metadaten werden nicht
   zurückgegeben
4. OpenAPI deklariert `application/octet-stream`, tatsächlich wird der persistierte `ContentType`
   gesendet (`AttachmentEndpoint.cs:295,314`; `TemporaryEndpoint.cs:91`)
5. 404-Bodies sind JSON-Strings, keine ProblemDetails → Frontend-Fallback `payload?.Message` greift
   nicht
6. `Attachment`-Felder gleichzeitig `required` und `nullable: true`
7. Keine Entity-/Tenant-Rechteprüfung in diesem Service (nur Scope + Tenant-Filter)
8. Temporary-Expiry hart auf 30 min kodiert, nur der Cleanup-Cron ist konfigurierbar
9. `StoragePath` enthält den Roh-Dateinamen ⇒ Pfad-/Namespace-Härtung offen
10. Konfig-Notiz fremder Repos: `ballware-docker-compose` setzt `Authorization__RequiredScopes`
    (`docker-compose.yml:216`), das im Service nicht gebunden wird (bound: `RequiredUserScopes`,
    `RequiredServiceScopes`); `appsettings.Swagger.json:5` nutzt ebenfalls `RequiredScopes`;
    `ballware-generic-service/.../appsettings.local.json` nutzt Scope `storageApi` statt `serviceApi`

## Task 8 — Fix-Vorschläge (nur dokumentiert, nicht implementiert)

In `docs/api/known-deviations.md` als „Recommended changes“ und im PR-Text; nach Risiko gruppiert:

- **Spezifikations-genau (keine Verhaltensänderung)**: `IDocumentFilter` je Swagger-Dokument, der das
  `security`-Array pro Dokument setzt (`storage` → `RequiredUserScopes`, `service` →
  `RequiredServiceScopes`) statt des globalen Blocks in `Startup.cs:208-216`; CI/`fetch-spec.sh`
  um das `storage`-Dokument erweitern (`publish-packages.yml:50-58`); `appsettings.Swagger.json`
  auf `RequiredUserScopes`/`RequiredServiceScopes` umstellen
- **Additiv (Response-Körper)**: Uploads geben das erzeugte `Attachment`/`Temporary` im 201-Body
  zurück (`AttachmentEndpoint.cs:198`, `TemporaryEndpoint.cs:117`) — zieht `Temporary`-Schema in den
  Snapshot nach; korrekte `Produces(contentType)`-Deklaration statt hart `octet-stream`
- **Konfigurierbarkeit**: Temporary-Expiry (aktuell 30 min, `TemporaryEndpoint.cs:112`) als
  `Trigger`-Option
- **Härtung (Verhaltensänderung, nur mit abgestimmtem Rollout)**: Dateinamen-Sanitisierung vor
  `GenerateBlobPath`; optionale signierte/kurzlebige Download-URLs statt anonymer Routen — bricht
  sonst `window.open` (Angular `libs/ngrx-meta-services/.../attachment.store.ts:108`) und die
  Apigateway-Anonymous-Route; **nicht** einfach `RequireAuthorization` hinzufügen
- Fremde Repos (separate Tickets): Compose-Keys `Authorization__Required*Scopes`,
  `generic-service` Scope-Wert

## Task 9 — README-Anbindung

`README.md`: neuer Abschnitt „Documentation“ direkt nach „What this service provides“ mit
Verlinkung auf `docs/README.md` und die Unterseiten (API, Konzepte, OpenAPI-Regeneration).
Keine Änderung an Setup-/Konfigurations-/Docker-Abschnitten.

## Validierung

1. `bash docs/openapi/fetch-spec.sh` läuft; Ausgabe erwartet:
   `storage.json: paths 6, schemas 1` und `service.json: paths 12, schemas 1`, `manifest.json` geschrieben.
2. Counts gegen Snapshots: `jq '.paths|keys|length' docs/openapi/storage.json docs/openapi/service.json`
   ⇒ 6 und 12; die in `api/README.md`/`user-api.md`/`service-api.md` gelisteten Pfade müssen exakt mit
   den Snapshot-Pfaden übereinstimmen (Vergleich `jq -r '.paths|keys[]'` gegen die Tabellen).
3. Leak-Check der Snapshots:
   `rg -n 'openIdConnectUrl' docs/openapi/*.json` ⇒ ausschließlich
   `https://localhost/.well-known/openid-configuration`;
   `rg -ni 'AccountName|ballware.de|keycloak|UseDevelopmentStorage|Shared.Data.Ef' docs/openapi/*.json`
   ⇒ keine Treffer (keine echten IdP-/Storage-Werte).
4. Link-Check: alle relativen Markdown-Links in `docs/**` existieren (Walk über
   `rg -o '\]\(([^)#?]+)'` + Dateitest); kein Link auf `Documents/GitHub/ballware-fwpro/**` oder
   andere Repos außerhalb von `README.md`-Quellenangaben.
5. Inhaltliche Abgleichprüfung: jede Aussage mit Datei+Zeilenbeleg im Repo; keine Behauptung, dass der
   Service Rechte-/State-Prüfungen durchführt.
6. Unverändert-Beweis: `git status` zeigt ausschließlich `docs/**` (neu) und `README.md` (geändert) —
   keine `src/**`-, `test/**`-, `appsettings*`-Änderungen.
7. Optional (nur bei Bedarf): `dotnet test test/Ballware.Storage.Api.Tests` als Regressionssignal
   (unverändertes Verhalten).

## Risiken / offene Punkte

- Offlinedokument-Erzeugung braucht die privaten `Ballware.Shared.* 0.0.2`-Pakete (GitHub Packages).
  Lokal über `~/.nuget/packages` gecacht; in einer sauberen CI-Umgebung ist ein PAT nötig
  (`publish-packages.yml:38-46`) — im `docs/openapi/README.md` vermerken.
- `appsettings.local.json` existiert in dieser Arbeitskopie und überschreibt die Swagger-Werte:
  Snapshot-Erzeugung **muss** `Authorization__Authority` per Env-Variable pinnen; sonst wandert ein
  echter IdP-/Storage-Verweis in die Commits.
- Snapshots sind manuelle Artefakte: Regenerieren nach Endpoint-/DTO-Änderungen; Abweichung der Counts
  in `manifest.json` ⇒ API-Seiten nachziehen.
- Offene Punkte für spätere Runden (hier explizit out of scope): CI-Automatisierung der
  Snapshot-Erzeugung, Übernahme der Seiten in `ballware-docs` (dort existieren bereits automatisch
  generierte Per-Operation-Seiten `ballware-docs/api/storage*` aus Live-Snapshots — Kollisions-/
  Ersetzungsstrategie ist nicht Teil dieses Plans), eigentliche Code-Fixes aus Task 8.
