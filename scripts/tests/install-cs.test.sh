#!/usr/bin/env bash
# Tests for scripts/install-cs.sh (cutover spec §7.1) on a fake `dotnet` and a miniature git repo, in a guarded temp dir:
# AW_STATE_DIR, CLAUDE_LOCAL_BIN and TMPDIR all live under it, HOME is never changed and nothing real is read or written
# (no temp HOME: a real dotnet under one leaks a NuGet cache; the fake dotnet needs none). Covers: dry run writes nothing;
# --publish builds the seven binaries into bin/cs/<stamp>/ and touches neither `current` nor the wrappers; a second run is
# current and does not publish; the verify step runs each binary in a scratch AW_STATE_DIR; a failed publish and a build that
# does not answer leave nothing behind; --link needs a build for this source, switches `current`, writes the six wrappers over
# an existing TS wrapper, and the sindri wrapper exports the three binary paths (AW_UI_EVIDENCE_BIN among them) as resolved
# paths; the shipped recipes are staged from HEAD and gated by their evals (a count mismatch, a git archive failure and a non-git tree fail); pruning keeps the newest two, `current` and the build a running serve executes; --rebuild of that build is declined.
# Run: bash scripts/tests/install-cs.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { # has <name> <text> <fixed string>
  if printf '%s' "$2" | grep -qF -- "$3"; then echo "ok - $1"; else echo "not ok - $1"; echo "  missing: $3"; echo "  in: $2"; fail=1; fi
}
WORK="$(cd "$(mktemp -d)" && pwd -P)"
serve_pid=""
cleanup() { if [ -n "$serve_pid" ]; then kill "$serve_pid" 2> /dev/null; fi; find "$WORK" -delete 2> /dev/null; }   # by recorded pid only
trap cleanup EXIT

export AW_STATE_DIR="$WORK/state" CLAUDE_LOCAL_BIN="$WORK/localbin" TMPDIR="$WORK/tmp" AW_CS_SKIP_DOWNLOADS=1
export DOTNET_ROOT="$WORK/dotnet-root" AW_SINDRI_HOME="$WORK/sindri-home"
unset AW_SINDRI_JUDGE AW_PACKETS_BIN AW_UI_EVIDENCE_BIN AW_JUDGE_PROVIDERS
mkdir -p "$TMPDIR" "$CLAUDE_LOCAL_BIN" "$WORK/fakebin" "$DOTNET_ROOT"
export FAKE_SDK="10.0.100" FAKE_LOG="$WORK/publishes.log" FAKE_CALLS="$WORK/calls.log" FAKE_STAGED="$WORK/staged.log" FAKE_BIN="$WORK/fake-bin.sh"
: > "$FAKE_LOG"; : > "$FAKE_CALLS"

