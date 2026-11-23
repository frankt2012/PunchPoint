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

echo "== windows x64 =="
dotnet publish src/PunchPoint.App "${COMMON[@]}" -r win-x64 -o "dist/win-x64"

echo "== windows arm64 =="
dotnet publish src/PunchPoint.App "${COMMON[@]}" -r win-arm64 -o "dist/win-arm64"

echo "== command line tools =="
dotnet publish src/PunchPoint.Cli -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o "dist/cli" >/dev/null

echo
echo "Done. Builds:"
ls -lh dist/win-x64/PunchPoint.exe dist/win-arm64/PunchPoint.exe 2>/dev/null || true
