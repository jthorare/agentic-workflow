#!/usr/bin/env bash
# UX-pass check F0 (no .test.sh suffix, so CI's bash-tests.sh does not pick it up): SND-RECIPE-003's fix text names a command the CLI accepts.
# Run: bash tests/ui-ux/f0.sh
# The fix text names the one-argument form the CLI takes (`sindri recipe approve <file|id@hash>`), not `<id> <version>`. Reads source only.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
SRC="$ROOT/src/Sindri/Aw.Sindri.Core/Workflow/Registry/RegistryApprovedRecipes.cs"
fail=0

ok() { echo "ok - $1"; }
bad() { echo "not ok - $1"; [ $# -lt 2 ] || echo "  $2"; fail=1; }

# --- (a) the fix text ---
lines="$(grep -n 'fix: sindri recipe approve' "$SRC")"
[ -n "$lines" ] || bad "the source has SND-RECIPE-003 fix text" "no 'fix: sindri recipe approve' in $SRC"
if printf '%s\n' "$lines" | grep -qE 'recipe approve (\{[^}]+\}|[^ ]+) \{[^}]+\}'; then
  bad "the fix text does not name '<id> <version>'" "$(printf '%s\n' "$lines" | grep -E 'recipe approve (\{[^}]+\}|[^ ]+) \{')"
else ok "the fix text does not name '<id> <version>'"; fi
if [ -n "$lines" ] && ! printf '%s\n' "$lines" | grep -vE 'recipe approve \{[^}]+\}@\{[^}]+\}' | grep -q .; then
  ok "every fix text names 'recipe approve <id>@<hash>'"
else bad "every fix text names 'recipe approve <id>@<hash>'" "$lines"; fi

exit "$fail"
