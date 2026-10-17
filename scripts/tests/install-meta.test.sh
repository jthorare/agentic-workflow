#!/usr/bin/env bash
# Tests for scripts/install-meta.sh on a sandboxed HOME and a temp state directory, without the NativeAOT publish
# (AW_SKIP_BUILD=1 with a stub binary; nothing is built, installed or linked outside the sandbox): for Claude both hooks
# registered once (Stop; PostToolUse matching SendMessage), the hook scripts and the fail-open logger copied, the PATH links
# made, and a rerun idempotent; for Codex and Cursor the lane hooks (session-id-report, meta-lane-gate) registered once through
# the adapters beside the user's own hooks, the Codex /hooks trust step printed, and nothing registered for the other events;
# macOS installs like Linux, any other OS is refused loudly, the publish gets the commit and build-time stamp, and the installed
# version is printed.
# Run: bash scripts/tests/install-meta.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # the caller may be a meta session: its team config, bin and claude must not reach the stubs
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_SKIP_BUILD=1
unset CLAUDE_DIR CLAUDE_LOCAL_BIN AW_DRY_RUN AW_HOOKS_DIR CODEX_HOOKS_FILE CODEX_HOME CURSOR_HOOKS_FILE CLAUDE_SETTINGS_FILE CLAUDE_HOOKS_DIR AW_HOOK_ADAPTER_FLAGS SETTINGS_FILE TOOLKIT_DIR
# the installer behaves the same on Linux and macOS: stub uname to Linux so the suite is one thing everywhere (the Darwin and other-OS cases below override it)
mkdir -p "$WORK/linuxbin"
printf '#!/usr/bin/env bash\necho Linux\n' > "$WORK/linuxbin/uname"; chmod +x "$WORK/linuxbin/uname"
export PATH="$WORK/linuxbin:$PATH"
mkdir -p "$HOME/.claude" "$AW_STATE_DIR/bin"
printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"
# This tree may be a linked worktree (a lane's): a configured team points at the canonical checkout, so the team config's repo
# is this tree, which is what the installer requires of a configured team
mkdir -p "$AW_STATE_DIR/meta"
jq -n --arg r "$(cd "$DIR/.." && pwd -P)" '{team:"t",repo:$r}' > "$AW_STATE_DIR/meta/team.json"
S="$HOME/.claude/settings.json"
echo '{"model":"opus","hooks":{"Stop":[{"hooks":[{"type":"command","command":"/x/done-gate.sh # aw:done-gate"}]}]}}' > "$S"

bash "$DIR/install-meta.sh" --provider claude > "$WORK/out" 2>&1; RC=$?
check "exit 0" "$RC" "0"
check "Stop: the meta gate added beside the existing hook" \
  "$(jq -r '[.hooks.Stop[].hooks[].command] | map(sub(".*# ";"")) | join(",")' "$S")" "aw:done-gate,aw:meta-stop-gate"
check "PostToolUse: the outbox hook, matching SendMessage" \
  "$(jq -r '.hooks.PostToolUse[] | select(.hooks[0].command | endswith("# aw:meta-outbox")) | .matcher' "$S")" "SendMessage"
check "other settings kept" "$(jq -r .model "$S")" "opus"
check "hook scripts and the fail-open logger copied, executable" \
  "$(test -x "$HOME/.claude/hooks/meta-stop-gate.sh" && test -x "$HOME/.claude/hooks/meta-outbox.sh" && test -f "$HOME/.claude/hooks/lib/failopen.sh" && echo yes)" "yes"
check "links on PATH" "$(readlink "$HOME/.local/bin/aw-meta"):$(basename "$(readlink "$HOME/.local/bin/aw-meta-launch")")" \
  "$AW_STATE_DIR/bin/aw-meta:meta-launch.sh"

cp "$S" "$WORK/before"
bash "$DIR/install-meta.sh" --provider claude > /dev/null 2>&1
check "rerun: settings unchanged (no duplicate hooks)" "$(cmp -s "$S" "$WORK/before" && echo same)" "same"

