#!/usr/bin/env bash
# tests/ui-checks/gh-1539-settings-leftovers.sh — regression check for #1539 (Settings leftovers).
# At 1280x800, in the real sindri-ui:
#   1. (gone with Onyx, #1598: the skipped Onyx step's Docker link. No setup step names a web address any more.)
#   2. the Text size slider has tick marks (TickPlacement is not None) and 90% / 100% / 130% labels under it;
#   3. the "Approve the workflows" step can be approved through the driver (button "Approve all workflows", then the step says done).
# Both run, every FAIL is printed, and the exit is 1 when any failed. Part 3 changes state, so it runs last.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh).
#   bash tests/ui-checks/gh-1539-settings-leftovers.sh        exit 0 pass; 1 FAIL (stderr lines); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1539-settings-leftovers-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1539-settings-leftovers-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh-1539-settings-leftovers-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1539-settings-leftovers: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
}
FAILED=0
fail() { echo "FAIL: $*" >&2; FAILED=1; }

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

sources_title="Connect your sources"
wf_title="Approve the workflows"

# Settings at 1280x800, by the Ctrl+, chord.
timeout 120 "$DRIVER" window --width 1280 --height 800 >/dev/null 2>&1 || setup_fail "could not size the window"
sleep 2
timeout 120 "$DRIVER" key --chord Ctrl+OemComma --node-id 0 >/dev/null 2>&1 || setup_fail "the Ctrl+, chord was not accepted"
# --contains matches shown text, and an Expander shows none: find the step by its accessible name, which starts with the title.
step_id() {
  "$DRIVER" search --type Expander --limit 200 2>/dev/null \
    | jq -r --arg p "$1" '[.matches[] | select(.visible and ((.name // "") | startswith($p))) | .id][0] // empty'
}
wait_step() { # <name prefix> <tries of 0.5 s>
  local id=""
  for _ in $(seq 1 "$2"); do
    id="$(step_id "$1")"
    [ -n "$id" ] && break
    sleep 0.5
  done
  printf '%s' "$id"
}
sources_id="$(wait_step "$sources_title, " 240)"
[ -n "$sources_id" ] || setup_fail "no '$sources_title' step in Settings"

# ---- Part 2: the Text size slider shows ticks and 90% / 100% / 130% labels under it ---------------------------------------------
# The slider's accessible name is the "Text size" label; --type Slider is the one slider in Settings. Scroll it into view first.
"$DRIVER" scroll --type Slider --dy 4000 >/dev/null 2>&1 || true
sleep 1
"$DRIVER" screenshot --out "$OUT/part2.png" >/dev/null 2>&1
"$DRIVER" tree --depth 64 >"$OUT/tree2.json" 2>/dev/null || setup_fail "the driver gave no tree"
slider_id="$(jq -r '[.. | objects | select(.type == "Slider" and .visible)][0].id // empty' "$OUT/tree2.json")"
[ -n "$slider_id" ] || setup_fail "no visible Slider in Settings (see $OUT/tree2.json)"
ticks="$("$DRIVER" props --node-id "$slider_id" 2>/dev/null | jq -r '.properties.TickPlacement // empty')"
[ -n "$ticks" ] || setup_fail "the driver gave no TickPlacement for the slider"
if [ "$ticks" = "None" ]; then
  fail "the Text size slider has TickPlacement None, so it shows no tick marks (screenshot in $OUT)"
fi
# Labels sit below the slider (the "100%" readout is above it, in the label row, and does not count): y after the slider's top,
# within 40 px of its bottom. Min and max are MinTextScale 0.9 and MaxTextScale 1.3 (UiPreferences).
missing="$(jq -r --arg id "$slider_id" '
  ([.. | objects | select((.id | tostring) == $id)][0]) as $s
  | if $s == null then "slider not in tree" else
    [ "90%", "100%", "130%" ] as $want
    | [ .. | objects | select(.type == "TextBlock" and .visible and .y > $s.y and .y <= ($s.y + $s.height + 40)) | .text ] as $found
    | [ $want[] | select(. as $w | $found | index($w) | not) ] | join(", ") end' "$OUT/tree2.json")"
if [ -n "$missing" ]; then
  fail "no tick labels under the Text size slider for: $missing (screenshot in $OUT)"
fi

# ---- Part 3: the workflows are approved through the driver (last: it changes state) --------------------------------------------
wf_id="$(wait_step "$wf_title, " 20)"
if [ -z "$wf_id" ]; then
  setup_fail "no '$wf_title' step in Settings"
fi
if [ -n "$(step_id "$wf_title, done")" ]; then
  setup_fail "the '$wf_title' step is already done before the click"
fi
"$DRIVER" action --node-id "$wf_id" --do expand >/dev/null 2>&1 || true
if ! "$DRIVER" wait-for --type Button --name "Approve all workflows" --state visible --wait-ms 30000 >/dev/null 2>&1; then
  fail "no 'Approve all workflows' button in the '$wf_title' step (screenshot in $OUT)"
elif ! "$DRIVER" click --type Button --name "Approve all workflows" >/dev/null 2>&1; then
  fail "the driver would not click 'Approve all workflows' (screenshot in $OUT)"
else
  approved=0
  for _ in $(seq 1 120); do
    if [ -n "$(step_id "$wf_title, done")" ]; then approved=1; break; fi
    sleep 0.5
  done
  [ "$approved" = 1 ] || fail "the '$wf_title' step never said done within 60 s of the click (screenshot in $OUT)"
fi
"$DRIVER" screenshot --out "$OUT/part3.png" >/dev/null 2>&1

if [ "$FAILED" = 0 ]; then
  echo "PASS: the Text size slider has ticks and labels, and the workflows approve through the driver (screenshots in $OUT)"
  exit 0
fi
exit 1
