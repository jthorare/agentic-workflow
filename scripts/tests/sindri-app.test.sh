#!/usr/bin/env bash
# Tests for scripts/sindri-app.sh on a sandboxed HOME and state dir, against a throwaway git repo holding a copy of the
# script, a stub UI publish (src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh), a stub scripts/binaries-lock.sh and a fake
# `dotnet` on PATH (its publish writes a stub `sindri` that prints its arguments), so nothing is built or fetched:
# the first run publishes both binaries into one build dir, links sindri-app, fetches temporal and runs sindri with the
# arguments; an unchanged tree reuses the build; an edit (committed or not) rebuilds; --rebuild forces one; two builds
# are kept; a failed publish leaves the last good build current; a foreign sindri-app on PATH is left alone, a dangling
# one replaced, AW_SINDRI_APP_NO_LINK=1 links nothing; stdout carries only sindri's output. At the end: --publish-only
# publishes and never execs or hints; the shipped recipes staged into the build copy are HEAD's (git archive), never the
# working tree's; the stamp carries the commit and the recipes tree hash; a dirty tree says so; a non-git tree is refused.
# Run: bash scripts/tests/sindri-app.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$WORK"' EXIT
unset AW_SINDRI_APP_NO_LINK AW_SINDRI_HOME CLAUDE_LOCAL_BIN DOTNET_ROOT AW_SINDRI_MCP_REPO
export AW_SINDRI_APP_NO_MCP=1   # the MCP registration has its own tests at the end
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state"
mkdir -p "$HOME"
CALLS="$WORK/calls"; : > "$CALLS"
export CALLS

# The stub sindri: prints its arguments, and answers the publish's recipe steps: `recipe manifest <dir> --json` (the digest is a
# checksum of the directory's files) and `recipe eval --shipped --json` (EVAL_FAIL=1 fails it; EVAL_COUNT_DELTA shifts the embedded count).
cat > "$WORK/stub-sindri" <<'EOF'
#!/usr/bin/env bash
if [ "$1 $2" = "recipe manifest" ]; then
  echo "manifest-call" >> "$CALLS"
  echo "{\"recipes\":[],\"invalid\":[],\"digest\":\"sha256:$(find "$3" -type f | LC_ALL=C sort | xargs cat | cksum | tr -d ' ')\"}"
  exit 0
fi
if [ "$1 $2" = "recipe eval" ]; then
  echo "eval-call" >> "$CALLS"
  c=$(( $(cat "$(dirname "$0")/embedded-count") + ${EVAL_COUNT_DELTA:-0} )); recs=""; i=0
  while [ "$i" -lt "$c" ]; do recs="$recs{},"; i=$((i+1)); done
  echo "{\"passed\":$([ "${EVAL_FAIL:-0}" = 1 ] && echo false || echo true),\"recipes\":[${recs%,}]}"
  [ "${EVAL_FAIL:-0}" = 1 ] && { echo "workflow.a@1: happy fail" >&2; exit 1; }
  exit 0
fi
echo "sindri $*"
echo "root=$DOTNET_ROOT" >&2
echo "sindri $*" >> "$CALLS"
echo "judge=${AW_SINDRI_JUDGE:-}" >> "$CALLS"
case "$1" in mcp) echo "codex: registered" ; exit "${MCP_EXIT:-0}" ;; esac
EOF
export STUB_SINDRI="$WORK/stub-sindri"