# What every published fake binary does (the generated stubs set FAKE_NAME, FAKE_REV, FAKE_RECIPES and FAKE_SELF, then run this).
cat > "$FAKE_BIN" <<'FB'
#!/usr/bin/env bash
echo "$FAKE_NAME state=${AW_STATE_DIR:-} args=$*" >> "$FAKE_CALLS"
case "$FAKE_NAME" in
  sindri)
    case "${1:-}" in
      help) echo "usage: sindri <verb>" ;;
      recipe)
        if [ "$2" = manifest ]; then echo "{\"digest\":\"sha256:$(find "$3" -type f | LC_ALL=C sort | xargs cat | cksum | tr -d ' ')\"}"
        else
          echo "eval-call" >> "$FAKE_CALLS"
          if [ "${EVAL_FAIL:-}" = 1 ]; then echo "workflow.a@1: happy fail"; exit 1; fi
          list="" i=0   # no seq: BSD seq differs from GNU (-s, count-down on an empty range)
          while [ "$i" -lt $((FAKE_RECIPES + ${EVAL_COUNT_DELTA:-0})) ]; do i=$((i + 1)); list="${list:+$list,}$i"; done
          echo "{\"recipes\":[$list]}"
        fi ;;
      agents) if [ -n "${FAKE_OPEN_RUNS:-}" ]; then echo '{"sessions":[{"ref":"claude/abc","state":"running","launch":{"instance":"ITM-1/line/3/scoping.1","state":"challenge","attempt":1}}]}'; else echo '{"sessions":[]}'; fi ;;
      profile) echo "${FAKE_PROFILE_OUT:-}"; exit "${FAKE_PROFILE_RC:-0}" ;;
      env) echo "judge=$AW_SINDRI_JUDGE packets=$AW_PACKETS_BIN evidence=$AW_UI_EVIDENCE_BIN dir=$(cd "$(dirname "$FAKE_SELF")" && pwd -P) droot=$DOTNET_ROOT" ;;
    esac ;;
  judge) echo "judge health ok" ;;
  ui-evidence)
    if [ "${1:-}" = version ]; then echo "ui-evidence ${FAKE_BAD_VERSION:+bad}${FAKE_BAD_VERSION:-$FAKE_REV}"
    elif [ "${1:-}" = install-browser ]; then echo "browser" >> "$FAKE_CALLS"; fi ;;
  *) echo "usage: $FAKE_NAME"; exit 2 ;;
esac
FB

# The fake dotnet: --list-sdks names the pinned SDK; `publish` writes the binary its project names, and logs the call.
cat > "$WORK/fakebin/dotnet" <<'FD'
#!/usr/bin/env bash
case "${1:-}" in
  --list-sdks) echo "$FAKE_SDK [$DOTNET_ROOT/sdk]"; exit 0 ;;
  publish) ;;
  *) exit 0 ;;
esac
proj="$2"; shift 2
out="" rev="" recipes=""
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift ;;
    -p:SourceRevisionId=*) rev="${1#*=}" ;;
    -p:AwRecipesDir=*) recipes="${1#*=}" ;;
  esac
  shift
done
case "$proj" in
  *Aw.Sindri.Host*) name=sindri ;; *Ui.Desktop*) name=sindri-ui ;; *Aw.Judge.Host*) name=judge ;;
  *Aw.Packets.App*) name=aw-packets ;; *Aw.BugFix.Host*) name=bugfix-state ;; *Aw.Evidence.Host*) name=ui-evidence ;;
  *Aw.Readiness.App*) name=aw-readiness ;; *) exit 3 ;;
esac
echo "$name rev=$rev" >> "$FAKE_LOG"
if [ "$name" = sindri ]; then   # what the build copy's recipes are, for the staging test
  { echo "recipes=$(cd "$recipes" && find . -type f | LC_ALL=C sort | tr '\n' ' ')"; echo "recipe_one=$(cat "$recipes/one.yaml")"; } > "$FAKE_STAGED"
fi
[ "${FAKE_FAIL:-}" != "$name" ] || { echo "boom publishing $name" >&2; exit 1; }
mkdir -p "$out"
n=0; [ -z "$recipes" ] || n="$(find "$recipes" -name '*.yaml' | wc -l | tr -d ' ')"
printf '#!/usr/bin/env bash\nFAKE_SELF="$0" FAKE_NAME=%s FAKE_REV=%s FAKE_RECIPES=%s FAKE_CALLS="%s" FAKE_BAD_VERSION="%s" exec bash "%s" "$@"\n' "$name" "$rev" "$n" "$FAKE_CALLS" "${FAKE_BAD_VERSION:-}" "$FAKE_BIN" > "$out/$name"
chmod +x "$out/$name"
[ "$name" != judge ] || : > "$out/libe_sqlite3.so"
FD
chmod +x "$WORK/fakebin/dotnet"
export PATH="$WORK/fakebin:$PATH"