# Codex: the lane hooks, through the adapter, beside the user's own hooks; the trust step is printed
export CODEX_HOME="$HOME/.codex"; mkdir -p "$CODEX_HOME"
echo '{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"/x/own-stop.sh # mine"}]}]}}' > "$CODEX_HOME/hooks.json"
C="$CODEX_HOME/hooks.json"
OUT="$(bash "$DIR/install-meta.sh" --provider codex 2>&1)"; RC=$?
check "codex: exit 0" "$RC" "0"
check "codex: the trust step is printed" "$(printf '%s' "$OUT" | grep -c 'run `/hooks` to trust the `aw:\*` hooks')" "1"
check "codex: SessionStart runs session-id-report through the adapter" \
  "$(jq -r '.hooks.SessionStart[].hooks[].command' "$C" | grep -c "adapters/codex.sh $AW_STATE_DIR/hooks/session-id-report.sh # aw:session-id-report")" "1"
check "codex: UserPromptSubmit runs meta-lane-gate through the adapter" \
  "$(jq -r '.hooks.UserPromptSubmit[].hooks[].command' "$C" | grep -c "adapters/codex.sh $AW_STATE_DIR/hooks/meta-lane-gate.sh # aw:meta-lane-gate")" "1"
check "codex: Stop gets meta-lane-gate beside the user's own hook" "$(jq -r '[.hooks.Stop[].hooks[].command] | map(sub(".*# ";"")) | join(",")' "$C")" "mine,aw:meta-lane-gate"
check "codex: the Claude-only hooks are not registered" "$(grep -c 'meta-stop-gate\|meta-outbox' "$C")" "0"
check "codex: scripts, adapters and the fail-open logger staged" \
  "$(test -x "$AW_STATE_DIR/hooks/session-id-report.sh" && test -x "$AW_STATE_DIR/hooks/meta-lane-gate.sh" && test -x "$AW_STATE_DIR/hooks/adapters/codex.sh" && test -f "$AW_STATE_DIR/hooks/lib/failopen.sh" && echo yes)" "yes"
check "codex: nothing written to the Claude settings by this install" "$(cmp -s "$S" "$WORK/before" && echo same)" "same"
cp "$C" "$WORK/codex-before"
bash "$DIR/install-meta.sh" --provider codex > /dev/null 2>&1
check "codex: rerun leaves hooks.json unchanged" "$(cmp -s "$C" "$WORK/codex-before" && echo same)" "same"

# Cursor: flat entries; two scripts at sessionStart
mkdir -p "$HOME/.cursor"; U="$HOME/.cursor/hooks.json"
echo '{"version":1,"hooks":{"stop":[{"command":"/x/own-stop.sh # mine"}]}}' > "$U"
OUT="$(bash "$DIR/install-meta.sh" --provider cursor 2>&1)"; RC=$?
check "cursor: exit 0, no Codex trust step" "$RC:$(printf '%s' "$OUT" | grep -c 'run .\/hooks. to trust')" "0:0"
check "cursor: sessionStart has session-id-report and meta-lane-gate" "$(jq -r '[.hooks.sessionStart[].command] | map(sub(".*# ";"")) | sort | join(",")' "$U")" "aw:meta-lane-gate,aw:session-id-report"
check "cursor: postToolUse and stop run meta-lane-gate; the user's stop hook is kept" \
  "$(jq -r '[.hooks.postToolUse[].command] | map(sub(".*# ";"")) | join(",")' "$U"):$(jq -r '[.hooks.stop[].command] | map(sub(".*# ";"")) | join(",")' "$U")" "aw:meta-lane-gate:mine,aw:meta-lane-gate"
check "cursor: the entries go through the cursor adapter" "$(jq -r '.hooks.stop[].command' "$U" | grep -c "adapters/cursor.sh $AW_STATE_DIR/hooks/meta-lane-gate.sh")" "1"
cp "$U" "$WORK/cursor-before"
bash "$DIR/install-meta.sh" --provider cursor > /dev/null 2>&1
check "cursor: rerun leaves hooks.json unchanged" "$(cmp -s "$U" "$WORK/cursor-before" && echo same)" "same"
check "links on PATH are made for any provider too" "$(readlink "$HOME/.local/bin/aw-meta")" "$AW_STATE_DIR/bin/aw-meta"
OUT="$(AW_DRY_RUN=1 bash "$DIR/install-meta.sh" --provider codex 2>&1)"; RC=$?
check "codex dry-run: says what it would do, still prints the trust step, exit 0" "$RC:$(printf '%s' "$OUT" | grep -c 'dry-run.*lane hooks'):$(printf '%s' "$OUT" | grep -c 'run .\/hooks. to trust')" "0:1:1"
OUT="$(bash "$DIR/install-meta.sh" --provider nope 2>&1)"; RC=$?
check "an unknown provider is refused" "$RC" "1"
rm "$AW_STATE_DIR/bin/aw-meta"
bash "$DIR/install-meta.sh" --provider claude > /dev/null 2>&1; RC=$?
check "AW_SKIP_BUILD with no binary: exit 1" "$RC" "1"

