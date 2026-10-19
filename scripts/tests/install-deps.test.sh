#!/usr/bin/env bash
# Tests for scripts/install-deps.sh: the dry run prints each new step and writes nothing, a missing system package is
# installed through sudo + the package manager (brew on macOS) and checked again, AW_NO_SYSTEM_INSTALL=1 only prints the
# command, the AppArmor profile for bwrap is applied, and with everything present the checks pass. Runs on a PATH of symlinks to
# just the core tools plus fakes, so the host's real bwrap/uv/graphify never leak in.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$REPO/scripts/install-deps.sh"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/install-deps-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

BIN="$TMPROOT/bin"; mkdir -p "$BIN"
for t in bash sh jq grep sed awk head cat dirname basename uname mktemp find tr cut; do
  p="$(command -v "$t")" && ln -s "$p" "$BIN/$t"
done
fake() { printf '#!/bin/sh\n%s\n' "${2:-exit 0}" >"$BIN/$1"; chmod +x "$BIN/$1"; }

HOME_DIR="$TMPROOT/home"; mkdir -p "$HOME_DIR"
run() {   # <env assignments...> -- runs the script on the sandboxed PATH
  set +e; OUT="$(env -i PATH="$BIN" HOME="$HOME_DIR" AW_DEPS_ZLIB_H="$TMPROOT/zlib.h" "$@" bash "$SCRIPT" 2>&1)"; RC=$?; set -e
}
expect() {   # <label> <rc> [<regex>]...
  local label="$1" rc="$2"; shift 2
  if [ "$RC" -ne "$rc" ]; then fail "$label: exit $RC, want $rc"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  local re
  for re in "$@"; do
    printf '%s\n' "$OUT" | has -E -- "$re" || { fail "$label: output lacks /$re/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; }
  done
  ok "$label"
}

PIN="$(jq -r '.sindri.graphifyPin' "$REPO/sindri/package.json")"
DATE="$(jq -r '.sindri.graphifyPinDate' "$REPO/sindri/package.json")"

echo "install-deps"

LOG="$TMPROOT/calls.log"; : >"$LOG"
# Fakes: sudo logs and runs only the package manager (tee and apparmor_parser are logged, never run: they'd touch the real
# system); apt-get and brew log, and their `install` creates a fake command for each name in AW_FAKE_PROVIDES.
cat >"$BIN/sudo" <<EOF
#!/bin/sh
echo "sudo \$*" >>"$LOG"
case "\$1" in apt-get) exec "\$@" ;; apparmor_parser) : >"$TMPROOT/applied" ;; *) cat >/dev/null ;; esac
EOF
for m in apt-get brew; do
  cat >"$BIN/$m" <<EOF
#!/bin/sh
echo "$m \$*" >>"$LOG"
[ "\$1" = install ] || exit 0
for t in \${AW_FAKE_PROVIDES:-}; do printf '#!/bin/sh\nexit 0\n' >"$BIN/\$t"; $(command -v chmod) +x "$BIN/\$t"; done
EOF
done
chmod +x "$BIN/sudo" "$BIN/apt-get" "$BIN/brew"
# 1. Dry run on a bare Linux box: every step printed, the sudo line names every missing package, nothing is written.
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "dry run prints the new steps" 0 \
  "would install uv" \
  "would run: uv tool install graphifyy==$PIN --exclude-newer $DATE" \
  "would run: scripts/binaries-lock.sh fetch temporal\$" \
  "would run: scripts/binaries-lock.sh fetch temporal-test-server" \
  "bwrap: MISSING" "socat: MISSING" "secret-tool: MISSING" "systemd-run: MISSING" \
  "would run: sudo apt-get install -y bubblewrap socat libsecret-tools clang zlib1g-dev lsof tmux curl git"
[ -z "$(ls -A "$HOME_DIR")" ] && ok "dry run wrote nothing into HOME" || fail "dry run wrote: $(ls -A "$HOME_DIR")"

# 2. uv and graphify at the pin are recognised.
fake uv 'echo "uv 0.0.0"'
fake graphify "echo graphify $PIN"
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "installed uv and graphify at the pin are recognised" 0 "uv: already installed" "graphify: already installed \\($PIN\\)"

# 3. A graphify at another version is reinstalled.
fake graphify "echo graphify 0.0.1"
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "a graphify off the pin is reinstalled" 0 "would run: uv tool install graphifyy==$PIN"

