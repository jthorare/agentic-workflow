#!/usr/bin/env bash
# scripts/ci/recipe-versions.sh — "a content change bumps the version" for the shipped recipes (setup-once spec §5.3, SO-E1).
#
# Runs `sindri recipe manifest` over `git archive origin/main recipes/sindri` (the baseline) and over `git archive HEAD
# recipes/sindri` (the same archive command, so export-ignore applies equally), then `sindri recipe validate <file> --against
# <baseline manifest>` for every recipe in HEAD's archive: a recipe whose hash changed at the same version is RCP-VERSION-001.
# No manifest is committed anywhere. A base ref that has no recipes/sindri yet has nothing to compare, so the check passes.
#
# Usage: scripts/ci/recipe-versions.sh [--base-ref REF]      (default REF: origin/main)
# Env:   AW_SINDRI_CMD  the command that runs sindri, split on spaces
#                       (default: dotnet run --no-build --configuration Release --project src/Sindri/Aw.Sindri.Host --)
# Exit:  0 every recipe is new, unchanged or has a raised version; 1 a recipe changed at the same version (or a manifest is invalid); 2 usage or setup error.
# Bash 3.2 compatible.
set -uo pipefail

base_ref="origin/main"
while [ "$#" -gt 0 ]; do
  case "$1" in
    --base-ref) [ "$#" -ge 2 ] || { echo "recipe-versions: --base-ref needs a ref" >&2; exit 2; }; base_ref="$2"; shift 2 ;;
    *) echo "usage: scripts/ci/recipe-versions.sh [--base-ref REF]" >&2; exit 2 ;;
  esac
done

repo="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "recipe-versions: not a git checkout" >&2; exit 2; }
cd "$repo" || exit 2
IFS=' ' read -r -a sindri <<< "${AW_SINDRI_CMD:-dotnet run --no-build --configuration Release --project src/Sindri/Aw.Sindri.Host --}"

git rev-parse --verify --quiet "$base_ref^{commit}" >/dev/null || { echo "recipe-versions: $base_ref is not a commit here (fetch it first)" >&2; exit 2; }
work="$(mktemp -d)" || exit 2
cleanup() { rm -r -f "$work"; }
trap cleanup EXIT
mkdir -p "$work/base" "$work/head"

# Compare git objects, not working trees: an untracked or edited file never counts.
if [ -z "$(git ls-tree -d --name-only "$base_ref" recipes/sindri 2>/dev/null)" ]; then
  echo "recipe-versions: $base_ref has no recipes/sindri; nothing to compare"
  exit 0
fi
{ git archive "$base_ref" recipes/sindri | tar -x -C "$work/base"; } || { echo "recipe-versions: git archive $base_ref failed" >&2; exit 2; }
{ git archive HEAD recipes/sindri | tar -x -C "$work/head"; } || { echo "recipe-versions: git archive HEAD failed" >&2; exit 2; }

# The baseline may pre-date a rule (exit 1 lists its invalid recipes but still prints the manifest); only a tool error (2) or no output fails it.
"${sindri[@]}" recipe manifest "$work/base/recipes/sindri" --json > "$work/baseline.json"; rc=$?
if [ "$rc" -gt 1 ] || [ ! -s "$work/baseline.json" ]; then
  echo "recipe-versions: the baseline ($base_ref) does not make a manifest" >&2; exit 2
fi
"${sindri[@]}" recipe manifest "$work/head/recipes/sindri" > "$work/head-manifest.txt"; rc=$?
cat "$work/head-manifest.txt"
[ "$rc" -eq 0 ] || { echo "recipe-versions: HEAD's recipes do not make a manifest" >&2; exit 1; }

failed=0
for file in "$work"/head/recipes/sindri/*.yaml; do
  out="$("${sindri[@]}" recipe validate "$file" --against "$work/baseline.json" --skills skills)"; rc=$?
  if [ "$rc" -gt 1 ]; then
    echo "recipe-versions: recipe validate failed on $(basename "$file") (exit $rc), a tool error:" >&2
    echo "$out" >&2
    exit 2
  elif [ "$rc" -ne 0 ]; then
    failed=1
    echo "recipe-versions: $(basename "$file") changed without a version bump or does not validate:" >&2
    echo "$out" | grep -E '^RCP-' >&2
  fi
done
if [ "$failed" -ne 0 ]; then
  echo "recipe-versions: a content change bumps the version; raise 'version' in each recipe above." >&2
  exit 1
fi
echo "recipe-versions: every recipe is new, unchanged or has a raised version (base $base_ref)"
