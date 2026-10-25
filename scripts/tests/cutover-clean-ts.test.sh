#!/usr/bin/env bash
# Tests for scripts/cutover-clean-ts.sh (cutover spec §8, §8.1, P2). Run: bash scripts/tests/cutover-clean-ts.test.sh
# Each scenario seeds a fake TS install in its own temp directory (state dir, ~/.local/bin, LaunchAgents, git config, a
# profile with one repo that carries a v2 pre-commit hook, a TS-shaped judge database) and stub binaries in place of the
# published C# ones. Nothing here builds, starts sindri, touches the real HOME or kills a process (the one background
# process, a file holder for the step-1 refusal, is stopped by its recorded pid).
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$DIR/../.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/cutover-clean-ts.sh"

# The lane and agent environment must not steer the script or the stubs.
for v in $(env | sed -n 's/^\(AW_META[A-Z_]*\|AW_SINDRI[A-Z_]*\|TMUX\|TMUX_PANE\|AW_PROFILE_DIR\|AW_JUDGE[A-Z_]*\)=.*/\1/p'); do unset "$v"; done
ORIG_PATH="$PATH"
fail=0
ok() { echo "ok - $1"; }
bad() { echo "not ok - $1"; fail=1; }
check() { if [ "$2" == "$3" ]; then ok "$1"; else bad "$1"; echo "  expected: $3"; echo "  actual:   $2"; fi; }
has() { case "$2" in *"$3"*) ok "$1" ;; *) bad "$1"; echo "  missing: $3"; echo "  in: $2" | head -20 ;; esac; }
lacks() { case "$2" in *"$3"*) bad "$1"; echo "  unexpected: $3" ;; *) ok "$1" ;; esac; }
wipe() { local p; for p in "$@"; do case "$p" in "$WORK"/*) rm -rf "$p" ;; *) echo "wipe: refusing $p" >&2; exit 2 ;; esac; done; }
perm() { stat -c %a "$1" 2> /dev/null || stat -f %Lp "$1"; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# seed <dir> [c#]: a fake install; with "c#" the box has already been flipped (C# wrappers, a C# judge database).
seed() {
  local d="$1" flipped="${2:-}" s="$1/state" b="$1/bin" n
  mkdir -p "$s" "$b" "$d/la" "$d/home/.claude" "$d/stubs" "$d/root/scripts" "$d/repo"
  # --- stubs for the published C# build
  mkdir -p "$s/bin/cs/stamp1"
  echo built > "$s/bin/cs/stamp1/build-info"
  cat > "$s/bin/cs/stamp1/judge" <<'EOF'
#!/usr/bin/env bash
if [ "${1:-}" = health ]; then
  if grep -q TS_SHAPE "$AW_STATE_DIR/judge/decisions.sqlite" 2> /dev/null; then echo "$AW_STATE_DIR/judge/decisions.sqlite has tables but no schema_version table, so this runner didn't create it. Nothing was written. Check the path points at this package's own database." >&2; exit 1; fi
  echo '{"status":"ok","failures24h":0}'; exit 0
fi
exit 2
EOF
  cat > "$s/bin/cs/stamp1/sindri" <<EOF
#!/usr/bin/env bash
# stub: only \`scrub --install-pre-commit --repo P --bin B\`
echo "\$*" >> "$d/stubs/sindri.log"
[ "\${1:-}" = scrub ] || exit 2
while [ \$# -gt 0 ]; do case "\$1" in --repo) R="\$2"; shift ;; --bin) B="\$2"; shift ;; esac; shift; done
if git -C "\$R" config --get core.hooksPath > /dev/null 2>&1; then echo "sindri: SND-SCRUB-003 A different pre-commit hook is already installed" >&2; exit 2; fi
printf '#!/bin/sh\n# sindri-pre-commit v3\nSINDRI=%s\n' "\$B" > "\$R/.git/hooks/pre-commit"
EOF
  chmod +x "$s/bin/cs/stamp1/judge" "$s/bin/cs/stamp1/sindri"
  cat > "$d/stubs/claude" <<EOF
#!/usr/bin/env bash
echo "\$*" >> "$d/stubs/claude.log"
case "\$1 \$2 \$3" in
  "plugin list --json") [ -f "$d/stubs/plugin.gone" ] || echo '[{"id":"aw-live@agentic-workflow-mods"}]' ;;
  "plugin uninstall aw-live@agentic-workflow-mods") touch "$d/stubs/plugin.gone" ;;
  "plugin marketplace list") [ -f "$d/stubs/market.gone" ] || echo '[{"name":"agentic-workflow-mods"}]' ;;
  "plugin marketplace remove") touch "$d/stubs/market.gone" ;;
esac
exit 0
EOF
  chmod +x "$d/stubs/claude"
  cp "$SCRIPT" "$d/root/scripts/cutover-clean-ts.sh"
  cat > "$d/root/scripts/install-cs.sh" <<'EOF'
#!/usr/bin/env bash
[ "${1:-}" = --link ] || exit 2
ln -sfn "$AW_STATE_DIR/bin/cs/stamp1" "$AW_STATE_DIR/bin/cs/current"
for n in judge sindri; do
  printf '#!/usr/bin/env bash\nexec "%s/bin/cs/current/%s" "$@"\n' "$AW_STATE_DIR" "$n" > "$CLAUDE_LOCAL_BIN/$n.tmp"
  chmod +x "$CLAUDE_LOCAL_BIN/$n.tmp"; mv -f "$CLAUDE_LOCAL_BIN/$n.tmp" "$CLAUDE_LOCAL_BIN/$n"
done
EOF
  # --- the checkout: retired build output (ignored), and uncommitted work that git does not ignore
  git -C "$d/root" init -q
  printf '/judge/node_modules/\n/sindri/dist/\n' > "$d/root/.gitignore"
  mkdir -p "$d/root/judge/node_modules/p" "$d/root/sindri/dist" "$d/root/scorer/dist"
  echo x > "$d/root/judge/node_modules/p/i.js"; echo x > "$d/root/sindri/dist/cli.js"; echo mine > "$d/root/scorer/dist/notes.txt"
  echo '{}' > "$d/root/judge/package.json"
  git -C "$d/root" add .gitignore judge/package.json
  git -C "$d/root" -c user.name=t -c user.email=t@t commit -q -m seed
  # --- the kept list
  mkdir -p "$s/judge/sessions" "$s/judge/outbox" "$s/judge/evals" "$s/judge/trait-check" "$s/context-guard" "$s/locks/heavy-job.lock" \
    "$s/hooks" "$s/fleet" "$s/ui-evidence/runs/r1" "$s/myrepo-slug/plans" "$s/myrepo-slug/reviews" "$s/digests" "$s/meta" "$s/cache" \
    "$s/sindri-app" "$s/bugfix/myrepo-slug/t1" "$s/logs"
  echo '{"sid":"s1"}' > "$s/judge/sessions/s1.json"; echo '{}' > "$s/judge/outbox/s1.jsonl"; echo '{"name":"a"}' > "$s/judge/teammate-names.jsonl"
  echo e > "$s/judge/evals/e"; echo t > "$s/judge/trait-check/t"; echo g > "$s/context-guard/g.log"; echo me > "$s/locks/heavy-job.lock/owner"
  echo h > "$s/hooks/h.sh"; echo '{}' > "$s/fleet/f.json"; echo '{}' > "$s/ui-evidence/runs/r1/summary.json"
  echo p > "$s/myrepo-slug/plans/p.md"; echo '{}' > "$s/myrepo-slug/reviews/r.json"; echo d > "$s/digests/d.md"; echo '{}' > "$s/meta/m.json"
  echo c > "$s/cache/c"; echo cs > "$s/sindri-app/ledger.db"; echo '{"pid": 999999}' > "$s/sindri-app/control.json"
  echo '{}' > "$s/bugfix/myrepo-slug/t1/state.json"; echo old > "$s/logs/older.log"; echo bin > "$s/bin/aw-meta"
  ln -s "$d/root" "$s/toolkit"
  cat > "$d/home/.claude/settings.json" <<'EOF'
{"hooks":{"UserPromptSubmit":[{"hooks":[{"type":"command","command":"/x/prompt-sort.sh # aw:prompt-sort"}]}],"PreToolUse":[{"hooks":[{"type":"command","command":"/x/trait-check.sh # aw:trait-check"}]}]}}
EOF
  # --- the repo with a v2 hook; the git config with the TS template
  git -C "$d/repo" init -q
  printf '#!/bin/sh\n# sindri-pre-commit v2\nSINDRI=%s\n' "'$b/sindri'" > "$d/repo/.git/hooks/pre-commit"
  printf '[init]\n\ttemplateDir = %s/sindri/git-template\n' "$s" > "$d/gitconfig"
  if [ "$flipped" = "c#" ]; then
    echo '{"schema_version":3}' > "$s/judge/decisions.sqlite"; echo '{"enabled":true}' > "$s/judge/config.json"
    wipe "$d/root/judge/node_modules" "$d/root/sindri/dist" "$d/root/scorer"
    ln -sfn "$s/bin/cs/stamp1" "$s/bin/cs/current"
    for n in judge sindri; do printf '#!/usr/bin/env bash\nexec "%s/bin/cs/current/%s" "$@"\n' "$s" "$n" > "$b/$n"; chmod +x "$b/$n"; done
    printf '[user]\n\tname = t\n' > "$d/gitconfig"
    touch "$d/stubs/plugin.gone" "$d/stubs/market.gone"
    return 0
  fi
  # --- the TS side
  mkdir -p "$s/sindri/git-template/hooks" "$s/sindri/channels/stable/abc" "$s/scorer/reports" "$s/scorer/live" "$s/audit/weekly" \
    "$s/profile/repos" "$s/myrepo-slug/bugfix" "$s/bin/sindri-app/stampA"
  echo db > "$s/sindri/ledger.db"; echo t > "$s/sindri/git-template/hooks/pre-commit"; echo n > "$s/sindri/channels/stable/abc/x"
  echo db > "$s/scorer/scorer.sqlite"; echo r > "$s/scorer/reports/r"; echo a > "$s/audit/weekly/a"; echo '{}' > "$s/myrepo-slug/bugfix/run.json"
  echo bin > "$s/bin/sindri-app/stampA/sindri"
  printf 'name: demo\ntoken: ghp_0123456789abcdefghijABCDEFGH\nrepos: [app]\n' > "$s/profile/profile.yaml"
  printf 'name: app\npath: %s\napi_key: hunter2supersecret\n' "$d/repo" > "$s/profile/repos/app.yaml"
  echo 'TS_SHAPE tables but no schema_version' > "$s/judge/decisions.sqlite"; echo wal > "$s/judge/decisions.sqlite-wal"; echo shm > "$s/judge/decisions.sqlite-shm"
  echo '{"threshold":0.7,"providers":["claude"],"promptSort":{"enabled":true,"budgetMs":900,"scaffolds":{"brief":true,"bugfix":false}}}' > "$s/judge/config.json"
  for n in scorer scorer-audit sindri-observe sindri-index-quick sindri-index sindri-evolve; do echo '<plist/>' > "$d/la/com.agentic-workflow.$n.plist"; done
  for n in judge sindri scorer sindri-next; do
    printf '#!/usr/bin/env bash\n# exec node dist/cli.js\n[ "${1:-} ${2:-}" = "label status" ] && { echo "labels 12 evals 3"; exit 0; }\nexit 0\n' > "$b/$n"; chmod +x "$b/$n"
  done
  echo 'unrelated' > "$b/sindri-view"; chmod +x "$b/sindri-view"
  echo '#!/bin/sh' > "$d/root/scripts/sindri-app.sh"; ln -s "$d/root/scripts/sindri-app.sh" "$b/sindri-app"
}

# env <dir>: point the script at a seeded box (everything under the temp dir; the real HOME is never read).
use() {
  export AW_STATE_DIR="$1/state" CLAUDE_LOCAL_BIN="$1/bin" AW_LAUNCH_AGENTS_DIR="$1/la" GIT_CONFIG_GLOBAL="$1/gitconfig" HOME="$1/home"
  export PATH="$1/bin:$1/stubs:$ORIG_PATH"
  unset AW_PROFILE_DIR
}
run() { bash "$1/root/scripts/cutover-clean-ts.sh" "${@:2}" 2>&1; }

# names <dir>: every path under the box (state, bin, plists, config, checkout, home), minus the log dir, `current` and the stubs.
names() {
  (cd "$1" && find . -path ./stubs -prune -o -path ./state/logs -prune -o -path ./state/bin/cs/current -prune -o -path '*/.git/*' -prune -o -print | sort)
}
# snap <dir>: names plus a checksum of every file and the target of every link, and the logs (all but cutover logs).
snap() {
  (cd "$1" && find . -path ./stubs -prune -o -path './state/logs/cutover-*' -prune -o -path '*/.git/objects' -prune -o \( -type f -o -type l -o -type d \) -print | sort | while IFS= read -r f; do
    if [ -L "$f" ]; then echo "L $f $(readlink "$f")"; elif [ -d "$f" ]; then echo "D $f"; else echo "F $f $(cksum < "$f")"; fi
  done)
}
# kept <dir>: the paths of the §8.1 kept list, checksummed.
kept() {
  (cd "$1/state" && for p in judge/sessions judge/outbox judge/teammate-names.jsonl judge/evals judge/trait-check context-guard locks hooks fleet \
      ui-evidence/runs myrepo-slug/plans myrepo-slug/reviews digests meta cache sindri-app bugfix bin/aw-meta bin/cs/stamp1; do
    find "$p" -type f -exec cksum {} \; 2> /dev/null; done | sort
    readlink toolkit
  ) ; cksum < "$1/home/.claude/settings.json"; cksum < "$1/state/logs/older.log"
}

