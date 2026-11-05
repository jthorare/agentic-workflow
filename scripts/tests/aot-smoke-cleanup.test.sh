#!/usr/bin/env bash
# src/Sindri/aot-smoke/run.sh leaves no serve and no temporal dev server running when it exits (#2109).
#
# The contract this suite holds run.sh to (the fix is written against it):
#   - run.sh defines remember_servers: it reads "$AW_SINDRI_HOME/control.json" (.pid = serve, .temporal.pid = temporal) and
#     records both pids under "$work", so cleanup can stop them even after control.json is gone. It APPENDS, never
#     overwrites: every serve start's pids stay recorded. run.sh calls it after every serve start.
#   - cleanup() stops the recorded servers FIRST (SIGTERM, a short bounded wait, SIGKILL) and only then may try `sindri
#     stop`, all within a gate cancel's grace (SIGTERM, then SIGKILL after 45 s by default), before it deletes the scratch tree. A
#     hanging `stop` must not delay it (scenario 7).
#   - cleanup only signals a recorded pid whose command line (/proc/<pid>/cmdline) still names sindri or temporal, so a
#     reused pid is left alone (scenario 6).
#   - cleanup group-kills serve (`kill -KILL -- -<pgid>`) only when serve's pgid differs from the script's own pgid
#     (ps -o pgid= for $$); otherwise it signals the pid alone. The real serve runs in its own session.
#   - run.sh traps TERM, INT and HUP as well as EXIT (every top-level `trap` line that mentions cleanup is used here).
#
# The contract for #2116 (a run that was SIGKILLed, so no trap ran, left its serve and temporal running and its scratch
# dir on disk; the NEXT run reaps them at start):
#   - run.sh defines reap_stale (no arguments). For every dir matching "${TMPDIR:-/tmp}/aot-sindri-smoke.*" other than
#     "$scratch" it reads "$dir/owner" (line 1: the owning run.sh's pid; line 2: that pid's `ps -o lstart= -p <pid>`
#     output at write time). A missing or unreadable owner file: skip the dir. `ps -o lstart= -p <pid>` equal to line 2:
#     the owner is alive, skip. Otherwise: signal every pid in "$dir/run/servers.pids" whose command line still names
#     sindri or temporal (the cleanup rules: TERM, a short wait, KILL; a group signal when it leads its own group), then
#     delete the dir.
#   - run.sh writes "$scratch/owner" right after it creates $scratch, and calls reap_stale after that and before the
#     publish.
#   - the start time on line 2 is normalized: run.sh's `started <pid>` is `LC_ALL=C TZ=UTC ps -o lstart= -p <pid>` with blanks
#     squeezed and trimmed, used for the owner write and for the check, so a TZ or LC_TIME that differs between the two
#     runs doesn't make a live run look dead (scenario 15).
#   - reap_stale signals only pids whose command line also names aot-sindri-smoke (the smoke's serve is
#     <root>/artifacts/aot-sindri-smoke/bin/sindri, its temporal's --db-filename is under the scratch dir), because a pid
#     recorded days ago may now be the owner's real sindri or temporal (scenario 16). cleanup has no such extra condition.
#   - scenarios 8-14: 8 a killed run's servers and dir are reaped; 9 a live owner's are kept; 10 no owner file keeps
#     them; 11 a reused pid (not sindri or temporal) is left alone but the dir goes; 12 $scratch itself is never touched;
#     13 an owner pid reused by another process (different lstart) counts as dead; 14 the owner write and the reap_stale
#     call sit between mktemp -d and the first dotnet publish.
#
# The harness is built from run.sh WITHOUT running it: awk extracts every top-level function (cleanup, remember_servers,
# reap_stale, helpers a fix factors out) and the trap lines.
# The fakes are scripts named like the real ones, "$tmp/fakebin/sindri" (serve: own session via setsid, ignores TERM,
# has a child; stop: refused with exit 1, or hangs 60 s when FAKE_STOP_HANG=1) and "$tmp/fakebin/temporal" (own session).
# Only pids this suite started are ever signalled, by recorded pid.
if [ "$(uname -s)" != "Linux" ]; then echo "skip - aot-smoke-cleanup reads /proc, setsid and EPOCHREALTIME (Linux only)"; exit 0; fi
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN="$ROOT/src/Sindri/aot-smoke/run.sh"
tmp="$(mktemp -d)"
fail=0
recorded=() # every pid a fake wrote, for the EXIT trap
groups=()   # serve pids that lead their own group (setsid made them)