# macOS installs like Linux (its host adapters read sysctl, vm_stat and ps); an OS that is neither fails loudly, before touching anything
printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"
mkdir -p "$WORK/fakebin"
printf '#!/usr/bin/env bash\necho Darwin\n' > "$WORK/fakebin/uname"; chmod +x "$WORK/fakebin/uname"
OUT="$(PATH="$WORK/fakebin:$PATH" CLAUDE_LOCAL_BIN="$WORK/darwin-bin" bash "$DIR/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "Darwin: installs, exit 0, no refusal" "$RC:$(printf '%s' "$OUT" | grep -c 'Linux-only')" "0:0"
check "Darwin: linked" "$(readlink "$WORK/darwin-bin/aw-meta")" "$AW_STATE_DIR/bin/aw-meta"
OUT="$(PATH="$WORK/fakebin:$PATH" CLAUDE_LOCAL_BIN="$WORK/darwin-bin2" bash "$DIR/install-meta.sh" --provider codex 2>&1)"; RC=$?
check "Darwin, another provider: installs too" "$RC:$(printf '%s' "$OUT" | grep -c 'Linux-only')" "0:0"
mkdir -p "$WORK/bsdbin"
printf '#!/usr/bin/env bash\necho FreeBSD\n' > "$WORK/bsdbin/uname"; chmod +x "$WORK/bsdbin/uname"
OUT="$(PATH="$WORK/bsdbin:$PATH" CLAUDE_LOCAL_BIN="$WORK/bsd-bin" bash "$DIR/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "an unsupported OS: exit 1 naming the OS and the supported ones" "$RC:$(printf '%s' "$OUT" | grep -c 'Linux and macOS only (this is FreeBSD)')" "1:1"
check "an unsupported OS: nothing linked" "$(ls "$WORK/bsd-bin" 2>/dev/null | wc -l | tr -d ' ')" "0"

# The version stamp: the installed binary's version is printed, an old binary without `version` says so, and the publish is
# stamped with this tree's commit and a build time. A stub dotnet stands in for the SDK (under the sandboxed HOME's ~/.dotnet, where
# the script looks first); it records its publish arguments and writes a stub aw-meta and e_sqlite3 into the -o directory.
cat > "$AW_STATE_DIR/bin/aw-meta" <<'STUB'
#!/usr/bin/env bash
if [ "${1:-}" = version ]; then echo '{"commit":"abc123","builtAt":"2026-10-17T00:00:00Z","os":"macOS","rid":"osx-arm64"}'; exit 0; fi
exit 0
STUB
OUT="$(CLAUDE_LOCAL_BIN="$WORK/ver-bin" bash "$DIR/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "the installed version is printed: commit, build time, OS and runtime id" "$RC:$(printf '%s' "$OUT" | grep -c 'aw-meta abc123 (built 2026-10-17T00:00:00Z, macOS osx-arm64)')" "0:1"
printf '#!/usr/bin/env bash\nexit 2\n' > "$AW_STATE_DIR/bin/aw-meta"
OUT="$(CLAUDE_LOCAL_BIN="$WORK/ver-bin" bash "$DIR/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "a binary that has no version command is said to predate it, exit 0" "$RC:$(printf '%s' "$OUT" | grep -c 'predates .aw-meta version')" "0:1"
unlink "$AW_STATE_DIR/bin/aw-meta"
mkdir -p "$HOME/.dotnet"
sdk="$(jq -r '.sdk.version' "$DIR/../global.json")"
cat > "$HOME/.dotnet/dotnet" <<STUB
#!/usr/bin/env bash
case "\${1:-}" in
  --list-sdks) echo "$sdk [/stub]" ;;
  --info) echo "  RID:         osx-arm64" ;;
  publish)
    echo "\$*" > "$WORK/publish-args"
    [ -e "$WORK/publish-fails" ] && { echo "stub publish failed" >&2; exit 1; }
    out=""; prev=""; for a in "\$@"; do [ "\$prev" = -o ] && out="\$a"; prev="\$a"; done
    mkdir -p "\$out"
    printf '#!/usr/bin/env bash\necho built\n' > "\$out/aw-meta"; chmod +x "\$out/aw-meta"
    echo dylib > "\$out/libe_sqlite3.dylib" ;;
