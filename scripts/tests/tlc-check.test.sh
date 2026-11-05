#!/usr/bin/env bash
# Tests for scripts/tlc-check.sh: a stub tlc.sh (TLC_SH) scripted per spec by a file covers the exits and the output;
# one real case runs TLC on specs/tla/LedgerProjector when java and the jar are present.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

SCRIPT="$(cd "$(dirname "$0")/.." && pwd)/tlc-check.sh"
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/tlc-check-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# The stub logs each call to $TMPROOT/calls and answers from $TMPROOT/script/<spec file name>.<cfg file name>:
# first line the exit code, the rest its output. No script file -> a PASS.
STUB="$TMPROOT/tlc-stub.sh"
cat >"$STUB" <<'S'
#!/usr/bin/env bash
spec="$2"; cfg="$4"
echo "$spec $cfg" >>"$TMPROOT/calls"
f="$TMPROOT/script/$(basename "$spec").$(basename "$cfg")"
if [ -f "$f" ]; then
  tail -n +2 "$f"; exit "$(head -n 1 "$f")"
fi
echo "stub output noise"
echo "tlc: PASS distinct=7 depth=3"
S
export TMPROOT

mkspec() {   # <root> <Name> [cfg...]: a spec folder with Name.tla and the given cfgs (default Name.cfg)
  local root="$1" name="$2"; shift 2
  mkdir -p "$root/$name"; : >"$root/$name/$name.tla"
  [ $# -gt 0 ] || set -- "$name.cfg"
  local c; for c in "$@"; do : >"$root/$name/$c"; done
}
script() {   # <spec file> <cfg> <rc> <output...>
  mkdir -p "$TMPROOT/script"
  local f="$TMPROOT/script/$1.$2" rc="$3"; shift 3
  { echo "$rc"; printf '%s\n' "$@"; } >"$f"
}
reset() {
  find "$TMPROOT/calls" "$TMPROOT/script" "$TMPROOT/specs" -mindepth 0 -delete 2>/dev/null || true
  mkdir -p "$TMPROOT/specs"
}
run() { set +e; OUT="$(TLC_SH="$STUB" bash "$SCRIPT" "$@" 2>&1)"; RC=$?; set -e; }
expect() {   # <label> <rc> [<regex>]
  if [ "$RC" -ne "$2" ]; then fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  if [ -n "${3:-}" ] && ! printf '%s\n' "$OUT" | has -E -- "$3"; then fail "$1: output lacks /$3/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  ok "$1"
}
lacks() {   # <label> <regex>
  if printf '%s\n' "$OUT" | has -E -- "$2"; then fail "$1: output has /$2/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; else ok "$1"; fi
}
ncalls() { if [ -f "$TMPROOT/calls" ]; then wc -l <"$TMPROOT/calls" | tr -d ' '; else echo 0; fi; }

S="$TMPROOT/specs"
echo "tlc-check"

reset; mkspec "$S" Alpha; mkspec "$S" Beta
run "$S/Alpha" "$S/Beta"
expect "all pass: exit 0 with one summary line per cfg" 0 '^tlc-check: Alpha/Alpha\.cfg: PASS distinct=7 depth=3$'
expect "all pass: the second spec has its line" 0 '^tlc-check: Beta/Beta\.cfg: PASS distinct=7 depth=3$'
lacks "a passing check hides TLC's output" 'stub output noise'

reset; mkspec "$S" Alpha; : >"$S/Alpha/MCAlpha.tla"
run "$S/Alpha"
expect "MC<Name>.tla is used when present" 0
if has -E -- 'MCAlpha\.tla ' "$TMPROOT/calls"; then ok "tlc.sh got MCAlpha.tla"; else fail "tlc.sh got MCAlpha.tla"; fi

reset; mkspec "$S" Alpha Alpha.cfg Alpha-big.cfg
run "$S/Alpha"
expect "two cfgs in one folder both run" 0 '^tlc-check: Alpha/Alpha-big\.cfg: PASS'
expect "two cfgs: the other cfg too" 0 '^tlc-check: Alpha/Alpha\.cfg: PASS'
if [ "$(ncalls)" = 2 ]; then ok "two cfgs: two tlc.sh calls"; else fail "two cfgs: $(ncalls) tlc.sh calls"; fi

reset; mkspec "$S" Alpha
script Alpha.tla Alpha.cfg 1 "Error: Invariant Safe is violated." "State 1: <Initial predicate>" "tlc: VIOLATION invariant Safe"
run "$S/Alpha"
expect "a violation exits 1 with the summary" 1 '^tlc-check: Alpha/Alpha\.cfg: VIOLATION invariant Safe$'
expect "a violation shows TLC's full output" 1 'State 1: <Initial predicate>'

reset; mkspec "$S" Alpha
script Alpha.tla Alpha.cfg 2 "parse error at line 3" "tlc: ERROR parse error at line 3"
run "$S/Alpha"
expect "an error exits 2" 2 '^tlc-check: Alpha/Alpha\.cfg: ERROR parse error at line 3$'

reset; mkspec "$S" Alpha
script Alpha.tla Alpha.cfg 3 "java 11 or newer is missing" "  fix: bash tlc.sh fetch"
run "$S/Alpha"
expect "exit 3 passes through with the fix line" 3 'fix: bash tlc\.sh fetch'

reset; mkspec "$S" Alpha; mkspec "$S" Beta; mkspec "$S" Gamma
script Alpha.tla Alpha.cfg 1 "tlc: VIOLATION deadlock"
script Beta.tla Beta.cfg 2 "tlc: ERROR boom"
run "$S/Alpha" "$S/Beta" "$S/Gamma"
expect "a violation and an error together: the error wins" 2
script Gamma.tla Gamma.cfg 3 "tlc: ERROR java missing"
run "$S/Alpha" "$S/Beta" "$S/Gamma"
expect "exit 3 beats an error and a violation" 3
if [ "$(ncalls)" = 6 ]; then ok "a failure doesn't stop the later specs"; else fail "ran $(ncalls) checks, want 6"; fi

reset; mkdir -p "$S/NoTla"; : >"$S/NoTla/NoTla.cfg"
run "$S/NoTla"
expect "a folder without <Name>.tla exits 2" 2 'no NoTla\.tla'

reset; mkdir -p "$S/NoCfg"; : >"$S/NoCfg/NoCfg.tla"
run "$S/NoCfg"
expect "a folder without a .cfg exits 2" 2 'no \.cfg'

reset; run "$S/Missing"
expect "a missing folder exits 2" 2 'no such folder'

reset; run --bogus
expect "a bad option exits 2" 2

reset; mkspec "$S" Alpha; mkspec "$S" Beta
run "$S/Beta"
if [ "$(ncalls)" = 1 ] && has -E -- 'Beta\.tla' "$TMPROOT/calls"; then ok "explicit dirs run only those"; else fail "explicit dirs ran: $(cat "$TMPROOT/calls")"; fi

# README line 1 "models: <path>[:<line>] ..." warnings: never change the exit code.
reset; mkspec "$S" Alpha
run "$S/Alpha"
expect "a missing README warns" 0 '^tlc-check: Alpha: warning: README line 1 is not models:$'

reset; mkspec "$S" Alpha; printf 'Alpha is a spec\nmodels: x\n' >"$S/Alpha/README.md"
run "$S/Alpha"
expect "a line 1 without models: warns" 0 '^tlc-check: Alpha: warning: README line 1 is not models:$'

reset; mkspec "$S" Alpha; : >"$S/real.cs"; mkdir -p "$S/realdir"
printf 'models: %s/real.cs:12 %s/nope.cs:3 %s/realdir/ %s/nodir/\n' "$S" "$S" "$S" "$S" >"$S/Alpha/README.md"
run "$S/Alpha"
expect "a missing path warns, line suffix stripped" 0 "^tlc-check: Alpha: warning: models path missing: $S/nope\\.cs\$"
expect "a missing folder path warns" 0 "warning: models path missing: $S/nodir/\$"
lacks "existing paths don't warn" 'missing: .*(real\.cs|realdir)'

reset; mkspec "$S" Alpha; : >"$S/real.cs"
printf 'models: %s/real.cs:12\n' "$S" >"$S/Alpha/README.md"
run "$S/Alpha"
expect "a good models: line passes" 0 '^tlc-check: Alpha/Alpha\.cfg: PASS'
lacks "a good models: line has no warning" 'warning'

# --root: another repo's specs/tla/ and models: paths resolve against it, not the script's checkout.
reset; R="$TMPROOT/otherrepo"; mkspec "$R/specs/tla" Rx; : >"$R/code.cs"
printf 'models: code.cs:4\n' >"$R/specs/tla/Rx/README.md"
run --root "$R"
expect "--root runs the folders under <dir>/specs/tla" 0 '^tlc-check: Rx/Rx\.cfg: PASS'
lacks "--root resolves models: paths against <dir>" 'warning'
run "$R/specs/tla/Rx"
expect "the same paths without --root warn" 0 'warning: models path missing: code\.cs'
run --root "$R" specs/tla/Rx
expect "--root: a relative spec-dir is taken from <dir>" 0 '^tlc-check: Rx/Rx\.cfg: PASS'
mkspec "$R/model" Mx; printf 'models: code.cs:9 %s/code.cs\n' "$R" >"$R/model/Mx/README.md"
rm -f "$TMPROOT/calls"
run --root "$R" specs/tla/Rx/ model/Mx/ "$R/model/Mx"
expect "--root: relative folders outside specs/tla (trailing /) and absolute ones all run" 0 '^tlc-check: Mx/Mx\.cfg: PASS'
lacks "--root: folder args' models: paths (relative and absolute) resolve" 'warning'
if [ "$(ncalls)" = 3 ]; then ok "--root: explicit folders run, not the specs/tla default"; else fail "--root explicit: $(ncalls) calls, want 3"; fi
run --root "$TMPROOT/nope"
expect "--root missing exits 2" 2 'tlc-check: --root'
: >"$TMPROOT/afile"; run --root "$TMPROOT/afile"
expect "--root a file exits 2" 2 'tlc-check: --root'

# The per-cfg line format is parsed by a Sindri step: pin it exactly.
reset; mkspec "$S" Alpha Alpha.cfg Alpha-big.cfg
run "$S/Alpha"
want="$(printf 'tlc-check: Alpha/Alpha-big.cfg: PASS distinct=7 depth=3\ntlc-check: Alpha/Alpha.cfg: PASS distinct=7 depth=3')"
if [ "$(printf '%s\n' "$OUT" | grep -v warning)" = "$want" ]; then ok "per-cfg line format is pinned (a Sindri step parses it)"; else fail "per-cfg line format changed"; fi

# gaps/ cfgs live in a subfolder and are not run.
reset; mkspec "$S" Alpha; mkdir -p "$S/Alpha/gaps"; : >"$S/Alpha/gaps/Gap.cfg"
run "$S/Alpha"
if [ "$(ncalls)" = 1 ]; then ok "gaps/ cfgs are skipped"; else fail "gaps/ cfgs ran: $(ncalls) calls"; fi

# --timeout
reset; mkspec "$S" Alpha
run --timeout 0 "$S/Alpha"; expect "--timeout 0 exits 2" 2 'tlc-check: --timeout'
run --timeout abc "$S/Alpha"; expect "--timeout abc exits 2" 2 'tlc-check: --timeout'
run --timeout "$S/Alpha"; expect "--timeout without a number exits 2" 2 'tlc-check: --timeout'
run --timeout 5 "$S/Alpha"
expect "--timeout: a fast check still passes" 0 '^tlc-check: Alpha/Alpha\.cfg: PASS distinct=7 depth=3$'

# A stub that ignores TERM and keeps a child: both pids must be gone after the group KILL.
HANG="$TMPROOT/hang-stub.sh"
cat >"$HANG" <<'S'
#!/usr/bin/env bash
trap '' TERM
sleep 300 &
echo "$$ $!" >"$TMPROOT/pids"
wait
S
reset; mkspec "$S" Alpha; rm -f "$TMPROOT/pids"
start=$SECONDS
set +e; OUT="$(TLC_SH="$HANG" bash "$SCRIPT" --timeout 1 "$S/Alpha" 2>&1)"; RC=$?; set -e
took=$((SECONDS - start))
expect "--timeout: a hung check prints TIMEOUT and counts as exit 2" 2 '^tlc-check: Alpha/Alpha\.cfg: TIMEOUT after 1 s$'
if [ "$took" -lt 10 ]; then ok "--timeout: finished in ${took}s"; else fail "--timeout: took ${took}s"; fi
p1=""; p2=""; read -r p1 p2 <"$TMPROOT/pids" || true
if [ -n "$p1" ] && ! kill -0 "$p1" 2>/dev/null && ! kill -0 "$p2" 2>/dev/null; then ok "--timeout: stub and its child are gone"; else fail "--timeout: pids $p1 $p2 survive"; fi

# No arguments: every specs/tla/* of the repo, sorted. A copy of the script in a scratch repo root sees only that repo's folders.
reset
FAKE="$TMPROOT/fakerepo"
mkdir -p "$FAKE/scripts" "$FAKE/specs/tla"
cp "$SCRIPT" "$FAKE/scripts/tlc-check.sh"
set +e; OUT="$(TLC_SH="$STUB" bash "$FAKE/scripts/tlc-check.sh" 2>&1)"; RC=$?; set -e
expect "no spec folders at all exits 2" 2 'no spec folders'
mkspec "$FAKE/specs/tla" Zed; mkspec "$FAKE/specs/tla" Alpha
set +e; OUT="$(TLC_SH="$STUB" bash "$FAKE/scripts/tlc-check.sh" 2>&1)"; RC=$?; set -e
expect "no arguments runs every folder under specs/tla" 0 '^tlc-check: Zed/Zed\.cfg: PASS'
first="$(head -n 1 "$TMPROOT/calls" | sed 's|.*/\([A-Za-z]*\)\.tla .*|\1|')"
if [ "$first" = Alpha ]; then ok "no arguments: sorted order"; else fail "no arguments: first spec was '$first'"; fi

# Real TLC, when this box has java and the pinned jar.
if bash "$REPO/skills/formal-verify/scripts/tlc.sh" doctor >/dev/null 2>&1; then
  set +e; OUT="$(bash "$SCRIPT" "$REPO/specs/tla/LedgerProjector" 2>&1)"; RC=$?; set -e
  expect "real TLC passes specs/tla/LedgerProjector" 0 'tlc-check: LedgerProjector/.*PASS'
else
  echo "  skip real TLC case: java or tla2tools.jar missing (bash skills/formal-verify/scripts/tlc.sh doctor)"
fi

echo
echo "tlc-check: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
