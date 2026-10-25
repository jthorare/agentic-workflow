#!/usr/bin/env bash
# Regression tests for GitHub issue #1032: a running Sindri control plane must keep the build it executes.
#   A. scripts/cutover-clean-ts.sh reads the serve pid from control.json as serve writes it (compact, one line, with
#      temporal.pid after the serve pid), honours AW_SINDRI_HOME, and neither --pre-link nor --post-link deletes
#      bin/sindri-app/<stamp> while that serve is alive.
#   B. scripts/install-cs.sh --link, when the control plane runs from a bin/cs/<stamp> build other than the one it links,
#      says "still running build" and names `sindri stop && sindri`; the running build is not pruned.
# Everything lives under one temp dir: HOME, AW_STATE_DIR, CLAUDE_LOCAL_BIN. Nothing builds, publishes or touches the real
# HOME. The only processes are fake serve / temporal stand-ins, each stopped by its recorded pid.
# Run: bash scripts/tests/serve-build-guard.test.sh
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$DIR/../.." && pwd)"

for v in $(env | sed -n 's/^\(AW_META[A-Z_]*\|AW_SINDRI[A-Z_]*\|TMUX\|TMUX_PANE\|AW_PROFILE_DIR\|AW_JUDGE[A-Z_]*\)=.*/\1/p'); do unset "$v"; done
fail=0
ok() { echo "ok - $1"; }
bad() { echo "not ok - $1"; fail=1; }
check() { if [ "$2" == "$3" ]; then ok "$1"; else bad "$1"; echo "  expected: $3"; echo "  actual:   $2"; fi; }
has() { case "$2" in *"$3"*) ok "$1" ;; *) bad "$1"; echo "  missing: $3"; echo "  in: $2" | head -20 ;; esac; }
exists() { if [ -e "$2" ]; then ok "$1"; else bad "$1"; echo "  missing path: $2"; fi; }

WORK="$(cd "$(mktemp -d)" && pwd -P)"
PIDS=()
stop_all() {
  local p
  for p in ${PIDS[@]+"${PIDS[@]}"}; do kill "$p" 2> /dev/null || true; done   # recorded pids only
  PIDS=()
}
trap 'stop_all; find "$WORK" -delete 2> /dev/null || true' EXIT

# A fake serve: a script at <build>/sindri whose command line (ps) names the build dir; it runs until stopped.
make_serve() { # make_serve <build dir>
  mkdir -p "$1"
  printf '#!/usr/bin/env bash\nwhile :; do sleep 1; done\n' > "$1/sindri"
  chmod +x "$1/sindri"
  echo built > "$1/build-info"
}
# start_serve <build dir> <home dir>: starts the fake serve and a fake temporal; sets SERVE_PID; writes control.json compact
# on one line in the field order serve writes (url, pid, epoch, temporal{pid, port}; FileEndpoint.cs).
start_serve() {
  local temporal_pid
  "$1/sindri" serve 300 > /dev/null 2>&1 < /dev/null &
  SERVE_PID=$!; PIDS+=("$SERVE_PID")
  sleep 300 > /dev/null 2>&1 < /dev/null &
  temporal_pid=$!; PIDS+=("$temporal_pid")
  mkdir -p "$2"
  printf '{"url":"http://127.0.0.1:7470","pid":%s,"epoch":1,"temporal":{"pid":%s,"port":7233}}\n' "$SERVE_PID" "$temporal_pid" > "$2/control.json"
  sleep 0.3
}

