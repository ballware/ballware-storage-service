# Examples

Copy-paste sequences against the two documents. Placeholders:

- `BASE=http://localhost:5005` (dev HTTP; use `https://localhost:6005` for the Swagger UI host)
- `TOKEN` — user token with the `storageApi` scope (via Swagger UI *Authorize* or your login flow)
- `SERVICE_TOKEN` — machine token with the `serviceApi` scope

Every path below exists in the committed snapshots; if it doesn't, the snapshots or the endpoints
changed — regenerate per [openapi/README.md](../openapi/README.md).

## 1. User flow: upload → query → download → drop

```bash
ENTITY=document
OWNER_ID=9b1de2f4-5c6a-4b8e-a7d3-0e1f2a3b4c5d
TENANT_ID=0b8a4d2c-1e3f-4a5b-8c9d-0e1f2a3b4c5d   # from the "tenant" claim

# Upload (multipart part name "files"; per-part content type is persisted)
curl -sS -X POST "$BASE/storage/attachment/uploadforentityandowner/$ENTITY/$OWNER_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -F "files=@report.pdf;type=application/pdf"
# -> 201, empty body (created metadata is NOT returned — query it next)

# Query the attachment list to discover the Id
curl -sS "$BASE/storage/attachment/allforentityandowner/$ENTITY/$OWNER_ID" \
  -H "Authorization: Bearer $TOKEN"
# -> [{ "Id": "4f6d…", "Entity": "document", … }]

ATTACHMENT_ID=4f6d2c1e-8a3b-4c9d-9e0f-1a2b3c4d5e6f

# Download — anonymous route (no Authorization header needed)
curl -sS -o report.pdf \
  "$BASE/storage/attachment/downloadforentityandownerbyid/$TENANT_ID/$ENTITY/$OWNER_ID/$ATTACHMENT_ID"

# Drop by id (authenticated)
curl -sS -X DELETE \
  "$BASE/storage/attachment/dropforentityandownerbyid/$ENTITY/$OWNER_ID/$ATTACHMENT_ID" \
  -H "Authorization: Bearer $TOKEN"
```

Re-uploading `report.pdf` for the same `document`/owner **replaces** the blob and row, `Id`
unchanged — see [storage-model.md](../concepts/storage-model.md#indexes-and-the-unique-file-name-rule).

## 2. Service flow: client-credentials → upload → download by filename → drop

```bash
# Client-credentials token (Keycloak realm used by ballware-docker-compose; client "storage"
# carries serviceApi as a default client scope)
SERVICE_TOKEN=$(curl -sS -X POST "http://localhost:3001/realms/ballware/protocol/openid-connect/token" \
  -d "grant_type=client_credentials" \
  -d "client_id=storage" \
  -d "client_secret=$STORAGE_CLIENT_SECRET" | jq -r .access_token)

TENANT_ID=0b8a4d2c-1e3f-4a5b-8c9d-0e1f2a3b4c5d
USER_ID=1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d     # audit identity ("behalf of user")
ENTITY=document
OWNER_ID=9b1de2f4-5c6a-4b8e-a7d3-0e1f2a3b4c5d

# Upload behalf of user
curl -sS -X POST "$BASE/storage/attachment/uploadfortenantentityandownerbehalfofuser/$TENANT_ID/$USER_ID/$ENTITY/$OWNER_ID" \
  -H "Authorization: Bearer $SERVICE_TOKEN" \
  -F "files=@report.xml;type=application/xml"

# Download by fixed file name (anonymous route — no token)
curl -sS -o report.xml \
  "$BASE/storage/attachment/downloadfortenantentityandownerbyfilename/$TENANT_ID/$ENTITY/$OWNER_ID/report.xml"

# Drop behalf of user
curl -sS -X DELETE \
  "$BASE/storage/attachment/dropfortenantentityandownerbyfilenamebehalfofuser/$TENANT_ID/$USER_ID/$ENTITY/$OWNER_ID/report.xml" \
  -H "Authorization: Bearer $SERVICE_TOKEN"
```

## 3. Export round-trip with a preset id

Import/export hand-off between services (as Meta/Generic use it): the **caller invents the
temporary id** so the download URL can be composed before/without the upload response (uploads
answer 201 empty, deviation 3).

```bash
TEMPORARY_ID=$(uuidgen | tr 'A-Z' 'a-z')

# Service side: upload the export artifact under the chosen id
curl -sS -X POST "$BASE/storage/temporary/uploadfortenantandidbehalfofuser/$TENANT_ID/$USER_ID/$TEMPORARY_ID" \
  -H "Authorization: Bearer $SERVICE_TOKEN" \
  -F "files=@export.zip;type=application/zip"

# Hand the browser URL to the user (user-API twin, anonymous):
echo "$BASE/storage/temporary/downloadbyid/$TENANT_ID/$TEMPORARY_ID"

# Browser side: fetch it
curl -sS -o export.zip "$BASE/storage/temporary/downloadbyid/$TENANT_ID/$TEMPORARY_ID"

# Optional early cleanup (expiry would reclaim it anyway)
curl -sS -X DELETE \
  "$BASE/storage/temporary/dropfortenantandidbehalfofuser/$TENANT_ID/$USER_ID/$TEMPORARY_ID" \
  -H "Authorization: Bearer $SERVICE_TOKEN"
```

**Expiry window:** the record lives 30 minutes from the *last* upload (`TemporaryEndpoint.cs:112`);
the cleanup job sweeps expired entries every 10 minutes by default. Consume the link within the
window ([best-practices.md](best-practices.md)).

## 4. .NET client (`StorageServiceClient`)

Generated client (`src/Ballware.Storage.Service.Client`, generated in CI from the `service`
document via `nswag.json`). Consumers wire it like `ballware-generic-service`'s
`Adapter/StorageServiceFileStorageAdapter.cs`:

```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using Ballware.Storage.Service.Client;

var httpClient = new HttpClient { BaseAddress = new Uri("http://storage:5005") };
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", serviceToken); // scope: serviceApi

await using var client = new StorageServiceClient(httpClient);

// Upload temporary (caller-chosen id), FileParameter carries name + content type
var temporaryId = Guid.NewGuid();
await using var zip = File.OpenRead("export.zip");
await client.TemporaryUploadForTenantAndIdBehalfOfUserAsync(
    tenantId, userId, temporaryId,
    [new FileParameter(zip, "export.zip", "application/zip")]);

// Download attachment by file name -> FileResponse with the open stream
FileResponse file = await client.AttachmentDownloadForTenantEntityAndOwnerByFilenameAsync(
    tenantId, "document", ownerId, "report.xml");
if (file.StatusCode == 200)
{
    await using var stream = file.Stream; // FileResponse owns the response/client lifetime
    // … consume stream …
}
```

Notes: the client has synchronous mirrors (`…Async` variants shown); `FileParameter.Data/.FileName/
.ContentType`; `FileResponse.Stream/.Headers/.StatusCode`. All 12 service operations are available
under their OpenAPI `operationId`s (table in [README.md](README.md#service-document-12-operations)).
