#!/usr/bin/env bash
# Tests for skills/formal-verify/scripts/tlc.sh: usage, fetch against a file:// lock (install, cache hit, sha256 refusal, bad
# lock), doctor and check refusing without java or the jar, then the real thing: fetch the pinned jar from the repo's
# tla2tools.lock into a temp dir and run TLC on small models (a check-then-set mutex TLC must break, its fix, a starving
# liveness property, a deadlock, a parse error, a PlusCal lost update). The real part needs java 11+ and the network; without
# either it prints SKIP and passes.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$REPO/skills/formal-verify/scripts/tlc.sh"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/formal-verify-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT
export HOME="$TMPROOT/home" AW_STATE_DIR="$TMPROOT/state"
mkdir -p "$HOME"

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { set +e; OUT="$(bash "$SCRIPT" "$@" 2>&1)"; RC=$?; set -e; }
expect() {   # <label> <rc> [<regex>]
  if [ "$RC" -ne "$2" ]; then fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  if [ -n "${3:-}" ] && ! printf '%s\n' "$OUT" | has -E -- "$3"; then fail "$1: output lacks /$3/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  ok "$1"
}
sha_of() { { shasum -a 256 "$1" 2>/dev/null || sha256sum "$1"; } | awk '{print $1}'; }

echo "formal-verify tlc.sh"

run
expect "no command is a usage error" 2 'Usage: tlc.sh doctor'
run check
expect "check without a spec is a usage error" 2
run frobnicate
expect "an unknown command is a usage error" 2
run check x.tla --workers two
expect "a non-number --workers is refused" 2 'wants a number'

# fetch against a local file:// jar
printf 'not really a jar\n' >"$TMPROOT/fake.jar"
GOOD="$(sha_of "$TMPROOT/fake.jar")"
printf '# a comment\n9.9.9  %s  file://%s\n' "$GOOD" "$TMPROOT/fake.jar" >"$TMPROOT/good.lock"
run fetch --lock "$TMPROOT/good.lock" --dest "$TMPROOT/cache"
expect "fetch installs the jar under <dest>/tla2tools-<version>/" 0 "^$TMPROOT/cache/tla2tools-9.9.9/tla2tools.jar$"
cmp -s "$TMPROOT/fake.jar" "$TMPROOT/cache/tla2tools-9.9.9/tla2tools.jar" && ok "the installed jar is the downloaded file" \
  || fail "the installed jar differs from the source"
mv "$TMPROOT/fake.jar" "$TMPROOT/fake.moved"
run fetch --lock "$TMPROOT/good.lock" --dest "$TMPROOT/cache"
expect "an installed jar is kept (no download)" 0 "tla2tools-9.9.9/tla2tools.jar$"
mv "$TMPROOT/fake.moved" "$TMPROOT/fake.jar"

printf '9.9.8  %s  file://%s\n' "$(printf '0%.0s' $(seq 64))" "$TMPROOT/fake.jar" >"$TMPROOT/bad-sha.lock"
run fetch --lock "$TMPROOT/bad-sha.lock" --dest "$TMPROOT/cache"
expect "a sha256 mismatch is refused" 1 'sha256 mismatch'
if [ -z "$(find "$TMPROOT/cache/tla2tools-9.9.8" -type f 2>/dev/null)" ]; then ok "a refused download leaves no jar or partial file"
else fail "a refused download left $(find "$TMPROOT/cache/tla2tools-9.9.8" -type f)"; fi

printf '9.9.7  nothex  file:///x\n' >"$TMPROOT/bad-row.lock"
run fetch --lock "$TMPROOT/bad-row.lock" --dest "$TMPROOT/cache"
expect "a malformed lock row is refused" 2 "wants one row 'version sha256 url'"
run fetch --lock "$TMPROOT/missing.lock"
expect "a missing lock is refused" 2 'cannot read'

