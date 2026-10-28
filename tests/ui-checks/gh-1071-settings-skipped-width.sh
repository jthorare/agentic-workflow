#!/usr/bin/env bash
# tests/ui-checks/gh-1071-settings-skipped-width.sh — regression check for #1071 (Settings: a skipped setup step shows the same
# tick as a Done step, and Settings is a narrow column).
# At 1280x800: open Settings, press "Not now" on the Onyx step until its row says "skipped", then (1) the number circle of the
# skipped step must not be the "✓" a done step gets, and (2) every setup step card must reach to within 64 px of the window's
# right edge (on main the cards are x=25 w=741 in a 1280 px window).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh).
#   bash tests/ui-checks/gh-1071-settings-skipped-width.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1071-settings-skipped-width-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1071-settings-skipped-width-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh-1071-settings-skipped-width-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1071-settings-skipped-width: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
}
fail() { echo "FAIL: $*" >&2; exit 1; }

cleanup() {
  local app_pid=""
  if [ -f "$AW_UI_DRIVER_DIR/driver.json" ]; then
    app_pid="$(sed -n 's/.*"pid":\([0-9][0-9]*\).*/\1/p' "$AW_UI_DRIVER_DIR/driver.json")"
  fi
  if [ -n "$app_pid" ]; then kill "$app_pid" 2>/dev/null || true; fi
  if [ -n "$APP_WRAPPER_PID" ]; then kill "$APP_WRAPPER_PID" 2>/dev/null || true; fi
  bash "$FIXTURE" down >/dev/null 2>&1 || true
}
trap cleanup EXIT

command -v jq >/dev/null 2>&1 || setup_fail "jq is needed"

# 1. The control plane with an approved profile, then the profile changes so Settings asks for the approval.
bash "$FIXTURE" up >"$OUT/fixture-up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/fixture-up.log)"

# The fixture's profile has no repo, and the settings step waits for one ("Waits for a repository"). Give the profile a repo the
# way 'context up' does (a git repo under the home, a repos: block after user:) and approve it as the owner; then change it.
profile="$AW_SINDRI_HOME/profile/profile.yaml"
repo_dir="$AW_SINDRI_HOME/ui-fixture-repo/check-repo"
[ -f "$profile" ] || setup_fail "no live profile at $profile"
mkdir -p "$repo_dir" || setup_fail "cannot make $repo_dir"
if [ ! -d "$repo_dir/.git" ]; then
  printf "# check\n" >"$repo_dir/README.md"
  g=(env -u GIT_DIR -u GIT_WORK_TREE -u GIT_INDEX_FILE -u GIT_COMMON_DIR GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
     GIT_AUTHOR_NAME=Fixture GIT_AUTHOR_EMAIL=fixture@example.invalid GIT_COMMITTER_NAME=Fixture GIT_COMMITTER_EMAIL=fixture@example.invalid git -C "$repo_dir")
  "${g[@]}" init -q -b main && "${g[@]}" add -A && "${g[@]}" commit -q --no-gpg-sign -m "check repo" || setup_fail "cannot build the check repo"
fi
if ! grep -q "name: check-repo" "$profile"; then
  if grep -q "^repos:" "$profile"; then
    awk -v e="  - { name: check-repo, path: $repo_dir }" "{print} /^repos:/ {print e}" "$profile" >"$profile.new"
  else
    awk -v e="repos:\n  - { name: check-repo, path: $repo_dir }" "{print} /^user:/ {print e}" "$profile" >"$profile.new"
  fi && mv "$profile.new" "$profile" || setup_fail "cannot add the repo to the profile"
fi
hash="$("$PROVE" profile validate --json 2>/dev/null | jq -r ".hash // empty")"
[ -n "$hash" ] || setup_fail "profile validate gave no hash"
printf "approve profile %s\n" "${hash:0:6}" | "$PROVE" human profile approve "$hash" >/dev/null 2>&1 || setup_fail "the owner's approval of the profile with a repo"

# 2. The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
#    make the build the owner's current one), unless AW_SINDRI_UI names a binary.
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
if [ -z "${DISPLAY:-}" ]; then
  # An Xvfb this check owns and stops by pid on exit (xvfb-run leaves its Xvfb behind when a timeout kills the check).
  source "$ROOT/tests/ui-checks/xvfb-lib.sh"
  xvfb_start || setup_fail "no Xvfb display could be started"
