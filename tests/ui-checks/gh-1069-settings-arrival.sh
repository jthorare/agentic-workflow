#!/usr/bin/env bash
# tests/ui-checks/gh-1069-settings-arrival.sh — frozen regression check for #1069 (UX-pass finding B-8, ui-ux-pass-2026-10-25).
# A changed profile, then Home > Continue (Approve your settings). On arrival in Settings: no red "This changed while you were reading
# it" line, the "Set up Sindri" title below the 48 px top bar, and every open step's body (and everything drawn in it) ending at least
# 12 px inside its card's right edge (the body had a 36 px left inset and none on the right, so the progress bar and the
# "approved" column ran to the card's edge).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh). Takes well under a minute.
#   bash tests/ui-checks/gh-1069-settings-arrival.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1069-settings-arrival-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1069-settings-arrival-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh-1069-settings-arrival-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1069-settings-arrival: setup failed: $* (screenshot in $OUT)" >&2
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
bash "$FIXTURE" drift >"$OUT/fixture-drift.log" 2>&1 || setup_fail "sindri-ui-fixture drift (see $OUT/fixture-drift.log)"

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
continue_btn=(--type Button --name "Continue, Approve your settings")

# 3. Home > Continue: the setup line names the profile step; it opens Settings at the checklist.
"$DRIVER" wait-for "${continue_btn[@]}" --state visible --wait-ms 90000 >/dev/null 2>&1 || setup_fail "no Continue on Home's setup line"
"$DRIVER" click "${continue_btn[@]}" >/dev/null 2>&1 || setup_fail "the Continue click was not accepted by the driver"
"$DRIVER" wait-for "${approve[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "no Approve settings button after Continue"
sleep 3   # the list's next reads land; a warning set by a refresh would show by now
"$DRIVER" screenshot --out "$OUT/arrival.png" >/dev/null 2>&1

# 4. No refusal on arrival: nothing was sent, so nothing changed under the reader.
changed="$("$DRIVER" search --type TextBlock --contains "changed while you were reading" 2>/dev/null | jq '[.matches[] | select(.visible)] | length')"
[ "$changed" = 0 ] || fail "Settings shows 'This changed while you were reading it' on arrival, before any approval (screenshot in $OUT)"

# 5. The title is in view, below the top bar.
title_y="$("$DRIVER" search --type TextBlock --text "Set up Sindri" 2>/dev/null | jq '[.matches[] | select(.visible) | .y][0] // -1')"
awk -v y="$title_y" 'BEGIN { exit !(y >= 48) }' || fail "the 'Set up Sindri' title is hidden under the top bar (y=$title_y) (screenshot in $OUT)"

# 6. Every open step's body stays inside its card: each visible element under a SetupStepBody ends 12 px or more inside the
#    card's (the step's Expander's) right edge.
"$DRIVER" tree --depth 64 >"$OUT/tree.json" 2>/dev/null || setup_fail "the driver gave no tree"
over="$(jq -r '
  [.. | objects | select(has("type"))] as $all
  | [$all[] | select(.type == "Expander" and .visible)] as $cards
  | [$all[] | select(.name == "SetupStepBody" and .visible)] as $bodies
  | [ $bodies[] as $b
      | ([$cards[] | select(.id as $c | $b.id | startswith($c + "/"))] | max_by(.id | length)) as $card
      | $all[] | select(.visible and .width > 0 and (.id as $i | $i == $b.id or ($i | startswith($b.id + "/"))))
      | select(.x + .width > $card.x + $card.width - 12)
      | "\(.type) \(.name // .text // "") ends at \(.x + .width), card edge \($card.x + $card.width)" ]
  | .[0] // empty' "$OUT/tree.json")"
[ -z "$over" ] || fail "a setup step's content runs to its card's right edge: $over (screenshot in $OUT)"

echo "PASS: Settings on arrival has no refusal line, its title in view and every step's content inside its card (screenshots in $OUT)"