esac
STUB
chmod +x "$HOME/.dotnet/dotnet"
unset AW_SKIP_BUILD
OUT="$(CLAUDE_LOCAL_BIN="$WORK/pub-bin" bash "$DIR/install-meta.sh" --provider claude 2>&1)"; RC=$?
ARGS="$(cat "$WORK/publish-args" 2>/dev/null)"
check "a real install publishes for the host runtime id and installs the binary and e_sqlite3" "$RC:$(test -x "$AW_STATE_DIR/bin/aw-meta" && test -f "$AW_STATE_DIR/bin/libe_sqlite3.dylib" && echo yes):$(printf '%s' "$ARGS" | grep -c -- '-r osx-arm64')" "0:yes:1"
check "the publish is stamped with this tree's commit" "$(printf '%s' "$ARGS" | grep -c -- "-p:SourceRevisionId=$(git -C "$DIR/.." rev-parse HEAD)")" "1"
check "the publish is stamped with a UTC build time" "$(printf '%s' "$ARGS" | grep -cE -- '-p:AwBuildTime=[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z')" "1"
: > "$WORK/publish-fails"; printf 'old\n' > "$AW_STATE_DIR/bin/aw-meta"
OUT="$(CLAUDE_LOCAL_BIN="$WORK/pub-bin" bash "$DIR/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "a failed publish exits 1 with its log and leaves the installed binary alone" "$RC:$(printf '%s' "$OUT" | grep -c 'stub publish failed'):$(cat "$AW_STATE_DIR/bin/aw-meta")" "1:1:old"
unlink "$HOME/.dotnet/dotnet"
export AW_SKIP_BUILD=1
printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"

# Linking: aw-meta-launch points into the canonical checkout only. A temp git repo holding a copy of the installer stands in for the
# tree it runs from; a linked worktree of it is the throwaway one.
TOP="$DIR/.."
mkdir -p "$WORK/tree/scripts" "$WORK/tree/config/hooks/adapters" "$WORK/tree/config/lib"
cp "$DIR/install-meta.sh" "$DIR/meta-launch.sh" "$DIR/meta-team-lib.sh" "$DIR/meta-version-lib.sh" "$WORK/tree/scripts/"
cp "$TOP/config/hooks/adapters/"* "$WORK/tree/config/hooks/adapters/" 2>/dev/null
cp -r "$TOP/config/lib/." "$WORK/tree/config/lib/"
cp "$TOP/config/hooks/meta-stop-gate.sh" "$TOP/config/hooks/meta-outbox.sh" "$WORK/tree/config/hooks/"
cp "$TOP/global.json" "$WORK/tree/"
git -C "$WORK/tree" init -q && git -C "$WORK/tree" add -A && git -C "$WORK/tree" -c user.email=t@t -c user.name=t commit -qm t
git -C "$WORK/tree" worktree add -q --detach "$WORK/wt" HEAD
export CLAUDE_LOCAL_BIN="$WORK/linkbin"
link() { readlink "$CLAUDE_LOCAL_BIN/aw-meta-launch" 2>/dev/null || echo none; }

rm -f "$AW_STATE_DIR/meta/team.json"
OUT="$(bash "$WORK/wt/scripts/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "worktree, no team repo: refused with its own exit code 3" "$RC:$(printf '%s' "$OUT" | grep -c 'linked git worktree')" "3:1"
check "worktree, no team repo: nothing linked" "$(link)" "none"
OUT="$(bash "$WORK/wt/scripts/install-meta.sh" --provider claude --allow-worktree 2>&1)"; RC=$?
check "worktree with --allow-worktree: linked into the worktree" "$RC:$(link)" "0:$WORK/wt/scripts/meta-launch.sh"