# ---------------------------------------------------------------------------------------------------------------------
echo "# dry run changes nothing (each mode)"
D1="$WORK/dry"; seed "$D1"; use "$D1"
before="$(snap "$D1")"
for m in --pre-link --post-link --all; do
  flag="$m"; [ "$m" = --all ] && flag=""
  out="$(run "$D1" ${flag:+"$flag"} --dry-run)"; rc=$?
  check "dry-run $m exits 0" "$rc" 0
  has "dry-run $m prints would lines" "$out" "would"
  check "dry-run $m changes nothing" "$(snap "$D1")" "$before"
done
check "dry-run creates no log" "$(ls "$D1/state/logs")" "older.log"

# ---------------------------------------------------------------------------------------------------------------------
echo "# a step-1 refusal changes nothing"
D2="$WORK/refuse"; seed "$D2"; use "$D2"
before="$(snap "$D2")"
if command -v lsof > /dev/null 2>&1 || command -v fuser > /dev/null 2>&1; then
  sleep 60 9< "$D2/state/sindri/ledger.db" & holder=$!
  sleep 1
  out="$(run "$D2" --pre-link)"; rc=$?
  kill "$holder" 2> /dev/null; wait "$holder" 2> /dev/null
  check "refusal exits 1" "$rc" 1
  has "refusal says nothing was changed" "$out" "Nothing was changed"
  check "refusal changes nothing" "$(snap "$D2")" "$before"
  check "refusal creates no log" "$(ls "$D2/state/logs")" "older.log"
