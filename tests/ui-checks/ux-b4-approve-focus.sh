#!/usr/bin/env bash
# tests/ui-checks/ux-b4-approve-focus.sh — frozen regression check for UX-pass finding B-4 (ui-ux-pass-2026-10-25).
# Approving the changed settings must leave keyboard focus somewhere: the Approve settings button is removed with focus in it,
# and every shortcut goes dead. Expect, within 2 s of profile.approved by human:ui, that the driver finds a focused element.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh). Takes well under a minute.
#   bash tests/ui-checks/ux-b4-approve-focus.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b4-approve-focus-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b4-approve-focus-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/ux-b4-approve-focus-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "ux-b4-approve-focus: setup failed: $* (screenshot in $OUT)" >&2
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
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"

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
bash "$FIXTURE" drift >/dev/null 2>&1 || setup_fail "sindri-ui-fixture drift"

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

approve=(--type Button --name "Approve settings")
# Settings opens by the Ctrl+, chord (ShellWindow.Overlays.cs OpenSettingsByKey). Nothing has focus in a fresh window, and a key
# with no selector fails then, so the chord goes to the window itself (node 0).
open_settings() {
  "$DRIVER" key --chord Ctrl+OemComma --node-id 0 >/dev/null 2>&1 || setup_fail "the Ctrl+, chord was not accepted by the window"
}


# 3. Settings shows the approval; click Approve settings (one click, no phrase) and wait for the ledger.
open_settings
"$DRIVER" wait-for "${approve[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "no Approve settings button in Settings (see $OUT/app.log)"
# The finding is about a keyboard user: a driver click invokes the button through its automation peer and never gives it focus, so the
# button would not be removed with focus in it. Put focus ON the button, check it landed, and press Enter on it.
"$DRIVER" action --do focus "${approve[@]}" >/dev/null 2>&1 || setup_fail "the Approve settings button could not take focus"
"$DRIVER" key --chord LeftShift >/dev/null 2>&1 || setup_fail "no element has focus after focusing the Approve settings button"
"$DRIVER" key --chord Enter >/dev/null 2>&1 || setup_fail "the Enter key was not accepted on the focused Approve settings button"
approved=0
for _ in $(seq 1 20); do
  ledger="$("$PROVE" human ledger --max 1000 2>&1 | tr -d "\r" | grep "profile.approved")"
  if printf "%s\n" "$ledger" | grep -q "human:ui"; then approved=1; break; fi   # the same line: the fixture's own approval is not by human:ui
  sleep 0.5
done
[ "$approved" = 1 ] || setup_fail "no profile.approved by human:ui in the ledger"

# 4. The driver has no focus query: a key with no selector goes to the focused element and fails with AW-UIDRIVER-002
#    "nothing has keyboard focus" when there is none. LeftShift alone changes nothing.
focused=0
for _ in $(seq 1 4); do
  if "$DRIVER" key --chord LeftShift >/dev/null 2>"$OUT/focus.err"; then focused=1; break; fi
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-approve.png" >/dev/null 2>&1
[ "$focused" = 1 ] || fail "nothing has focus after approve (screenshot in $OUT; $(tr "\n" " " <"$OUT/focus.err"))"

echo "PASS: focus is on an element after Approve settings (screenshots in $OUT)"
