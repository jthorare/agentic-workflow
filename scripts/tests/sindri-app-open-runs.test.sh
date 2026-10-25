#!/usr/bin/env bash
# Test for #1000 ask 4: scripts/sindri-app.sh must not republish Sindri under a running control plane while agent runs are
# open, unless the owner says so. Same sandbox as sindri-app.test.sh (throwaway git repo with a copy of the script and its
# lib/, fake `dotnet`, stub sindri), plus a fake running plane (a sleeping process whose command line names the build dir,
# its pid in control.json) and a stub `sindri agents --json` that reports one running session with a launch.
# Without a terminal, a publish with open runs stops (exit 1, nothing published) and the message names the runs and
# AW_SINDRI_STOP_OK=1; with AW_SINDRI_STOP_OK=1 the publish goes ahead.
# Run: bash scripts/tests/sindri-app-open-runs.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
unset TMUX AW_SINDRI_APP_NO_LINK AW_SINDRI_HOME CLAUDE_LOCAL_BIN DOTNET_ROOT AW_SINDRI_MCP_REPO AW_SINDRI_STOP_OK
for v in $(compgen -v | grep '^AW_META_LANE' || true); do unset "$v"; done
WORK="$(cd "$(mktemp -d)" && pwd -P)"
PLANE=""
trap '[ -n "$PLANE" ] && kill "$PLANE" 2>/dev/null; rm -rf "$WORK"' EXIT
export AW_SINDRI_APP_NO_MCP=1
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state"
mkdir -p "$HOME"
CALLS="$WORK/calls"; : > "$CALLS"
export CALLS

cat > "$WORK/stub-sindri" <<'EOF'
#!/usr/bin/env bash
if [ "$1 $2" = "recipe manifest" ]; then
  echo "{\"recipes\":[],\"invalid\":[],\"digest\":\"sha256:$(find "$3" -type f | LC_ALL=C sort | xargs cat | cksum | tr -d ' ')\"}"
  exit 0
fi
if [ "$1 $2" = "recipe eval" ]; then
  c=$(cat "$(dirname "$0")/embedded-count"); recs=""; i=0
  while [ "$i" -lt "$c" ]; do recs="$recs{},"; i=$((i+1)); done
  echo "{\"passed\":true,\"recipes\":[${recs%,}]}"
  exit 0
fi
if [ "$1" = agents ]; then
  echo "agents-call" >> "$CALLS"
  echo '{"sessions":[{"ref":"claude/abc","state":"running","launch":{"instance":"ITM-1/line/3/scoping.1","state":"challenge","attempt":1}}],"recentlyEnded":[],"problems":[],"counts":{},"empty":null,"asOf":"2026-10-25T00:00:00Z"}'
  exit 0
fi
echo "sindri $*"
echo "sindri $*" >> "$CALLS"
EOF
export STUB_SINDRI="$WORK/stub-sindri"

mkdir -p "$WORK/fakebin"
cat > "$WORK/fakebin/dotnet" <<'EOF'
#!/usr/bin/env bash
case "$1" in
  --list-sdks) echo "10.0.401 [/fake/dotnet/sdk]" ;;
  publish)
    case "$*" in *Aw.Packets.App*)
      out=""; while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
      mkdir -p "$out"; printf '#!/bin/sh\n' > "$out/aw-packets"; chmod +x "$out/aw-packets"
      exit 0 ;;
    esac
    case "$*" in *Aw.Judge.Host*)
      out=""; while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
      mkdir -p "$out"; printf '#!/bin/sh\n' > "$out/judge"; chmod +x "$out/judge"; : > "$out/libe_sqlite3.so"
      exit 0 ;;
    esac
    echo "publish" >> "$CALLS"
    rd=""; for a in "$@"; do case "$a" in -p:AwRecipesDir=*) rd="${a#-p:AwRecipesDir=}" ;; esac; done
    out=""; while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
    mkdir -p "$out"
    n=0; [ -n "$rd" ] && [ -d "$rd" ] && n="$(find "$rd" -maxdepth 1 -name '*.yaml' | wc -l | tr -d ' ')"
    echo "$n" > "$out/embedded-count"
    cp "$STUB_SINDRI" "$out/sindri"; chmod +x "$out/sindri" ;;