# The fake dotnet: the pinned SDK is installed; publish -o DIR writes the sindri stub there (FAIL_PUBLISH=1 fails it).
mkdir -p "$WORK/fakebin"
cat > "$WORK/fakebin/dotnet" <<'EOF'
#!/usr/bin/env bash
case "$1" in
  --list-sdks) echo "10.0.401 [/fake/dotnet/sdk]" ;;
  publish)
    case "$*" in *Aw.Judge.Host*)
      echo "publish-judge" >> "$CALLS"
      echo "judgeargs=$*" >> "$CALLS"
      [ "${FAIL_JUDGE:-0}" = 1 ] && { echo "error CS0002: judge boom"; exit 1; }
      out=""; while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
      mkdir -p "$out"; printf '#!/bin/sh\n' > "$out/judge"; chmod +x "$out/judge"; : > "$out/libe_sqlite3.so"
      exit 0 ;;
    esac
    echo "publish" >> "$CALLS"
    echo "cwd=$(pwd -P)" >> "$CALLS"
    [ -d recipes/sindri ] && echo "recipes=$(cd recipes/sindri && find . -type f | LC_ALL=C sort | tr '\n' ' ')" >> "$CALLS"
    [ -f recipes/sindri/a.yaml ] && echo "recipe_a=$(cat recipes/sindri/a.yaml)" >> "$CALLS"
    for f in config/launcher/builtins.json BannedSymbols.All.txt BannedSymbols.NonHost.txt; do
      [ -f "$f" ] && echo "has $f" >> "$CALLS"
    done
    [ "${FAIL_PUBLISH:-0}" = 1 ] && { echo "error CS0001: boom"; exit 1; }
    rd=""; for a in "$@"; do case "$a" in -p:AwRecipesDir=*) rd="${a#-p:AwRecipesDir=}" ;; esac; done
    out=""; while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
    mkdir -p "$out"
    # The embedded count is the *.yaml files of the recipes dir the build was told to embed (-p:AwRecipesDir).
    n=0; [ -n "$rd" ] && [ -d "$rd" ] && n="$(find "$rd" -maxdepth 1 -name '*.yaml' | wc -l | tr -d ' ')"
    echo "$n" > "$out/embedded-count"
    cp "$STUB_SINDRI" "$out/sindri"; chmod +x "$out/sindri" ;;
esac
EOF
chmod +x "$WORK/fakebin/dotnet"
export PATH="$WORK/fakebin:$PATH"

# The throwaway repo.
R="$WORK/repo"
mkdir -p "$R/scripts" "$R/src/Sindri/Aw.Sindri.Host" "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke"
mkdir -p "$R/scripts/lib" "$R/config/launcher"
cp "$DIR/sindri-app.sh" "$R/scripts/"
cp "$DIR/lib/sindri-build-inputs.sh" "$R/scripts/lib/"
echo '{}' > "$R/config/launcher/builtins.json"
echo 'T:System.X' > "$R/BannedSymbols.All.txt"
echo 'T:System.Y' > "$R/BannedSymbols.NonHost.txt"
printf '{\n  "sdk": {\n    "version": "10.0.401"\n  }\n}\n' > "$R/global.json"
echo '<Project />' > "$R/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj"
printf '#!/usr/bin/env bash\necho ui >> "$CALLS"\necho "uicwd=$(pwd -P)" >> "$CALLS"\nprintf "#!/bin/sh\\n" > "$1/sindri-ui"; chmod +x "$1/sindri-ui"\n' \
  > "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh"
printf '#!/usr/bin/env bash\necho "fetch $2" >> "$CALLS"\n' > "$R/scripts/binaries-lock.sh"
chmod +x "$R/scripts/binaries-lock.sh" "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh"
git -C "$R" init -q && git -C "$R" add -A && git -C "$R" -c user.email=t@t -c user.name=t commit -qm init

B="$AW_STATE_DIR/bin/sindri-app"
C="$AW_STATE_DIR/cache/sindri-app-build"
L="$HOME/.local/bin/sindri-app"
builds() { find "$B" -mindepth 1 -maxdepth 1 -type d ! -name '.*' | wc -l | tr -d ' '; }
publishes() { grep -c '^publish$' "$CALLS"; }

