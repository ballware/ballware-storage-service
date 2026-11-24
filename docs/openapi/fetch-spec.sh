#!/usr/bin/env bash
# Regenerates the OpenAPI snapshots committed in docs/openapi/ (storage.json, service.json, manifest.json).
#
# The snapshot is produced offline from the built service assembly via Swashbuckle's
# swagger-to-file tool, using ASPNETCORE_ENVIRONMENT=Swagger (fake Authority/clients,
# no database, no IdP needed). See docs/openapi/README.md for details and the live-server fallback.

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: docs/openapi/fetch-spec.sh

Regenerates docs/openapi/storage.json, docs/openapi/service.json and docs/openapi/manifest.json.
Run from any location inside the repository; the script resolves the repository root itself.
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

TOOL_PACKAGE="Swashbuckle.AspNetCore.Cli"
TOOL_VERSION="10.1.3"
CONFIG_PROJECT="src/Ballware.Storage.Service"
OUTPUT_DIR="docs/openapi"

step() { printf '\n==> %s\n' "$*"; }

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is required but was not found on PATH." >&2
  exit 1
fi

step "Building $CONFIG_PROJECT (Release)"
dotnet build "$CONFIG_PROJECT" --configuration Release

step "Ensuring $TOOL_PACKAGE $TOOL_VERSION is available"
# The tool version must match the service's Swashbuckle packages (10.1.3) — see docs/openapi/README.md.
# Install via the local tool manifest, exactly like CI does:
#   dotnet new tool-manifest || true
#   dotnet tool update Swashbuckle.AspNetCore.Cli --version 10.1.3
# The manifest file is only needed for the install and is removed again at the end of this
# script when the repository did not have one (keeps the working tree clean).
# Invocation: prefer the tool variant from the NuGet package cache that matches the service's
# target framework. A host SDK newer than the tool's highest bundled TFM makes
# "dotnet tool run swagger" pick a variant that needs a runtime which may not be installed;
# the matching-TFM variant always binds against the same framework as the service assembly.
MANIFEST_CREATED=0
if [[ ! -f .config/dotnet-tools.json && ! -f dotnet-tools.json ]]; then
  MANIFEST_CREATED=1
fi
dotnet new tool-manifest || true
dotnet tool update "$TOOL_PACKAGE" --version "$TOOL_VERSION"

TARGET_FRAMEWORK="$(sed -n 's/.*<TargetFramework>\(net[0-9.]*\)<.*/\1/p' "$CONFIG_PROJECT"/*.csproj | head -1)"
TOOL_PKG_DIR="$HOME/.nuget/packages/$(printf '%s' "$TOOL_PACKAGE" | tr '[:upper:]' '[:lower:]')/$TOOL_VERSION"
TOOL_DLL="$TOOL_PKG_DIR/tools/$TARGET_FRAMEWORK/any/dotnet-swagger.dll"
if [[ -f "$TOOL_DLL" ]]; then
  SWAGGER_RUNNER=(dotnet "$TOOL_DLL")
  echo "swagger tool: $TOOL_DLL ($TARGET_FRAMEWORK)"
else
  SWAGGER_RUNNER=(dotnet tool run swagger)
  echo "swagger tool: dotnet tool run swagger"
fi

step "Generating documents"
# ASPNETCORE_ENVIRONMENT=Swagger loads appsettings.Swagger.json (fake Authority/clients).
# appsettings.local.json is applied after the environment file even in the Swagger
# environment, so the OIDC discovery URL in the generated documents is pinned to the
# fake value below via an environment override. This keeps developer IdP URLs out of
# the committed snapshots (see docs/openapi/README.md).
export ASPNETCORE_ENVIRONMENT=Swagger
export Authorization__Authority="https://localhost"

# Run from the service project directory: the appsettings files are resolved relative
# to the content root (current working directory), like in CI. The absolute output
# path keeps the snapshots in docs/openapi/ regardless of the invocation directory.
cd "$CONFIG_PROJECT"

for doc in storage service; do
  "${SWAGGER_RUNNER[@]}" tofile --output "$REPO_ROOT/$OUTPUT_DIR/$doc.json" "bin/Release/$TARGET_FRAMEWORK/Ballware.Storage.Service.dll" "$doc"
done

cd "$REPO_ROOT"

step "Validating and writing manifest"
if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required for validation and manifest generation." >&2
  exit 1
fi

service_version="$(git describe --tags --always --dirty 2>/dev/null || echo unknown)"

TOOL_VERSION="$TOOL_VERSION" SERVICE_VERSION="$service_version" \
python3 - "$OUTPUT_DIR" <<'PY'
import datetime
import json
import os
import sys

output_dir = sys.argv[1]
manifest = {
    "source": "Swashbuckle.AspNetCore.Cli swagger tofile",
    "toolVersion": os.environ["TOOL_VERSION"],
    "environment": "Swagger",
    "serviceVersion": os.environ["SERVICE_VERSION"],
    "fetchedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "documents": {},
}

for name in ("storage", "service"):
    path = os.path.join(output_dir, f"{name}.json")
    with open(path, encoding="utf-8") as file:
        doc = json.load(file)

    version = str(doc.get("openapi", ""))
    paths = doc.get("paths") or {}
    schemas = (doc.get("components") or {}).get("schemas") or {}

    if not version.startswith("3."):
        sys.exit(f"{path}: openapi version {version!r} does not start with '3.'")
    if not paths:
        sys.exit(f"{path}: paths is empty")

    title = (doc.get("info") or {}).get("title", "")
    manifest["documents"][name] = {
        "file": f"{name}.json",
        "openapi": version,
        "title": title,
        "pathCount": len(paths),
        "schemaCount": len(schemas),
    }
    print(f"{path}: openapi {version}, title {title!r}, paths {len(paths)}, schemas {len(schemas)}")

with open(os.path.join(output_dir, "manifest.json"), "w", encoding="utf-8") as file:
    json.dump(manifest, file, indent=2)
    file.write("\n")
PY

step "Cleaning up transient tool manifest"
if [[ "$MANIFEST_CREATED" == "1" ]]; then
  rm -f dotnet-tools.json .config/dotnet-tools.json
  rmdir .config 2>/dev/null || true
  echo "Removed the tool manifest created by this run (repository did not have one)."
fi

step "Done"
echo "Updated $OUTPUT_DIR/{storage,service,manifest}.json"
