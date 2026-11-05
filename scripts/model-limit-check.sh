#!/usr/bin/env bash
# scripts/model-limit-check.sh — the owner's model-limit check (#2112, model routing spec §12 "R-7") on fake harnesses, in a scratch home.
#
#   scripts/model-limit-check.sh up [--dry-run] [--no-ui] [--no-embeddings] [--no-graph]
#   scripts/model-limit-check.sh resume [--cli] [--dry-run]
#   scripts/model-limit-check.sh limit|unlimit <claude-code|codex> [--dry-run]
#   scripts/model-limit-check.sh status [--dry-run]
#   scripts/model-limit-check.sh down [--dry-run]
#
# up      1. Scratch home $AW_STATE_DIR/sindri-app-firstrun-modellimit (marker .first-run-scratch), port 7482.
#         2. Fake `claude` and `codex` in <home>/fake-bin, first on the PATH of everything this script starts (serve too). Each fake prints
#            its CLI's real usage-limit answer while <home>/model-limit/limited-<harness> exists (both do at first), and otherwise a well-formed
#            answer (labels "Fake feature <id>", cleanup keeps all, no decisions). Every call is one line in <home>/model-limit/calls.log.
#         3. scripts/context-first-run.sh up --stop-after 5 on that home and port, with `context.models.default.harnesses: [claude-code, codex]`
#            in the profile: preflight, profile, the approval stop (exit 3: approve, then run `up` again), serve, the first code index build.
#         4. `sindri context map refresh --detach --json`, then `sindri jobs show <job> --json` until the job is waiting on model-limit.
#         5. The Sindri app on the scratch home, and what to look for on its Jobs page.
# resume  claude-code comes back (codex stays limited), the job is resumed (by you in the app, or `sindri jobs resume <job>` with --cli),
#         and the script waits for the job to succeed, then shows it, the map's status and the fakes' answers.
# limit, unlimit  Put one fake at its usage limit, or take it off.
# status  The plane, the job, which fakes are limited and the last calls.
# down    scripts/context-first-run.sh down on the scratch home: stop the plane, close the app, delete the home.
#
# --dry-run prints every command and file it would write and runs nothing, writes nothing.
#
# Environment (all optional; the real home $AW_STATE_DIR/sindri-app is never read or used):
#   AW_STATE_DIR                 default ~/.agentic-workflow
#   MODEL_LIMIT_HOME             default $AW_STATE_DIR/sindri-app-firstrun-modellimit; its name must start with sindri-app-firstrun-modellimit
#   MODEL_LIMIT_PORT             default 7482 (7410 is refused: the real instance's port)
#   MODEL_LIMIT_POLL_SECONDS     seconds between job reads; default 10
#   MODEL_LIMIT_PAUSE_TIMEOUT    seconds `up` waits for the job to pause; default 1800
#   MODEL_LIMIT_JOB_TIMEOUT      seconds `resume` waits for the job to end; default 1800
#   SINDRI, AW_SINDRI_UI, FIRST_RUN_REPO_PATH, FIRST_RUN_REPO_NAME   as for scripts/context-first-run.sh
#
# Exit: 0 ok; 1 a step failed (the message names it); 2 usage or a refused setup; 3 stopped for you to approve the profile.
# Covers steps 1 and 2 of #2112. Step 3 (a source sync's decisions) needs a record feed with a real token and step 4 a Jev key: those stay
# the owner's run on the real box. Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

die()   { echo "model-limit-check: $*" >&2; exit 2; }
broke() { echo "model-limit-check: $*" >&2; exit 1; }
usage() { sed -n '4,8p' "$0" | sed 's/^# \{0,1\}//' >&2; exit 2; }

[ "$#" -ge 1 ] || usage
action="$1"; shift
case "$action" in up|resume|limit|unlimit|status|down) ;; *) usage ;; esac

harness=""
if [ "$action" = limit ] || [ "$action" = unlimit ]; then
  [ "$#" -ge 1 ] || usage
  case "$1" in claude-code|codex) harness="$1" ;; *) die "$action takes claude-code or codex (got '$1')" ;; esac
  shift
fi

dry=0 cli=0 pass=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run) dry=1 ;;
    --cli) [ "$action" = resume ] || usage; cli=1 ;;
    --no-ui|--no-embeddings|--no-graph) [ "$action" = up ] || usage; pass+=("$1") ;;
    *) echo "model-limit-check: unknown option $1" >&2; usage ;;
  esac
  shift
done

