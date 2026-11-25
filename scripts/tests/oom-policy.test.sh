#!/usr/bin/env bash
# Tests for the DefaultOOMPolicy guard (issue #2931, items 1 and 2), on a sandboxed HOME, XDG_CONFIG_HOME and state dir with a
# stub systemctl (no real systemd call; nothing outside the temp dir is touched):
# - scripts/install-meta.sh writes ${XDG_CONFIG_HOME}/systemd/user.conf.d/10-oom-continue.conf ([Manager] DefaultOOMPolicy=continue)
#   only when `systemctl --user show -p DefaultOOMPolicy --value` says something other than continue, prints the
#   `systemctl --user daemon-reload` step and never runs it, leaves neighbour drop-ins alone, is idempotent, and writes nothing on a
#   dry run, when the value is continue, when systemctl fails or is missing, or on macOS.
# - scripts/meta-launch.sh warns once on stderr (naming the fix and daemon-reload) when the policy is not continue, and says
#   nothing when it is continue, unreadable, or systemctl is missing.
# Run: bash scripts/tests/oom-policy.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0; pass=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; pass=$((pass + 1)); else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=$((fail + 1))
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done
unset TMUX CLAUDE_DIR CLAUDE_LOCAL_BIN AW_DRY_RUN AW_HOOKS_DIR CODEX_HOOKS_FILE CODEX_HOME CURSOR_HOOKS_FILE CLAUDE_SETTINGS_FILE CLAUDE_HOOKS_DIR AW_HOOK_ADAPTER_FLAGS SETTINGS_FILE TOOLKIT_DIR
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_SKIP_BUILD=1 XDG_CONFIG_HOME="$WORK/xdg" AW_META_NO_STALE_CHECK=1
mkdir -p "$WORK/linuxbin" "$WORK/darwinbin" "$HOME/.claude" "$AW_STATE_DIR/bin" "$AW_STATE_DIR/meta"
printf '#!/usr/bin/env bash\necho Linux\n' > "$WORK/linuxbin/uname"
printf '#!/usr/bin/env bash\necho Darwin\n' > "$WORK/darwinbin/uname"
cat > "$WORK/linuxbin/systemctl" <<'SH'
#!/usr/bin/env bash
echo "$*" >> "$WORK_LOG"
if [ "$*" = "--user show -p DefaultOOMPolicy --value" ]; then
  [ "${STUB_OOM:-stop}" = fail ] && exit 1
  echo "${STUB_OOM:-stop}"; exit 0
fi
exit 0
SH
chmod +x "$WORK/linuxbin/"* "$WORK/darwinbin/uname"
export WORK_LOG="$WORK/systemctl.log"
# a stub tmux keeps the launcher off any real tmux server
if ! command -v tmux > /dev/null; then printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/linuxbin/tmux"; chmod +x "$WORK/linuxbin/tmux"; fi
# a PATH with the real tools but no systemctl
mkdir -p "$WORK/nosys"
for t in bash sh env jq git tmux sed awk grep cat cp mv rm ln mkdir rmdir chmod readlink dirname basename date mktemp sort head tail tr cut wc ls stat cmp id hostname find xargs tee touch printf realpath uniq diff install sleep true false expr test ps flock; do
  p="$(PATH="$WORK/linuxbin:/usr/bin:/bin" command -v "$t" 2> /dev/null)" && [ -n "$p" ] && ln -sf "$p" "$WORK/nosys/$t"
done
cp "$WORK/linuxbin/uname" "$WORK/nosys/uname"
NOSYS_PATH="$WORK/nosys"
if PATH="$NOSYS_PATH" command -v systemctl > /dev/null; then echo "not ok - harness: systemctl still resolves on the no-systemctl PATH"; exit 1; fi
STOCK_PATH="$WORK/linuxbin:$PATH"

printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"
jq -n --arg r "$(cd "$DIR/.." && pwd -P)" '{team:"t",repo:$r}' > "$AW_STATE_DIR/meta/team.json"
echo '{"model":"opus"}' > "$HOME/.claude/settings.json"

DROP="$XDG_CONFIG_HOME/systemd/user.conf.d/10-oom-continue.conf"
NEIGH="$XDG_CONFIG_HOME/systemd/user.conf.d/50-mine.conf"
WANT=$'[Manager]\nDefaultOOMPolicy=continue'
fresh() { rm -rf "$XDG_CONFIG_HOME"; : > "$WORK/systemctl.log"; }
inst() { PATH="$1" bash "$DIR/install-meta.sh" --provider claude > "$WORK/out" 2>&1; echo $?; }
yes_if() { if [ "$1" -gt 0 ] 2> /dev/null; then echo yes; else echo no; fi; }
written() { if [ -e "$DROP" ]; then echo written; else echo none; fi; }

