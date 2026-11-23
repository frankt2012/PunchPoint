#!/usr/bin/env bash
# Publish a release to GitHub.
#
#   scripts/release.sh 1.6.0
#
# Sets the version, commits, tags v<version> and pushes. The GitHub Actions workflow then
# builds the x64 and ARM64 installers on Windows and attaches them to the release, which is
# exactly what the in-app update check reads.
set -euo pipefail

version="${1:-}"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "usage: $0 <version>    e.g. $0 1.6.0" >&2
  exit 2
fi

cd "$(dirname "$0")/.."

if [[ -n "$(git status --porcelain | grep -v '^ M Directory.Build.props$' || true)" ]]; then
  echo "Working tree has uncommitted changes. Commit them first:" >&2
  git status --short >&2
  exit 1
fi

if git rev-parse "v$version" >/dev/null 2>&1; then
  echo "Tag v$version already exists." >&2
  exit 1
fi

echo "==> Tests"
dotnet test tests/PunchPoint.Core.Tests -c Release --nologo

echo "==> Version $version"
perl -pi -e "s|<Version>[^<]*</Version>|<Version>$version</Version>|" Directory.Build.props

git add Directory.Build.props
git commit -q -m "Version $version"
git tag -a "v$version" -m "PunchPoint $version"

echo "==> Pushing"
git push origin main
git push origin "v$version"

echo
echo "Pushed v$version. GitHub Actions builds the installers:"
echo "  gh run watch"
echo "  gh release view v$version"
echo
echo "Once the release is published, running copies of PunchPoint will offer $version."
