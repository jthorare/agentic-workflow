#!/usr/bin/env bash
# Tests for scripts/sindri-app.sh on a sandboxed HOME and state dir, against a throwaway git repo holding a copy of the
# script, a stub UI publish (src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh), a stub scripts/binaries-lock.sh and a fake
# `dotnet` on PATH (its publish writes a stub `sindri` that prints its arguments), so nothing is built or fetched:
# the first run publishes both binaries into one build dir, links sindri-app, fetches temporal and runs sindri with the
# arguments; an unchanged tree reuses the build; an edit (committed or not) rebuilds; --rebuild forces one; two builds
# are kept; a failed publish leaves the last good build current; a foreign sindri-app on PATH is left alone, a dangling
# one replaced, AW_SINDRI_APP_NO_LINK=1 links nothing; stdout carries only sindri's output.
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

# The fake dotnet: the pinned SDK is installed; publish -o DIR writes the sindri stub there (FAIL_PUBLISH=1 fails it).
mkdir -p "$WORK/fakebin"
cat > "$WORK/fakebin/dotnet" <<'EOF'
#!/usr/bin/env bash
case "$1" in
  --list-sdks) echo "10.0.401 [/fake/dotnet/sdk]" ;;
  publish)
    echo "publish" >> "$CALLS"
    echo "cwd=$(pwd -P)" >> "$CALLS"
    for f in config/launcher/builtins.json BannedSymbols.All.txt BannedSymbols.NonHost.txt; do
      [ -f "$f" ] && echo "has $f" >> "$CALLS"
    done
    [ "${FAIL_PUBLISH:-0}" = 1 ] && { echo "error CS0001: boom"; exit 1; }
    out=""; while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
    mkdir -p "$out"
    printf '#!/usr/bin/env bash\necho "sindri $*"\necho "root=$DOTNET_ROOT" >&2\necho "sindri $*" >> "$CALLS"\ncase "$1" in mcp) echo "codex: registered" ; exit "${MCP_EXIT:-0}" ;; esac\n' > "$out/sindri"; chmod +x "$out/sindri" ;;
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
check "B2: the host and UI publishes both run under heavy --expensive" "$(grep -c '^heavy --expensive$' "$CALLS")" "2"
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

exit "$fail"