# java and the jar missing
mkdir -p "$TMPROOT/fakebin" "$TMPROOT/spec"
printf '#!/bin/sh\necho '"'"'openjdk version "1.8.0_402"'"'"' >&2\n' >"$TMPROOT/fakebin/java"
chmod +x "$TMPROOT/fakebin/java"
printf -- '---- MODULE M ----\nVARIABLE x\nInit == x = 0\nNext == x'"'"' = x\n====\n' >"$TMPROOT/spec/M.tla"
printf 'INIT Init\nNEXT Next\n' >"$TMPROOT/spec/M.cfg"
OLDPATH="$PATH"
PATH="$TMPROOT/fakebin:$OLDPATH" run doctor
expect "doctor flags java 8 as too old, with the fix" 3 'java: 8, too old'
PATH="$TMPROOT/fakebin:$OLDPATH" run check "$TMPROOT/spec/M.tla"
expect "check refuses java 8" 3 'java 8 is too old'
run doctor --jar "$TMPROOT/nowhere.jar"
if command -v java >/dev/null 2>&1; then
  expect "doctor reports a missing jar with the fetch line" 3 'tlc.sh fetch'
else
  expect "doctor reports missing java" 3 'java: missing'
fi
run check "$TMPROOT/spec/Nope.tla"
expect "check refuses a missing spec" 2 'no spec at'
printf -- '---- MODULE N ----\n====\n' >"$TMPROOT/spec/N.tla"
run check "$TMPROOT/spec/N.tla"
expect "check refuses a spec without a config" 2 'no config at'

# the real jar and TLC
major="$(java -version 2>&1 | awk -F'"' '/version/ { split($2, v, "."); print (v[1] == "1" ? v[2] : v[1]); exit }' || true)"
if [ -z "$major" ] || [ "$major" -lt 11 ]; then
  echo "  SKIP real TLC: java 11 or newer is not on PATH"
else
  set +e; JAR="$(bash "$SCRIPT" fetch --dest "$TMPROOT/real" 2>"$TMPROOT/fetch.err")"; rc=$?; set -e
  if [ "$rc" -eq 2 ] && has 'download failed' "$TMPROOT/fetch.err"; then
    echo "  SKIP real TLC: the pinned jar could not be downloaded ($(head -1 "$TMPROOT/fetch.err"))"
  elif [ "$rc" -ne 0 ]; then
    fail "fetching the pinned jar from tla2tools.lock: exit $rc"; sed 's/^/      | /' "$TMPROOT/fetch.err"
  else
    ok "the pinned jar downloads and matches tla2tools.lock's sha256"
    W="$TMPROOT/models"; mkdir -p "$W"
    cat >"$W/Race.tla" <<'EOF'
