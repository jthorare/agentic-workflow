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
for t in bash sh jq grep sed awk head cat dirname basename uname mktemp find tr cut wc sha256sum shasum mkdir chmod; do
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
  "bwrap: MISSING" "secret-tool: MISSING" \
  "would run: sudo apt-get install -y bubblewrap libsecret-tools clang zlib1g-dev lsof tmux curl git"
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
for t in bwrap secret-tool clang lsof tmux curl git; do fake "$t"; done
: >"$TMPROOT/zlib.h"
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "all system packages present" 0 "bwrap: found" "secret-tool: found"
printf '%s\n' "$OUT" | has "sudo apt-get" && fail "no sudo line when nothing is missing" || ok "no sudo line when nothing is missing"

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

# 5b. The install runs but the command is still missing: exit 1.
rm "$BIN/bwrap"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=true
expect "a package still missing after the install fails" 1 "still missing after the install: bubblewrap"

# 5c. AW_NO_SYSTEM_INSTALL=1: nothing installed, the exact command printed, exit 1.
: >"$LOG"
run AW_DEPS_OS=Linux AW_NO_SYSTEM_INSTALL=1 AW_DEPS_BWRAP_PROBE=true
expect "AW_NO_SYSTEM_INSTALL=1 prints the command and fails" 1 "sudo apt-get install -y bubblewrap"
grep -q "install" "$LOG" && fail "AW_NO_SYSTEM_INSTALL=1 still installed: $(cat "$LOG")" || ok "AW_NO_SYSTEM_INSTALL=1 installs nothing"
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

# A code index graphify environment at the pinned version under <state>, so a real run installs nothing.
ci_env() {
  local d="$1/cache/code-index/graphify-$(bash "$REPO/scripts/code-index-lock.sh" get graphify-version)-$(bash "$REPO/scripts/code-index-lock.sh" get requirements-sha256 | cut -c1-12)"
  mkdir -p "$d/bin"
  printf '#!/bin/sh\necho graphify %s\n' "$(bash "$REPO/scripts/code-index-lock.sh" get graphify-version)" >"$d/bin/graphify"
  chmod +x "$d/bin/graphify"
}

# 7. Temporal: a cached fetch is found in the cache layout sindri serve reads, and `temporal --version` is run.
STATE="$TMPROOT/state"
V="$(awk '$1=="temporal"{print $2; exit}' "$REPO/binaries.lock")"; TV="$(awk '$1=="temporal-test-server"{print $2; exit}' "$REPO/binaries.lock")"
mkdir -p "$STATE/cache/bin/temporal-$V" "$STATE/cache/bin/temporal-test-server-$TV"
printf '#!/bin/sh\necho "temporal version %s"\n' "$V" >"$STATE/cache/bin/temporal-$V/temporal"
: >"$STATE/cache/bin/temporal-test-server-$TV/temporal-test-server"
chmod +x "$STATE"/cache/bin/*/*
ci_env "$STATE"
fake bwrap
run AW_DEPS_OS=Linux AW_STATE_DIR="$STATE"
expect "temporal is found at cache/bin/temporal-<pin>/ and runs" 0 "temporal: .*/cache/bin/temporal-$V/temporal \\(temporal version $V\\)" "temporal-test-server: .*temporal-test-server-$TV"

# 8. A temporal that won't run is reported and fails the run.
printf '#!/bin/sh\necho broken >&2; exit 3\n' >"$STATE/cache/bin/temporal-$V/temporal"
run AW_DEPS_OS=Linux AW_STATE_DIR="$STATE"
expect "a temporal that won't run fails clearly" 1 "WARN: .*temporal does not run: broken"

# 9. The code index pins (context/code-index.lock): the dry run prints the graphify environment and the model pull from the lock.
CI_LOCK="$REPO/scripts/code-index-lock.sh"
CI_VER="$(bash "$CI_LOCK" get graphify-version)"; CI_DATE="$(bash "$CI_LOCK" get graphify-date)"
CI_MODEL="$(bash "$CI_LOCK" get model)"; CI_ID="$(bash "$CI_LOCK" get model-digest | sed 's/^sha256://' | cut -c1-12)"
fake ollama 'exit 0'
run AW_DEPS_OS=Linux AW_DRY_RUN=1 AW_STATE_DIR="$TMPROOT/ci-state"
expect "dry run prints the code index graphify and model steps from the lock" 0 \
  "would run: uv venv .*/cache/code-index/graphify-$CI_VER-[0-9a-f]{12} && uv pip install .*--require-hashes -r .*/context/code-index-requirements.txt --exclude-newer $CI_DATE" \
  "would run: ollama pull $CI_MODEL \\(unless ollama list shows id $CI_ID\\)"