reap_all() {
  local p
  for p in ${groups[@]+"${groups[@]}"}; do
    [ "$(ps -o pgid= -p "$p" 2>/dev/null | tr -d ' ')" = "$p" ] && kill -KILL -- "-$p" 2>/dev/null
  done
  for p in ${recorded[@]+"${recorded[@]}"}; do kill -KILL "$p" 2>/dev/null; done
  find "$tmp" -delete
}
trap reap_all EXIT

check() { # name condition-exit-code
  if [ "$2" -eq 0 ]; then echo "ok - $1"; else echo "not ok - $1"; fail=1; fi
}
alive() { # pid: running and not a zombie
  local s
  s="$(sed -E 's/^[^)]*\) (.).*/\1/' "/proc/$1/stat" 2>/dev/null)" || return 1
  [ -n "$s" ] && [ "$s" != Z ]
}
gone_within() { # seconds pid...
  local end=$((SECONDS + $1)) p any
  shift
  while :; do
    any=0
    for p in "$@"; do alive "$p" && any=1; done
    [ "$any" -eq 0 ] && return 0
    [ "$SECONDS" -ge "$end" ] && return 1
    sleep 0.2
  done
}

# --- the harness: run.sh's cleanup, remember_servers and traps, nothing else
has_remember=1
grep -q '^remember_servers() {' "$RUN" || has_remember=0
has_reap=1
grep -q '^reap_stale() {' "$RUN" || has_reap=0
{
  echo 'set -u'
  echo "source '$ROOT/scripts/lib/with-tty.sh'"
  grep -m1 '^alarm=(' "$RUN"
  # every top-level function: `name() { ... }` on one line, or `name() {` through the next line that is just `}`
  awk '/^[A-Za-z_][A-Za-z0-9_]*\(\) \{/ { if ($0 ~ /\}[ \t]*$/) { print; next } p = 1 } p { print } p && /^}/ { p = 0 }' "$RUN"
  [ "$has_remember" -eq 1 ] || echo 'remember_servers() { :; }'
  [ "$has_reap" -eq 1 ] || echo 'reap_stale() { :; }'
  grep -E '^trap .*cleanup' "$RUN"
  cat <<'TAIL'
scratch="$H_SCRATCH"; work="$scratch/run"; bin="$H_BIN"; export AW_SINDRI_HOME="$work/home"
# H_CTLS: control files to copy to control.json and remember, in order; H_RM=1 removes control.json before cleanup
for f in ${H_CTLS:-}; do cp "$f" "$AW_SINDRI_HOME/control.json"; remember_servers; done
[ "${H_RM:-0}" = 1 ] && rm -f "$AW_SINDRI_HOME/control.json"
if [ "${H_REAP:-0}" = 1 ]; then trap - EXIT TERM INT HUP; reap_stale
elif [ "${H_SIGNAL:-0}" = 1 ]; then sleep 300 & echo $! >"$H_SLEEPPID"; wait; else cleanup; fi
TAIL
} >"$tmp/harness.sh"

mkdir -p "$tmp/fakebin"
cat >"$tmp/fakebin/sindri" <<'FAKE'
#!/usr/bin/env bash
case "${1:-}" in
  serve)
    trap "" TERM
    echo $$ >"$FAKE_DIR/serve.pid"
    sleep 300 & echo $! >"$FAKE_DIR/child.pid"
    wait ;;
  stop)
    [ -n "${FAKE_STOP_HANG:-}" ] || exit 1
    echo $$ >"$FAKE_DIR/stop.pid"
    sleep 60 & echo $! >"$FAKE_DIR/stopsleep.pid"
    wait ;;