# 4. Everything present: exit 0, no sudo line.
fake graphify "echo graphify $PIN"
for t in bwrap socat secret-tool clang lsof tmux curl git systemd-run; do fake "$t"; done
: >"$TMPROOT/zlib.h"
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "all system packages present" 0 "bwrap: found" "secret-tool: found"
printf '%s\n' "$OUT" | has "sudo apt-get" && fail "no sudo line when nothing is missing" || ok "no sudo line when nothing is missing"

# 4a. systemd-run: a working user scope is reported, a failing one is a warning that never fails the run (exit 0).
run AW_DEPS_OS=Linux AW_DRY_RUN=1 AW_DEPS_SYSTEMD_PROBE=true
expect "a working systemd user scope is reported" 0 "systemd-run: user scope works"
run AW_DEPS_OS=Linux AW_DRY_RUN=1 AW_DEPS_SYSTEMD_PROBE=false
expect "a missing systemd user manager only warns" 0 "systemd-run: found but no user scope \\(WARN"
# The default probe command itself: a fake systemd-run that succeeds only for exactly `--user --scope --quiet true`.
fake systemd-run 'echo "$*" >"'"$TMPROOT"'/sd-args"; [ "$*" = "--user --scope --quiet true" ]'
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "the default systemd probe is a quiet user scope running true" 0 "systemd-run: user scope works"
[ "$(cat "$TMPROOT/sd-args")" = "--user --scope --quiet true" ] && ok "the default probe ran systemd-run --user --scope --quiet true" || fail "the default probe ran systemd-run --user --scope --quiet true"
fake systemd-run
printf '%s\n' "$OUT" | has "sudo apt-get" && fail "no sudo line for socat when present" || ok "socat present: no install line"

# 4b. bwrap present but its sandbox fails with the AppArmor restriction on: the profile is written and loaded through
# sudo, then the sandbox is probed again (the fake probe passes once apparmor_parser ran).
fake bwrap
echo 1 >"$TMPROOT/userns"
PROBE="[ -e '$TMPROOT/applied' ] || { echo 'setting up uid map: Permission denied' >&2; false; }"
: >"$LOG"
run AW_DEPS_OS=Linux AW_DRY_RUN=1 AW_DEPS_BWRAP_PROBE="$PROBE" AW_DEPS_USERNS_FILE="$TMPROOT/userns"
expect "dry run shows the AppArmor profile and the commands" 0 "cannot create a sandbox \\(setting up uid map" "profile bwrap /usr/bin/bwrap flags=\\(unconfined\\)" "would run: sudo tee /etc/apparmor.d/bwrap; sudo apparmor_parser -r"
[ -s "$LOG" ] && fail "dry run ran sudo: $(cat "$LOG")" || ok "dry run runs nothing as root"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE="$PROBE" AW_DEPS_USERNS_FILE="$TMPROOT/userns" AW_STATE_DIR="$TMPROOT/nostate"
grep -q "^sudo tee /etc/apparmor.d/bwrap" "$LOG" && grep -q "^sudo apparmor_parser -r /etc/apparmor.d/bwrap" "$LOG" \
  && ok "a real run writes and loads the profile through sudo" || fail "profile not applied: $(cat "$LOG")"
printf '%s\n' "$OUT" | has "bwrap: sandbox works" && ok "the sandbox is probed again and works" \
  || { fail "no second probe"; printf '%s\n' "$OUT" | sed 's/^/      | /'; }
run AW_DEPS_OS=Linux AW_NO_SYSTEM_INSTALL=1 AW_DEPS_BWRAP_PROBE=false AW_DEPS_USERNS_FILE="$TMPROOT/userns"
expect "AW_NO_SYSTEM_INSTALL=1 prints the AppArmor fix and fails" 1 "nothing is applied" "sudo apparmor_parser -r /etc/apparmor.d/bwrap"
echo 0 >"$TMPROOT/userns"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=false AW_DEPS_USERNS_FILE="$TMPROOT/userns"
expect "a failing bwrap with the restriction off still fails, without the AppArmor advice" 1 "cannot create a sandbox"
printf '%s\n' "$OUT" | has "apparmor_parser" && fail "no AppArmor advice when the sysctl is 0" || ok "no AppArmor advice when the sysctl is 0"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=true AW_DRY_RUN=1
expect "a working bwrap probe passes" 0 "bwrap: sandbox works"

# 5. Real run with a package missing: installed through sudo apt-get (after an update), then checked again.
rm "$BIN/bwrap"; : >"$LOG"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=true AW_FAKE_PROVIDES=bwrap AW_STATE_DIR="$TMPROOT/nostate"
grep -q "^sudo apt-get update" "$LOG" && grep -q "^apt-get install -y bubblewrap$" "$LOG" \
  && ok "a missing package is installed with sudo apt-get" || fail "install not run: $(cat "$LOG")"