# A miniature checkout: the script, its inputs list, a pinned SDK, some sources and two shipped recipes.
REPO="$WORK/repo"
mkdir -p "$REPO/scripts/lib" "$REPO/src/Sindri/Aw.Sindri.Host" "$REPO/src/Judge" "$REPO/recipes/sindri"
cp "$DIR/install-cs.sh" "$REPO/scripts/"
cp "$DIR/lib/sindri-build-inputs.sh" "$DIR/lib/sindri-open-runs.sh" "$REPO/scripts/lib/"
printf '{"sdk":{"version":"%s"}}\n' "$FAKE_SDK" > "$REPO/global.json"
echo a > "$REPO/src/Sindri/Aw.Sindri.Host/a.cs"; echo b > "$REPO/src/Judge/b.cs"
echo "name: one" > "$REPO/recipes/sindri/one.yaml"; echo "name: two" > "$REPO/recipes/sindri/two.yaml"
g() { git -C "$REPO" -c user.name=t -c user.email=t@example.invalid "$@"; }
g init -q -b main && g add -A && g commit -q -m base
INSTALL="$REPO/scripts/install-cs.sh"
B="$AW_STATE_DIR/bin/cs"
stamps() { find "$B" -mindepth 1 -maxdepth 1 -type d ! -name '.*' -exec basename {} \; | sort | tr '\n' ' '; }
# change a source file (uncommitted: the stamp changes, the tree is "-dirty")
bump() { echo "$1" >> "$REPO/src/Judge/b.cs"; }
# give the named builds distinct old mtimes, oldest first, so "newest two" is deterministic
age() { local m=1 d; for d in "$@"; do touch -t "20200101$(printf '%02d' "$m")00" "$B/$d"; m=$((m + 1)); done; }
others() { stamps | tr ' ' '\n' | grep . | grep -vxF -f <(printf '%s\n' "$@") || true; }

# --- usage and dry run ---
bash "$INSTALL" --bogus > /dev/null 2>&1; check "an unknown flag is exit 2" "$?" "2"
bash "$INSTALL" --publish --link > /dev/null 2>&1; check "--publish with --link is exit 2" "$?" "2"
out="$(bash "$INSTALL" --dry-run 2>&1)"; rc=$?
check "dry run exits 0" "$rc" "0"
has "dry run says what it would do" "$out" "install-cs: [dry-run] would"
check "dry run writes no state" "$([ -e "$AW_STATE_DIR" ] && echo yes || echo no)" "no"
check "dry run publishes nothing" "$(wc -l < "$FAKE_LOG" | tr -d ' ')" "0"

# --- link before any build ---
out="$(bash "$INSTALL" --link 2>&1)"; rc=$?
check "--link without a build fails" "$rc" "1"
has "--link names the fix" "$out" "run scripts/install-cs.sh --publish first"
check "--link without a build writes no wrapper" "$(ls "$CLAUDE_LOCAL_BIN" | wc -l | tr -d ' ')" "0"

# --- publish ---
out="$(bash "$INSTALL" --publish 2>&1)"; rc=$?
check "--publish exits 0" "$rc" "0"
S1="$(stamps | tr -d ' ')"
check "one stamped build" "$(printf '%s' "$S1" | wc -c | tr -d ' ')" "16"
for f in sindri sindri-ui judge aw-packets bugfix-state ui-evidence aw-readiness libe_sqlite3.so build-info; do
  check "build has $f" "$([ -e "$B/$S1/$f" ] && echo yes || echo no)" "yes"