[ ! -e "$TMPROOT/ci-state" ] && ok "the dry run created no environment" || fail "the dry run created $TMPROOT/ci-state"

# A graphify environment already at the pinned version (and the temporal cache) so the real runs below touch no network.
CI_SHA12="$(bash "$CI_LOCK" get requirements-sha256 | cut -c1-12)"
mkdir -p "$TMPROOT/ci-state/cache/code-index/graphify-$CI_VER-$CI_SHA12/bin"
printf '#!/bin/sh\necho graphify %s\n' "$CI_VER" >"$TMPROOT/ci-state/cache/code-index/graphify-$CI_VER-$CI_SHA12/bin/graphify"
chmod +x "$TMPROOT/ci-state/cache/code-index/graphify-$CI_VER-$CI_SHA12/bin/graphify"

mkdir -p "$TMPROOT/ci-state/cache/bin/temporal-$V" "$TMPROOT/ci-state/cache/bin/temporal-test-server-$TV"
printf '#!/bin/sh\necho "temporal version %s"\n' "$V" >"$TMPROOT/ci-state/cache/bin/temporal-$V/temporal"
: >"$TMPROOT/ci-state/cache/bin/temporal-test-server-$TV/temporal-test-server"
chmod +x "$TMPROOT"/ci-state/cache/bin/*/*
run AW_DEPS_OS=Linux AW_DRY_RUN=1 AW_STATE_DIR="$TMPROOT/ci-state"
expect "an installed code index graphify is recognised" 0 "code-index graphify: already installed"

# 9b. The model is already pulled at the pinned digest: no pull. A different digest: pulled. The server down: a warning, not a failure.
fake ollama "[ \"\$1\" = list ] && { echo 'NAME ID SIZE MODIFIED'; echo '$CI_MODEL $CI_ID 274 MB 1 day ago'; }; [ \"\$1\" = pull ] && echo pulled >>\"$LOG\"; exit 0"
: >"$LOG"; run AW_DEPS_OS=Linux AW_STATE_DIR="$TMPROOT/ci-state"
expect "a model at the pinned digest is not pulled" 0 "ollama model: already pulled \\($CI_MODEL, $CI_ID\\)"
grep -q pulled "$LOG" && fail "pulled a model that was present" || ok "no pull when the digest matches"
# An ollama fake with a state: `list` shows the model at the id in $OLLAMA_ID_FILE, `pull` logs, exits <rc> and (on success) leaves <id after pull> there.
ollama_fake() {   # <listed id> <id after pull> <pull exit code>
  echo "$1" >"$TMPROOT/ollama-id"
  fake ollama "[ \"\$1\" = list ] && { echo 'NAME ID SIZE MODIFIED'; echo \"$CI_MODEL \$(cat '$TMPROOT/ollama-id') 274 MB 1 day ago\"; exit 0; }
[ \"\$1\" = pull ] && { echo \"pulled \$2\" >>\"$LOG\"; [ $3 -eq 0 ] && echo '$2' >'$TMPROOT/ollama-id'; exit $3; }
exit 0"
}
ollama_fake 000000000000 "$CI_ID" 0
: >"$LOG"; run AW_DEPS_OS=Linux AW_STATE_DIR="$TMPROOT/ci-state"
expect "a model at another digest is pulled and re-checked" 0 "ollama model: pulled \\($CI_MODEL, $CI_ID\\)"
grep -q "pulled $CI_MODEL" "$LOG" && ok "ollama pull ran with the lock's model name" || fail "no pull: $(cat "$LOG")"
ollama_fake 000000000000 111111111111 0
run AW_DEPS_OS=Linux AW_STATE_DIR="$TMPROOT/ci-state"
expect "a pull that leaves another id (the tag moved) fails and names both ids" 1 "WARN: .*$CI_MODEL.*111111111111.*$CI_ID"
printf '%s\n' "$OUT" | has "ollama model: pulled" && fail "said pulled for the wrong id" || ok "never says pulled for the wrong id"
ollama_fake 000000000000 "$CI_ID" 1
run AW_DEPS_OS=Linux AW_STATE_DIR="$TMPROOT/ci-state"
expect "a failed ollama pull fails the run" 1 "WARN: ollama pull $CI_MODEL failed"
fake ollama 'echo "connection refused" >&2; exit 1'
run AW_DEPS_OS=Linux AW_STATE_DIR="$TMPROOT/ci-state"
expect "an Ollama server that is down warns" 0 "the Ollama server does not answer"
rm "$BIN/ollama"
run AW_DEPS_OS=Linux AW_DRY_RUN=1 AW_STATE_DIR="$TMPROOT/ci-state"
expect "no ollama skips the model with a warning" 0 "WARN: ollama not installed; skipping the embedding model $CI_MODEL"

# 9d. The real graphify install path, with fakes: a half-built environment is removed, `uv venv` and `uv pip install --require-hashes
# --exclude-newer` run with the lock's arguments, and the version is checked again. <mode>: ok | installfail | wrongver.
fresh_state() {   # <dir>: a state dir with the temporal cache only
  mkdir -p "$1/cache/bin/temporal-$V" "$1/cache/bin/temporal-test-server-$TV"
  cp "$TMPROOT/ci-state/cache/bin/temporal-$V/temporal" "$1/cache/bin/temporal-$V/temporal"
  : >"$1/cache/bin/temporal-test-server-$TV/temporal-test-server"; chmod +x "$1"/cache/bin/*/*
}
uv_fake() {
  fake uv "echo \"uv \$*\" >>\"$LOG\"
case \"\$1\" in
  venv) mkdir -p \"\$3/bin\"; printf '#!/bin/sh\\necho graphify %s\\n' '$([ "$1" = wrongver ] && echo 0.0.1 || echo "$CI_VER")' >\"\$3/bin/graphify\"; chmod +x \"\$3/bin/graphify\" ;;
  pip) [ '$1' = installfail ] && { echo 'hash mismatch' >&2; exit 1; } ;;
