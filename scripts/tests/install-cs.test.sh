#!/usr/bin/env bash
# Tests for scripts/install-cs.sh (cutover spec §7.1) on a fake `dotnet` and a miniature git repo, in a guarded temp dir:
# AW_STATE_DIR, CLAUDE_LOCAL_BIN and TMPDIR all live under it, HOME is never changed and nothing real is read or written
# (no temp HOME: a real dotnet under one leaks a NuGet cache; the fake dotnet needs none). Covers: dry run writes nothing;
# --publish builds the eight binaries into bin/cs/<stamp>/ and touches neither `current` nor the wrappers; a second run is
# current and does not publish; the verify step runs each binary in a scratch AW_STATE_DIR; a failed publish and a build that
# does not answer leave nothing behind; --link needs a build for this source, switches `current`, writes the six wrappers over
# an existing TS wrapper, and the sindri wrapper exports the three binary paths (AW_UI_EVIDENCE_BIN among them) as resolved
# paths; a non-git tree fails; pruning keeps the newest two, `current` and the build a running serve executes; --rebuild of that build is declined.
# Run: bash scripts/tests/install-cs.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$DIR/lib/test-portable.sh"   # timeout for macOS (the staged-scripts rows)
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
export FAKE_SDK="10.0.100" FAKE_LOG="$WORK/publishes.log" FAKE_CALLS="$WORK/calls.log" FAKE_BIN="$WORK/fake-bin.sh"
: > "$FAKE_LOG"; : > "$FAKE_CALLS"

# What every published fake binary does (the generated stubs set FAKE_NAME, FAKE_REV and FAKE_SELF, then run this).
cat > "$FAKE_BIN" <<'FB'
#!/usr/bin/env bash
echo "$FAKE_NAME state=${AW_STATE_DIR:-} args=$*" >> "$FAKE_CALLS"
case "$FAKE_NAME" in
  sindri)
    case "${1:-}" in
      help) echo "usage: sindri <verb>" ;;
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
  pack)
    # pack <project> ... -p:Version=V -o OUT: writes OUT/Aw.Sindri.Toolkit.V.nupkg (FAKE_PACK_FAIL=1 fails), and logs the call
    shift 2; out="" ver=""
    while [ $# -gt 0 ]; do case "$1" in -o) out="$2"; shift ;; -p:Version=*) ver="${1#*=}" ;; esac; shift; done
    an=no; [ -f src/Aw.Analyzers/Aw.Analyzers.csproj ] && an=yes
    echo "pack version=$ver analyzers=$an" >> "$FAKE_LOG"
    [ -z "${FAKE_PACK_FAIL:-}" ] || { echo "boom packing" >&2; exit 1; }
    mkdir -p "$out"; : > "$out/Aw.Sindri.Toolkit.$ver.nupkg"; exit 0 ;;
  *) exit 0 ;;
esac
proj="$2"; shift 2
out="" rev=""
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift ;;
    -p:SourceRevisionId=*) rev="${1#*=}" ;;
  esac
  shift
done
case "$proj" in
  *Aw.Sindri.Host*) name=sindri ;; *Ui.Desktop*) name=sindri-ui ;; *Aw.Judge.Host*) name=judge ;;
  *Aw.Packets.App*) name=aw-packets ;; *Aw.BugFix.Host*) name=bugfix-state ;; *Aw.Evidence.Host*) name=ui-evidence ;;
  *Aw.Readiness.App*) name=aw-readiness ;; *Aw.Sindri.CodeRunner*) name=sindri-code-runner ;; *) exit 3 ;;
esac
echo "$name rev=$rev" >> "$FAKE_LOG"
[ "${FAKE_FAIL:-}" != "$name" ] || { echo "boom publishing $name" >&2; exit 1; }
mkdir -p "$out"
printf '#!/usr/bin/env bash\nFAKE_SELF="$0" FAKE_NAME=%s FAKE_REV=%s FAKE_CALLS="%s" FAKE_BAD_VERSION="%s" exec bash "%s" "$@"\n' "$name" "$rev" "$FAKE_CALLS" "${FAKE_BAD_VERSION:-}" "$FAKE_BIN" > "$out/$name"
chmod +x "$out/$name"
[ "$name" != judge ] || : > "$out/libe_sqlite3.so"
# Microsoft.Playwright puts its driver (node + the playwright package) in a dot folder beside the binary.
[ "$name" != ui-evidence ] || { mkdir -p "$out/.playwright/package"; echo driver > "$out/.playwright/package/cli.js"; }
# The staged libSkiaSharp.so: sindri-ui's build needs fontconfig, judge's NoDependencies build does not (FAKE_NOFONT=1: sindri-ui's lacks it too).
case "$name" in
  sindri-ui) if [ -n "${FAKE_NOFONT:-}" ]; then echo "NEEDED libc.so.6" > "$out/libSkiaSharp.so"; else printf 'NEEDED libfontconfig.so.1\nNEEDED libc.so.6\n' > "$out/libSkiaSharp.so"; fi ;;
  judge) echo "NEEDED libc.so.6" > "$out/libSkiaSharp.so" ;;