esac
EOF
chmod +x "$WORK/fakebin/dotnet"
export PATH="$WORK/fakebin:$PATH"

R="$WORK/repo"
mkdir -p "$R/scripts/lib" "$R/src/Sindri/Aw.Sindri.Host" "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke" "$R/config/launcher"
cp "$DIR/sindri-app.sh" "$R/scripts/"
cp "$DIR"/lib/*.sh "$R/scripts/lib/"
echo '{}' > "$R/config/launcher/builtins.json"
echo 'T:System.X' > "$R/BannedSymbols.All.txt"
echo 'T:System.Y' > "$R/BannedSymbols.NonHost.txt"
printf '{\n  "sdk": {\n    "version": "10.0.401"\n  }\n}\n' > "$R/global.json"
echo '<Project />' > "$R/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj"
printf '#!/usr/bin/env bash\nprintf "#!/bin/sh\\n" > "$1/sindri-ui"; chmod +x "$1/sindri-ui"\n' \
  > "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh"
printf '#!/usr/bin/env bash\nexit 0\n' > "$R/scripts/binaries-lock.sh"
chmod +x "$R/scripts/binaries-lock.sh" "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh"
git -C "$R" init -q && git -C "$R" add -A && git -C "$R" -c user.email=t@t -c user.name=t commit -qm init

B="$AW_STATE_DIR/bin/sindri-app"
builds() { find "$B" -mindepth 1 -maxdepth 1 -type d ! -name '.*' | wc -l | tr -d ' '; }
publishes() { grep -c '^publish$' "$CALLS"; }

# (1) The first run publishes build S1; no plane runs yet.
bash "$R/scripts/sindri-app.sh" status </dev/null >/dev/null 2>"$WORK/err1"; RC=$?
S1="$(cat "$B/current" 2>/dev/null || true)"
check "step 1: first run publishes build S1" "$RC:$(publishes):$(builds)" "0:1:1"

# (2) A fake running plane executing S1, recorded in control.json.
bash -c 'sleep 120; :' "$B/$S1/sindri serve" >/dev/null 2>&1 &
PLANE=$!
mkdir -p "$AW_STATE_DIR/sindri-app"
printf '{"pid":%s}\n' "$PLANE" > "$AW_STATE_DIR/sindri-app/control.json"

# (3) A source change, so the stamp changes and a publish is needed.
echo '<Project><!-- changed --></Project>' > "$R/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj"
git -C "$R" -c user.email=t@t -c user.name=t commit -qam change

# (4) Open agent runs, no terminal: the publish stops.
: > "$CALLS"
bash "$R/scripts/sindri-app.sh" status </dev/null >/dev/null 2>"$WORK/err"; RC=$?
ERR="$(cat "$WORK/err")"
check "step 4: exit code is 1" "$RC" "1"
check "step 4: nothing was published" "$(publishes)" "0"
check "step 4: stderr names the open session" "$(grep -c 'claude/abc' <<<"$ERR")" "1"
check "step 4: stderr names the launch instance" "$(grep -c 'ITM-1/line/3/scoping.1' <<<"$ERR")" "1"
check "step 4: stderr names AW_SINDRI_STOP_OK=1" "$(grep -c 'AW_SINDRI_STOP_OK=1' <<<"$ERR")" "1"
check "step 4: still one build" "$(builds)" "1"

# (5) The owner says go: the same run publishes.
: > "$CALLS"
AW_SINDRI_STOP_OK=1 bash "$R/scripts/sindri-app.sh" status </dev/null >/dev/null 2>"$WORK/err5"; RC=$?
check "step 5: with AW_SINDRI_STOP_OK=1 the run exits 0 and publishes once" "$RC:$(publishes)" "0:1"

exit $fail