rm -f "$CLAUDE_LOCAL_BIN/aw-meta-launch"
mkdir -p "$WORK/canon2/scripts"; printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/canon2/scripts/meta-launch.sh"
jq -n --arg r "$WORK/canon2" '{team:"t",repo:$r}' > "$AW_STATE_DIR/meta/team.json"
OUT="$(bash "$WORK/wt/scripts/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "a team repo set, run from another tree: refused, nothing linked" "$RC:$(printf '%s' "$OUT" | grep -c 'not the team.s repo'):$(link)" "3:1:none"
OUT="$(bash "$WORK/tree/scripts/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "a team repo set, run from a second plain clone: refused too" "$RC:$(link)" "3:none"
OUT="$(bash "$WORK/wt/scripts/install-meta.sh" --provider claude --allow-worktree 2>&1)"; RC=$?
check "a team repo set, --allow-worktree: installs from this tree and links it" "$RC:$(link)" "0:$WORK/wt/scripts/meta-launch.sh"
rm -f "$CLAUDE_LOCAL_BIN/aw-meta-launch"
cp "$WORK/tree/scripts/install-meta.sh" "$WORK/tree/scripts/meta-team-lib.sh" "$WORK/tree/scripts/meta-version-lib.sh" "$WORK/canon2/scripts/"
cp -r "$WORK/tree/config" "$WORK/tree/global.json" "$WORK/canon2/"
OUT="$(bash "$WORK/canon2/scripts/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "a team repo set, run from it: linked into it, no flag needed" "$RC:$(link)" "0:$WORK/canon2/scripts/meta-launch.sh"
rm -f "$CLAUDE_LOCAL_BIN/aw-meta-launch" "$AW_STATE_DIR/meta/team.json"
OUT="$(bash "$WORK/tree/scripts/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "normal checkout, no team repo: linked into that checkout" "$RC:$(link)" "0:$WORK/tree/scripts/meta-launch.sh"

# setup.sh --with-meta (config/lib/setup-meta-step.sh): exit 3 from the installer (a refused tree) only warns; any other failure fails the step
mkdir -p "$WORK/fake/scripts" "$WORK/fake/config/lib"
step() { # step <installer exit code> -> "<step rc>:<warnings>:<installer calls>"
  printf '#!/usr/bin/env bash\necho "$*" >> "%s/calls"\nexit %s\n' "$WORK" "$1" > "$WORK/fake/scripts/install-meta.sh"
  : > "$WORK/calls"
  local out rc=0
  out="$( ( source "$DIR/../config/lib/setup-meta-step.sh"; aw_setup_meta_step "$WORK/fake" claude codex ) 2>&1 )" || rc=$?
  echo "$rc:$(printf '%s' "$out" | grep -c 'NOT installed'):$(wc -l < "$WORK/calls" | tr -d ' ')"
}
check "setup step: a tree refusal (3) warns for each provider and returns 0" "$(step 3)" "0:2:2"
check "setup step: a publish failure (1) fails the step at once" "$(step 1)" "1:0:1"
check "setup step: a failure says setup stops and the installed binary may be stale" "$( ( source "$DIR/../config/lib/setup-meta-step.sh"; step 1 > /dev/null; aw_setup_meta_step "$WORK/fake" claude ) 2>&1 | grep -c 'install-meta.sh failed for claude (exit 1); setup stops')" "1"
check "setup step: an installer success returns 0, no warning" "$(step 0)" "0:0:2"

# setup.sh --with-meta runs the meta step on every OS (it used to print "skipped" on macOS), under the real uname
mkdir -p "$WORK/shome"
OUT="$(PATH="${PATH#"$WORK/linuxbin:"}" HOME="$WORK/shome" AW_STATE_DIR="$WORK/sstate" AW_NO_SYSTEM_INSTALL=1 AW_SKIP_BUILD= bash "$DIR/../setup.sh" --providers claude --dry-run --with-meta 2>&1)"; RC=$?
check "setup.sh --with-meta --dry-run: the meta step is planned, never skipped" "$RC:$(printf '%s' "$OUT" | grep -c 'would run scripts/install-meta.sh --provider claude'):$(printf '%s' "$OUT" | grep -c 'meta: skipped')" "0:1:0"
exit $fail