esac
FD
chmod +x "$WORK/fakebin/dotnet"
# A fake readelf: `readelf -d <file>` lists a NEEDED entry for each "NEEDED <lib>" line of the fake .so (no compiler needed for a real ELF).
cat > "$WORK/fakebin/readelf" <<'FR'
#!/usr/bin/env bash
sed -n 's/^NEEDED \(.*\)$/ 0x0000000000000001 (NEEDED)             Shared library: [\1]/p' "${2:?}"
FR
chmod +x "$WORK/fakebin/readelf"
export PATH="$WORK/fakebin:$PATH"

# A miniature checkout: the script, its inputs list, a pinned SDK, some sources.
REPO="$WORK/repo"
mkdir -p "$REPO/scripts/lib" "$REPO/skills/formal-verify/scripts" "$REPO/src/Sindri/Aw.Sindri.Host" "$REPO/src/Judge"
cp "$DIR/install-cs.sh" "$REPO/scripts/"
cp "$DIR/lib/sindri-build-inputs.sh" "$DIR/lib/sindri-open-runs.sh" "$REPO/scripts/lib/"
printf '#!/usr/bin/env bash\n[ "${1:-}" = --expensive ] && shift\nexec "$@"\n' > "$REPO/scripts/heavy.sh"; chmod +x "$REPO/scripts/heavy.sh"
printf '#!/bin/sh\necho "reserve-stub $*"\n' > "$REPO/scripts/meta-gate-reserve.sh"; chmod +x "$REPO/scripts/meta-gate-reserve.sh"
printf '{"sdk":{"version":"%s"}}\n' "$FAKE_SDK" > "$REPO/global.json"
echo a > "$REPO/src/Sindri/Aw.Sindri.Host/a.cs"; echo b > "$REPO/src/Judge/b.cs"
# A small reference graph that mirrors the real one where it matters (#3041): sindri, judge and aw-packets reference the shared
# Aw.Kernel project; sindri-ui reaches it only through Aw.Sindri.Core (a transitive reference); the other four reference nothing.
# The Include paths are relative to the csproj and mix / and \, as the real ones do.
proj() { # proj <dir> [<reference as written>]...
  local d="$1" n r; n="$(basename "$d")"; shift
  mkdir -p "$REPO/$d"
  { echo '<Project Sdk="Microsoft.NET.Sdk">'; echo '  <ItemGroup>'
    for r in "$@"; do printf '    <ProjectReference Include="%s" />\n' "$r"; done
    echo '  </ItemGroup>'; echo '</Project>'; } > "$REPO/$d/$n.csproj"
}
proj src/Aw.Kernel
proj src/Sindri/Aw.Sindri.Core '..\..\Aw.Kernel\Aw.Kernel.csproj'
proj src/Sindri/Aw.Sindri.Host '../Aw.Sindri.Core/Aw.Sindri.Core.csproj'
proj src/Sindri/Aw.Sindri.Ui.Desktop '..\Aw.Sindri.Core\Aw.Sindri.Core.csproj'
proj src/Sindri/Aw.Sindri.CodeRunner
proj src/Judge/Aw.Judge.Host '..\..\Aw.Kernel\Aw.Kernel.csproj'
proj src/Packets/Aw.Packets.App '../../Aw.Kernel/Aw.Kernel.csproj'
proj src/BugFix/Aw.BugFix.Host
proj src/Evidence/Aw.Evidence.Host
proj src/Readiness/Aw.Readiness.App
echo '<Project />' > "$REPO/Directory.Build.props"
g() { git -C "$REPO" -c user.name=t -c user.email=t@example.invalid "$@"; }
ccommit() { g commit -q "$@" > /dev/null 2>&1; }
g init -q -b main && g add -A && g commit -q -m base
INSTALL="$REPO/scripts/install-cs.sh"
B="$AW_STATE_DIR/bin/cs"
stamps() { find "$B" -mindepth 1 -maxdepth 1 -type d ! -name '.*' -exec basename {} \; | sort | tr '\n' ' '; }
# change a root build file (uncommitted: the stamp changes, the tree is "-dirty", and every binary's inputs change, so all eight publish)
bump() { echo "$1" >> "$REPO/Directory.Build.props"; }
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
has "dry run says it would link heavy through current" "$out" "[dry-run] would link $AW_STATE_DIR/bin-r1b/heavy -> $AW_STATE_DIR/bin/cs/current/heavy"
has "dry run says it would link meta-gate-reserve through current" "$out" "[dry-run] would link $AW_STATE_DIR/bin-r1b/meta-gate-reserve -> $AW_STATE_DIR/bin/cs/current/meta-gate-reserve"