esac
FAKE
cat >"$tmp/fakebin/temporal" <<'FAKE'
#!/usr/bin/env bash
echo $$ >"$FAKE_DIR/temporal.pid"
sleep 300 & echo $! >"$FAKE_DIR/tchild.pid"
trap 'kill $! 2>/dev/null; exit 143' TERM
wait
FAKE
chmod +x "$tmp/fakebin/sindri" "$tmp/fakebin/temporal"

# start_pair DIR: a fake serve and temporal in their own sessions; sets serve child temporal tchild and records them
start_pair() { # DIR [BINDIR]: the fakes run from BINDIR (default fakebin); the reap rows pass a dir named aot-sindri-smoke.*
  local d="$1" i fb="${2:-$tmp/fakebin}"
  mkdir -p "$d"
  FAKE_DIR="$d" setsid bash "$fb/sindri" serve </dev/null >/dev/null 2>&1 &
  FAKE_DIR="$d" setsid bash "$fb/temporal" server start-dev --db-filename "$d/temporal.db" </dev/null >/dev/null 2>&1 &
  for i in $(seq 50); do
    [ -s "$d/serve.pid" ] && [ -s "$d/child.pid" ] && [ -s "$d/temporal.pid" ] && [ -s "$d/tchild.pid" ] && break
    sleep 0.1
  done
  serve="$(cat "$d/serve.pid")"; child="$(cat "$d/child.pid")"; temporal="$(cat "$d/temporal.pid")"; tchild="$(cat "$d/tchild.pid")"
  recorded+=("$serve" "$child" "$temporal" "$tchild"); groups+=("$serve")
}
# write_control DIR SERVE TEMPORAL: control.json under DIR/scratch naming those pids
write_control() { # DIR SERVE TEMPORAL FILE
  mkdir -p "$1/scratch/run/home"
  printf '{"url":"http://127.0.0.1:1","pid":%s,"epoch":1,"temporal":{"pid":%s,"port":1,"uiPort":2}}\n' "$2" "$3" >"$4"
}

harness() { # dir [ENV=value...]: the harness in the foreground, bounded at 25 s
  local d="$1"; shift
  env H_SCRATCH="$d/scratch" H_BIN="$tmp/fakebin/sindri" H_SLEEPPID="$d/sleep.pid" FAKE_DIR="$d" "$@" \
    perl -e 'alarm 25; exec @ARGV' bash "$tmp/harness.sh" >/dev/null 2>&1
}

verdicts() { # label scratch-expected-gone(0|1) role=pid...: every pid shares one deadline
  local label="$1" scratch_gone="$2" rp
  shift 2
  local live=()
  for rp in "$@"; do live+=("${rp#*=}"); done
  gone_within 10 "${live[@]}"
  for rp in "$@"; do
    gone_within 0 "${rp#*=}"; check "$label: ${rp%%=*} (pid ${rp#*=}) is gone" $?
  done
  if [ "$scratch_gone" -eq 1 ]; then [ ! -e "$tmp/$label/scratch" ]; check "$label: the scratch dir is deleted" $?; fi
}

# 1. normal exit: serve ignores TERM, has a child
d="$tmp/normal"; start_pair "$d"; write_control "$d" "$serve" "$temporal" "$d/ctl.json"
harness "$d" H_CTLS="$d/ctl.json"
verdicts normal 1 "serve=$serve" "serve's child=$child" "temporal=$temporal" "temporal's child=$tchild"

# 2. control.json gone before cleanup
d="$tmp/nocontrol"; start_pair "$d"; write_control "$d" "$serve" "$temporal" "$d/ctl.json"
harness "$d" H_CTLS="$d/ctl.json" H_RM=1
[ "$has_remember" -eq 1 ]; check "nocontrol: run.sh defines remember_servers" $?
verdicts nocontrol 0 "serve=$serve" "serve's child=$child" "temporal=$temporal" "temporal's child=$tchild"