OUT="$(cd "$WORK" && bash "$R/scripts/sindri-app.sh" status --json 2>"$WORK/err")"; RC=$?
check "first run: exit is sindri's" "$RC" "0"
check "first run: stdout is only sindri's, with the arguments" "$OUT" "sindri status --json"
check "first run: one host publish and one UI publish" "$(publishes):$(grep -c '^ui$' "$CALLS")" "1:1"
check "first run: temporal fetched" "$(grep -c '^fetch temporal$' "$CALLS")" "1"
S1="$(cat "$B/current")"
check "B1: a successful publish leaves no tree.* copy behind" "$(find "$C" -mindepth 1 -maxdepth 1 -name 'tree.*' | wc -l | tr -d ' ')" "0"
check "I1: the host publish runs inside the build copy, the UI publish inside the checkout, whatever the caller's cwd" \
  "$(grep '^cwd=' "$CALLS" | sed 's|.*/||; s|\..*||'):$(grep '^uicwd=' "$CALLS" | sed 's|.*=||')" "tree:$R"
check "B3: the files outside src/ the build reads are copied" \
  "$(grep '^has ' "$CALLS" | sort | tr '\n' ' ')" "has BannedSymbols.All.txt has BannedSymbols.NonHost.txt has config/launcher/builtins.json "
. "$DIR/lib/test-grep.sh"
check "B3: sindri-app.sh and the aot-smoke share one file list" \
  "$(has 'sindri-build-inputs.sh' "$DIR/sindri-app.sh" && has 'sindri-build-inputs.sh' "$DIR/../src/Sindri/aot-smoke/run.sh" && has 'SINDRI_BUILD_PATHSPECS' "$DIR/lib/sindri-build-inputs.sh" && echo shared)" "shared"
check "B3: that list names config/launcher and BannedSymbols*.txt" \
  "$(has -F config/launcher "$DIR/lib/sindri-build-inputs.sh" && has -F "'BannedSymbols*.txt'" "$DIR/lib/sindri-build-inputs.sh" && echo yes)" "yes"
check "first run: both binaries in the current build" \
  "$(test -x "$B/$S1/sindri" && test -x "$B/$S1/sindri-ui" && echo yes)" "yes"
check "first run: the C# judge is published NativeAOT for the platform, once" \
  "$(grep -c '^publish-judge$' "$CALLS"):$(grep '^judgeargs=' "$CALLS" | grep -c -- ' -r linux-x64 \| -r osx-arm64 \| -r osx-x64 \| -r linux-arm64 ')" "1:1"
check "first run: judge and its e_sqlite3 are in the build, next to sindri" \
  "$(test -x "$B/$S1/judge" && test -f "$B/$S1/libe_sqlite3.so" && echo yes)" "yes"
check "first run: sindri runs with AW_SINDRI_JUDGE set to the build's judge" "$(grep '^judge=' "$CALLS" | tail -1)" "judge=$B/$S1/judge"
OUT="$(AW_SINDRI_JUDGE=/custom/judge bash "$R/scripts/sindri-app.sh" status 2>/dev/null)"
check "a caller's AW_SINDRI_JUDGE is kept" "$(grep '^judge=' "$CALLS" | tail -1)" "judge=/custom/judge"
check "first run: DOTNET_ROOT derives from the SDK found (not ~/.dotnet)" "$(grep -c "^root=/fake/dotnet$" "$WORK/err")" "1"
check "first run: sindri-app linked to the script" "$(readlink "$L")" "$R/scripts/sindri-app.sh"

OUT="$(bash "$L" 2>/dev/null)"
check "unchanged tree, through the link: no publish, bare sindri" "$(publishes):$OUT" "1:sindri "

echo '// edit' > "$R/src/Sindri/Aw.Sindri.Host/New.cs"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
S2="$(cat "$B/current")"
check "an uncommitted file rebuilds" "$(publishes):$([ "$S1" != "$S2" ] && echo new)" "2:new"
echo '// edit 2' >> "$R/src/Sindri/Aw.Sindri.Host/New.cs"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "the newest two builds are kept" "$(publishes):$(builds):$(test -d "$B/$S1" || echo pruned)" "3:2:pruned"

bash "$R/scripts/sindri-app.sh" --rebuild version >/dev/null 2>&1
check "--rebuild publishes the same stamp again" "$(publishes):$(builds)" "4:2"