else
  echo "skip - neither lsof nor fuser exists"
fi

# ---------------------------------------------------------------------------------------------------------------------
echo "# a log that cannot be written stops with nothing changed"
D3="$WORK/nolog"; seed "$D3"; use "$D3"
wipe "$D3/state/logs"; : > "$D3/state/logs"
before="$(snap "$D3")"
out="$(run "$D3" --pre-link)"; rc=$?
check "failed log write exits 1" "$rc" 1
has "failed log write says nothing was changed" "$out" "nothing was changed"
check "failed log write changes nothing" "$(snap "$D3")" "$before"

# ---------------------------------------------------------------------------------------------------------------------
echo "# a repo with its own pre-commit hook (husky, core.hooksPath) is skipped with the manual line, not a failure (#1029)"
DH="$WORK/husky"; seed "$DH"; use "$DH"
git -C "$DH/repo" config core.hooksPath .husky/_
out_h="$(run "$DH" --pre-link)"; rc=$?
check "a foreign hook does not stop step 2" "$rc" 0
has "the skip names the repo" "$out_h" "hook v3 skipped in app"
has "the skip prints the manual line" "$out_h" "add 'sindri scrub --staged || exit 1' to it by hand"
has "the log records the reason" "$(cat "$DH"/state/logs/cutover-*.log)" "hook skipped (SND-SCRUB-003) app"

