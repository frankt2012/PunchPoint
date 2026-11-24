#!/usr/bin/env bash
# Write the release notes for one or more published releases from CHANGELOG.md.
#
#   scripts/sync-release-notes.sh              # every version in the changelog
#   scripts/sync-release-notes.sh 1.5.2        # just this one
#
# The notes are the changelog section plus a standard "Install" footer, so every release page
# reads the same way. CI applies the same footer to new drafts automatically.
set -euo pipefail

cd "$(dirname "$0")/.."

repo="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"

footer() {
  local version="$1"
  cat <<EOF

---

### Install

1. Download **PunchPoint-Setup-$version.exe** below and run it.
2. The first time you open PunchPoint you choose a supervisor password.

Windows 11 (x64 or ARM64) · self-contained, no .NET install · your data lives in
\`C:\\ProgramData\\PunchPoint\` and survives upgrades.

Prefer no installer? The portable builds are attached as
\`PunchPoint-$version-win-x64.zip\` and \`PunchPoint-$version-win-arm64.zip\` — unzip and keep
both \`.exe\` files together.

[User guide](https://github.com/$repo/blob/main/docs/user-guide.md) ·
[Full changelog](https://github.com/$repo/blob/main/CHANGELOG.md)
EOF
}

sync_one() {
  local version="$1"
  local tag="v$version"

  if ! gh release view "$tag" >/dev/null 2>&1; then
    echo "skip $tag (no such release)"
    return
  fi

  local notes
  notes="$(mktemp)"

  awk -v version="$version" '
    $0 ~ "^## " version "([^0-9.]|$)" { inside = 1; next }
    inside && /^## / { exit }
    inside { print }
  ' CHANGELOG.md > "$notes"

  if [[ ! -s "$notes" ]]; then
    echo "skip $version (no CHANGELOG section)"
    rm -f "$notes"
    return
  fi

  footer "$version" >> "$notes"
  gh release edit "$tag" --notes-file "$notes" >/dev/null
  rm -f "$notes"
  echo "updated $tag"
}

if [[ $# -gt 0 ]]; then
  for version in "$@"; do
    sync_one "$version"
  done
else
  # newest first; only versions that actually have a release
  grep -oE '^## [0-9]+\.[0-9]+\.[0-9]+' CHANGELOG.md | awk '{print $2}' | while read -r version; do
    sync_one "$version"
  done
fi