# install-meta.sh
fresh; mkdir -p "$(dirname "$NEIGH")"; echo "# my own drop-in" > "$NEIGH"; cp "$NEIGH" "$WORK/neigh.before"
export STUB_OOM=stop
check "1: stop: exit 0" "$(inst "$STOCK_PATH")" "0"
check "1: drop-in holds exactly the two lines" "$(cat "$DROP" 2> /dev/null)" "$WANT"
check "1: output names the drop-in path" "$(yes_if "$(grep -cF "$DROP" "$WORK/out")")" "yes"
check "1: output prints the daemon-reload step" "$(yes_if "$(grep -cF 'systemctl --user daemon-reload' "$WORK/out")")" "yes"
check "1: daemon-reload never run" "$(grep -c daemon-reload "$WORK/systemctl.log")" "0"
M1="$(stat -c %Y "$DROP" 2> /dev/null)"; cp "$DROP" "$WORK/drop.before" 2> /dev/null; sleep 1
check "2: rerun: exit 0" "$(inst "$STOCK_PATH")" "0"
check "2: rerun: drop-in byte-identical" "$(cmp -s "$DROP" "$WORK/drop.before" && echo same)" "same"
check "2: rerun: mtime unchanged" "$(stat -c %Y "$DROP" 2> /dev/null)" "$M1"
check "3: neighbour drop-in untouched" "$(cmp -s "$NEIGH" "$WORK/neigh.before" && echo same)" "same"

fresh
check "4: dry run: exit 0" "$(AW_DRY_RUN=1 inst "$STOCK_PATH")" "0"
check "4: dry run: no drop-in" "$(written)" "none"
check "4: dry run: [dry-run] line names the drop-in" "$(yes_if "$(grep -F '[dry-run]' "$WORK/out" | grep -cF "$DROP")")" "yes"

fresh; STUB_OOM=continue
check "5: continue: exit 0" "$(inst "$STOCK_PATH")" "0"
check "5: continue: no drop-in" "$(written)" "none"
check "5: continue: no DefaultOOMPolicy line" "$(grep -c DefaultOOMPolicy "$WORK/out")" "0"
fresh; STUB_OOM=fail
check "6: show fails: exit 0" "$(inst "$STOCK_PATH")" "0"
check "6: show fails: no drop-in" "$(written)" "none"
check "6: show fails: no DefaultOOMPolicy line" "$(grep -c DefaultOOMPolicy "$WORK/out")" "0"
fresh; STUB_OOM=stop
check "7: no systemctl: exit 0" "$(inst "$NOSYS_PATH")" "0"
check "7: no systemctl: no drop-in" "$(written)" "none"
fresh
check "8: macOS: exit 0" "$(inst "$WORK/darwinbin:$STOCK_PATH")" "0"
check "8: macOS: no drop-in" "$(written)" "none"
check "8: macOS: no DefaultOOMPolicy call" "$(grep -c DefaultOOMPolicy "$WORK/systemctl.log")" "0"

# meta-launch.sh (no team config: it exits 1 after the place the warning goes)
rm -rf "$AW_STATE_DIR/meta"
launch() { PATH="$1" bash "$DIR/meta-launch.sh" --dry-run > /dev/null 2> "$WORK/err"; echo $?; }
STUB_OOM=stop; : > "$WORK/systemctl.log"
check "13: no team config: exit 1" "$(launch "$STOCK_PATH")" "1"
check "9: stop: exactly one DefaultOOMPolicy line" "$(grep -c DefaultOOMPolicy "$WORK/err")" "1"
check "9: the line names the fix" "$(yes_if "$(grep DefaultOOMPolicy "$WORK/err" | grep -cE '10-oom-continue\.conf|install-meta\.sh')")" "yes"
check "9: the line names daemon-reload" "$(yes_if "$(grep DefaultOOMPolicy "$WORK/err" | grep -c daemon-reload)")" "yes"
STUB_OOM=continue; launch "$STOCK_PATH" > /dev/null
check "10: continue: no warning" "$(grep -c DefaultOOMPolicy "$WORK/err")" "0"
STUB_OOM=fail; launch "$STOCK_PATH" > /dev/null
check "11: show fails: no warning" "$(grep -c DefaultOOMPolicy "$WORK/err")" "0"
STUB_OOM=stop; launch "$NOSYS_PATH" > /dev/null
check "12: no systemctl: no warning" "$(grep -c DefaultOOMPolicy "$WORK/err")" "0"

echo "oom-policy: $pass passed, $fail failed"
[ "$fail" = 0 ]