# ---------------------------------------------------------------------------------------------------------------------
echo "# the full removal, pre-link then the link then post-link"
D4="$WORK/full"; seed "$D4"; use "$D4"
names_before="$(names "$D4")"
kept_before="$(kept "$D4")"
out_pre="$(run "$D4" --pre-link)"; rc=$?
check "pre-link exits 0" "$rc" 0
has "pre-link reports the hook" "$out_pre" "installed hook v3 in app"
check "hook v3 replaced v2" "$(sed -n 2p "$D4/repo/.git/hooks/pre-commit")" "# sindri-pre-commit v3"
has "hook install passes the stable wrapper path" "$(cat "$D4/stubs/sindri.log")" "--bin $D4/bin/sindri"
[ ! -e "$D4/state/judge/decisions.sqlite" ] && ok "TS judge database deleted" || bad "TS judge database still there"
[ ! -e "$D4/state/judge/config.json" ] && ok "judge config deleted" || bad "judge config still there"
check "template dir unset" "$(git config --global --get init.templateDir || echo unset)" unset
if [ "$(uname)" = Darwin ]; then
  [ ! -e "$D4/la/com.agentic-workflow.scorer.plist" ] && ok "plists deleted (macOS)" || bad "plist still there"
  plists='|la/com\.agentic-workflow\.[a-z-]+\.plist$'