unset TMUX TMUX_PANE AW_SINDRI_PROFILE
export AW_SINDRI_APP_NO_MCP=1

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
home="${MODEL_LIMIT_HOME:-$state/sindri-app-firstrun-modellimit}"
port="${MODEL_LIMIT_PORT:-7482}"
poll="${MODEL_LIMIT_POLL_SECONDS:-10}"
pause_timeout="${MODEL_LIMIT_PAUSE_TIMEOUT:-1800}"
job_timeout="${MODEL_LIMIT_JOB_TIMEOUT:-1800}"
sindri_cmd="${SINDRI:-sindri}"
read -r -a SX <<<"$sindri_cmd"
self="scripts/model-limit-check.sh"
first_run="$root/scripts/context-first-run.sh"
fakes="$home/fake-bin"
lim="$home/model-limit"
job_file="$lim/job.id"

# ---- the guard: only ever a scratch home of this check's own ----
guard() {
  case "$home" in /*) ;; *) die "MODEL_LIMIT_HOME must be an absolute path (got '$home')" ;; esac
  case "$(basename "$home")" in
    sindri-app-firstrun-modellimit | sindri-app-firstrun-modellimit-*) ;;
    *) die "refusing $home: this check's home is named sindri-app-firstrun-modellimit or sindri-app-firstrun-modellimit-<name>" ;;
  esac
  [ "$home" != "$state/sindri-app" ] || die "refusing $home: that is the real Sindri home"
  [ ! -L "$home" ] || die "refusing $home: it is a link"
  if [ -e "$home" ] && [ ! -f "$home/.first-run-scratch" ] && [ -n "$(ls -A "$home" 2>/dev/null)" ]; then
    die "refusing $home: it exists, holds files, and has no .first-run-scratch marker (this looks like a real home)"
  fi
  case "$port" in '' | *[!0-9]*) die "MODEL_LIMIT_PORT must be a number (got '$port')" ;; esac
  [ "$port" != 7410 ] || die "refusing port 7410: that is the real instance's port"
  for n in "$poll" "$pause_timeout" "$job_timeout"; do
    case "$n" in '' | *[!0-9]*) die "MODEL_LIMIT_POLL_SECONDS, MODEL_LIMIT_PAUSE_TIMEOUT and MODEL_LIMIT_JOB_TIMEOUT are whole seconds (got '$n')" ;; esac
  done
}

# ---- running things (as in context-first-run.sh) ----
step()  { echo; echo "== $1"; }
note()  { echo "-- $*"; }
shown() { printf '%s' "PATH=$fakes:\$PATH AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port AW_SINDRI_APP_NO_MCP=1 $sindri_cmd"; }
sx() {
  if [ "$dry" = 1 ]; then echo "+ $(shown) $*"; return 0; fi
  PATH="$fakes:$PATH" AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" "${SX[@]}" "$@"
}
sx_out() {
  if [ "$dry" = 1 ]; then echo "+ $(shown) $*"; OUT=""; RC=0; return 0; fi
  OUT="$(PATH="$fakes:$PATH" AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" "${SX[@]}" "$@")"; RC=$?
}
check() { [ "$dry" != 1 ] || echo "  check: $*"; }
job_id() { if [ "$dry" = 1 ]; then printf '<job>'; else cat "$job_file" 2>/dev/null; fi; }

# ---- the fakes ----
# One script, two names: `claude` and `codex` (it reads its own name). Context model calls run with a minimal environment (PATH, HOME, ...),
# so a fake finds its flags and log from its own path, never from AW_SINDRI_HOME.
fake_script() {
  cat <<'FAKE'
#!/usr/bin/env bash
# The model-limit check's fake harness (scripts/model-limit-check.sh). Not a real CLI: never put it on a real PATH.
set -uo pipefail
name="$(basename "$0")"
dir="$(cd "$(dirname "$0")/../model-limit" && pwd)"
case "$name" in claude) harness=claude-code ;; codex) harness=codex ;; *) echo "fake harness: unknown name $name" >&2; exit 2 ;; esac
case " $* " in
  *" -p "*|" exec "*) ;;
  *) echo "fake-$name 0.0.0 (model-limit check)"; exit 0 ;;
esac
log() { printf '%s %s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$harness" "$*" >> "$dir/calls.log"; }
prompt="$(cat)"
if [ -e "$dir/limited-$harness" ]; then
  log "limit"
  if [ "$harness" = claude-code ]; then
    jq -cn '{type:"result",subtype:"success",is_error:true,result:"You'"'"'ve hit your session limit · resets 2:50am (America/Los_Angeles)"}'
  else
    msg="You've hit your usage limit. Upgrade to Pro (https://chatgpt.com/explore/pro), visit https://chatgpt.com/codex/settings/usage to purchase more credits or try again at 9:52 PM."
    jq -cn --arg m "$msg" '{type:"error",message:$m}'
    jq -cn --arg m "$msg" '{type:"turn.failed",error:{message:$m}}'
  fi
  exit 1
fi
# The answer by the prompt's own "Answer with JSON" line: kind (for the log) and text (the JSON the caller parses).
answer="$(printf '%s' "$prompt" | jq -Rsc '
  . as $p
  | def ids(re): [$p | split("\n")[] | capture("^(?<id>" + re + "): ") | .id];
  if $p | contains("Name each group of related source files") then
    {kind:"labeling", text:(ids("[^\\s:]+") | map({id:., label:("Fake feature " + .), labels:["Fake feature " + ., "Fake area " + ., "Fake part " + .]}))}
  elif $p | contains("For each numbered proposal") then {kind:"map-cleanup", text:(ids("n[0-9]+") | map({id:., keep:true}))}
  elif $p | contains("as one core concept") then
    {kind:"concept-naming", text:(ids("g[0-9]+") | map({id:., name:("Fake concept " + .), summary:"Named by the model-limit check fake."}))}
  elif $p | contains("\"decisions\"") then {kind:"decision-extraction", text:{decisions:[]}}
  elif $p | contains("\"concepts\"") then {kind:"concept-scan", text:{concepts:[]}}
  elif $p | contains("\"verdict\"") then {kind:"conflict", text:{verdict:"agrees"}}
  elif $p | contains("\"relevant\"") then {kind:"adjudication", text:(ids("n[0-9]+") | map({id:., relevant:false}))}
  elif $p | contains("\"summary\"") then {kind:"concept-synthesis", text:{name:"Fake concept", summary:"Named by the model-limit check fake."}}
  else {kind:"other", text:[]} end
  | .text |= tojson')" || { log "error"; echo "fake harness: jq failed" >&2; exit 1; }
log "answer $(printf '%s' "$answer" | jq -r .kind)"
in=$(( ${#prompt} / 4 + 1 ))
if [ "$harness" = claude-code ]; then
  printf '%s' "$answer" | jq -c --argjson in "$in" '{type:"result",subtype:"success",is_error:false,result:"",
    structured_output:{decision:"answered", text:.text}, usage:{input_tokens:$in, output_tokens:50}}'
else
  printf '%s' "$answer" | jq -c '{type:"item.completed", item:{id:"item_0", type:"agent_message", text:({decision:"answered", text:.text} | tojson)}}'
  jq -cn --argjson in "$in" '{type:"turn.completed", usage:{input_tokens:$in, cached_input_tokens:0, output_tokens:50}}'
fi
FAKE
}

write_fakes() {
  step "fake claude and codex in $fakes (both at their usage limit)"
  if [ "$dry" = 1 ]; then
    echo "+ write $fakes/claude and $fakes/codex (one script; limited while $lim/limited-<harness> exists; calls logged to $lim/calls.log)"
    echo "+ touch $lim/limited-claude-code $lim/limited-codex"
    return 0
  fi
  (umask 077 && mkdir -p "$fakes" "$lim") || broke "can't create $fakes"
  fake_script >"$fakes/claude" || broke "can't write $fakes/claude"
  chmod 700 "$fakes/claude"
  cp "$fakes/claude" "$fakes/codex" && chmod 700 "$fakes/codex" || broke "can't write $fakes/codex"
  if [ ! -f "$job_file" ]; then : >"$lim/limited-claude-code"; : >"$lim/limited-codex"; fi
  note "limited: $(limited_list)"
}

limited_list() {
  local h out=""
  for h in claude-code codex; do [ -e "$lim/limited-$h" ] && out="$out${out:+, }$h"; done
  printf '%s' "${out:-none}"
}

# ---- up ----
make_home() {
  step "scratch home $home (port $port)"
  if [ "$dry" = 1 ]; then echo "+ mkdir -p $home && chmod 700 $home && touch $home/.first-run-scratch"; return 0; fi
  (umask 077 && mkdir -p "$home") || broke "can't create $home"
  chmod 700 "$home"
  : >"$home/.first-run-scratch" || broke "can't mark $home"
}

setup() {
  step "context-first-run.sh up --stop-after 5 on the scratch home (profile, approval, serve, code index), PATH=$fakes:\$PATH so serve finds the fakes"
  local extra
  extra="$(printf '  models:\n    default: { harnesses: [claude-code, codex] }')"
  if [ "$dry" = 1 ]; then echo "  (FIRST_RUN_CONTEXT_EXTRA adds to the profile's context: section:)"; printf '%s\n' "$extra" | sed 's/^/    | /'; fi
  local args=(up --stop-after 5 ${pass[@]+"${pass[@]}"})
  [ "$dry" = 0 ] || args+=(--dry-run)
  PATH="$fakes:$PATH" FIRST_RUN_HOME="$home" FIRST_RUN_PORT="$port" FIRST_RUN_CONTEXT_EXTRA="$extra" "$first_run" "${args[@]}"
  local rc=$?
  case "$rc" in
    0) ;;
    3) echo >&2; echo "Then run:  $self up" >&2; exit 3 ;;
    *) broke "scripts/context-first-run.sh up exited $rc (see above); fix it, then run $self up again" ;;
  esac
  if [ "$dry" = 0 ]; then
    grep -q 'harnesses: \[claude-code, codex\]' "$home/profile/profile.yaml" 2>/dev/null \
      || broke "the profile in $home has no 'harnesses: [claude-code, codex]' (it was written before this check?); run $self down, then up"
  fi
}

# wait_job <what> <timeout> <jq test of the job>: poll `jobs show <job> --json` until the test is true; fail when the job ends first.
wait_job() {
  local what="$1" limit="$2" test="$3" waited=0 st
  if [ "$dry" = 1 ]; then
    sx jobs show "<job>" --json
    check "every $poll s for up to $limit s until $what ($test)"
    return 0
  fi
  while :; do
    sx_out jobs show "$(job_id)" --json
    [ "$RC" = 0 ] || broke "'jobs show $(job_id)' exited $RC; run: $(shown) jobs show $(job_id)"
    printf '%s' "$OUT" | jq -e "$test" >/dev/null 2>&1 && return 0
    st="$(printf '%s' "$OUT" | jq -r '"\(.state)\(if .waitingOn then " on " + .waitingOn else "" end)"' 2>/dev/null)"
    case "$st" in succeeded* | failed* | cancelled*) [ "$what" = "it succeeds" ] || broke "the job $(job_id) is $st before $what; run: $(shown) jobs show $(job_id)" ;; esac
    case "$st" in failed* | cancelled*) broke "the job $(job_id) is $st; run: $(shown) jobs show $(job_id)   (a job left from an earlier run? $self down, then up)" ;; esac
    [ "$waited" -lt "$limit" ] || broke "the job $(job_id) was still $st after ${waited}s, not $what; run: $(shown) jobs show $(job_id)"
    sleep "$poll"; waited=$((waited + poll))
    note "the job is $st (${waited}s)"
  done
}

start_refresh() {
  step "a map refresh with every harness at its limit"
  if [ "$dry" = 1 ]; then
    sx context map refresh --detach --json
    check "prints the JobStarted body; its .jobId is kept in $job_file"
    wait_job "it pauses" "$pause_timeout" '.state == "waiting" and .waitingOn == "model-limit"'
    return 0
  fi
  if [ -f "$job_file" ]; then
    note "the refresh job $(job_id) was started before"
  else
    sx_out context map refresh --detach --json
    [ "$RC" = 0 ] || broke "'context map refresh --detach' exited $RC: $OUT"
    local id; id="$(printf '%s' "$OUT" | jq -r '.jobId // empty' 2>/dev/null)"
    [ -n "$id" ] || broke "couldn't read the job id from: $OUT"
    printf '%s\n' "$id" >"$job_file"
    note "started $id"
  fi
  wait_job "it pauses" "$pause_timeout" '.state == "waiting" and .waitingOn == "model-limit"'
  echo "  $(job_id) is waiting on model-limit. The job, as 'sindri jobs show' prints it:"
  sx jobs show "$(job_id)" | sed 's/^/    /'
}

open_ui() {
  step "the Sindri app on the scratch home"
  local pidf="$home/first-run/ui.pid"
  if [ "${#pass[@]}" -gt 0 ] && printf '%s\n' "${pass[@]}" | grep -qx -- --no-ui; then note "skipped (--no-ui)"; return 0; fi
  if [ "$dry" = 1 ]; then
    echo "+ PATH=$fakes:\$PATH AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port <sindri-ui: \$AW_SINDRI_UI, else $state/bin/cs/current/sindri-ui> > $home/logs/ui.log 2>&1 &"
    return 0
  fi
  if [ -f "$pidf" ] && kill -0 "$(cat "$pidf")" 2>/dev/null; then note "the app is already open"; return 0; fi
  local ui="${AW_SINDRI_UI:-$state/bin/cs/current/sindri-ui}"
  [ -x "$ui" ] || broke "no sindri-ui at $ui; build it with scripts/install-cs.sh, or set AW_SINDRI_UI, or use --no-ui"
  mkdir -p "$home/first-run" "$home/logs"
  PATH="$fakes:$PATH" AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" nohup "$ui" >"$home/logs/ui.log" 2>&1 &
  echo $! >"$pidf"
  sleep 2
  kill -0 "$(cat "$pidf")" 2>/dev/null || broke "sindri-ui exited at once; see $home/logs/ui.log"
  echo "  open (pid $(cat "$pidf"))"
}

do_up() {
  guard
  make_home
  write_fakes
  setup
  start_refresh
  open_ui
  cat <<EOF

Check 1 (#2112 step 1): in the app, open Jobs. The refresh job $(job_id 2>/dev/null || echo '<job>') should say
  waiting on model-limit: claude-code at its usage limit; codex at its usage limit
with a Resume button on its row and on its sheet. Then:  $self resume        (you press Resume in the app)
                                                    or:  $self resume --cli  (the script runs 'sindri jobs resume')
EOF
}

# ---- resume ----
do_resume() {
  guard
  [ "$dry" = 1 ] || [ -f "$job_file" ] || die "no paused job in $home; run $self up first"
  step "claude-code is back (codex stays at its limit)"
  if [ "$dry" = 1 ]; then echo "+ rm $lim/limited-claude-code"; else rm -f "$lim/limited-claude-code"; note "limited: $(limited_list)"; fi
  local mark=0
  [ "$dry" = 1 ] || mark="$(wc -l <"$lim/calls.log" 2>/dev/null || echo 0)"
  if [ "$cli" = 1 ]; then
    step "resume the job"
    sx jobs resume "$(job_id)" || broke "'jobs resume $(job_id)' failed (SND-JOB-003 when it isn't waiting on model-limit)"
  else
    step "press Resume on the job in the app (Jobs, or the job's sheet)"
    wait_job "it leaves model-limit" "$job_timeout" '(.state == "waiting" and .waitingOn == "model-limit") | not'
  fi
  step "the job goes on and finishes"
  wait_job "it succeeds" "$job_timeout" '.state == "succeeded"'
  [ "$dry" = 0 ] || return 0
  sx jobs show "$(job_id)" | sed 's/^/    /'
  sx context map status | sed 's/^/    /'
  local answered
  answered="$(tail -n +"$((mark + 1))" "$lim/calls.log" 2>/dev/null | grep -c ' claude-code answer ')"
  [ "$answered" -gt 0 ] || broke "the job succeeded but the fake claude answered no call after the resume; see $lim/calls.log"
  grep -q ' claude-code answer labeling' "$lim/calls.log" || broke "no labeling call was answered; see $lim/calls.log (labeling off in the profile?)"
  cat <<EOF

Check 2 (#2112 step 2): the job $(job_id) succeeded after the resume; the fake claude answered $answered call(s) since, labeling among them.
In the app: the job's sheet lists the model calls (claude-code limit, then answered); the Context page's map has "Fake feature ..." labels.
Clean up with:  $self down
EOF
}

# ---- limit, unlimit ----
do_limit() {
  guard
  [ "$dry" = 1 ] || [ -d "$lim" ] || die "no fakes in $home; run $self up first"
  if [ "$action" = limit ]; then
    if [ "$dry" = 1 ]; then echo "+ touch $lim/limited-$harness"; return 0; fi
    : >"$lim/limited-$harness"
  else
    if [ "$dry" = 1 ]; then echo "+ rm $lim/limited-$harness"; return 0; fi
    rm -f "$lim/limited-$harness"
  fi
  echo "limited: $(limited_list)"
}

do_status() {
  guard
  if [ "$dry" != 1 ] && [ ! -d "$home" ]; then echo "no model-limit home at $home (nothing to report)"; exit 1; fi
  sx status
  if [ "$dry" = 1 ]; then sx jobs show "<job>"; echo "+ the limited fakes, then: tail -5 $lim/calls.log"; return 0; fi
  if [ -f "$job_file" ]; then sx jobs show "$(job_id)"; else echo "no refresh job yet"; fi
  echo "limited: $(limited_list)"
  tail -5 "$lim/calls.log" 2>/dev/null || echo "no fake calls yet"
}

do_down() {
  guard
  local args=(down)
  [ "$dry" = 0 ] || args+=(--dry-run)
  FIRST_RUN_HOME="$home" FIRST_RUN_PORT="$port" "$first_run" "${args[@]}"
}

case "$action" in
  up) do_up ;;
  resume) do_resume ;;
  limit|unlimit) do_limit ;;
  status) do_status ;;
  down) do_down ;;
esac