# --- link before any build ---
out="$(bash "$INSTALL" --link 2>&1)"; rc=$?
check "--link without a build fails" "$rc" "1"
has "--link names the fix" "$out" "run scripts/install-cs.sh --publish first"
check "--link without a build writes no wrapper" "$(ls "$CLAUDE_LOCAL_BIN" | wc -l | tr -d ' ')" "0"

# --- publish ---
out="$(bash "$INSTALL" --publish 2>&1)"; rc=$?
check "--publish exits 0" "$rc" "0"
S1="$(stamps | tr -d ' ')"
if [ "$(uname -s)" = Linux ]; then check "the staged libSkiaSharp.so is sindri-ui's, the one that needs fontconfig" "$(readelf -d "$B/$S1/libSkiaSharp.so" | grep -c fontconfig)" "1"; fi
check "one stamped build" "$(printf '%s' "$S1" | wc -c | tr -d ' ')" "16"
for f in sindri sindri-code-runner sindri-ui judge aw-packets bugfix-state ui-evidence aw-readiness libe_sqlite3.so libSkiaSharp.so build-info; do
  check "build has $f" "$([ -e "$B/$S1/$f" ] && echo yes || echo no)" "yes"
done
check "the build keeps ui-evidence's Playwright driver (a dot folder)" "$(cat "$B/$S1/.playwright/package/cli.js" 2>/dev/null)" "driver"
check "eight publishes, each stamped with the commit" "$(grep -c "rev=$(g rev-parse HEAD)$" "$FAKE_LOG")" "8"
check "the build carries heavy, executable" "$([ -x "$B/$S1/heavy" ] && echo yes || echo no)" "yes"
check "the build's heavy is scripts/heavy.sh" "$(cmp -s "$B/$S1/heavy" "$REPO/scripts/heavy.sh" && echo same || echo differs)" "same"
check "the build carries meta-gate-reserve, executable" "$([ -x "$B/$S1/meta-gate-reserve" ] && echo yes || echo no)" "yes"
check "the build's meta-gate-reserve is scripts/meta-gate-reserve.sh" "$(cmp -s "$B/$S1/meta-gate-reserve" "$REPO/scripts/meta-gate-reserve.sh" && echo same || echo differs)" "same"
check "--publish leaves the meta-gate-reserve link alone" "$([ -e "$AW_STATE_DIR/bin-r1b/meta-gate-reserve" ] || [ -L "$AW_STATE_DIR/bin-r1b/meta-gate-reserve" ] && echo yes || echo no)" "no"
check "--publish leaves the heavy link alone" "$([ -e "$AW_STATE_DIR/bin-r1b/heavy" ] || [ -L "$AW_STATE_DIR/bin-r1b/heavy" ] && echo yes || echo no)" "no"
check "--publish leaves current alone" "$([ -e "$B/current" ] && echo yes || echo no)" "no"
check "--publish writes no wrapper" "$(ls "$CLAUDE_LOCAL_BIN" | wc -l | tr -d ' ')" "0"
has "the verify step ran sindri help" "$(cat "$FAKE_CALLS")" "sindri state="
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
HV="$AW_STATE_DIR/bin-r1b/heavy"
mkdir -p "$AW_STATE_DIR/bin-r1b"; printf '#!/bin/sh\necho old-heavy\n' > "$HV"; chmod +x "$HV"
MR="$AW_STATE_DIR/bin-r1b/meta-gate-reserve"
printf '#!/bin/sh\necho old-reserve\n' > "$MR"; chmod +x "$MR"
out="$(bash "$INSTALL" --link 2>&1)"; rc=$?
check "--link exits 0" "$rc" "0"
check "heavy links through current" "$(readlink "$HV")" "$B/current/heavy"
check "heavy resolves into the build dir" "$(readlink -f "$HV")" "$(cd "$B/$S1" && pwd -P)/heavy"
case "$(readlink -f "$HV")" in "$REPO"/*) r=repo ;; *) r=outside ;; esac
check "heavy never resolves into the repo" "$r" "outside"
git_top="$(cd "$(dirname "$(readlink -f "$HV")")" && git rev-parse --show-toplevel 2> /dev/null || true)"
check "heavy never resolves into any git tree" "$git_top" ""
check "the resolved heavy is scripts/heavy.sh" "$(cmp -s "$HV" "$REPO/scripts/heavy.sh" && echo same || echo differs)" "same"
check "the old real file moved to heavy.pre-repo" "$(sh "$HV.pre-repo")" "old-heavy"
bash "$INSTALL" --link > /dev/null 2>&1
check "a second --link keeps the link" "$(readlink "$HV")" "$B/current/heavy"
check "meta-gate-reserve links through current" "$(readlink "$MR")" "$B/current/meta-gate-reserve"
check "meta-gate-reserve resolves into the build dir" "$(readlink -f "$MR")" "$(cd "$B/$S1" && pwd -P)/meta-gate-reserve"
case "$(readlink -f "$MR")" in "$REPO"/*) r=repo ;; *) r=outside ;; esac
check "meta-gate-reserve never resolves into the repo" "$r" "outside"
check "the resolved meta-gate-reserve is scripts/meta-gate-reserve.sh" "$(cmp -s "$MR" "$REPO/scripts/meta-gate-reserve.sh" && echo same || echo differs)" "same"
check "the old real meta-gate-reserve moved to meta-gate-reserve.pre-repo" "$(sh "$MR.pre-repo")" "old-reserve"
check "a second --link adds no other file" "$(ls "$AW_STATE_DIR/bin-r1b" | tr '\n' ' ')" "heavy heavy.pre-repo meta-gate-reserve meta-gate-reserve.pre-repo "
check "the linked meta-gate-reserve runs" "$("$MR" --show)" "reserve-stub --show"
check "the linked heavy runs its command" "$("$HV" --expensive echo ran)" "ran"
mv "$B/$S1/heavy" "$B/$S1/heavy.away"
out="$(bash "$INSTALL" --link 2>&1)"; rc=$?
check "--link of a build without heavy fails" "$rc" "1"
has "...and names the fix" "$out" "has no heavy; run scripts/install-cs.sh --publish --rebuild"
check "...current unchanged" "$(readlink "$B/current")" "$S1"
check "...heavy link unchanged" "$(readlink "$HV")" "$B/current/heavy"
mv "$B/$S1/heavy.away" "$B/$S1/heavy"
mv "$B/$S1/meta-gate-reserve" "$B/$S1/meta-gate-reserve.away"
out="$(bash "$INSTALL" --link 2>&1)"; rc=$?
check "--link of a build without meta-gate-reserve fails" "$rc" "1"
has "...and names the fix" "$out" "has no meta-gate-reserve; run scripts/install-cs.sh --publish --rebuild"
check "...current unchanged" "$(readlink "$B/current")" "$S1"
check "...meta-gate-reserve link unchanged" "$(readlink "$MR")" "$B/current/meta-gate-reserve"
mv "$B/$S1/meta-gate-reserve.away" "$B/$S1/meta-gate-reserve"
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
envline="$(env -u DOTNET_ROOT "$CLAUDE_LOCAL_BIN/sindri" env)"
check "with DOTNET_ROOT unset, the wrapper's default is the bare path (no literal quotes)" "${envline##*droot=}" "$DOTNET_ROOT"
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
if [ "$(uname -s)" = Linux ]; then
  out="$(FAKE_NOFONT=1 bash "$INSTALL" --publish 2>&1)"; rc=$?
  check "a staged libSkiaSharp.so without a libfontconfig NEEDED entry fails the install" "$rc" "1"
  has "and says why" "$out" "install didn't take: the staged libSkiaSharp.so does not need libfontconfig, so it is not sindri-ui's build"
  check "and no new build" "$(stamps | tr -d ' ')" "$S1"
fi

# --- prune: newest two, current, and the build a running serve executes ---
out="$(bash "$INSTALL" --publish 2>&1)"; check "publish of the changed source" "$?" "0"
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
check "and publishes the eight binaries again" "$(wc -l < "$FAKE_LOG" | tr -d ' ')" "8"

# A profile this build can't validate: --link says the one command and still exits 0.
bash "$INSTALL" --publish > /dev/null 2>&1   # --link needs a build to link
out="$(FAKE_PROFILE_RC=2 FAKE_PROFILE_OUT="SND-PROFILE-001 x/profile.yaml: unknown key" bash "$INSTALL" --link 2>&1)"; rc=$?
check "an invalid profile: --link still exits 0" "$rc" "0"
has "an invalid profile: the hint names the command" "$out" "run \`sindri profile validate\` to see why"
out="$(bash "$INSTALL" --link 2>&1)"
check "a valid profile: no hint, and no word of approving it" "$(printf '%s' "$out" | grep -c 'profile approve\|to see why')" "0"

# A tree that is not a git checkout cannot be published.
NG="$WORK/nogit"; mkdir -p "$NG/scripts/lib" "$NG/src/Sindri/Aw.Sindri.Host"
cp "$DIR/install-cs.sh" "$NG/scripts/"; cp "$DIR/lib/sindri-build-inputs.sh" "$DIR/lib/sindri-open-runs.sh" "$NG/scripts/lib/"; cp "$REPO/global.json" "$NG/"
out="$(cd "$WORK" && GIT_CEILING_DIRECTORIES="$WORK" bash "$NG/scripts/install-cs.sh" --publish 2>&1)"; rc=$?
check "not a git checkout: exit 1" "$rc" "1"
has "not a git checkout: the sentence" "$out" "can only be published from a git checkout"

# --- every staged script runs from the build folder alone (#2145) ---
# Expected: a script install-cs.sh stages into the build (<build>/heavy, <build>/meta-gate-reserve) runs from that folder with
# nothing of the checkout beside it; heavy.sh sources ../config/lib/heavy-config.sh, which the build does not carry. The earlier
# rows stage stubs, so this one stages the REAL scripts into the miniature checkout, publishes, and runs them from the build.
mkdir -p "$REPO/config/lib"
cp "$DIR/heavy.sh" "$REPO/scripts/heavy.sh"; cp "$DIR/meta-gate-reserve.sh" "$REPO/scripts/meta-gate-reserve.sh"
cp "$DIR/../config/lib/"*.sh "$REPO/config/lib/"
g add -A; ccommit -m "real heavy and meta-gate-reserve"
bash "$INSTALL" --publish > /dev/null 2>&1; check "staged-scripts: the publish with the real scripts exits 0" "$?" "0"
SB="$(ls -td "$B"/*/ | head -1)"; SB="${SB%/}"   # the build just published is the newest
SCR="$WORK/staged-state"; mkdir -p "$SCR"
errf="$WORK/staged.err"
(cd / && AW_STATE_DIR="$SCR" timeout 20 "$SB/heavy" --expensive true > /dev/null 2> "$errf"); rc=$?
check "staged heavy runs from its build folder alone: exit 0" "$rc" "0"
check "staged heavy runs from its build folder alone: no 'command not found' (heavy_cap)" "$(grep -c 'command not found' "$errf")" "0"
check "staged heavy runs from its build folder alone: no 'No such file' (heavy-config.sh)" "$(grep -c 'No such file' "$errf")" "0"
(cd / && AW_STATE_DIR="$SCR" timeout 20 "$SB/meta-gate-reserve" --show > /dev/null 2> "$errf"); rc=$?
check "staged meta-gate-reserve runs from its build folder alone: exit 0" "$rc" "0"
check "staged meta-gate-reserve runs from its build folder alone: no 'command not found'" "$(grep -c 'command not found' "$errf")" "0"
check "staged meta-gate-reserve runs from its build folder alone: no 'No such file'" "$(grep -c 'No such file' "$errf")" "0"