# 3. signal mid-run (perl resets the dispositions a background job inherits as ignored)
for sig in TERM INT; do
  d="$tmp/sig$sig"; start_pair "$d"; write_control "$d" "$serve" "$temporal" "$d/ctl.json"
  env H_SCRATCH="$d/scratch" H_BIN="$tmp/fakebin/sindri" H_SLEEPPID="$d/sleep.pid" FAKE_DIR="$d" H_CTLS="$d/ctl.json" H_SIGNAL=1 \
    perl -e '$SIG{INT}="DEFAULT"; $SIG{TERM}="DEFAULT"; $SIG{HUP}="DEFAULT"; exec @ARGV' bash "$tmp/harness.sh" >/dev/null 2>&1 &
  hpid=$!
  recorded+=("$hpid")
  for _ in $(seq 50); do [ -s "$d/sleep.pid" ] && break; sleep 0.1; done
  [ -s "$d/sleep.pid" ] && recorded+=("$(cat "$d/sleep.pid")")
  kill "-$sig" "$hpid" 2>/dev/null
  verdicts "sig$sig" 0 "serve=$serve" "serve's child=$child" "temporal=$temporal" "temporal's child=$tchild"
done

# 4. temporal on its own: serve already exited, control.json still names temporal
d="$tmp/temporal"; start_pair "$d"
kill -KILL -- "-$serve" 2>/dev/null   # the serve this suite started, its own group
gone_within 5 "$serve" "$child"
write_control "$d" "$serve" "$temporal" "$d/scratch/run/home/control.json"
harness "$d"
verdicts temporal-only 0 "temporal=$temporal" "temporal's child=$tchild"

# 5. every start is remembered: pair A, then pair B overwrites control.json, then control.json is removed
a="$tmp/pairA"; b="$tmp/pairB"
start_pair "$a"; sa="$serve"; ca="$child"; ta="$temporal"; tca="$tchild"
start_pair "$b"; sb="$serve"; cb="$child"; tb="$temporal"; tcb="$tchild"
write_control "$a" "$sa" "$ta" "$a/ctl.json"; write_control "$a" "$sb" "$tb" "$a/ctlB.json"
harness "$a" H_CTLS="$a/ctl.json $a/ctlB.json" H_RM=1
verdicts remembered 0 "pair A serve=$sa" "pair A serve's child=$ca" "pair A temporal=$ta" "pair A temporal's child=$tca" "pair B serve=$sb" "pair B serve's child=$cb" "pair B temporal=$tb" "pair B temporal's child=$tcb"

# 6. a reused pid is left alone: a plain sleep, no sindri or temporal in its command line, is what control.json names
d="$tmp/reuse"; mkdir -p "$d"
sleep 300 </dev/null >/dev/null 2>&1 &
bystander=$!; recorded+=("$bystander")
printf '{"url":"http://127.0.0.1:1","pid":%s,"epoch":1}\n' "$bystander" >"$d/ctl.json"
mkdir -p "$d/scratch/run/home"
harness "$d" H_CTLS="$d/ctl.json"
alive "$bystander"; check "reused-pid: a process that is not sindri or temporal is left alone (pid $bystander was signalled)" $?

# 7. cleanup is fast even when `stop` hangs: under 8 s, servers gone
d="$tmp/hang"; start_pair "$d"; write_control "$d" "$serve" "$temporal" "$d/ctl.json"
t0=$EPOCHREALTIME
env H_SCRATCH="$d/scratch" H_BIN="$tmp/fakebin/sindri" H_SLEEPPID="$d/sleep.pid" FAKE_DIR="$d" H_CTLS="$d/ctl.json" FAKE_STOP_HANG=1 \
  bash "$tmp/harness.sh" >/dev/null 2>&1 &
hpid=$!; recorded+=("$hpid")
gone_within 12 "$hpid"   # a harness still running after 12 s is a cleanup that hangs
elapsed="$(awk -v a="$t0" -v b="$EPOCHREALTIME" 'BEGIN { printf "%.1f", b - a }')"
alive "$hpid" && kill -KILL "$hpid" 2>/dev/null
sleep 0.5
for f in stop.pid stopsleep.pid; do [ -s "$d/$f" ] && recorded+=("$(cat "$d/$f")"); done
awk -v e="$elapsed" 'BEGIN { exit !(e < 8) }'; check "hung-stop: cleanup returned in under 8 s (took ${elapsed} s)" $?
verdicts hung-stop 0 "serve=$serve" "serve's child=$child" "temporal=$temporal" "temporal's child=$tchild"