# ---- A: cutover-clean-ts.sh ---------------------------------------------------------------------------------------------
# scenario_a <name> <default|custom> <--pre-link|--post-link>: a fresh box with a serve running from bin/sindri-app/stampA.
scenario_a() {
  local name="$1" home_mode="$2" flag="$3" d out rc home
  d="$WORK/a-$name"
  mkdir -p "$d/state" "$d/bin" "$d/home" "$d/root/scripts" "$d/la"
  cp "$REPO_ROOT/scripts/cutover-clean-ts.sh" "$d/root/scripts/"
  git -C "$d/root" init -q
  printf '#!/usr/bin/env bash\necho "{\\"status\\":\\"ok\\"}"\n' > "$d/bin/judge"; chmod +x "$d/bin/judge"   # the post-link health gate
  home="$d/state/sindri-app"
  if [ "$home_mode" = custom ]; then home="$d/elsewhere/sindri-home"; fi
  make_serve "$d/state/bin/sindri-app/stampA"
  start_serve "$d/state/bin/sindri-app/stampA" "$home"
  local envs=(HOME="$d/home" AW_STATE_DIR="$d/state" CLAUDE_LOCAL_BIN="$d/bin" AW_LAUNCH_AGENTS_DIR="$d/la" AW_PROFILE_DIR="$d/state/profile")
  if [ "$home_mode" = custom ]; then envs+=(AW_SINDRI_HOME="$home"); fi
  rc=0; out="$(env "${envs[@]}" bash "$d/root/scripts/cutover-clean-ts.sh" "$flag" 2>&1)" || rc=$?
  if [ "$flag" = --pre-link ]; then
    check "A $name $flag refuses while the serve runs (exit 1)" "$rc" 1
  else
    if [ "$rc" != 0 ]; then ok "A $name $flag exits non-zero ($rc) while the serve runs"; else
      has "A $name $flag exits 0 only if it says the build was kept" "$out" "still running"
    fi
  fi
  exists "A $name $flag keeps bin/sindri-app/stampA" "$d/state/bin/sindri-app/stampA"
  exists "A $name $flag keeps the serve's binary" "$d/state/bin/sindri-app/stampA/sindri"
  check "A $name $flag leaves the serve alive" "$(kill -0 "$SERVE_PID" 2> /dev/null && echo alive || echo dead)" alive
  stop_all
}
scenario_a default-pre default --pre-link
scenario_a default-post default --post-link
scenario_a custom-pre custom --pre-link
scenario_a custom-post custom --post-link

# ---- B: install-cs.sh --link --------------------------------------------------------------------------------------------
# A miniature checkout, as scripts/tests/install-cs.test.sh builds one; no dotnet is needed for --link on a made-up build.
d="$WORK/b"
REPO="$d/repo"
mkdir -p "$d/state" "$d/bin" "$d/tmp" "$d/dotnet-root" "$REPO/scripts/lib" "$REPO/src/Sindri/Aw.Sindri.Host" "$REPO/src/Judge" "$REPO/recipes/sindri"
cp "$DIR/../install-cs.sh" "$REPO/scripts/"
cp "$DIR/../lib/sindri-build-inputs.sh" "$DIR/../lib/sindri-open-runs.sh" "$REPO/scripts/lib/"
printf '{"sdk":{"version":"10.0.100"}}\n' > "$REPO/global.json"
echo a > "$REPO/src/Sindri/Aw.Sindri.Host/a.cs"; echo b > "$REPO/src/Judge/b.cs"
echo "name: one" > "$REPO/recipes/sindri/one.yaml"
git -C "$REPO" -c user.name=t -c user.email=t@example.invalid init -q -b main
git -C "$REPO" add -A
git -C "$REPO" -c user.name=t -c user.email=t@example.invalid commit -q -m base
export HOME="$d/home" AW_STATE_DIR="$d/state" CLAUDE_LOCAL_BIN="$d/bin" TMPDIR="$d/tmp" DOTNET_ROOT="$d/dotnet-root" AW_CS_SKIP_DOWNLOADS=1
export AW_SINDRI_HOME="$d/sindri-home"
mkdir -p "$HOME"
INSTALL="$REPO/scripts/install-cs.sh"
BASE="$AW_STATE_DIR/bin/cs"

# The stamp --link wants: the dry run names it.
stampB="$(bash "$INSTALL" --link --dry-run 2>&1 | sed -n 's/.*would point .*current at \([0-9a-f]\{16\}\),.*/\1/p' | head -n 1)"
check "B the dry run names the stamp to link" "${#stampB}" 16
mkdir -p "$BASE/stampA" "$BASE/stampC" "$BASE/stampD" "$BASE/$stampB"
for s in stampC stampD "$stampB"; do echo built > "$BASE/$s/build-info"; done
make_serve "$BASE/stampA"
touch -t 202001010100 "$BASE/stampA"; touch -t 202001010200 "$BASE/stampC"; touch -t 202001010300 "$BASE/stampD"
ln -s stampA "$BASE/current"
start_serve "$BASE/stampA" "$AW_SINDRI_HOME"
rc=0; out="$(bash "$INSTALL" --link 2>&1)" || rc=$?
check "B --link exits 0" "$rc" 0
check "B --link switched current to the new build" "$(readlink "$BASE/current")" "$stampB"
has "B --link says the old build is still running" "$out" "still running build"
has "B --link names the build" "$out" "stampA"
has "B --link says how to restart onto the new build" "$out" "sindri stop && sindri"
exists "B the running build is not pruned" "$BASE/stampA"
check "B an idle old build is pruned (prune did run)" "$([ -e "$BASE/stampC" ] && echo kept || echo pruned)" pruned
check "B the serve is still alive" "$(kill -0 "$SERVE_PID" 2> /dev/null && echo alive || echo dead)" alive
stop_all

if [ "$fail" = 0 ]; then echo "all passed"; else echo "FAILED"; fi
exit "$fail"
