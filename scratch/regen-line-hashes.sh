#!/usr/bin/env bash
# Recomputes the packet hashes the line fixture pins after a gold packet changed (tests/fixtures/line/README.md, Choice 1).
# usage: regen-line-hashes.sh <path-to-aw-packets-binary>
# In chain order: scope packets -> design (its index.json parents) -> plan (its index.json parents) -> the seals' planHash.
# Prints every old -> new value and edits only files under tests/fixtures/line/gold and tests/fixtures/line/holdout (text replacement, formatting kept).
set -euo pipefail

BIN="${1:?usage: regen-line-hashes.sh <path-to-aw-packets-binary>}"
[ -x "$BIN" ] || { echo "not executable: $BIN" >&2; exit 2; }
ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
LINE="$ROOT/tests/fixtures/line"
GOLD="$LINE/gold"
FREEZE="$GOLD/freeze.json"
DESIGN_INDEX="$GOLD/design/expiry/index.json"
PLAN_INDEX="$GOLD/plan/expiry/index.json"
PLAN_PACKET="$GOLD/plan/expiry/packet.json"
for f in "$FREEZE" "$DESIGN_INDEX" "$PLAN_INDEX" "$PLAN_PACKET"; do [ -f "$f" ] || { echo "missing $f" >&2; exit 2; }; done

# frozen_hash <packet>: the hash gold/freeze.json records for that packet.
frozen_hash() { node -e 'const f=JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).frozen.find(x=>x.packet===process.argv[2]); if(!f){process.exit(3)} console.log(f.hash)' "$FREEZE" "$1"; }

# replace <old> <new> <file>...: exact text replacement, formatting kept, only files under gold/ or holdout/.
replace() {
  local old="$1" new="$2"; shift 2
  [ "$old" = "$new" ] && return 0
  for file in "$@"; do
    case "$file" in "$GOLD"/*|"$LINE/holdout"/*) ;; *) echo "refusing to touch $file" >&2; exit 2 ;; esac
    node -e 'const fs=require("fs");const [p,a,b]=process.argv.slice(1);const t=fs.readFileSync(p,"utf8");if(t.includes(a)){fs.writeFileSync(p,t.split(a).join(b));console.log("  updated "+p)}' "$file" "$old" "$new"
  done
}

step() { # step <label> <packet> <kind> <files-that-name-this-hash...>
  local label="$1" packet="$2" kind="$3"; shift 3
  local old new
  old="$(frozen_hash "$packet")"
  new="$("$BIN" hash --kind "$kind" "$GOLD/$packet")"
  echo "$label: $old -> $new"
  replace "$old" "$new" "$@"
}

echo "== scope packets"
step "scope/expiry" scope/expiry scoping "$FREEZE" "$DESIGN_INDEX" "$PLAN_INDEX"
step "scope/archive" scope/archive scoping "$FREEZE"

echo "== design (its parent changed, so its hash did)"
step "design/expiry" design/expiry design "$FREEZE" "$PLAN_INDEX"

echo "== plan (its parents changed, so its hash did)"
step "plan/expiry" plan/expiry plan "$FREEZE"

echo "== seals (planHash is the SHA-256 of the plan's packet.json, README Choice 10; unchanged unless its links changed)"
NEWSEAL="sha256:$(sha256sum "$PLAN_PACKET" | cut -d' ' -f1)"
for seal in "$LINE"/holdout/expiry/P*.seal.json; do
  OLDSEAL="$(node -e 'console.log(JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).planHash)' "$seal")"
  echo "$(basename "$seal"): $OLDSEAL -> $NEWSEAL"
  replace "$OLDSEAL" "$NEWSEAL" "$seal"
done

echo "done; now run: node tests/fixtures/line/selfcheck.mjs, and the Aw.Packets.Tests LineFixtureTests"