# --- #2116: reap_stale. TMPDIR is a dir under the suite's tmp, never the real /tmp.
rtmp="$tmp/rtmp"; mkdir -p "$rtmp"
own="$rtmp/aot-sindri-smoke.own"

ustart() { LC_ALL=C TZ=UTC ps -o lstart= -p "$1" 2>/dev/null | tr -s ' ' | sed 's/^ //;s/ $//'; } # the normalized start time run.sh writes
smoke_fakes() { # name: copies of the fakes under the dir being reaped, so their command lines name aot-sindri-smoke; sets sfb
  sfb="$rtmp/aot-sindri-smoke.$1/bin"; mkdir -p "$sfb"; cp "$tmp/fakebin/sindri" "$tmp/fakebin/temporal" "$sfb/"
}
reap() { # scratch-dir [ENV=value...]: reap_stale in the harness with TMPDIR=$rtmp, bounded at 25 s
  local rs="$1"; shift
  env TMPDIR="$rtmp" "$@" H_REAP=1 H_SCRATCH="$rs" H_BIN="$tmp/fakebin/sindri" H_SLEEPPID="$tmp/reap.sleep" \
    perl -e 'alarm 25; exec @ARGV' bash "$tmp/harness.sh" >/dev/null 2>&1
}
stale_dir() { # name ownerpid ownerlstart pids...: "$rtmp/aot-sindri-smoke.NAME"; ownerpid "-" writes no owner file
  local sd="$rtmp/aot-sindri-smoke.$1" op="$2" ol="$3"
  shift 3
  mkdir -p "$sd/run"
  [ "$op" = - ] || printf '%s\n%s\n' "$op" "$ol" >"$sd/owner"
  printf '%s\n' "$@" >"$sd/run/servers.pids"
}
dead_owner() { # sets opid olstart: a process this suite started and SIGKILLed (reaped, so no zombie)
  sleep 300 </dev/null >/dev/null 2>&1 &
  opid=$!; recorded+=("$opid")
  olstart="$(ustart "$opid")"
  kill -KILL "$opid"; wait "$opid" 2>/dev/null
}
live_owner() { # sets opid olstart: a live process this suite started
  sleep 300 </dev/null >/dev/null 2>&1 &
  opid=$!; recorded+=("$opid")
  olstart="$(ustart "$opid")"
}
keep() { # label role=pid...: all alive after the reap
  local label="$1" rp
  shift
  for rp in "$@"; do alive "${rp#*=}"; check "$label: ${rp%%=*} (pid ${rp#*=}) is still running" $?; done
}
reaped() { # label dir-name: serve, its child and temporal gone within 8 s, the dir deleted
  local rp
  gone_within 8 "$serve" "$child" "$temporal"
  for rp in "serve=$serve" "serve's child=$child" "temporal=$temporal"; do
    gone_within 0 "${rp#*=}"; check "$1: ${rp%%=*} (pid ${rp#*=}) is gone" $?
  done
  [ ! -e "$rtmp/aot-sindri-smoke.$2" ]; check "$1: dir $2 is deleted" $?
}

# 8. killed run: owner dead, serve (with child) and temporal reaped, dir gone
smoke_fakes A; start_pair "$tmp/fk8" "$sfb"; dead_owner
stale_dir A "$opid" "$olstart" "$serve" "$temporal"
reap "$own"
reaped killed-run A

# 9. live run: owner alive with its real lstart, nothing touched
start_pair "$tmp/fk9"; live_owner
stale_dir B "$opid" "$olstart" "$serve" "$temporal"
reap "$own"
keep live-run "serve=$serve" "serve's child=$child" "temporal=$temporal"
[ -d "$rtmp/aot-sindri-smoke.B" ]; check "live-run: dir B is still there" $?

# 10. no owner file: skipped
start_pair "$tmp/fk10"
stale_dir C - - "$serve" "$temporal"
reap "$own"
keep no-owner "serve=$serve" "serve's child=$child" "temporal=$temporal"
[ -d "$rtmp/aot-sindri-smoke.C" ]; check "no-owner: dir C is still there" $?