else
  [ -e "$D4/la/com.agentic-workflow.scorer.plist" ] && ok "plists kept (Linux has no launchd)" || bad "a plist was deleted on Linux"
  has "Linux gets the cron reminder" "$out_pre" "remove any cron line you added by hand"
  plists=''
fi
bash "$D4/root/scripts/install-cs.sh" --link   # step 6, the stub
out_post="$(run "$D4" --post-link)"; rc=$?
check "post-link exits 0" "$rc" 0
has "post-link prints the next steps" "$out_post" "sindri profile init"
has "post-link prints the repo onboard line" "$out_post" "sindri repo onboard $D4/repo"
has "post-link reports what it left alone" "$out_post" "left alone (not a TS wrapper this script knows): sindri-view"
check "judge health passed through the wrapper" "$("$D4/bin/judge" health | jq -r .status)" ok
names_after="$(names "$D4")"
expected="$(printf '%s\n' "$names_before" | grep -vE '^\./(state/(sindri|scorer|audit|profile)(/|$)|state/myrepo-slug/bugfix(/|$)|state/bin/sindri-app(/|$)|state/judge/(config\.json|decisions\.sqlite(-shm|-wal)?)$'"$plists"'|bin/(scorer|sindri-next|sindri-app)$|root/judge/node_modules(/|$)|root/sindri/dist(/|$))' | grep -vx './state/logs' )"
check "exactly the §8 list was removed" "$names_after" "$expected"
check "the kept list is byte-identical" "$(kept "$D4")" "$kept_before"
[ -f "$D4/root/scorer/dist/notes.txt" ] && ok "uncommitted work in a retired dir is kept" || bad "uncommitted work was deleted"
[ -f "$D4/root/judge/package.json" ] && ok "tracked files are kept" || bad "tracked file deleted"
has "claude plugin uninstall ran" "$(cat "$D4/stubs/claude.log")" "plugin uninstall aw-live@agentic-workflow-mods --scope user"
has "claude marketplace remove ran" "$(cat "$D4/stubs/claude.log")" "plugin marketplace remove agentic-workflow-mods"
# the log
LOGF="$(ls "$D4"/state/logs/cutover-*.log | head -n 1)"
check "one log for both halves" "$(ls "$D4"/state/logs/cutover-*.log | wc -l | tr -d ' ')" 1
check "log file is 0600" "$(perm "$LOGF")" 600
logtxt="$(cat "$LOGF")"
has "log has the repo list" "$logtxt" "repo app $D4/repo"
has "log has the judge config as commands" "$logtxt" "judge config set threshold 0.7"
has "log has the scaffold that was on" "$logtxt" "judge prompt-sort scaffold brief on"
has "log has the TS label status" "$logtxt" "labels 12 evals 3"
has "log has a completed line" "$logtxt" "completed"
lacks "log has no token" "$logtxt" "ghp_0123456789"
lacks "log has no api key" "$logtxt" "hunter2supersecret"
has "log shows redaction" "$logtxt" "[redacted]"

