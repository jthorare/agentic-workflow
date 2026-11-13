# Helpers of the Sindri host smoke (run.sh sources this; scripts/tests/aot-smoke-helpers.test.sh drives it with a fake sindri).
# The caller defines the globals `alarm`, `bin` and AW_SINDRI_HOME and the functions `bounded`, `with_tty` and `settle`.

exit_line() { # $1 = exit code. Reads the caller's t0 and call (set with its rc): each call's wall time goes to timings.txt (#3456)
  local rc="$1" ms=""
  [ "$rc" -ne 142 ] || rc=124   # killed by the alarm
  if [ -n "${t0:-}" ]; then
    ms=$(( (${EPOCHREALTIME/./} - ${t0/./}) / 1000 ))
    [ ! -d "${out:-}" ] || printf '%s ms  exit %s  sindri %s\n' "$ms" "$rc" "${call:-}" >>"$out/timings.txt"
  fi
  [ "$rc" -eq 0 ] || echo "exit $rc"
  # A call the alarm stopped: say how long it ran, and where every call's time and serve's log are (only on a failing run)
  [ "$rc" -ne 124 ] || echo "timed out after $ms ms (every call's time: $out/timings.txt; serve's log: $out/serve.log)"
}
run_service_retried() { # $1 = header suffix, $2 = service, then the command: retried while a scheduled run is in flight (SND-JOB-409;
  # heartbeat runs every 5 s, so a manual run can land on one). Bounded by wall time like settle; only the last attempt is printed
  echo "## sindri services run $2 ($1)"
  local out rc deadline=$((SECONDS + 60))
  shift 2
  while :; do
    rc=0
    out="$("$@")" || rc=$?
    [[ "$out" == *SND-JOB-409* ]] && ((SECONDS < deadline)) || break
    sleep 0.5
  done
  [ -z "$out" ] || printf '%s\n' "$out"
  exit_line "$rc"
}
human_run_once() { with_tty "${alarm[@]}" "$bin" services run "$1" </dev/null; } # one attempt, stdout only
session_run_once() { without_token bounded "$bin" services run "$1" </dev/null 2>&1; } # one attempt, stdout and stderr
human_run_service() { # $1 = service: `human services run`, retried past SND-JOB-409
  run_service_retried "human, terminal" "$1" human_run_once "$1"
}
session_run_service() { # $1 = service: a session's `services run`, retried past SND-JOB-409 (stderr included: the 409 is there)
  run_service_retried "session" "$1" session_run_once "$1"
}
without_token() { # runs a command while the per-start token file is out of the way: the CLI can't read it, so it calls as a session
  local file="$AW_SINDRI_HOME/auth/human.token" rc=0
  mv "$file" "$file.aside"
  "$@" || rc=$?
  mv "$file.aside" "$file"
  return "$rc"
}
session() { # a CLI that can't read the token (an agent's environment changes nothing now), no terminal
  echo "## sindri $* (session)"
  local rc=0 t0=$EPOCHREALTIME call="$*"
  without_token bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  exit_line "$rc"
}
manual_service_runs() { # $1 = service: the human's manual run, then (after the views settle) a session's
  human_run_service "$1"
  settle
  session_run_service "$1"
}