done
check "seven publishes, each stamped with the commit" "$(grep -c "rev=$(g rev-parse HEAD)$" "$FAKE_LOG")" "7"
check "--publish leaves current alone" "$([ -e "$B/current" ] && echo yes || echo no)" "no"
check "--publish writes no wrapper" "$(ls "$CLAUDE_LOCAL_BIN" | wc -l | tr -d ' ')" "0"
has "the recipe evals ran" "$(cat "$FAKE_CALLS")" "sindri state="
has "the verify step ran judge health" "$(cat "$FAKE_CALLS")" "args=health"
check "every verify call used a scratch AW_STATE_DIR" "$(grep -c "state=$AW_STATE_DIR/cache/install-cs/verify\." "$FAKE_CALLS")" "$(grep -c 'args=\(health\|version\|help\|$\)' "$FAKE_CALLS")"
check "the real state dir was never a verify state" "$(grep -c "state=$AW_STATE_DIR " "$FAKE_CALLS")" "0"
check "no stage or lock left behind" "$(find "$B" -mindepth 1 -maxdepth 1 -name '.*' | wc -l | tr -d ' ')" "0"
: > "$FAKE_LOG"
out="$(bash "$INSTALL" --publish 2>&1)"; rc=$?
check "a second --publish is current" "$rc" "0"
has "says current" "$out" "build $S1 is current"
check "a second --publish publishes nothing" "$(wc -l < "$FAKE_LOG" | tr -d ' ')" "0"

# --- link ---
printf '#!/bin/sh\necho ts-wrapper\n' > "$CLAUDE_LOCAL_BIN/judge"; chmod +x "$CLAUDE_LOCAL_BIN/judge"
out="$(bash "$INSTALL" --link 2>&1)"; rc=$?
check "--link exits 0" "$rc" "0"
check "current is a symlink to the stamp" "$(readlink "$B/current")" "$S1"
check "the six wrappers, not sindri-ui" "$(ls "$CLAUDE_LOCAL_BIN" | tr '\n' ' ')" "aw-packets aw-readiness bugfix-state judge sindri ui-evidence "
check "the TS judge wrapper was replaced" "$(grep -c ts-wrapper "$CLAUDE_LOCAL_BIN/judge")" "0"
check "no temp file left in the bin dir" "$(ls -A "$CLAUDE_LOCAL_BIN" | grep -c '^\.')" "0"
envline="$(cd / && AW_STATE_DIR="$WORK/other" "$CLAUDE_LOCAL_BIN/sindri" env)"
RES="$(cd "$B/$S1" && pwd -P)"
has "sindri wrapper exports AW_SINDRI_JUDGE" "$envline" "judge=$RES/judge"
has "sindri wrapper exports AW_PACKETS_BIN" "$envline" "packets=$RES/aw-packets"
has "sindri wrapper exports AW_UI_EVIDENCE_BIN" "$envline" "evidence=$RES/ui-evidence"
has "sindri wrapper execs the real binary by its resolved path" "$envline" "dir=$RES "
has "sindri wrapper exports DOTNET_ROOT" "$envline" "droot=$DOTNET_ROOT"
envline="$(AW_PACKETS_BIN=/custom/packets "$CLAUDE_LOCAL_BIN/sindri" env)"
has "a caller's AW_PACKETS_BIN wins" "$envline" "packets=/custom/packets "
check "the ui-evidence wrapper runs the build" "$("$CLAUDE_LOCAL_BIN/ui-evidence" version | cut -d' ' -f1)" "ui-evidence"
"$CLAUDE_LOCAL_BIN/aw-packets" > /dev/null 2>&1; check "a wrapper passes the exit code through" "$?" "2"
check "--link skipped the browser download" "$(grep -c '^browser$' "$FAKE_CALLS")" "0"
AW_CS_SKIP_DOWNLOADS=0 bash "$INSTALL" --link > /dev/null 2>&1; check "--link with downloads on still exits 0" "$?" "0"
check "--link ran ui-evidence install-browser" "$(grep -c '^browser$' "$FAKE_CALLS")" "1"
mv "$B/current" "$B/current.away"
out="$("$CLAUDE_LOCAL_BIN/judge" 2>&1)"; rc=$?
check "a wrapper with no linked build fails" "$rc" "1"
has "and says how to fix it" "$out" "scripts/install-cs.sh"
mv "$B/current.away" "$B/current"