---- MODULE Race ----
CONSTANT Procs
VARIABLES pc, held
vars == <<pc, held>>
Init == pc = [p \in Procs |-> "idle"] /\ held = FALSE
Check(p) == pc[p] = "idle" /\ ~held /\ pc' = [pc EXCEPT ![p] = "set"] /\ UNCHANGED held
Set(p)   == pc[p] = "set" /\ held' = TRUE /\ pc' = [pc EXCEPT ![p] = "cs"]
Leave(p) == pc[p] = "cs" /\ held' = FALSE /\ pc' = [pc EXCEPT ![p] = "idle"]
Next == \E p \in Procs : Check(p) \/ Set(p) \/ Leave(p)
Spec == Init /\ [][Next]_vars
MutualExclusion == \A p, q \in Procs : (pc[p] = "cs" /\ pc[q] = "cs") => p = q
NobodyInCS == \A p \in Procs : pc[p] # "cs"
====
EOF
    printf 'CONSTANT Procs = {p1, p2}\nSPECIFICATION Spec\nINVARIANT MutualExclusion\n' >"$W/Race.cfg"
    run check "$W/Race.tla" --jar "$JAR"
    expect "TLC breaks the check-then-set mutex" 1 '^tlc: VIOLATION invariant MutualExclusion$'
    printf '%s\n' "$OUT" | has -E -- 'pc = \(p1 :> "cs" @@ p2 :> "cs"\)' && ok "the trace ends with both processes in the critical section" \
      || fail "the trace lacks the both-in-cs state"

    sed 's/MODULE Race/MODULE Locked/; s/^Check(p) == .*/Check(p) == pc[p] = "idle" \/\\ ~held \/\\ held'"'"' = TRUE \/\\ pc'"'"' = [pc EXCEPT ![p] = "set"]/; s/^Set(p)   == .*/Set(p)   == pc[p] = "set" \/\\ pc'"'"' = [pc EXCEPT ![p] = "cs"] \/\\ UNCHANGED held/' \
      "$W/Race.tla" >"$W/Locked.tla"
    cp "$W/Race.cfg" "$W/Locked.cfg"
    run check "$W/Locked.tla" --jar "$JAR"
    expect "the atomic test-and-set passes" 0 '^tlc: PASS distinct=[1-9][0-9]* depth=[0-9]+$'
    printf 'CONSTANT Procs = {p1, p2}\nSPECIFICATION Spec\nINVARIANT NobodyInCS\n' >"$W/Locked.witness.cfg"
    run check "$W/Locked.tla" --config "$W/Locked.witness.cfg" --jar "$JAR"
    expect "its witness is violated (the critical section is reachable)" 1 'VIOLATION invariant NobodyInCS'

    { sed '/^====/d' "$W/Locked.tla" | sed 's/MODULE Locked/MODULE Starve/'
      printf 'EveryoneRuns == \\A p \\in Procs : pc[p] = "idle" ~> pc[p] = "cs"\n====\n'; } >"$W/Starve.tla"
    printf 'CONSTANT Procs = {p1, p2}\nSPECIFICATION Spec\nPROPERTY EveryoneRuns\n' >"$W/Starve.cfg"
    run check "$W/Starve.tla" --jar "$JAR"
    expect "a liveness property without fairness fails, named from the config" 1 '^tlc: VIOLATION property EveryoneRuns$'

    printf -- '---- MODULE Stop ----\nEXTENDS Naturals\nVARIABLE x\nInit == x = 0\nNext == x < 2 /\\ x'"'"' = x + 1\nSpec == Init /\\ [][Next]_x\n====\n' >"$W/Stop.tla"
    printf 'SPECIFICATION Spec\n' >"$W/Stop.cfg"
    run check "$W/Stop.tla" --jar "$JAR"
    expect "a model that stops is a deadlock" 1 '^tlc: VIOLATION deadlock reached$'
    run check "$W/Stop.tla" --no-deadlock --jar "$JAR"
    expect "--no-deadlock lets it pass" 0 '^tlc: PASS'

    printf -- '---- MODULE Broken ----\nVARIABLE x\nInit == x = \n====\n' >"$W/Broken.tla"
    printf 'INIT Init\nNEXT Init\n' >"$W/Broken.cfg"
    run check "$W/Broken.tla" --jar "$JAR"
    expect "a parse error is an ERROR with the parser's line" 2 '^tlc: ERROR Encountered'

    cat >"$W/Count.tla" <<'EOF'
---- MODULE Count ----
EXTENDS Naturals
CONSTANT N
(* --algorithm count
variables count = 0;
process w \in 1..N
variable tmp = 0;
begin
  Read: tmp := count;
  Write: count := tmp + 1;
end process;
end algorithm; *)
\* BEGIN TRANSLATION
\* END TRANSLATION
NoLostUpdate == (\A p \in 1..N : pc[p] = "Done") => count = N
====
EOF
    printf 'CONSTANT N = 2\nSPECIFICATION Spec\nINVARIANT NoLostUpdate\n' >"$W/Count.cfg"
    run translate "$W/Count.tla" --jar "$JAR"
    expect "PlusCal translates in place" 0
    has -E -- '^Spec == ' "$W/Count.tla" && ok "the translation defines Spec" || fail "the translation has no Spec"
    run check "$W/Count.tla" --jar "$JAR"
    expect "TLC finds the PlusCal lost update" 1 'VIOLATION invariant NoLostUpdate'

    left="$(find "$W" -type f ! -name '*.tla' ! -name '*.cfg' | sort)"
    [ -z "$left" ] && ok "check and translate leave only .tla and .cfg files next to the spec" || fail "left behind: $left"
  fi
fi

echo
echo "formal-verify tlc.sh: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