# 11. reused pid: the owner is dead and servers.pids names a plain sleep; the sleep lives, the dir goes
sleep 300 </dev/null >/dev/null 2>&1 &
bystander11=$!; recorded+=("$bystander11")
dead_owner
stale_dir D "$opid" "$olstart" "$bystander11"
reap "$own"
alive "$bystander11"; check "reused-pid-reap: sleep (pid $bystander11) is left alone" $?
[ ! -e "$rtmp/aot-sindri-smoke.D" ]; check "reused-pid-reap: dir D is deleted" $?

# 12. own dir: $scratch matches the pattern, has a dead owner and live fakes, and is never touched
start_pair "$tmp/fk12"; dead_owner
mkdir -p "$own/run"; printf '%s\n%s\n' "$opid" "$olstart" >"$own/owner"; printf '%s\n' "$serve" "$temporal" >"$own/run/servers.pids"
reap "$own"
keep own-dir "serve=$serve" "serve's child=$child" "temporal=$temporal"
[ -f "$own/owner" ] && [ -f "$own/run/servers.pids" ]; check "own-dir: the scratch dir is untouched" $?

# 13. owner pid reused: line 1 is a live pid, line 2 is a different lstart -> dead -> reaped
smoke_fakes E; start_pair "$tmp/fk13" "$sfb"; live_owner
stale_dir E "$opid" "Mon Jan  1 00:00:00 1990" "$serve" "$temporal"
reap "$own"
reaped owner-reused E
alive "$opid"; check "owner-reused: the live process that reused the owner pid is left alone" $?

# 15. TZ and locale: the owner file is written under UTC, the reap runs under another TZ and LC_TIME -> still alive, kept
start_pair "$tmp/fk15"; live_owner
stale_dir G "$opid" "$olstart" "$serve" "$temporal"
reap "$own" TZ=America/Los_Angeles LC_TIME=de_DE.UTF-8
keep tz-differs "serve=$serve" "serve's child=$child" "temporal=$temporal"
[ -d "$rtmp/aot-sindri-smoke.G" ]; check "tz-differs: dir G is still there" $?

# 16. a dead owner's servers.pids names a live sindri serve and temporal with no aot-sindri-smoke in the command line
#     (the owner's real ones, by pid reuse): left alone, the dir is deleted
start_pair "$tmp/fk16"; dead_owner
stale_dir F "$opid" "$olstart" "$serve" "$temporal"
reap "$own"
keep foreign-sindri "serve=$serve" "serve's child=$child" "temporal=$temporal"
[ ! -e "$rtmp/aot-sindri-smoke.F" ]; check "foreign-sindri: dir F is deleted" $?

# 14. structural: the owner write and the reap_stale call sit between `mktemp -d` and the first `dotnet publish`
mk="$(grep -n 'mktemp -d' "$RUN" | head -1 | cut -d: -f1)"
ow="$(grep -n 'owner' "$RUN" | grep -v '^[0-9]*:[[:space:]]*#' | awk -F: -v m="${mk:-0}" '$1 > m { print $1; exit }')"
rc="$(grep -nE '^[[:space:]]*reap_stale([[:space:]]|$)' "$RUN" | head -1 | cut -d: -f1)"
pb="$(grep -n 'dotnet publish' "$RUN" | head -1 | cut -d: -f1)"
[ -n "$mk" ] && [ -n "$ow" ] && [ "$ow" -gt "$mk" ]; check "structure: run.sh writes \$scratch/owner after the mktemp -d line (mktemp ${mk:-none}, owner ${ow:-none})" $?
[ -n "$rc" ] && [ -n "$ow" ] && [ "$rc" -gt "$ow" ] && [ -n "$pb" ] && [ "$rc" -lt "$pb" ]; check "structure: run.sh calls reap_stale after the owner write and before dotnet publish (call ${rc:-none}, publish ${pb:-none})" $?

if [ "$fail" -eq 0 ]; then echo "PASS: aot-smoke-cleanup tests"; else echo "FAIL: aot-smoke-cleanup tests"; exit 1; fi