S3="$(cat "$B/current")"
echo '// edit 3' >> "$R/src/Sindri/Aw.Sindri.Host/New.cs"
FAIL_PUBLISH=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>"$WORK/err"; RC=$?
check "a failed publish exits 1 with the log's tail" "$RC:$(grep -c 'CS0001' "$WORK/err")" "1:1"
check "a failed publish leaves the last good build current" "$(cat "$B/current"):$(test -d "$B/.lock" || echo unlocked)" "$S3:unlocked"
FAIL_JUDGE=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>"$WORK/err"; RC=$?
check "a failed judge publish fails the publish: exit 1, the tail, no new build, the last good build current" \
  "$RC:$(grep -c 'CS0002' "$WORK/err"):$(cat "$B/current")" "1:1:$S3"

rm -f "$L"; echo '#!/bin/sh' > "$L"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>"$WORK/err"
check "a foreign sindri-app is left alone, with a note" "$(test -L "$L" || echo file):$(grep -c 'already something else' "$WORK/err")" "file:1"
rm -f "$L"; ln -s "$WORK/gone/sindri-app.sh" "$L"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "a dangling sindri-app is replaced" "$(readlink "$L")" "$R/scripts/sindri-app.sh"
rm -f "$L"
AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "AW_SINDRI_APP_NO_LINK=1 links nothing" "$(test -e "$L" || test -L "$L" || echo none)" "none"

# B2: with the box-wide heavy wrapper present, both publishes go through `heavy --expensive`.
mkdir -p "$AW_STATE_DIR/bin-r1b"
printf '#!/usr/bin/env bash\necho "heavy $1" >> "$CALLS"\nshift\nexec "$@"\n' > "$AW_STATE_DIR/bin-r1b/heavy"
chmod +x "$AW_STATE_DIR/bin-r1b/heavy"
echo '// heavy' >> "$R/src/Sindri/Aw.Sindri.Host/New.cs"
: > "$CALLS"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "B2: the host, UI and judge publishes all run under heavy --expensive" "$(grep -c '^heavy --expensive$' "$CALLS")" "3"
rm -f "$AW_STATE_DIR/bin-r1b/heavy"

# I3: a build for this stamp is not published again, even when it is not the current one; a build the running control
# plane executes is neither pruned nor rebuilt.
NEWCS="$R/src/Sindri/Aw.Sindri.Host/New.cs"
SC="$(cat "$B/current")"
cp "$NEWCS" "$WORK/new.cs.orig"
echo '// other' >> "$NEWCS"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1   # a new stamp: SC becomes previous
SN="$(cat "$B/current")"
cp "$WORK/new.cs.orig" "$NEWCS"
P1="$(publishes)"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1   # the earlier source again: SC's build still exists
check "I3: an existing build for the stamp is made current without a publish" \
  "$(publishes):$(cat "$B/current")" "$P1:$SC"
check "I3: the build it replaced is now previous, and two builds are kept" "$(cat "$B/previous"):$(builds)" "$SN:2"

# A running control plane executing SN (a process whose command line names that build); a third stamp would prune SN.
bash -c 'sleep 15; :' "$B/$SN/sindri" >/dev/null 2>&1 &
HOLD=$!
mkdir -p "$AW_STATE_DIR/sindri-app"
printf '{"pid": %s}\n' "$HOLD" > "$AW_STATE_DIR/sindri-app/control.json"
echo '// third' >> "$NEWCS"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "I3: a build the running control plane uses is not pruned" "$(test -d "$B/$SN" && echo kept)" "kept"
kill "$HOLD" 2>/dev/null; wait "$HOLD" 2>/dev/null

SX="$(cat "$B/current")"
bash -c 'sleep 15; :' "$B/$SX/sindri" >/dev/null 2>&1 &
HOLD=$!
printf '{"pid": %s}\n' "$HOLD" > "$AW_STATE_DIR/sindri-app/control.json"
PB="$(publishes)"
bash "$R/scripts/sindri-app.sh" --rebuild >/dev/null 2>"$WORK/err"
check "I3: --rebuild of the build in use does not publish or delete it" \
  "$(publishes):$(test -x "$B/$SX/sindri" && echo kept):$(grep -c 'not rebuilding' "$WORK/err")" "$PB:kept:1"