# --- the toolkit SDK package (PR 15) ---
# Expected: with src/Toolkit/Aw.Sindri.Toolkit in the checkout, a publish also packs it into the folder feed
# ($AW_SINDRI_SDK_FEED, default $AW_SINDRI_HOME/sdk/packages) as 0.1.0-local.<timestamp>; a second run, with the sources
# unchanged, packs nothing; a change under src/Toolkit packs again (two newest packages stay); --dry-run prints the step and
# writes nothing; a failed pack warns, exits 0 with the wrappers linked and leaves the feed and its stamp as they were, so
# the next install packs again; the pack copy carries src/Aw.Analyzers (every production project's analyzer reference); without the SDK project the step is silent.
# why: the pack runs after the link, through the real heavy.sh copied in above; it puts ~/.dotnet first unless told otherwise
export AW_HEAVY_TEST_PATH="$WORK/fakebin"
FEED="$WORK/sdk-feed"
export AW_SINDRI_SDK_FEED="$FEED"
packs() { grep -c '^pack ' "$FAKE_LOG" | tr -d ' '; }
out="$(bash "$INSTALL" --publish 2>&1)"
check "no SDK project: no pack call" "$(packs)" "0"
check "no SDK project: no feed folder" "$([ -e "$FEED" ] && echo yes || echo no)" "no"
mkdir -p "$REPO/src/Toolkit/Aw.Sindri.Toolkit"
echo '<Project Sdk="Microsoft.NET.Sdk" />' > "$REPO/src/Toolkit/Aw.Sindri.Toolkit/Aw.Sindri.Toolkit.csproj"
mkdir -p "$REPO/src/Aw.Analyzers"; echo '<Project Sdk="Microsoft.NET.Sdk" />' > "$REPO/src/Aw.Analyzers/Aw.Analyzers.csproj"
echo 'class A {}' > "$REPO/src/Toolkit/Aw.Sindri.Toolkit/A.cs"
g add -A; ccommit -m "sdk project"
out="$(bash "$INSTALL" --dry-run 2>&1)"
has "dry run prints the pack step" "$out" "[dry-run] would pack src/Toolkit/Aw.Sindri.Toolkit/Aw.Sindri.Toolkit.csproj into $FEED"
check "dry run writes no feed" "$([ -e "$FEED" ] && echo yes || echo no)" "no"
out="$(bash "$INSTALL" --publish 2>&1)"; rc=$?
check "--publish with the SDK exits 0" "$rc" "0"
has "--publish says it packed" "$out" "packed Aw.Sindri.Toolkit 0.1.0-local."
check "--publish packs once" "$(packs)" "1"
check "the pack copy carries src/Aw.Analyzers" "$(grep -c "^pack .* analyzers=yes" "$FAKE_LOG" | tr -d " ")" "1"
check "the feed holds one package" "$(ls "$FEED"/Aw.Sindri.Toolkit.*.nupkg | wc -l | tr -d ' ')" "1"
check "the feed's stamp file exists" "$([ -s "$FEED/.sdk-stamp" ] && echo yes || echo no)" "yes"
check "the pack copy did not touch the checkout" "$(git -C "$REPO" status --porcelain | wc -l | tr -d ' ')" "0"
out="$(bash "$INSTALL" --publish 2>&1)"
has "second run says the SDK package is current" "$out" "toolkit SDK package is current"
check "second run packs nothing" "$(packs)" "1"
# Follow-up to #3218: a git that dies on the pack path's own `ls-files` (the only call that names src/Toolkit) must stop with a message.
REALGIT="$(command -v git)"
mkdir "$WORK/failpack"
cat > "$WORK/failpack/git" <<STUB
#!/usr/bin/env bash
case " \$* " in *" ls-files "*src/Toolkit*) exit 137 ;; esac
exec "$REALGIT" "\$@"
STUB
chmod +x "$WORK/failpack/git"
fpout="$(PATH="$WORK/failpack:$PATH" bash "$INSTALL" --publish 2>&1)" && fprc=0 || fprc=$?
check "git ls-files failing on the pack path: the publish exits 1" "$fprc" "1"
has "git ls-files failing on the pack path: says which step failed" "$fpout" "git ls-files failed while listing the toolkit SDK inputs"
check "git ls-files failing on the pack path: packs nothing" "$(packs)" "1"
# Follow-up to #3218: the stamp's own git calls. Its `ls-files` is the only one that names scripts/heavy.sh; `status` decides "-dirty".
for what in "ls-files:*scripts/heavy.sh*" "status:*"; do
  verb="${what%%:*}"; pat="${what#*:}"
  mkdir "$WORK/failstamp-$verb"
  cat > "$WORK/failstamp-$verb/git" <<STUB
