#!/usr/bin/env bash
# Builds the two Windows programs and the 64-bit installer.
# Usage: ./scripts/build-installer.sh [version]
set -euo pipefail

cd "$(dirname "$0")/.."
VERSION="${1:-1.0.0}"

echo "== tests =="
dotnet test tests/PunchPoint.Core.Tests -v quiet

echo "== payload =="
rm -rf build/payload src/PunchPoint.Setup/payload.zip
mkdir -p build/payload dist

COMMON=(-c Release --self-contained true
        -p:PublishSingleFile=true
        -p:IncludeNativeLibrariesForSelfExtract=true
        -p:EnableCompressionInSingleFile=true
        -p:Version="$VERSION"
        -p:DebugType=none)

dotnet publish src/PunchPoint.App   "${COMMON[@]}" -r win-x64 -o build/payload
dotnet publish src/PunchPoint.Clock "${COMMON[@]}" -r win-x64 -o build/payload
cp installer/README.txt build/payload/

# Keep only the two programs and the read-me in the payload.
find build/payload -mindepth 1 -maxdepth 1 ! -name 'PunchPoint.exe' ! -name 'PunchPointClock.exe' ! -name 'README.txt' -exec rm -rf {} +

( cd build/payload && zip -qr ../../src/PunchPoint.Setup/payload.zip . )

echo "== setup =="
dotnet publish src/PunchPoint.Setup -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:Version="$VERSION" \
  -p:DebugType=none \
  -o build/setup

cp build/setup/PunchPointSetup.exe "dist/PunchPoint-Setup-$VERSION.exe"
rm -f src/PunchPoint.Setup/payload.zip

ls -lh "dist/PunchPoint-Setup-$VERSION.exe"
