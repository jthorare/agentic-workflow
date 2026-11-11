#!/usr/bin/env bash
# Bump EXTERNAL_PINS.env entries to upstream HEAD for each external pack.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PINS_FILE="$REPO_ROOT/EXTERNAL_PINS.env"

# "VAR repo" pairs — a plain list rather than `declare -A`, so this runs on
# macOS's stock bash 3.2.
PACKS="IMPECCABLE_PIN pbakaus/impeccable
EMIL_PIN emilkowalski/skill
TASTE_PIN Leonxlnx/taste-skill
TDD_ARTIFACT_PIN heyitsalec/tdd-artifact"

while read -r var repo; do
  [ -n "$var" ] || continue
  sha=$(gh api "repos/$repo/commits/HEAD" --jq .sha </dev/null)
  if [[ ! "$sha" =~ ^[0-9a-f]{40}$ ]]; then
    echo "ERROR: invalid SHA returned by gh api for $repo: $sha" >&2
    exit 1
  fi
  echo "  $var = $sha ($repo)"
  if grep -q "^${var}=" "$PINS_FILE"; then
    tmp=$(mktemp)
    sed "s|^${var}=.*|${var}=${sha}|" "$PINS_FILE" > "$tmp" && mv "$tmp" "$PINS_FILE"
  else
    echo "${var}=${sha}" >> "$PINS_FILE"
  fi
done <<EOF
$PACKS
EOF

echo "Updated $PINS_FILE"