kill "$HOLD" 2>/dev/null; wait "$HOLD" 2>/dev/null
rm -f "$AW_STATE_DIR/sindri-app/control.json"

# A linked worktree creates no PATH link (its link would dangle once the worktree is removed).
rm -f "$L"
git -C "$R" -c user.email=t@t -c user.name=t add -A >/dev/null 2>&1
git -C "$R" -c user.email=t@t -c user.name=t commit -qm wip >/dev/null 2>&1
git -C "$R" worktree add -q "$WORK/wt" -b wt >/dev/null 2>&1
bash "$WORK/wt/scripts/sindri-app.sh" >/dev/null 2>&1
check "a linked worktree creates no sindri-app link" "$(test -e "$L" || test -L "$L" || echo none)" "none"
git -C "$R" worktree remove --force "$WORK/wt" >/dev/null 2>&1

# In use through a symlinked state dir: the plane's command line shows the resolved path.
mkdir -p "$WORK/realstate"; ln -s "$WORK/realstate" "$WORK/linkstate"
export AW_STATE_DIR="$WORK/linkstate"
AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
RB="$WORK/realstate/bin/sindri-app"
S_OLD="$(cat "$RB/current")"
bash -c 'sleep 15; :' "$RB/$S_OLD/sindri" >/dev/null 2>&1 &
HOLD=$!
mkdir -p "$WORK/realstate/sindri-app"
printf '{"pid": %s}\n' "$HOLD" > "$WORK/realstate/sindri-app/control.json"
for n in 1 2; do
  echo "// sym $n" >> "$NEWCS"
  AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
done
check "I3: the in-use build is kept when the state dir is a symlink" "$(test -d "$RB/$S_OLD" && echo kept)" "kept"
kill "$HOLD" 2>/dev/null; wait "$HOLD" 2>/dev/null

