#!/usr/bin/env bash
# Codex keeps the aw:* hooks trusted across reinstalls (#2275).
# Run: bash providers/tests/codex-hooks-stable.test.sh
#
# Codex stores trust in config.toml as [hooks.state."<hooks.json>:<event>:<group index>:<handler index>"] trusted_hash,
# and the hash covers the event, matcher and handler (command text, timeout). So a reinstall keeps trust only when every
# entry keeps its position in its event's list and its exact command text. Everything runs under a temp HOME and a temp
# CODEX_HOME; the real ~/.codex and ~/.agentic-workflow are never touched.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
unset AW_HOOKS_DIR CODEX_HOOKS_FILE CURSOR_HOOKS_FILE CLAUDE_SETTINGS_FILE CLAUDE_HOOKS_DIR AW_DRY_RUN AW_HOOK_ADAPTER_FLAGS
export AW_STATE_DIR="$WORK/state" CODEX_HOME="$WORK/codex" AW_JUDGE_BIN="$WORK/bin/judge"
mkdir -p "$WORK/bin" "$CODEX_HOME" "$AW_STATE_DIR/bin"
export AW_SKIP_BUILD=1   # install-meta.sh would publish aw-meta; a stub stands in
printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/bin/judge"; chmod +x "$WORK/bin/judge"
export PATH="$WORK/bin:$PATH"
H="$CODEX_HOME/hooks.json"

# The Codex installers in the order setup.sh runs them (providers/codex/install.sh, then the lever and meta steps).
installers() {
  echo "providers/codex/install-hooks.sh"
  echo "scripts/install-judge-hooks.sh --provider codex"
  echo "scripts/install-sindri-hooks.sh --provider codex"
  echo "scripts/install-scope-gate.sh --provider codex"
  echo "scripts/install-context-guard.sh --provider codex"
  echo "scripts/install-meta.sh --provider codex --allow-worktree"
}
run_one() { local -a a; read -r -a a <<< "$1"; bash "$ROOT/${a[0]}" "${a[@]:1}" > /dev/null 2>&1 < /dev/null; }
run_all() { local c; while read -r c; do run_one "$c"; done < <(installers); }

# One line per handler: the key Codex would use, plus the text its hash covers.
keys() {
  jq -r '.hooks | to_entries[] | .key as $e | .value | to_entries[] | .key as $g | .value as $grp
         | $grp.hooks | to_entries[] | "\($e):\($g):\(.key)\t\($grp.matcher // "")\t\(.value.timeout // "")\t\(.value.command)"' "$H"
}

run_all
check "the full install wrote hooks for every event the installers cover" \
  "$(jq -r '.hooks | keys | join(",")' "$H")" "PostToolUse,PreToolUse,SessionStart,Stop,SubagentStart,UserPromptSubmit"
base="$(cat "$H")"
base_keys="$(keys)"

run_all
check "a second full install leaves hooks.json byte-identical" "$([ "$(cat "$H")" = "$base" ] && echo same)" "same"

# Re-running ONE installer on its own (install-meta.sh by hand, a lever by hand) must not move any other entry.
while read -r c; do
  run_one "$c"
  check "re-running '$c' alone keeps every key and command text" "$(keys | cmp -s - <(printf '%s\n' "$base_keys") && echo same || echo changed)" "same"
done < <(installers)

# One quoting style: every aw:* command names the adapter and the script in single quotes.
check "every aw:* command quotes its paths the same way" \
  "$(jq -r '.. | .command? // empty' "$H" | grep '# aw:' | grep -vc "^'[^']*' '[^']*'\( [^ ]*\)* # aw:")" "0"

# Upgrade from today's mixed layout: base hooks unquoted, lane hooks quoted, a foreign hook between ours.
jq --arg q "'" '.hooks.PreToolUse |= ([.[0]] + [{"hooks":[{"type":"command","command":"/x/own-pre.sh # mine"}]}] + .[1:])
    | .hooks |= map_values(map(.hooks |= map(.command |= (if test("# aw:") then gsub($q; "") else . end))))' "$H" > "$WORK/mixed.json"
cp "$WORK/mixed.json" "$H"
run_all
upgraded="$(cat "$H")"
check "upgrade: the foreign hook stays where the owner put it" "$(jq -r '.hooks.PreToolUse[1].hooks[0].command' "$H")" "/x/own-pre.sh # mine"
check "upgrade: every aw:* command is in the one quoting style" \
  "$(jq -r '.. | .command? // empty' "$H" | grep '# aw:' | grep -vc "^'[^']*' '[^']*'\( [^ ]*\)* # aw:")" "0"
check "upgrade: no aw:* entry is duplicated in an event" \
  "$(jq -r '.hooks | to_entries[] | .key as $e | .value[].hooks[].command | select(test("# aw:")) | "\($e) \(sub(".*# ";""))"' "$H" | sort | uniq -d | wc -l | tr -d ' ')" "0"
run_all
check "upgrade: the next install changes nothing" "$([ "$(cat "$H")" = "$upgraded" ] && echo same)" "same"

[ "$fail" = 0 ] && echo "ALL PASS" || { echo "FAILED"; exit 1; }