# --- a source change: the stamp moves, --link refuses until published ---
bump one
bash "$INSTALL" --link > /dev/null 2>&1; check "--link after a source change needs a publish" "$?" "1"
check "current unchanged" "$(readlink "$B/current")" "$S1"

# --- failures change nothing ---
: > "$FAKE_LOG"
out="$(FAKE_FAIL=judge bash "$INSTALL" --publish 2>&1)"; rc=$?
check "a failed publish exits 1" "$rc" "1"
has "names the binary" "$out" "publishing judge failed"
check "no new build" "$(stamps | tr -d ' ')" "$S1"
check "no stage left" "$(find "$B" -mindepth 1 -maxdepth 1 -name '.*' | wc -l | tr -d ' ')" "0"
check "current unchanged after a failed publish" "$(readlink "$B/current")" "$S1"
out="$(FAKE_BAD_VERSION=nothing bash "$INSTALL" --publish 2>&1)"; rc=$?
check "a ui-evidence that does not name the commit fails" "$rc" "1"
has "install didn't take" "$out" "install didn't take"
check "still no new build" "$(stamps | tr -d ' ')" "$S1"

# --- prune: newest two, current, and the build a running serve executes ---
out="$(bash "$INSTALL" --publish 2>&1)"; check "publish of the changed source" "$?" "0"
has "unchanged recipes skip the evals" "$out" "evals skipped"
S2="$(others "$S1")"
age "$S1" "$S2"; bump two; bash "$INSTALL" --publish > /dev/null 2>&1
S3="$(others "$S1" "$S2")"
check "three builds: newest two plus current" "$(stamps | wc -w | tr -d ' ')" "3"
age "$S1" "$S2" "$S3"
mkdir -p "$AW_SINDRI_HOME"
bash -c 'sleep 60; :' "$B/$S2/sindri serve" > /dev/null 2>&1 &
serve_pid=$!
printf '{"pid":%s}\n' "$serve_pid" > "$AW_SINDRI_HOME/control.json"
bump three; bash "$INSTALL" --publish > /dev/null 2>&1
S4="$(others "$S1" "$S2" "$S3")"
check "S4 is a new build" "$([ -n "$S4" ] && echo yes || echo no)" "yes"
check "S4 and S3 are the newest two, S1 is current, S2 runs: four builds" "$(stamps | wc -w | tr -d ' ')" "4"
age "$S1" "$S2" "$S3" "$S4"
bump four; bash "$INSTALL" --publish > /dev/null 2>&1
S5="$(others "$S1" "$S2" "$S3" "$S4")"
check "S2 survives while its serve runs" "$([ -d "$B/$S2" ] && echo yes || echo no)" "yes"
check "S1 survives as current" "$([ -d "$B/$S1" ] && echo yes || echo no)" "yes"
check "S3 (neither newest two, current nor running) was pruned" "$([ -d "$B/$S3" ] && echo yes || echo no)" "no"
check "S4 and S5 are kept" "$([ -d "$B/$S4" ] && [ -d "$B/$S5" ] && echo yes || echo no)" "yes"