fi
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
"$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

onyx_title="Connect Linear and Slack (Onyx)"
not_now=(--type Button --name "Not now")

# 3. Settings at 1280x800, by the Ctrl+, chord.
timeout 120 "$DRIVER" window --width 1280 --height 800 >/dev/null 2>&1 || setup_fail "could not size the window"
sleep 2
timeout 120 "$DRIVER" key --chord Ctrl+OemComma --node-id 0 >/dev/null 2>&1 || setup_fail "the Ctrl+, chord was not accepted"
# --contains matches shown text, and an Expander shows none: find the step by its accessible name, which starts with the title.
step_id() {
  "$DRIVER" search --type Expander --limit 200 2>/dev/null \
    | jq -r --arg p "$1" '[.matches[] | select(.visible and ((.name // "") | startswith($p))) | .id][0] // empty'
}
onyx_id=""
for _ in $(seq 1 120); do
  onyx_id="$(step_id "$onyx_title, ")"
  [ -n "$onyx_id" ] && break
  sleep 0.5
done
[ -n "$onyx_id" ] || setup_fail "no '$onyx_title' step in Settings"

# 4. Open the step if it is closed, press "Not now", wait for the row to say "skipped" (name: "<title>, skipped. <sub>").
if ! "$DRIVER" search "${not_now[@]}" 2>/dev/null | jq -e '[.matches[] | select(.visible)] | length > 0' >/dev/null; then
  "$DRIVER" action --node-id "$onyx_id" --do expand >/dev/null 2>&1 || setup_fail "could not expand the Onyx step"
fi
"$DRIVER" wait-for "${not_now[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "no 'Not now' button on the Onyx step"
"$DRIVER" click "${not_now[@]}" >/dev/null 2>&1 || setup_fail "the Not now click was not accepted by the driver"
skipped=0
for _ in $(seq 1 60); do
  if [ -n "$(step_id "$onyx_title, skipped")" ]; then skipped=1; break; fi
  sleep 0.5
done
[ "$skipped" = 1 ] || setup_fail "the Onyx step never said skipped"
sleep 1
"$DRIVER" screenshot --out "$OUT/skipped.png" >/dev/null 2>&1
"$DRIVER" tree --depth 64 >"$OUT/tree.json" 2>/dev/null || setup_fail "the driver gave no tree"

# 5. The skipped step's number circle (the first visible TextBlock of its Expander: the header's) is not a done step's tick.
circle="$(jq -r --arg t "$onyx_title, skipped" '
  [.. | objects | select(.type == "Expander" and .visible and ((.name // "") | startswith($t)))][0]
  | [.. | objects | select(.type == "TextBlock" and .visible)][0].text // empty' "$OUT/tree.json")"
[ -n "$circle" ] || setup_fail "no number circle found for the skipped Onyx step (see $OUT/tree.json)"
done_circles="$(jq -r '
  [.. | objects | select(.type == "Expander" and .visible and ((.name // "") | test(", done\\.")))
   | [.. | objects | select(.type == "TextBlock" and .visible)][0].text] | unique | join(" ")' "$OUT/tree.json")"
[ "$circle" != "✓" ] || fail "the skipped Onyx step shows '$circle', the same tick as a done step (done circles: '$done_circles') (screenshot in $OUT)"
case " $done_circles " in *" $circle "*) fail "the skipped Onyx step's circle '$circle' equals a done step's (screenshot in $OUT)";; esac

# 6. Every visible setup step card reaches within 64 px of the window's right edge.
win_w="$(jq -r '.root.width // .width' "$OUT/tree.json")"
narrow="$(jq -r --argjson w "$win_w" '
  [.. | objects | select(.type == "Expander" and .visible and ((.name // "") | test(", (done|to do|needs you|working|failed|skipped|waiting|blocked)[.,]")))
   | select(.x + .width < $w - 64)
   | "\(.name | split(",")[0]) ends at \(.x + .width)"] | .[0] // empty' "$OUT/tree.json")"
[ -z "$narrow" ] || fail "Settings is a narrow column: $narrow, the window is $win_w wide (screenshot in $OUT)"

echo "PASS: a skipped setup step has its own icon and every step card reaches the window's right edge (screenshots in $OUT)"
