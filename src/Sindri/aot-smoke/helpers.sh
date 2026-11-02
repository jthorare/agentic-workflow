# Helpers of the Sindri host smoke (run.sh sources this; scripts/tests/aot-smoke-helpers.test.sh drives it with a fake sindri).
# The caller defines the globals `alarm`, `bin` and AW_SINDRI_HOME and the functions `bounded`, `with_tty` and `settle`.

exit_line() { # $1 = exit code
  local rc="$1"
  [ "$rc" -ne 142 ] || rc=124   # killed by the alarm
  [ "$rc" -eq 0 ] || echo "exit $rc"
}
human_run_service() { # $1 = service: `human services run`, retried while a scheduled run is in flight (SND-JOB-409; heartbeat
  # runs every 5 s, so the manual run can land on one). Bounded by wall time like settle; only the last attempt is printed
  echo "## sindri services run $1 (human, terminal)"
  local out rc deadline=$((SECONDS + 60))
  while :; do
    rc=0
    out="$(with_tty "${alarm[@]}" "$bin" services run "$1" </dev/null)" || rc=$?
    [[ "$out" == *SND-JOB-409* ]] && ((SECONDS < deadline)) || break
    sleep 0.5
  done
  [ -z "$out" ] || printf '%s\n' "$out"
  exit_line "$rc"
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
  local rc=0
  without_token bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  exit_line "$rc"
}
manual_service_runs() { # $1 = service: the human's manual run, then (after the views settle) a session's
  human_run_service "$1"
  settle
  session services run "$1"
}