#!/usr/bin/env bash
case " \$* " in *" $verb "*$pat) exit 137 ;; esac
exec "$REALGIT" "\$@"
STUB
  chmod +x "$WORK/failstamp-$verb/git"
  fsout="$(PATH="$WORK/failstamp-$verb:$PATH" bash "$INSTALL" --publish 2>&1)" && fsrc=0 || fsrc=$?
  check "git $verb failing in the stamp: the publish exits 1" "$fsrc" "1"
  has "git $verb failing in the stamp: says which step failed" "$fsout" "git $verb failed while stamping the build"
done
echo 'class B {}' >> "$REPO/src/Toolkit/Aw.Sindri.Toolkit/A.cs"
sleep 1
bash "$INSTALL" --publish > /dev/null 2>&1
check "a source change packs again" "$(packs)" "2"
echo 'class C {}' >> "$REPO/src/Toolkit/Aw.Sindri.Toolkit/A.cs"
sleep 1
bash "$INSTALL" --publish > /dev/null 2>&1
check "a third pack keeps only the two newest packages" "$(ls "$FEED"/Aw.Sindri.Toolkit.*.nupkg | wc -l | tr -d ' ')" "2"
before="$(ls "$FEED" | tr '\n' ' ')"
echo 'class D {}' >> "$REPO/src/Toolkit/Aw.Sindri.Toolkit/A.cs"
stamp_before="$(cat "$FEED/.sdk-stamp")"
rm -f "$CLAUDE_LOCAL_BIN"/*
out="$(FAKE_PACK_FAIL=1 bash "$INSTALL" 2>&1)"; rc=$?
check "a failed pack in the default publish + link exits 0" "$rc" "0"
has "a failed pack warns and names the step" "$out" "warning: packing the toolkit SDK failed; the next install retries"
check "a failed pack still links the wrappers" "$(ls "$CLAUDE_LOCAL_BIN" | tr '\n' ' ')" "aw-packets aw-readiness bugfix-state judge sindri ui-evidence "
check "a failed pack leaves the feed as it was" "$(ls "$FEED" | tr '\n' ' ')" "$before"
check "a failed pack leaves the stamp as it was" "$(cat "$FEED/.sdk-stamp")" "$stamp_before"
out="$(bash "$INSTALL" --link 2>&1)"
check "--link alone packs nothing" "$(packs)" "4"
out="$(bash "$INSTALL" --publish 2>&1)"
has "the install after a failed pack packs again" "$out" "packed Aw.Sindri.Toolkit 0.1.0-local."
check "the retry is one more pack call" "$(packs)" "5"
unset AW_SINDRI_SDK_FEED AW_HEAVY_TEST_PATH

# --- only the binaries whose inputs changed are published (#3041) ---
# Expected: each binary's inputs are its project's ProjectReference closure plus the root build files; the build records a hash
# per binary (inputs/<name>.sha256) and the files it published (inputs/<name>.files). A publish copies a binary whose hash equals
# the one in `current` instead of publishing it, in the same order as a fresh publish (sindri-ui's libSkiaSharp.so still wins).
# A root build file or --rebuild publishes all eight; a current build without inputs/ publishes all eight.
export AW_STATE_DIR="$WORK/state2" AW_SINDRI_HOME="$WORK/sindri-home2" AW_HEAVY_TEST_PATH="$WORK/fakebin"
B="$AW_STATE_DIR/bin/cs"
ALL8="aw-packets aw-readiness bugfix-state judge sindri sindri-code-runner sindri-ui ui-evidence "
NB=""
pubs() { grep -v '^pack ' "$FAKE_LOG" | cut -d' ' -f1 | LC_ALL=C sort | tr '\n' ' '; }
run_pl() { : > "$FAKE_LOG"; out="$(bash "$INSTALL" --publish "$@" 2>&1)"; rc=$?; bash "$INSTALL" --link > /dev/null 2>&1; NB="$(readlink "$B/current")"; }
have_all() { local n r=yes; for n in $ALL8; do [ -x "$B/$NB/$n" ] || r=no; done; echo "$r"; }
differs() { if [ "$1" != "$2" ]; then echo yes; else echo no; fi; }

run_pl; S0="$NB"
check "per-binary: the first publish builds all eight" "$(pubs)" "$ALL8"
check "per-binary: the build records a hash per binary" "$(ls "$B/$S0/inputs"/*.sha256 2> /dev/null | wc -l | tr -d ' ')" "8"
check "per-binary: the build records the file list per binary" "$(ls "$B/$S0/inputs"/*.files 2> /dev/null | wc -l | tr -d ' ')" "8"
check "per-binary: the judge file list names its libe_sqlite3.so" "$(grep -cx 'libe_sqlite3.so' "$B/$S0/inputs/judge.files" 2> /dev/null)" "1"
check "per-binary: the ui-evidence file list names its dot folder" "$(grep -cx '.playwright' "$B/$S0/inputs/ui-evidence.files" 2> /dev/null)" "1"

# (a) a change under the judge project: judge only
echo "judge change" >> "$REPO/src/Judge/Aw.Judge.Host/J.cs"
run_pl; S1="$NB"
check "judge change: the publish exits 0" "$rc" "0"
check "judge change: a new build" "$(differs "$S1" "$S0")" "yes"
check "judge change: only judge is published" "$(pubs)" "judge "
check "judge change: the new build has all eight binaries" "$(have_all)" "yes"
has "judge change: says it reused sindri" "$out" "reusing sindri from $S0"
has "judge change: says it reused sindri-ui" "$out" "reusing sindri-ui from $S0"
check "judge change: says nothing is reused for judge" "$(printf '%s' "$out" | grep -c 'reusing judge')" "0"
check "judge change: a copied binary is the earlier build's file" "$(cmp -s "$B/$S0/sindri" "$B/$S1/sindri" && echo same || echo differs)" "same"
check "judge change: (f) the staged libSkiaSharp.so is still sindri-ui's (needs fontconfig)" "$(grep -c fontconfig "$B/$S1/libSkiaSharp.so")" "1"
check "judge change: ui-evidence's Playwright dot folder was copied" "$(cat "$B/$S1/.playwright/package/cli.js" 2> /dev/null)" "driver"
check "judge change: judge's libe_sqlite3.so is there" "$([ -e "$B/$S1/libe_sqlite3.so" ] && echo yes || echo no)" "yes"
check "judge change: the copied binaries carry their hash and file list" "$(cmp -s "$B/$S0/inputs/sindri.sha256" "$B/$S1/inputs/sindri.sha256" && cmp -s "$B/$S0/inputs/sindri.files" "$B/$S1/inputs/sindri.files" && echo same || echo differs)" "same"
check "judge change: judge's hash changed" "$(differs "$(cat "$B/$S0/inputs/judge.sha256")" "$(cat "$B/$S1/inputs/judge.sha256")")" "yes"
check "judge change: no stage left" "$(find "$B" -mindepth 1 -maxdepth 1 -name '.stage*' | wc -l | tr -d ' ')" "0"
check "judge change: current points at the new build" "$S1" "$(readlink "$B/current")"

# (a2) #3218: a git that fails while the per-binary hashes are made (killed under memory pressure) must stop the publish with a message.
# It used to end the script silently after the "uncommitted changes" line (set -e), which looked like a dirty tree that would not reuse sindri.
# The stub fails every `git ls-files` after the first (the stamp's own call), like a git killed mid-publish.
REALGIT="$(command -v git)"
mkdir "$WORK/failgit"
cat > "$WORK/failgit/git" <<STUB
#!/usr/bin/env bash
case " \$* " in *" ls-files "*) n="\$(cat "$WORK/failgit/n" 2> /dev/null || echo 0)"; echo \$((n + 1)) > "$WORK/failgit/n"; [ "\$n" -lt 1 ] || exit 137 ;; esac
exec "$REALGIT" "\$@"
STUB
chmod +x "$WORK/failgit/git"
cp "$REPO/src/Judge/Aw.Judge.Host/J.cs" "$WORK/J.cs.before"
echo "git fails" >> "$REPO/src/Judge/Aw.Judge.Host/J.cs"
fgout="$(PATH="$WORK/failgit:$PATH" bash "$INSTALL" --publish 2>&1)" && fgrc=0 || fgrc=$?
mv "$WORK/J.cs.before" "$REPO/src/Judge/Aw.Judge.Host/J.cs"  # copy back, not an in-place sed edit: BSD sed differs (#3575)
check "git failing in the per-binary hashes: the publish exits 1" "$fgrc" "1"
has "git failing in the per-binary hashes: says which step failed" "$fgout" "git ls-files failed"

# (a3) Follow-up to #3218: the same for `git hash-object`, which the stamp and every per-binary hash pipe into sha256. A hash-object that dies
# (137, killed) under pipefail ended the script silently. The stub fails every hash-object, so the stamp's own call fails first.
mkdir "$WORK/failhash"
cat > "$WORK/failhash/git" <<STUB
#!/usr/bin/env bash
case " \$* " in *" hash-object "*) exit 137 ;; esac
exec "$REALGIT" "\$@"
STUB
chmod +x "$WORK/failhash/git"
fhout="$(PATH="$WORK/failhash:$PATH" bash "$INSTALL" --publish 2>&1)" && fhrc=0 || fhrc=$?
check "git hash-object failing: the publish exits 1" "$fhrc" "1"
has "git hash-object failing: says which step failed" "$fhout" "git hash-object failed"

# (b) scripts/heavy.sh only: nothing is published, the new build has the new heavy
echo "# a comment" >> "$REPO/scripts/heavy.sh"
run_pl; S2="$NB"
check "heavy change: the publish exits 0" "$rc" "0"
check "heavy change: a new build" "$(differs "$S2" "$S1")" "yes"
check "heavy change: nothing is published" "$(pubs)" ""
check "heavy change: the new build has all eight binaries" "$(have_all)" "yes"
check "heavy change: the new build's heavy is the new scripts/heavy.sh" "$(cmp -s "$B/$S2/heavy" "$REPO/scripts/heavy.sh" && echo same || echo differs)" "same"

# (c) a root build file: all eight
echo "<!-- root -->" >> "$REPO/Directory.Build.props"
run_pl; S3="$NB"
check "root build file change: the publish exits 0" "$rc" "0"
check "root build file change: all eight are published" "$(pubs)" "$ALL8"
check "root build file change: all eight binaries are there" "$(have_all)" "yes"

# (d) --rebuild: all eight, although nothing changed
run_pl --rebuild
check "--rebuild: the publish exits 0" "$rc" "0"
check "--rebuild: all eight are published" "$(pubs)" "$ALL8"

# (e) the shared kernel project: every binary whose closure includes it, none of the others
echo "kernel change" >> "$REPO/src/Aw.Kernel/K.cs"
run_pl
check "kernel change: the publish exits 0" "$rc" "0"
check "kernel change: sindri, sindri-ui (through Aw.Sindri.Core), judge and aw-packets are published" "$(pubs)" "aw-packets judge sindri sindri-ui "
check "kernel change: all eight binaries are there" "$(have_all)" "yes"
has "kernel change: says it reused bugfix-state" "$out" "reusing bugfix-state from"
check "kernel change: the staged libSkiaSharp.so is still sindri-ui's" "$(grep -c fontconfig "$B/$NB/libSkiaSharp.so")" "1"

# (e2) the middle project: sindri and sindri-ui, not judge
echo "core change" >> "$REPO/src/Sindri/Aw.Sindri.Core/C.cs"
run_pl
check "core change: only sindri and sindri-ui are published" "$(pubs)" "sindri sindri-ui "
check "core change: all eight binaries are there" "$(have_all)" "yes"
check "core change: the staged libSkiaSharp.so is still sindri-ui's" "$(grep -c fontconfig "$B/$NB/libSkiaSharp.so")" "1"

# (g) a current build without inputs/ (made before this change): everything is published
find "$B/$(readlink "$B/current")/inputs" -delete
echo "# another comment" >> "$REPO/scripts/heavy.sh"
run_pl
check "no inputs/ in current: the publish exits 0" "$rc" "0"
check "no inputs/ in current: all eight are published" "$(pubs)" "$ALL8"
check "no inputs/ in current: the new build records its inputs" "$(ls "$B/$NB/inputs"/*.sha256 2> /dev/null | wc -l | tr -d ' ')" "8"
unset AW_HEAVY_TEST_PATH

[ "$fail" = 0 ] && echo "all install-cs tests passed" || { echo "install-cs tests FAILED"; exit 1; }