esac
exit 0"
}
ollama_fake "$CI_ID" "$CI_ID" 0
S="$TMPROOT/ci-fresh"; fresh_state "$S"
ENVDIR="$S/cache/code-index/graphify-$CI_VER-$CI_SHA12"
mkdir -p "$ENVDIR/lib"; : >"$ENVDIR/lib/half-built"
uv_fake ok; : >"$LOG"; run AW_DEPS_OS=Linux AW_STATE_DIR="$S"
expect "a half-built graphify environment is rebuilt" 0 "code-index graphify: installed \\($ENVDIR\\)"
[ ! -e "$ENVDIR/lib/half-built" ] && ok "the half-built environment was removed first" || fail "the half-built environment is still there"
grep -qF "uv venv --quiet $ENVDIR" "$LOG" && ok "uv venv ran on the environment path" || fail "no uv venv: $(cat "$LOG")"
grep -qF "uv pip install --quiet --python $ENVDIR/bin/python --require-hashes -r $REPO/context/code-index-requirements.txt --exclude-newer $CI_DATE" "$LOG" \
  && ok "uv pip install ran with --require-hashes, the requirements file and the lock's date" || fail "wrong install arguments: $(cat "$LOG")"

S="$TMPROOT/ci-fresh2"; fresh_state "$S"
uv_fake installfail; run AW_DEPS_OS=Linux AW_STATE_DIR="$S"
expect "a failing uv install fails the run" 1 "WARN: code-index graphify install failed"
S="$TMPROOT/ci-fresh3"; fresh_state "$S"
uv_fake wrongver; run AW_DEPS_OS=Linux AW_STATE_DIR="$S"
expect "an environment whose graphify is not the pin fails the run" 1 "WARN: code-index graphify install failed"
S="$TMPROOT/ci-fresh4"; fresh_state "$S"
rm "$BIN/uv"; run AW_DEPS_OS=Linux AW_STATE_DIR="$S"
expect "no uv fails the run" 1 "WARN: code-index graphify not installed \\(uv missing\\)"

# 9e. A lock that fails its check: the code index pins are skipped and the run fails. (A copy of the tree, the lock corrupted.)
COPY="$TMPROOT/tree"; mkdir -p "$COPY/context" "$COPY/sindri" "$COPY/config"
cp -R "$REPO/scripts" "$COPY/scripts"; cp -R "$REPO/config/lib" "$COPY/config/lib"; cp "$REPO/binaries.lock" "$COPY/"; cp "$REPO/sindri/package.json" "$COPY/sindri/"
cp "$REPO"/context/code-index* "$COPY/context/"; sed -i 's/^need disk-bytes.*/need disk-bytes 0/' "$COPY/context/code-index.lock"
S="$TMPROOT/ci-fresh5"; fresh_state "$S"; fake uv 'exit 0'; ollama_fake "$CI_ID" "$CI_ID" 0
set +e; OUT="$(env -i PATH="$BIN" HOME="$HOME_DIR" AW_DEPS_ZLIB_H="$TMPROOT/zlib.h" AW_DEPS_OS=Linux AW_STATE_DIR="$S" bash "$COPY/scripts/install-deps.sh" 2>&1)"; RC=$?; set -e
expect "a lock that fails its check skips the code index pins and fails" 1 "WARN: context/code-index.lock fails scripts/code-index-lock.sh check"
printf '%s\n' "$OUT" | has "code-index graphify" && fail "went on to install with a bad lock" || ok "nothing is installed from a bad lock"

echo ""
echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