# --- --rebuild of the build a running serve executes ---
kill "$serve_pid" 2> /dev/null; serve_pid=""
bash -c 'sleep 60; :' "$B/$S5/sindri serve" > /dev/null 2>&1 &
serve_pid=$!
printf '{"pid":%s}\n' "$serve_pid" > "$AW_SINDRI_HOME/control.json"
before="$(cat "$B/$S5/build-info")"; : > "$FAKE_LOG"
out="$(bash "$INSTALL" --publish --rebuild 2>&1)"; rc=$?
check "--rebuild of the running build exits 0" "$rc" "0"
has "tells the owner to stop sindri" "$out" "run sindri stop, then re-run"
check "and publishes nothing" "$(wc -l < "$FAKE_LOG" | tr -d ' ')" "0"
check "the running build is untouched" "$(cat "$B/$S5/build-info")" "$before"
# --- open agent runs under a running serve (#1000): no terminal stops the publish, AW_SINDRI_STOP_OK=1 goes on ---
bump open-runs; : > "$FAKE_LOG"
out="$(FAKE_OPEN_RUNS=1 bash "$INSTALL" --publish 2>&1 < /dev/null)"; rc=$?
check "open agent runs with no terminal stop the publish" "$rc" "1"
has "lists the open run" "$out" "claude/abc  ITM-1/line/3/scoping.1 · challenge  running"
has "names the override" "$out" "AW_SINDRI_STOP_OK=1"
check "and publishes nothing" "$(wc -l < "$FAKE_LOG" | tr -d ' ')" "0"
out="$(FAKE_OPEN_RUNS=1 AW_SINDRI_STOP_OK=1 bash "$INSTALL" --publish 2>&1 < /dev/null)"; rc=$?
check "AW_SINDRI_STOP_OK=1 publishes anyway" "$rc" "0"
has "and says so" "$out" "AW_SINDRI_STOP_OK=1: going on"
: > "$FAKE_LOG"
kill "$serve_pid" 2> /dev/null; serve_pid=""
sleep 0.3
bash "$INSTALL" --publish --rebuild > /dev/null 2>&1; check "--rebuild with no serve exits 0" "$?" "0"
check "and publishes the seven binaries again" "$(wc -l < "$FAKE_LOG" | tr -d ' ')" "7"

# --- the shipped recipes: staged from HEAD, and the eval gate ---
ccommit() { g commit -q "$@" > /dev/null 2>&1; }
echo "name: one-edited" > "$REPO/recipes/sindri/one.yaml"; echo "name: x" > "$REPO/recipes/sindri/x.yaml"
out="$(bash "$INSTALL" --publish --rebuild 2>&1)"; rc=$?
check "staging: the rebuild exits 0" "$rc" "0"
check "staging: the build copy holds HEAD's recipe files only" "$(grep '^recipes=' "$FAKE_STAGED")" "recipes=./one.yaml ./two.yaml "
check "staging: the edited recipe is HEAD's content" "$(grep '^recipe_one=' "$FAKE_STAGED")" "recipe_one=name: one"
check "staging: the working tree is untouched" "$(cat "$REPO/recipes/sindri/one.yaml"):$([ -f "$REPO/recipes/sindri/x.yaml" ] && echo kept)" "name: one-edited:kept"
g checkout -q -- recipes/sindri/one.yaml; rm -f "$REPO/recipes/sindri/x.yaml"

cur_before="$(readlink "$B/current")"; DG="$B/recipes-digest"; D_BEFORE="$(cat "$DG")"
echo "name: one-v3" > "$REPO/recipes/sindri/one.yaml"; g add -A; ccommit -m "recipe 3"
: > "$FAKE_CALLS"
out="$(EVAL_FAIL=1 bash "$INSTALL" --publish 2>&1)"; rc=$?
check "eval: a failing eval fails the publish" "$rc" "1"
has "eval: the sentence" "$out" "recipe evals failed"
has "eval: the eval's own output" "$out" "workflow.a@1: happy fail"
check "eval: the digest is as it was, no stage or lock left" "$([ "$(cat "$DG")" = "$D_BEFORE" ] && echo same):$(find "$B" -mindepth 1 -maxdepth 1 -name '.*' | wc -l | tr -d ' ')" "same:0"
check "eval: current unchanged" "$(readlink "$B/current")" "$cur_before"

: > "$FAKE_CALLS"
bash "$INSTALL" --publish > /dev/null 2>&1; rc=$?
check "eval: the same source publishes when the evals pass, running them once" "$rc:$(grep -c '^eval-call$' "$FAKE_CALLS")" "0:1"
check "eval: the new digest is recorded" "$([ "$(cat "$DG")" != "$D_BEFORE" ] && echo recorded)" "recorded"