# MCP registration: `mcp register --apply` is the human's verb and sindri refuses it without a terminal (SND-AUTH-002), which a
# background watcher never has. So once the plane is up (a live control.json) a bare run or serve prints the exact command to run,
# with --repo from a linked worktree or AW_SINDRI_MCP_REPO, on stderr, and runs nothing; AW_SINDRI_APP_NO_MCP=1 skips it.
export AW_STATE_DIR="$WORK/mcpstate"
mkdir -p "$AW_STATE_DIR/sindri-app"
sleep 60 & PLANE=$!
printf '{"pid": %s, "url": "http://127.0.0.1:7480/"}\n' "$PLANE" > "$AW_STATE_DIR/sindri-app/control.json"
: > "$CALLS"
unset AW_SINDRI_APP_NO_MCP
waitfor() { local i; for i in $(seq 1 40); do grep -q "$1" "$2" && return 0; sleep 0.25; done; return 1; }
HINT='run: sindri-app mcp register --apply --providers all'
(cd "$WORK" && AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" >"$WORK/out" 2>"$WORK/err"); RC=$?
waitfor "mcp register" "$WORK/err"; sleep 0.5
check "mcp: a bare run prints the register command once the plane is up" "$(grep -c "$HINT\$" "$WORK/err")" "1"
check "mcp: it runs no registration itself (sindri would refuse it without a terminal)" "$(grep -c '^sindri mcp' "$CALLS")" "0"
check "mcp: the bare run's exit is unaffected" "$RC" "0"
check "mcp: the hint goes to stderr, never stdout" "$(grep -c 'mcp register' "$WORK/out")" "0"

(cd "$WORK" && AW_SINDRI_MCP_REPO=demo AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" serve >/dev/null 2>"$WORK/err")
waitfor "mcp register" "$WORK/err"; sleep 0.5
check "mcp: AW_SINDRI_MCP_REPO is in the command as --repo (serve prints it too)" "$(grep -c "$HINT --repo demo\$" "$WORK/err")" "1"

git -C "$R" worktree add -q "$WORK/wt2" -b wt2 >/dev/null 2>&1
(cd "$WORK/wt2" && AW_SINDRI_APP_NO_LINK=1 bash "$WORK/wt2/scripts/sindri-app.sh" >/dev/null 2>"$WORK/err")
waitfor "mcp register" "$WORK/err"; sleep 0.5
check "mcp: from a linked worktree the main checkout's name is the --repo" "$(grep -c "$HINT --repo repo\$" "$WORK/err")" "1"
git -C "$R" worktree remove --force "$WORK/wt2" >/dev/null 2>&1

(cd "$WORK" && AW_SINDRI_APP_NO_MCP=1 AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>"$WORK/err"); sleep 1
check "mcp: AW_SINDRI_APP_NO_MCP=1 prints nothing about it" "$(grep -c 'mcp register' "$WORK/err")" "0"

AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" status >/dev/null 2>"$WORK/err"; sleep 1
check "mcp: other commands print nothing about it" "$(grep -c 'mcp register' "$WORK/err")" "0"
check "mcp: and nothing ran a registration" "$(grep -c '^sindri mcp' "$CALLS")" "0"
kill "$PLANE" 2>/dev/null; wait "$PLANE" 2>/dev/null

# --- SO-A7a: --publish-only, and the recipes staged from HEAD ------------------------------------------------------
git -C "$R" -c user.email=t@t -c user.name=t add -A >/dev/null 2>&1
git -C "$R" -c user.email=t@t -c user.name=t commit -qm settle >/dev/null 2>&1
mkdir -p "$R/recipes/sindri/evals/a"
echo 'id: a' > "$R/recipes/sindri/a.yaml"
echo '# readme' > "$R/recipes/sindri/README.md"
echo '{}' > "$R/recipes/sindri/evals/a/happy.jsonl"
git -C "$R" -c user.email=t@t -c user.name=t add -A >/dev/null 2>&1
git -C "$R" -c user.email=t@t -c user.name=t commit -qm recipes >/dev/null 2>&1
export AW_SINDRI_APP_NO_LINK=1
export AW_STATE_DIR="$WORK/a7state"; B="$AW_STATE_DIR/bin/sindri-app"; C="$AW_STATE_DIR/cache/sindri-app-build"
unset AW_SINDRI_APP_NO_MCP

: > "$CALLS"
OUT="$(cd "$WORK" && bash "$R/scripts/sindri-app.sh" --publish-only 2>"$WORK/err")"; RC=$?
sleep 1
check "publish-only: exit 0, publishes, stdout empty" "$RC:$(publishes):$OUT" "0:1:"
check "publish-only: sindri is never run" "$(grep -c '^sindri ' "$CALLS")" "0"
check "publish-only: no register hint" "$(grep -c 'mcp register' "$WORK/err")" "0"
SP="$(cat "$B/current")"
check "publish-only: the build is current, with both binaries" "$(test -x "$B/$SP/sindri" && test -x "$B/$SP/sindri-ui" && echo yes)" "yes"
check "publish-only: the build dir records the commit and the recipes tree hash" \
  "$(grep -c "^commit=$(git -C "$R" rev-parse HEAD)\$" "$B/$SP/build-info"):$(grep -c "^recipes-tree=$(git -C "$R" rev-parse HEAD:recipes/sindri)\$" "$B/$SP/build-info")" "1:1"
check "publish-only: a clean tree is not called dirty" "$(grep -c 'uncommitted' "$WORK/err")" "0"

: > "$CALLS"
(cd "$WORK" && bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>&1)
check "publish-only: a current build is not published again" "$(publishes)" "0"
(cd "$WORK" && bash "$R/scripts/sindri-app.sh" --publish-only --rebuild >/dev/null 2>&1); RC=$?
check "publish-only --rebuild: publishes again, exit 0" "$RC:$(publishes)" "0:1"
bash "$R/scripts/sindri-app.sh" --publish-only status >/dev/null 2>"$WORK/err"; RC=$?
check "publish-only with another argument: exit 2" "$RC" "2"
FAIL_PUBLISH=1 bash "$R/scripts/sindri-app.sh" --publish-only --rebuild >/dev/null 2>"$WORK/err"; RC=$?
check "publish-only: a failed publish exits 1 with the sentence" "$RC:$(grep -c 'publishing sindri failed' "$WORK/err")" "1:1"

# The embedded set is HEAD's: an edit to a tracked recipe and an untracked one are in the working tree, not in the copy.
echo 'id: a-edited' > "$R/recipes/sindri/a.yaml"
echo 'id: x' > "$R/recipes/sindri/x.yaml"
: > "$CALLS"
bash "$R/scripts/sindri-app.sh" --publish-only --rebuild >/dev/null 2>"$WORK/err"; RC=$?
check "staging: the copy holds HEAD's recipe files only" "$RC:$(grep '^recipes=' "$CALLS")" "0:recipes=./README.md ./a.yaml ./evals/a/happy.jsonl "
check "staging: the edited recipe is HEAD's content" "$(grep '^recipe_a=' "$CALLS")" "recipe_a=id: a"
check "staging: a dirty tree says so" "$(grep -c 'uncommitted changes; the embedded recipes are HEAD' "$WORK/err")" "1"
check "staging: the working tree is untouched" "$(cat "$R/recipes/sindri/a.yaml"):$(test -f "$R/recipes/sindri/x.yaml" && echo kept)" "id: a-edited:kept"
git -C "$R" checkout -q -- recipes/sindri/a.yaml && rm -f "$R/recipes/sindri/x.yaml"

# The stamp: a recipe-only commit and an empty commit both republish.
bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>&1; S_A="$(cat "$B/current")"
echo 'id: a2' > "$R/recipes/sindri/a.yaml"
git -C "$R" -c user.email=t@t -c user.name=t commit -qam "recipe only" >/dev/null 2>&1
: > "$CALLS"; bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>&1; S_B="$(cat "$B/current")"
check "stamp: a recipe-only commit republishes" "$(publishes):$([ "$S_A" != "$S_B" ] && echo new)" "1:new"
git -C "$R" -c user.email=t@t -c user.name=t commit -q --allow-empty -m "empty" >/dev/null 2>&1
: > "$CALLS"; bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>&1; S_C="$(cat "$B/current")"
check "stamp: the commit is part of it" "$(publishes):$([ "$S_B" != "$S_C" ] && echo new)" "1:new"

# Without --publish-only the arguments still go to sindri, as before.
: > "$CALLS"
OUT="$(bash "$R/scripts/sindri-app.sh" status 2>/dev/null)"
check "no flag: arguments go to sindri" "$OUT" "sindri status"

# The eval step: after the publish, the published binary's `recipe eval --shipped` runs when the recipes manifest digest differs from
# the last successful publish's. A failing eval fails the publish (no new build, no digest recorded); an unchanged digest skips it;
# an embedded count that differs from the staged recipes/sindri/*.yaml count fails it.
DG="$B/recipes-digest"
ccommit() { git -C "$R" -c user.email=t@t -c user.name=t commit -q "$@" >/dev/null 2>&1; }
echo 'id: a3' > "$R/recipes/sindri/a.yaml"; ccommit -am "recipe 3"
BEFORE="$(cat "$B/current")"; D_BEFORE="$(cat "$DG" 2>/dev/null || true)"; : > "$CALLS"
EVAL_FAIL=1 bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>"$WORK/err"; RC=$?
check "eval: a failing eval fails the publish (exit 1, the sentence, the eval's output)" \
  "$RC:$(grep -c 'recipe evals failed' "$WORK/err"):$(grep -c 'workflow.a@1: happy fail' "$WORK/err")" "1:1:1"
check "eval: a failed eval leaves the last build current and the digest as it was" \
  "$([ "$(cat "$B/current")" = "$BEFORE" ] && echo same):$([ "$(cat "$DG" 2>/dev/null || true)" = "$D_BEFORE" ] && echo same):$(test -d "$B/.lock" || echo unlocked)" "same:same:unlocked"
check "eval: a failed eval leaves no build for the stamp" "$(builds)" "2"

: > "$CALLS"
bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>"$WORK/err"; RC=$?
check "eval: the same source passes when the evals do, and runs them once" "$RC:$(grep -c '^eval-call$' "$CALLS")" "0:1"
check "eval: the digest is recorded next to the stamp" "$([ -s "$DG" ] && [ "$(cat "$DG")" != "$D_BEFORE" ] && echo recorded)" "recorded"

ccommit --allow-empty -m "empty again"
S_D="$(cat "$B/current")"; : > "$CALLS"
EVAL_FAIL=1 bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>"$WORK/err"; RC=$?
check "eval: an unchanged digest skips the evals (they would fail), the build still publishes" \
  "$RC:$(grep -c '^eval-call$' "$CALLS"):$(grep -c '^manifest-call$' "$CALLS"):$(publishes):$([ "$(cat "$B/current")" != "$S_D" ] && echo new)" "0:0:1:1:new"

echo 'id: a4' > "$R/recipes/sindri/a.yaml"; ccommit -am "recipe 4"
echo 'id: b' > "$R/recipes/sindri/b.yaml"; git -C "$R" add -A; ccommit -m "recipe b"
BEFORE="$(cat "$B/current")"; D_BEFORE="$(cat "$DG")"; : > "$CALLS"
EVAL_COUNT_DELTA=1 bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>"$WORK/err"; RC=$?
check "eval: an embedded count that differs from the staged yaml count fails (2 staged, 3 embedded)" \
  "$RC:$(grep -c 'embeds 3 recipes but recipes/sindri has 2' "$WORK/err"):$([ "$(cat "$B/current")" = "$BEFORE" ] && [ "$(cat "$DG")" = "$D_BEFORE" ] && echo unchanged)" "1:1:unchanged"
bash "$R/scripts/sindri-app.sh" --publish-only >/dev/null 2>&1; RC=$?
check "eval: with the right count the same commit publishes" "$RC:$([ "$(cat "$B/current")" != "$BEFORE" ] && echo new)" "0:new"

# A tree that is not a git checkout cannot be published.
NG="$WORK/nogit"; mkdir -p "$NG/scripts/lib" "$NG/src/Sindri/Aw.Sindri.Host"
cp "$DIR/sindri-app.sh" "$NG/scripts/"; cp "$DIR/lib/sindri-build-inputs.sh" "$NG/scripts/lib/"
cp "$R/global.json" "$NG/"; cp "$R/scripts/binaries-lock.sh" "$NG/scripts/"
(cd "$WORK" && GIT_CEILING_DIRECTORIES="$WORK" bash "$NG/scripts/sindri-app.sh" --publish-only >/dev/null 2>"$WORK/err"); RC=$?
check "not a git checkout: exit 1 with the sentence" "$RC:$(grep -c 'Sindri can only be published from a git checkout' "$WORK/err")" "1:1"

# A failed `git archive` fails the publish: nothing is published and no empty recipes set is embedded.
REALGIT="$(command -v git)"; mkdir -p "$WORK/gitfail"
printf '#!/usr/bin/env bash\nfor a in "$@"; do [ "$a" = archive ] && { echo "fatal: archive boom" >&2; exit 128; }; done\nexec "%s" "$@"\n' "$REALGIT" > "$WORK/gitfail/git"
chmod +x "$WORK/gitfail/git"
BEFORE="$(builds):$(cat "$B/current")"; : > "$CALLS"
PATH="$WORK/gitfail:$PATH" bash "$R/scripts/sindri-app.sh" --publish-only --rebuild >/dev/null 2>"$WORK/err"; RC=$?
check "archive failure: exit 1 with the sentence" "$RC:$(grep -c 'could not stage recipes/sindri from HEAD (git archive failed)' "$WORK/err")" "1:1"
check "archive failure: nothing is published" "$(publishes):$(builds):$(cat "$B/current")" "0:$BEFORE"

exit "$fail"
