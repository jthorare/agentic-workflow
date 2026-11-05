#!/usr/bin/env bash
# Tests for skills/formal-verify/scripts/tlc.sh: usage, fetch against a file:// lock (install, cache hit, sha256 refusal, bad
# lock), doctor and check refusing without java or the jar, a fake java (the -Xmx flag from TLC_HEAP, the out-of-memory
# line, TERM/INT/HUP to tlc.sh stopping the java it started), then the real thing: fetch the pinned jar from the repo's
# tla2tools.lock into a temp dir and run TLC on small models (a check-then-set mutex TLC must break, its fix, a starving
# liveness property named in several cfg layouts, relative paths, a deadlock, a parse error, a PlusCal lost update). The real part needs java 11+ and the network; without
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
# macOS with no JDK: /usr/bin/java is a stub whose `java -version` prints a message and exits 1 (#2096). Under pipefail that
# used to kill tlc.sh with exit 1 and no output; it is a missing java, exit 3 with the fix line.
mkdir -p "$TMPROOT/stubjava"
cat >"$TMPROOT/stubjava/java" <<'EOF3'
#!/bin/sh
echo "The operation couldn't be completed. Unable to locate a Java Runtime." >&2
exit 1
EOF3
chmod +x "$TMPROOT/stubjava/java"
PATH="$TMPROOT/stubjava:$OLDPATH" run doctor --jar "$TMPROOT/nowhere.jar"
expect "doctor treats a java stub that fails -version as missing java" 3 'java: missing'
printf '%s\n' "$OUT" | has -E -- '^  fix: ' && ok "doctor with the java stub prints the fix line" || fail "doctor with the java stub prints no fix line"
PATH="$TMPROOT/stubjava:$OLDPATH" run check "$TMPROOT/spec/M.tla"
expect "check refuses a java stub that fails -version, as not on PATH" 3 'java is not on PATH'
printf '%s\n' "$OUT" | has -E -- '^  fix: ' && ok "check with the java stub prints the fix line" || fail "check with the java stub prints no fix line"
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

# a fake java 21: argv lands in $FJ/args; FAKE_JAVA picks what it prints (pass | oom | hang)
FJ="$TMPROOT/fakejava"; mkdir -p "$FJ/bin"
cat >"$FJ/bin/java" <<'EOF2'
#!/usr/bin/env bash
if [ "$1" = -version ]; then echo 'openjdk version "21.0.2" 2024-01-16' >&2; exit 0; fi
printf '%s\n' "$@" >"$FJ/args"
case "${FAKE_JAVA:-pass}" in
  oom) echo 'Exception in thread "TLC Worker 1" java.lang.OutOfMemoryError: Java heap space'; exit 1 ;;
  hang) echo $$ >"$FJ/pid"; exec sleep 300 ;;
  *) echo 'Model checking completed. No error has been found.'
     echo '3 states generated, 2 distinct states found, 0 states left on queue.'
     echo 'The depth of the complete state graph search is 2.' ;;
esac
EOF2
chmod +x "$FJ/bin/java"; : >"$FJ/fake.jar"; export FJ
fj() { PATH="$FJ/bin:$OLDPATH" run check "$TMPROOT/spec/M.tla" --jar "$FJ/fake.jar"; }
fj
expect "check passes on the fake java" 0 '^tlc: PASS distinct=2 depth=2$'
has -x -- '-Xmx2g' "$FJ/args" && ok "java gets -Xmx2g by default" || fail "java args lack -Xmx2g: $(tr '\n' ' ' <"$FJ/args")"
TLC_HEAP=512m fj
has -x -- '-Xmx512m' "$FJ/args" && ok "TLC_HEAP=512m sets -Xmx512m" || fail "java args lack -Xmx512m: $(tr '\n' ' ' <"$FJ/args")"
for bad in 2gb g 0g -1g '2 g' 2x; do
  TLC_HEAP="$bad" fj
  expect "TLC_HEAP='$bad' is refused" 2 "TLC_HEAP wants"
