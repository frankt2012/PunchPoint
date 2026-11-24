#!/usr/bin/env bash
# Publish a tested release to GitHub.
#
#   scripts/publish-release.sh 1.6.0
#
# Releases are created as drafts by CI. This makes one public: it refreshes the notes from
# CHANGELOG.md and clears the draft flag, which is the moment running copies of PunchPoint can
# see it (the update check only reads published releases).
set -euo pipefail

version="${1:-}"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "usage: $0 <version>    e.g. $0 1.6.0" >&2
  exit 2
fi

cd "$(dirname "$0")/.."

tag="v$version"

if ! gh release view "$tag" >/dev/null 2>&1; then
  echo "There is no release $tag yet. Did the build finish?" >&2
  echo "  gh run list --limit 3" >&2
  exit 1
fi

notes="$(mktemp)"
awk -v version="$version" '
  $0 ~ "^## " version "([^0-9.]|$)" { inside = 1; next }
  inside && /^## / { exit }
  inside { print }
' CHANGELOG.md > "$notes"

if [[ ! -s "$notes" ]]; then
  echo "CHANGELOG.md has no section for $version — add one before publishing." >&2
  rm -f "$notes"
  exit 1
fi

echo "==> Publishing $tag with notes from CHANGELOG.md"
gh release edit "$tag" --draft=false --notes-file "$notes"
rm -f "$notes"

echo
gh release view "$tag" --json url,isDraft,assets --jq '"\(.url)\ndraft: \(.isDraft)\nassets: \([.assets[].name] | join(", "))"'
echo
echo "Running copies of PunchPoint will now be offered $version."
