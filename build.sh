#!/usr/bin/env bash
# Builds PunchPoint for Windows (x64 and ARM64) into dist/.
set -euo pipefail

cd "$(dirname "$0")"

VERSION="${1:-1.0.0}"
COMMON=(-c Release --self-contained true
        -p:PublishSingleFile=true
        -p:IncludeNativeLibrariesForSelfExtract=true
        -p:EnableCompressionInSingleFile=true
        -p:Version="$VERSION"
        -p:DebugType=none)

echo "== tests =="
dotnet test tests/PunchPoint.Core.Tests -v quiet

for rid in win-x64 win-arm64; do
  echo "== $rid =="
  dotnet publish src/PunchPoint.App   "${COMMON[@]}" -r "$rid" -o "dist/$rid"
  dotnet publish src/PunchPoint.Clock "${COMMON[@]}" -r "$rid" -o "dist/$rid"
done

echo "== command line tools =="
dotnet publish src/PunchPoint.Cli -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o "dist/cli" >/dev/null

echo
echo "Done. Builds:"
ls -lh dist/win-x64/PunchPoint*.exe dist/win-arm64/PunchPoint*.exe 2>/dev/null || true