printf '%s\n' "$OUT" | has "Checking again" && printf '%s\n' "$OUT" | has "bwrap: found" \
  && ok "the packages are checked again after the install" || { fail "no re-check"; printf '%s\n' "$OUT" | sed 's/^/      | /'; }

# The temporal binaries are cached, so only the system packages decide the exit code in the runs below and in 7.
STATE="$TMPROOT/state"
V="$(awk '$1=="temporal"{print $2; exit}' "$REPO/binaries.lock")"; TV="$(awk '$1=="temporal-test-server"{print $2; exit}' "$REPO/binaries.lock")"
mkdir -p "$STATE/cache/bin/temporal-$V" "$STATE/cache/bin/temporal-test-server-$TV"
printf '#!/bin/sh\necho "temporal version %s"\n' "$V" >"$STATE/cache/bin/temporal-$V/temporal"
: >"$STATE/cache/bin/temporal-test-server-$TV/temporal-test-server"
chmod +x "$STATE"/cache/bin/*/*

# 5b. The install runs but a hard package is still missing: exit 1. A soft one (bubblewrap, socat) only warns: nothing needs them until the Sindri sandbox is wired in (SB-5a).
rm "$BIN/tmux"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=true
expect "a package still missing after the install fails" 1 "still missing after the install: tmux"
fake tmux
rm "$BIN/bwrap" "$BIN/socat"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=true AW_STATE_DIR="$STATE"
expect "bubblewrap and socat still missing after the install only warn" 0 "still missing after the install: bubblewrap socat" "setup goes on"
fake bwrap; fake socat

# 5c. AW_NO_SYSTEM_INSTALL=1: nothing installed, the exact command printed; exit 1 for a hard package, a warning for the soft ones.
: >"$LOG"
rm "$BIN/tmux"
run AW_DEPS_OS=Linux AW_NO_SYSTEM_INSTALL=1 AW_DEPS_BWRAP_PROBE=true
expect "AW_NO_SYSTEM_INSTALL=1 prints the command and fails for a hard package" 1 "sudo apt-get install -y tmux"
grep -q "install" "$LOG" && fail "AW_NO_SYSTEM_INSTALL=1 still installed: $(cat "$LOG")" || ok "AW_NO_SYSTEM_INSTALL=1 installs nothing"
fake tmux
rm "$BIN/bwrap"
run AW_DEPS_OS=Linux AW_NO_SYSTEM_INSTALL=1 AW_DEPS_BWRAP_PROBE=true AW_STATE_DIR="$STATE"
expect "AW_NO_SYSTEM_INSTALL=1 with only bubblewrap missing prints the command and warns" 0 "sudo apt-get install -y bubblewrap" "setup goes on"
fake bwrap

# 6. macOS: no Linux-only packages are required.
run AW_DEPS_OS=Darwin AW_DRY_RUN=1
expect "macOS does not ask for bwrap" 0 "tmux: found"
printf '%s\n' "$OUT" | has "bwrap" && fail "macOS must not mention bwrap" || ok "macOS must not mention bwrap"

# 6b. macOS without tmux: brew installs it.
rm "$BIN/tmux"; : >"$LOG"
run AW_DEPS_OS=Darwin AW_FAKE_PROVIDES=tmux AW_STATE_DIR="$TMPROOT/nostate"
grep -q "^brew install tmux$" "$LOG" && ok "macOS installs a missing tmux with brew" || fail "brew not run: $(cat "$LOG")"
fake tmux

# 7. Temporal: a cached fetch is found in the cache layout sindri serve reads, and `temporal --version` is run.
fake bwrap
run AW_DEPS_OS=Linux AW_STATE_DIR="$STATE"
expect "temporal is found at cache/bin/temporal-<pin>/ and runs" 0 "temporal: .*/cache/bin/temporal-$V/temporal \\(temporal version $V\\)" "temporal-test-server: .*temporal-test-server-$TV"

# 8. A temporal that won't run is reported and fails the run.
printf '#!/bin/sh\necho broken >&2; exit 3\n' >"$STATE/cache/bin/temporal-$V/temporal"
run AW_DEPS_OS=Linux AW_STATE_DIR="$STATE"
expect "a temporal that won't run fails clearly" 1 "WARN: .*temporal does not run: broken"

echo ""
echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