ccommit --allow-empty -m "empty"
: > "$FAKE_CALLS"
EVAL_FAIL=1 bash "$INSTALL" --publish > /dev/null 2>&1; rc=$?
check "eval: an unchanged digest skips the evals, the build still publishes" "$rc:$(grep -c '^eval-call$' "$FAKE_CALLS")" "0:0"

echo "name: three" > "$REPO/recipes/sindri/three.yaml"; g add -A; ccommit -m "recipe three"
D_BEFORE="$(cat "$DG")"
out="$(EVAL_COUNT_DELTA=1 bash "$INSTALL" --publish 2>&1)"; rc=$?
check "eval: an embedded count that differs from the staged yaml count fails" "$rc" "1"
has "eval: names both counts" "$out" "embeds 4 recipes but recipes/sindri has 3"
check "eval: a count failure records no digest" "$([ "$(cat "$DG")" = "$D_BEFORE" ] && echo same)" "same"

# A profile approval this build can't read (#1143) or that isn't there: --link says the one command and still exits 0.
bash "$INSTALL" --publish > /dev/null 2>&1   # the count failure above left this source unbuilt; --link needs a build to link
out="$(FAKE_PROFILE_RC=2 FAKE_PROFILE_OUT="SND-PROFILE-005 x/profile.json doesn't match its approval hash" bash "$INSTALL" --link 2>&1)"; rc=$?
check "an unreadable approval: --link still exits 0" "$rc" "0"
has "an unreadable approval: the hint names the command" "$out" "run \`sindri profile approve\` to see the live hash"
out="$(FAKE_PROFILE_RC=1 FAKE_PROFILE_OUT="valid, not approved" bash "$INSTALL" --link 2>&1)"; rc=$?
has "an unapproved profile: the hint" "$out" "the profile isn't approved by this build; run \`sindri profile approve\`"
out="$(bash "$INSTALL" --link 2>&1)"
check "an approved profile: no hint" "$(printf '%s' "$out" | grep -c 'profile approve')" "0"

# A failed `git archive` fails the publish: nothing is published and no empty recipes set is embedded.
mkdir -p "$WORK/gitfail"
printf '#!/usr/bin/env bash\nfor a in "$@"; do [ "$a" = archive ] && { echo "fatal: archive boom" >&2; exit 128; }; done\nexec "%s" "$@"\n' "$(command -v git)" > "$WORK/gitfail/git"
chmod +x "$WORK/gitfail/git"
: > "$FAKE_LOG"
out="$(PATH="$WORK/gitfail:$PATH" bash "$INSTALL" --publish --rebuild 2>&1)"; rc=$?
check "archive failure: exit 1" "$rc" "1"
has "archive failure: the sentence" "$out" "could not stage recipes/sindri from HEAD (git archive failed)"
check "archive failure: nothing is published" "$(wc -l < "$FAKE_LOG" | tr -d ' ')" "0"

# A tree that is not a git checkout cannot be published.
NG="$WORK/nogit"; mkdir -p "$NG/scripts/lib" "$NG/src/Sindri/Aw.Sindri.Host"
cp "$DIR/install-cs.sh" "$NG/scripts/"; cp "$DIR/lib/sindri-build-inputs.sh" "$DIR/lib/sindri-open-runs.sh" "$NG/scripts/lib/"; cp "$REPO/global.json" "$NG/"
out="$(cd "$WORK" && GIT_CEILING_DIRECTORIES="$WORK" bash "$NG/scripts/install-cs.sh" --publish 2>&1)"; rc=$?
check "not a git checkout: exit 1" "$rc" "1"
has "not a git checkout: the sentence" "$out" "can only be published from a git checkout"

[ "$fail" = 0 ] && echo "all install-cs tests passed" || { echo "install-cs tests FAILED"; exit 1; }