done
FAKE_JAVA=oom fj
expect "an out-of-memory TLC is an ERROR naming OutOfMemoryError and TLC_HEAP" 2 '^tlc: ERROR .*java\.lang\.OutOfMemoryError: Java heap space.*TLC_HEAP=2g'
# a TERM, INT or HUP to tlc.sh alone (not its group) stops the java it started and removes its temp dir. Job control on:
# without it bash starts a background child with INT ignored, and an ignored-at-start signal can't be trapped. perl puts
# INT back to default before the exec: this suite itself starts with INT ignored when bash-tests.sh runs it in the background.
set -m
for sig in TERM INT HUP; do
  rm -f "$FJ/pid"; TT="$TMPROOT/tt-$sig"; mkdir -p "$TT"
  FAKE_JAVA=hang TMPDIR="$TT" PATH="$FJ/bin:$OLDPATH" perl -e '$SIG{INT} = "DEFAULT"; exec @ARGV' bash "$SCRIPT" check "$TMPROOT/spec/M.tla" --jar "$FJ/fake.jar" >/dev/null 2>&1 &
  tp=$!
  for _ in $(seq 1 300); do [ -s "$FJ/pid" ] && break; sleep 0.1; done
  jp="$(cat "$FJ/pid" 2>/dev/null || true)"
  kill -s "$sig" "$tp"
  set +e; wait "$tp"; RC=$?; set -e
  case "$sig" in TERM) want=143 ;; INT) want=130 ;; *) want=129 ;; esac
  if [ "$RC" -eq "$want" ]; then ok "SIG$sig: tlc.sh exits $want"; else fail "SIG$sig: tlc.sh exited $RC, want $want"; fi
  gone=1; for _ in $(seq 1 50); do kill -0 "$jp" 2>/dev/null || { gone=0; break; }; sleep 0.1; done
  if [ -n "$jp" ] && [ "$gone" = 0 ]; then ok "SIG$sig: the java it started is gone"; else fail "SIG$sig: java pid '$jp' survives"; [ -z "$jp" ] || kill "$jp" 2>/dev/null || true; fi
  if [ -z "$(ls -A "$TT")" ]; then ok "SIG$sig: no tlc-meta dir left"; else fail "SIG$sig: left $(ls "$TT")"; fi
done
set +m

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

    printf 'CONSTANT Procs = {p1, p2}\nSPECIFICATION Spec\nPROPERTIES\n    EveryoneRuns\n' >"$W/Starve.cfg"
    run check "$W/Starve.tla" --jar "$JAR"
    expect "a property name on the line after PROPERTIES is still named" 1 '^tlc: VIOLATION property EveryoneRuns$'
    printf 'CONSTANT Procs = {p1, p2}\nSPECIFICATION Spec\nPROPERTIES\n    \\* a comment\n    EveryoneRuns\nINVARIANT MutualExclusion\n' >"$W/Starve.cfg"
    run check "$W/Starve.tla" --jar "$JAR"
    expect "a comment line and a following INVARIANT section do not change the named property" 1 '^tlc: VIOLATION property EveryoneRuns$'
    { sed '/^====/d' "$W/Starve.tla" | sed 's/MODULE Starve/MODULE Starve2/'
      printf 'AlsoRuns == \\A p \\in Procs : pc[p] = "set" ~> pc[p] = "cs"\n====\n'; } >"$W/Starve2.tla"
    printf 'CONSTANT Procs = {p1, p2}\nSPECIFICATION Spec\nPROPERTIES\n    EveryoneRuns\n    AlsoRuns\n' >"$W/Starve2.cfg"
    run check "$W/Starve2.tla" --jar "$JAR"
    expect "two properties on separate lines are both counted" 1 'the config checks 2 properties'

    mkdir -p "$TMPROOT/other"
    cp "$W/Locked.witness.cfg" "$TMPROOT/other/w.cfg"
    pushd "$TMPROOT" >/dev/null
    run check models/Locked.tla --jar "$JAR"
    expect "a relative spec path finds its default config" 0 '^tlc: PASS'
    run check models/Locked.tla --config models/Locked.witness.cfg --jar "$JAR"
    expect "a relative --config resolves from the caller's directory" 1 'VIOLATION invariant NobodyInCS'
    run check models/Locked.tla --config other/w.cfg --jar "$JAR"
    expect "a relative --config in another folder than the spec resolves from the caller" 1 'VIOLATION invariant NobodyInCS'
    popd >/dev/null

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