# ---------------------------------------------------------------------------------------------------------------------
echo "# a second run changes nothing"
before="$(snap "$D4")"
out="$(run "$D4" --pre-link)"; rc=$?; check "second pre-link exits 0" "$rc" 0; has "second pre-link: nothing to remove" "$out" "nothing to remove"
out="$(run "$D4" --post-link)"; rc=$?; check "second post-link exits 0" "$rc" 0; has "second post-link: nothing to remove" "$out" "nothing to remove"
out="$(run "$D4")"; rc=$?; check "second full run exits 0" "$rc" 0
check "second runs change nothing" "$(snap "$D4")" "$before"
check "second runs write no log" "$(ls "$D4"/state/logs/cutover-*.log | wc -l | tr -d ' ')" 1

# ---------------------------------------------------------------------------------------------------------------------
echo "# the no-flag run does both halves and the link"
D5="$WORK/all"; seed "$D5"; use "$D5"
out="$(run "$D5")"; rc=$?
check "no-flag run exits 0" "$rc" 0
check "no-flag run leaves the same box as the split run" "$(names "$D5" | sed "s|^\./||")" "$(names "$D4" | sed "s|^\./||")"
[ "$(sed -n 1p "$D5/bin/sindri")" = '#!/usr/bin/env bash' ] && grep -q 'bin/cs/current/sindri' "$D5/bin/sindri" && ok "the link step replaced the TS sindri wrapper" || bad "sindri wrapper is not the C# one"

# ---------------------------------------------------------------------------------------------------------------------
echo "# a flipped box with live C# judge state keeps it"
D6="$WORK/flipped"; seed "$D6" "c#"; use "$D6"
before="$(snap "$D6")"
out="$(run "$D6")"; rc=$?
check "flipped run exits 0" "$rc" 0
has "flipped run: nothing to remove" "$out" "nothing to remove"
check "flipped run keeps the C# judge state" "$(snap "$D6")" "$before"

echo "# a log directory the script creates is 0700"
D7="$WORK/newlogdir"; seed "$D7"; use "$D7"; wipe "$D7/state/logs"
run "$D7" --pre-link > /dev/null
check "created log dir is 0700" "$(perm "$D7/state/logs")" 700

echo
if [ "$fail" = 0 ]; then echo "PASS"; else echo "FAIL"; exit 1; fi
