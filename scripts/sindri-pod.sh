#!/usr/bin/env bash
# scripts/sindri-pod.sh — the owner's door to a Sindri that runs in a Kubernetes pod (deployable-context spec K8, §C.3).
#
#   sindri-pod.sh connect [--onyx [--adopt]] [--port PORT] [--check] [--dry-run] [FLAGS]
#   sindri-pod.sh profile edit [--no-approve] [FLAGS]
#   sindri-pod.sh repo add <name> <url> [--credential env:NAME] [FLAGS]
#   sindri-pod.sh approve [FLAGS]
#   sindri-pod.sh doctor [FLAGS]
#
# FLAGS, taken by every verb: --context C, --namespace N (kubectl's own defaults when omitted) and the pod: --pod P, or
# --release R (the pod is <R>-0, the StatefulSet's name for its one pod); with neither, the pod is sindri-0.
#
# Exit codes are per verb (spec §C.3): only 0 and 2 mean the same everywhere (0 done; 2 cannot start: usage, a prerequisite
# or the pod unreachable).
#   connect, connect --onyx   0 a normal end (Ctrl-C); 2 a prerequisite or start failure, or Onyx off with --onyx; 3 gave up
#                             reconnecting. An `attach` failure is printed as `attach exited <n>` and the app stays connected.
#   profile edit              0 changed and approved, or unchanged, or --no-approve done; 1 approve refused or declined;
#                             2 usage or the pod unreachable; 3 an invalid edit aborted (your file is kept); 4 conflict: the
#                             pod's copy changed meanwhile (your edit is kept)
#   repo add, approve, doctor the wrapped command's own exit code, except 2 for this script's own failures
#
# repo add, approve and doctor are thin wrappers: `kubectl exec -it <pod> -c sindri -- sindri repo add --url <url> --name <name>
# [--credential env:NAME]`, `... sindri profile approve` and `... sindri doctor`.
#
# profile edit changes the pod's profile. The profile is a file on the pod's volume (the path the pod's Sindri reads:
# AW_SINDRI_PROFILE, else <AW_SINDRI_HOME>/profile/profile.yaml) and the image has no editor. With no profile in the pod it
# first runs `sindri profile init` there. It copies the profile to a temp file here (remembering the pod's copy by its sha256),
# opens it in $EDITOR (vi when unset) and stops with "unchanged" when you change nothing. Otherwise it copies the edit to a
# temp path next to the profile in the pod and validates it there (`sindri profile validate`; exit 2 is invalid: the errors
# are put at the top of the file as comments and the editor reopens; leaving it as it was aborts with exit 3). A valid edit
# replaces the live file only if the pod's copy still has the hash it had (exit 4 otherwise, nothing replaced), then
# `kubectl exec -it <pod> -c sindri -- sindri profile approve` runs at this terminal: the approval is the protected verb you
# type. --no-approve stops after the profile is in place. Your edited copy stays under
# $AW_STATE_DIR/sindri-pod/<context>-<namespace>/profile-edit/ on any failure (its path is printed) and is removed on success.
#
# connect opens the desktop app on the pod. The app only follows a control.json in a home on this machine, so the script
# builds one: it port-forwards the pod's loopback owner port (the AW_SINDRI_PORT in the pod's control.json), reads the
# pod's control.json and fresh human token over `kubectl exec`, and writes a mirror home in a fresh
# $TMPDIR/aw-sindri-pod.XXXXXX (dirs 0700, files 0600; removed on exit, Ctrl-C included; a crash leaves it, and the next run
# removes the ones whose owner process is gone) holding
#   control.json     {"url":"http://127.0.0.1:<local port>","pid":<pod pid>,"epoch":<lease epoch>}
#                    (the fields src/Sindri/Aw.Sindri.Ui/Api/ControlRecord.cs reads; the app checks pid and epoch against
#                    GET /sindri/ping and never checks the pid is alive locally, so the pod's own pid is right)
#   auth/human.token the pod's per-start human token (src/Sindri/Aw.Sindri.Ui.Desktop/ControlFilePath.cs)
# and starts sindri-ui on it (AW_SINDRI_HOME=<mirror>, AW_SINDRI_APP_NO_MCP=1 so the mirror never registers an MCP server in ~/.claude.json). First it waits up to 10 minutes for the sindri container to be
# ready, printing its state every 15 s. The script then watches: when the port-forward drops or the pod's pid, epoch or token
# changes (a restart), it re-reads the pod, restarts the forward and rewrites the mirror home; the app shows its "control
# plane not running" state meanwhile and the script prints "pod restarting, reconnecting (attempt n of 30)". The script ends
# when the app is closed, after 30 attempts 10 s apart (5 minutes) without the pod, or on Ctrl-C (it kills only the
# port-forwards it started; the app window is left alone).
#
#   --port PORT     the local port the owner port is forwarded to (default: the pod's own port)
#   --onyx          also forward the pod's Onyx web port (Onyx runs as sibling containers in the pod, started by the kubelet;
#                   its port is read from the pod's AW_SINDRI_ONYX_URL, 8081 when that has none; it is forwarded to the same
#                   local port, because Onyx's WEB_DOMAIN is that address; AW_SINDRI_POD_ONYX_PORT overrides the local port
#                   and warns). It prints the forwarded address, then runs
#                   `kubectl exec -it <pod> -c sindri -- env AW_SINDRI_ONYX_FORWARD_URL=<address> sindri onyx attach` at this
#                   terminal: attach waits for Onyx (up to 20 minutes), checks the pinned release, then the account steps; at
#                   the sign-up step you sign up in the browser at the forwarded address and press Enter. Nothing is pulled
#                   or started; re-running is safe. Ctrl-C during attach stops that command only. With
#                   AW_SINDRI_ONYX_URL unset in the pod (Onyx off) it refuses, exit 2
#   --adopt         with --onyx: pass --adopt to attach (an Onyx whose admin account an earlier Sindri volume made)
#   --check         report each prerequisite (kubectl, jq, the app binary, exec and port-forward rights, the sindri container ready, the
#                   local port free) and exit 0 if all hold, 2 if any does not; changes nothing
#   --dry-run       print the kubectl and sindri-ui commands and the mirror home's path; run and write nothing
#
# Environment (all optional; .env.example): AW_STATE_DIR, TMPDIR, EDITOR, AW_SINDRI_UI, AW_SINDRI_POD_RETRIES,
# AW_SINDRI_POD_RETRY_WAIT, AW_SINDRI_POD_POLL, AW_SINDRI_POD_ONYX_PORT. The retry settings also bound the wait for the
# sindri container (default 40 attempts 15 s apart, 10 minutes); for reconnecting they default to 30 attempts 10 s apart.
# Needs kubectl (exec and port-forward rights on the namespace), jq and sindri-ui (`scripts/install-cs.sh`) for connect.
# Each prerequisite failure is one line and exit 2. No colour, no spinner.
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

say() { echo "sindri-pod: $*" >&2; }
die() { say "$*"; exit 2; }
give_up() { say "$*"; exit 3; }
usage() {
  say "usage: sindri-pod.sh connect [--onyx [--adopt]] [--port PORT] [--check] [--dry-run] | profile edit [--no-approve] | repo add <name> <url> [--credential env:NAME] | approve | doctor"
  say "       every verb takes [--context C] [--namespace N] [--pod P | --release R]"
  exit 2
}

verb=""
case "${1:-}" in
  connect) verb=connect; shift ;;
  profile) [ "${2:-}" = edit ] || usage; verb=edit; shift 2 ;;
  repo) [ "${2:-}" = add ] || usage; verb=repo-add; shift 2 ;;
  approve) verb=approve; shift ;;
  doctor) verb=doctor; shift ;;
  *) usage ;;
esac
ctx="" ns="" pod="" release="" lport="" onyx=0 adopt=0 dry=0 check=0 noapprove=0 cred=""
pos=()
while [ $# -gt 0 ]; do
  case "$1" in
    --context) [ $# -ge 2 ] || usage; ctx="$2"; shift 2 ;;
    --namespace) [ $# -ge 2 ] || usage; ns="$2"; shift 2 ;;
    --pod) [ $# -ge 2 ] || usage; pod="$2"; shift 2 ;;
    --release) [ $# -ge 2 ] || usage; release="$2"; shift 2 ;;
    --port) [ $# -ge 2 ] || usage; lport="$2"; shift 2 ;;
    --credential) [ $# -ge 2 ] || usage; cred="$2"; shift 2 ;;
    --onyx) onyx=1; shift ;;
    --adopt) adopt=1; shift ;;
    --check) check=1; shift ;;
    --no-approve) noapprove=1; shift ;;
    --dry-run) dry=1; shift ;;
    -*) say "unknown argument '$1'"; usage ;;
    *) pos+=("$1"); shift ;;
  esac
done
[ -z "$pod" ] || [ -z "$release" ] || { say "--pod and --release name the same thing: pass one"; usage; }
[ -n "$pod" ] || pod="${release:-sindri}-0"
[ "$verb" = connect ] || [ "$onyx$adopt$dry$check$lport" = 0000 ] || usage
[ "$verb" = edit ] || [ "$noapprove" = 0 ] || usage
[ "$verb" = repo-add ] || [ -z "$cred" ] || usage
[ "$onyx" = 1 ] || [ "$adopt" = 0 ] || { say "--adopt goes with --onyx"; usage; }
if [ "$verb" = repo-add ]; then [ "${#pos[@]}" = 2 ] || usage; else [ "${#pos[@]}" = 0 ] || usage; fi
case "$lport" in ''|*[!0-9]*) [ -z "$lport" ] || die "--port must be a number" ;; esac

retries="${AW_SINDRI_POD_RETRIES:-30}"
retry_wait="${AW_SINDRI_POD_RETRY_WAIT:-10}"
wait_n="${AW_SINDRI_POD_RETRIES:-40}"
wait_every="${AW_SINDRI_POD_RETRY_WAIT:-15}"
poll="${AW_SINDRI_POD_POLL:-5}"
state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"

kargs=()   # --context and --namespace, once both are resolved
kc() { kubectl ${kargs[@]+"${kargs[@]}"} "$@"; }

# kubectl, the context and the namespace; exit 2 when there is no kubectl or no context
kube_setup() {
  command -v kubectl >/dev/null 2>&1 || die "kubectl is not on the PATH (it needs exec rights on the namespace)"
  [ -n "$ctx" ] || ctx="$(kubectl config current-context 2>/dev/null)"
  [ -n "$ctx" ] || die "no kubectl context (pass --context)"
  [ -n "$ns" ] || ns="$(kubectl --context "$ctx" config view --minify -o 'jsonpath={..namespace}' 2>/dev/null)"
  [ -n "$ns" ] || ns=default
  kargs=(--context "$ctx" --namespace "$ns")
}
pod_reachable() { kc get pod "$pod" -o name >/dev/null 2>&1 || die "can't reach pod $pod in $ctx/$ns (kubectl get pod failed)"; }

# repo add, approve, doctor: the wrapped command's own exit code
thin() {
  kube_setup; pod_reachable
  kc exec -it "$pod" -c sindri -- sindri "$@"
  exit $?
}

sha() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi; }

# "key<TAB>block" for each top-level key of a YAML file, a block's lines joined by \001
keyblocks() {
  awk '/^[A-Za-z0-9_.-]+:/ { k = $0; sub(/:.*/, "", k) }
       { if (!(k in b)) o[++n] = k; b[k] = b[k] $0 "\001" }
       END { for (i = 1; i <= n; i++) if (o[i] != "") printf "%s\t%s\n", o[i], b[o[i]] }' "$1"
}
differing_keys() {   # <old> <new>: the top-level keys that differ, comma separated
  diff <(keyblocks "$1") <(keyblocks "$2") | grep '^[<>]' | cut -c3- | cut -f1 | sort -u | paste -sd, - | sed 's/,/, /g'
}

errmark='# sindri-pod: '

# profile edit: see the header.
profile_edit() {
  kube_setup; pod_reachable
  local safe dir ppath tmp_remote orig edit up presented ans rc editor out lines hash cur keys invalid=0
  safe="$(printf '%s-%s' "$ctx" "$ns" | tr -c 'A-Za-z0-9._-' '_')"
  dir="$state/sindri-pod/$safe/profile-edit"
  umask 077
  mkdir -p "$dir" || die "can't create $dir"
  orig="$dir/profile.orig.yaml" edit="$dir/profile.yaml" up="$dir/profile.upload.yaml" presented="$dir/profile.presented.yaml"
  ppath="$(kc exec "$pod" -c sindri -- sh -c 'h="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"; echo "${AW_SINDRI_PROFILE:-$h/profile/profile.yaml}"' 2>/dev/null)"
  [ -n "$ppath" ] || die "can't read the profile path from $pod in $ctx/$ns"
  if ! kc exec "$pod" -c sindri -- test -f "$ppath" 2>/dev/null; then
    say "$pod has no profile yet; running 'sindri profile init' in it"
    kc exec -it "$pod" -c sindri -- sindri profile init || die "'sindri profile init' failed in $pod"
  fi
  kc exec "$pod" -c sindri -- cat "$ppath" > "$orig" 2>/dev/null || die "can't read $ppath from $pod"
  hash="$(sha "$orig")"
  cp "$orig" "$edit"
  tmp_remote="$ppath.edit-$$"
  editor="${EDITOR:-vi}"
  while :; do
    sh -c "$editor \"\$1\"" _ "$edit" || { say "the editor failed; your copy is at $edit"; return 2; }
    if [ "$invalid" = 1 ] && cmp -s "$presented" "$edit"; then
      grep -v "^$errmark" "$edit" > "$up"; cp "$up" "$edit"
      kc exec "$pod" -c sindri -- rm -f "$tmp_remote" >/dev/null 2>&1
      say "the profile is still invalid; aborted. The pod's profile is unchanged; your file is kept at $edit"
      return 3
    fi
    grep -v "^$errmark" "$edit" > "$up"
    if cmp -s "$orig" "$up"; then
      rm -r "$dir"
      say "profile unchanged; nothing to validate or approve"
      return 0
    fi
    kc exec -i "$pod" -c sindri -- sh -c 'cat > "$1"' _ "$tmp_remote" < "$up" || { cp "$up" "$edit"; say "can't copy the edit into $pod; your copy is at $edit"; return 2; }
    # K-C3 adds `sindri profile validate --file <path>`; until the C# flag exists the path goes in AW_SINDRI_PROFILE.
    out="$(kc exec "$pod" -c sindri -- sh -c 'AW_SINDRI_PROFILE="$1" sindri profile validate' _ "$tmp_remote" 2>&1)"; rc=$?
    if [ "$rc" -ge 2 ]; then
      say "the edited profile is not valid (sindri profile validate exit $rc); the editor reopens with the error"
      printf '%s\n' "$out" >&2
      { printf '%s\n' "$out" | sed "s/^/$errmark/"; cat "$up"; } > "$edit"
      cp "$edit" "$presented"
      invalid=1
      continue
    fi
    break
  done
  cp "$up" "$edit"
  kc exec "$pod" -c sindri -- sh -c 'cur="$(sha256sum "$2" | cut -d" " -f1)"; [ "$cur" = "$3" ] || exit 4; mv "$1" "$2"' _ "$tmp_remote" "$ppath" "$hash"; rc=$?
  if [ "$rc" = 4 ]; then
    kc exec "$pod" -c sindri -- rm -f "$tmp_remote" >/dev/null 2>&1
    cur="$dir/profile.pod-now.yaml"
    keys=""
    if kc exec "$pod" -c sindri -- cat "$ppath" > "$cur" 2>/dev/null; then keys="$(differing_keys "$orig" "$cur")"; fi
    say "The profile on the pod changed while you were editing (${keys:-unknown keys}). Nothing was replaced; your edit is kept at $edit. Run profile edit again and make the change on top of the new copy."
    return 4
  elif [ "$rc" != 0 ]; then
    say "can't move the edit into place; your copy is at $edit"
    return 2
  fi
  lines="$(diff "$orig" "$edit" | grep -c '^[<>]')"
  if [ "$noapprove" = 1 ]; then
    rm -r "$dir"
    say "profile changed, not yet approved ($lines lines differ, valid, in place in $pod); next: kubectl exec -it $pod -c sindri -- sindri profile approve"
    return 0
  fi
  kc exec -it "$pod" -c sindri -- sindri profile approve; rc=$?
  if [ "$rc" -ne 0 ]; then
    say "the profile is in place in $pod but not approved (sindri profile approve exit $rc); your edited copy is at $edit"
    say "next: kubectl exec -it $pod -c sindri -- sindri profile approve"
    return 1
  fi
  rm -r "$dir"
  say "profile changed ($lines lines differ) and approved in $pod; next: sindri-pod.sh connect, or 'kubectl exec $pod -c sindri -- sindri status' to see it take effect"
}

case "$verb" in
  edit) profile_edit; exit $? ;;
  approve) thin profile approve ;;
  doctor) thin doctor ;;
  repo-add)
    if [ -n "$cred" ]; then thin repo add --url "${pos[1]}" --name "${pos[0]}" --credential "$cred"
    else thin repo add --url "${pos[1]}" --name "${pos[0]}"; fi ;;
esac

# The pod's home, resolved in the pod the way the app resolves its own (ControlFilePath.cs).
pod_read='h="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"; cat "$h/control.json" && echo && echo @@aw-token@@ && cat "$h/auth/human.token"'

# The sindri container's ready flag (true, false, or empty when the pod or the container is not there) and the other
# containers that are not ready ("name=false ...", information only).
sindri_ready() { kc get pod "$pod" -o 'jsonpath={.status.containerStatuses[?(@.name=="sindri")].ready}' 2>/dev/null; }
other_containers() { kc get pod "$pod" -o 'jsonpath={range .status.containerStatuses[?(@.name!="sindri")]}{.name}={.ready} {end}' 2>/dev/null | tr ' ' '\n' | grep -v '=true$' | grep . | paste -sd' ' -; }
container_state() {
  local s r
  s="$(kc get pod "$pod" -o 'jsonpath={.status.containerStatuses[?(@.name=="sindri")].state}' 2>/dev/null)"
  r="$(printf '%s' "$s" | sed -n 's/.*"reason":"\([^"]*\)".*/\1/p')"
  case "$s" in
    '') echo "not found" ;;
    *'"running"'*) echo "running, not ready" ;;
    *'"waiting"'*) echo "waiting${r:+: $r}" ;;
    *'"terminated"'*) echo "terminated${r:+: $r}" ;;
    *) echo "$s" ;;
  esac
}
# Onyx's port in the pod: from AW_SINDRI_ONYX_URL (empty output when it is unset: Onyx is off); 8081 when the URL has no port.
pod_onyx_port() {
  local u
  u="$(kc exec "$pod" -c sindri -- sh -c 'printf %s "${AW_SINDRI_ONYX_URL:-}"' 2>/dev/null)"
  [ -n "$u" ] || return 0
  printf '%s\n' "$u" | sed -n 's#^[a-z]*://[^/:]*:\([0-9][0-9]*\).*#\1#p' | grep . || echo 8081
}

alive() { [ -n "$1" ] && kill -0 "$1" 2>/dev/null; }

find_ui() {
  if [ -n "${AW_SINDRI_UI:-}" ]; then printf '%s\n' "$AW_SINDRI_UI"; return; fi
  printf '%s\n' "$state/bin/cs/current/sindri-ui"
}

if [ "$dry" = 1 ]; then
  echo "+ checks: kubectl auth can-i create pods/exec and pods/portforward; kubectl get pod $pod (the sindri container ready: waits up to $wait_n attempts ${wait_every}s apart); the local port is free; the app binary exists"
  echo "mirror home: ${TMPDIR:-/tmp}/aw-sindri-pod.XXXXXX (0700; control.json and auth/human.token 0600; removed on exit)"
  echo "+ kubectl [--context ${ctx:-<current>}] [--namespace ${ns:-<current>}] exec $pod -c sindri -- sh -c '<cat control.json and auth/human.token>'"
  echo "+ kubectl port-forward pod/$pod ${lport:-<pod port>}:<pod port from control.json>"
  echo "+ AW_SINDRI_APP_NO_MCP=1 AW_SINDRI_HOME=<mirror home> <sindri-ui: \$AW_SINDRI_UI, else $state/bin/cs/current/sindri-ui>"
  echo "  (every ${poll}s: re-read pid, epoch and token; on a change or a dropped forward, retry up to $retries attempts ${retry_wait}s apart and rewrite the mirror home)"
  if [ "$onyx" = 1 ]; then
    echo "+ kubectl exec $pod -c sindri -- sh -c 'printf %s \$AW_SINDRI_ONYX_URL'   (Onyx's port; unset means Onyx is off: refused)"
    echo "+ kubectl port-forward pod/$pod ${AW_SINDRI_POD_ONYX_PORT:-<onyx port>}:<onyx port>; print Onyx's page http://127.0.0.1:${AW_SINDRI_POD_ONYX_PORT:-<onyx port>}"
    echo "+ kubectl exec -it $pod -c sindri -- env AW_SINDRI_ONYX_FORWARD_URL=<address> sindri onyx attach${adopt:+ --adopt}   (Ctrl-C stops attach only; a failure is printed as 'attach exited <n>')"
  fi
  exit 0
fi

fails=0
step_ok() { [ "$check" = 1 ] && say "ok: $*"; return 0; }
step_fail() { say "$*"; fails=1; [ "$check" = 1 ] || exit 2; }
port_taken() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }

onyx_remote="" onyx_local=""
have_k=1
if command -v kubectl >/dev/null 2>&1; then step_ok "kubectl is on the PATH"
else step_fail "kubectl is not on the PATH (it needs exec and port-forward rights on the namespace)"; have_k=0; fi
if command -v jq >/dev/null 2>&1; then step_ok "jq is on the PATH"; else step_fail "jq is not on the PATH"; fi
ui="$(find_ui)"
if [ -n "$ui" ] && [ -x "$ui" ]; then step_ok "sindri-ui found ($ui)"
else step_fail "sindri-ui not found; install it with scripts/install-cs.sh or set AW_SINDRI_UI"; fi

if [ "$have_k" = 1 ]; then
  [ -n "$ctx" ] || ctx="$(kubectl config current-context 2>/dev/null)"
  if [ -z "$ctx" ]; then step_fail "no kubectl context (pass --context)"; have_k=0; fi
fi
if [ "$have_k" = 1 ]; then
  [ -n "$ns" ] || ns="$(kubectl --context "$ctx" config view --minify -o 'jsonpath={..namespace}' 2>/dev/null)"
  [ -n "$ns" ] || ns=default
  kargs=(--context "$ctx" --namespace "$ns")
  for rverb in pods/exec pods/portforward; do
    ans="$(kc auth can-i create "$rverb" 2>/dev/null)"
    if [ "$ans" = yes ]; then step_ok "kubectl may create $rverb in $ns"
    else step_fail "kubectl is not allowed to 'create $rverb' in $ns on $ctx (answer: ${ans:-none})"; fi
  done
  # Ready means the sindri container, not the pod: an Onyx container that crash-loops keeps the pod not Ready and Sindri works.
  # connect waits for it (10 minutes, its state every 15 s); --check only reports.
  n=0
  while :; do
    sready="$(sindri_ready)"
    { [ "$sready" = true ] || [ "$check" = 1 ] || [ "$n" -ge "$wait_n" ]; } && break
    n=$((n + 1))
    say "waiting for the sindri container of pod $pod: $(container_state) (attempt $n of $wait_n)"
    sleep "$wait_every"
  done
  others="$(other_containers)"
  if [ "$sready" = true ]; then
    step_ok "the sindri container of pod $pod is ready"
    [ -z "$others" ] || say "Onyx: not answering, see sindri doctor"
  else
    step_fail "the sindri container of pod $pod is not running (state: $(container_state)); see: kubectl logs $pod -c sindri${others:+ (other containers, for information: $others)}"
  fi
  want="$lport"
  if [ -z "$want" ]; then   # the pod's own port, when its control.json is readable already
    want="$(kc exec "$pod" -c sindri -- sh -c "$pod_read" 2>/dev/null | sed '/^@@aw-token@@$/,$d' | jq -r '.url // empty' 2>/dev/null \
      | sed -n 's#^.*:\([0-9][0-9]*\)/*$#\1#p')"
  fi
  if [ -z "$want" ]; then step_ok "local port: skipped (the pod's control.json is not readable yet)"
  elif port_taken "$want"; then step_fail "local port $want is taken; pick another with --port"
  else step_ok "local port $want is free"; fi
  if [ "$onyx" = 1 ] && [ "$sready" = true ]; then
    onyx_remote="$(pod_onyx_port)"
    if [ -z "$onyx_remote" ]; then
      step_fail "Onyx is off in this install (onyx.enabled=false); use connect."
    else
      onyx_local="${AW_SINDRI_POD_ONYX_PORT:-$onyx_remote}"
      if [ "$onyx_local" != "$onyx_remote" ]; then
        say "warning: AW_SINDRI_POD_ONYX_PORT=$onyx_local: sign-up redirects and connector OAuth may land on port $onyx_remote, not $onyx_local"
      fi
      if port_taken "$onyx_local"; then step_fail "local port $onyx_local is in use: stop what uses it, then run this again"
      else step_ok "local port $onyx_local (Onyx) is free"; fi
    fi
  fi
fi
if [ "$check" = 1 ]; then
  [ "$fails" = 0 ] && exit 0
  exit 2
fi
[ "$fails" = 0 ] || exit 2

umask 077
tmp="${TMPDIR:-/tmp}"; tmp="${tmp%/}"
# A crashed run leaves its mirror home; the owner.pid file says whose it was.
for d in "$tmp"/aw-sindri-pod.*; do
  [ -d "$d" ] && [ -O "$d" ] && [ -f "$d/owner.pid" ] || continue
  alive "$(cat "$d/owner.pid" 2>/dev/null)" || rm -rf "$d"
done
home="$(mktemp -d "$tmp/aw-sindri-pod.XXXXXX")" || die "can't create a mirror home under $tmp"
echo $$ > "$home/owner.pid"
safe="$(printf '%s-%s' "$ctx" "$ns" | tr -c 'A-Za-z0-9._-' '_')"
logs="$state/sindri-pod/$safe/logs"   # logs hold no secrets and outlive the mirror home
mkdir -p "$home/auth" "$logs" || die "can't create $home"

pf_pid="" onyx_pf_pid="" ui_pid=""
cur_pid="" cur_epoch="" cur_token="" remote_port=""

stop_own() {   # only the port-forwards and the app this run recorded
  [ -z "$pf_pid" ] || kill "$pf_pid" 2>/dev/null
  [ -z "$onyx_pf_pid" ] || kill "$onyx_pf_pid" 2>/dev/null
  pf_pid="" onyx_pf_pid=""
  if [ -n "$ui_pid" ]; then   # the app is useless once the mirror home is gone
    kill "$ui_pid" 2>/dev/null
    local n=0; while alive "$ui_pid" && [ "$n" -lt 50 ]; do sleep 0.1; n=$((n + 1)); done
    ! alive "$ui_pid" || kill -KILL "$ui_pid" 2>/dev/null
    ui_pid=""
  fi
}
cleanup() { stop_own; case "$home" in "$tmp"/aw-sindri-pod.*) rm -rf "$home" ;; esac; }
trap 'cleanup' EXIT
trap 'exit 0' INT TERM

sleep_for() { sleep "$(awk -v p="$poll" -v n="$1" 'BEGIN { print p * n }')"; }

write_home() {   # atomic, owner-only
  jq -n --arg url "http://127.0.0.1:$lport" --argjson pid "$cur_pid" --argjson epoch "$cur_epoch" \
    '{url: $url, pid: $pid, epoch: $epoch}' > "$home/control.json.tmp" && mv "$home/control.json.tmp" "$home/control.json" || return 1
  printf '%s\n' "$cur_token" > "$home/auth/human.token.tmp" && mv "$home/auth/human.token.tmp" "$home/auth/human.token" || return 1
  chmod 700 "$home" "$home/auth"; chmod 600 "$home/control.json" "$home/auth/human.token"
}

start_forward() {
  [ -z "$pf_pid" ] || kill "$pf_pid" 2>/dev/null
  kubectl "${kargs[@]}" port-forward "pod/$pod" "$lport:$remote_port" >> "$logs/port-forward.log" 2>&1 &
  pf_pid=$!
  sleep_for 1   # settle: a forward that can't bind or reach the pod exits within moments
  alive "$pf_pid"
}

# One reconcile: read the pod, keep the forward on the pod's port, rewrite the mirror home when pid, epoch or token moved.
reconcile() {
  local out ctl tok pid epoch port
  out="$(kc exec "$pod" -c sindri -- sh -c "$pod_read" 2>>"$logs/exec.log")" || return 1
  ctl="$(printf '%s\n' "$out" | sed '/^@@aw-token@@$/,$d')"
  tok="$(printf '%s\n' "$out" | sed '1,/^@@aw-token@@$/d' | head -1 | tr -d '[:space:]')"
  pid="$(printf '%s' "$ctl" | jq -r '.pid // empty' 2>/dev/null)"
  epoch="$(printf '%s' "$ctl" | jq -r '.epoch // empty' 2>/dev/null)"
  port="$(printf '%s' "$ctl" | jq -r '.url // empty' 2>/dev/null | sed -n 's#^.*:\([0-9][0-9]*\)/*$#\1#p')"
  [ -n "$pid" ] && [ -n "$epoch" ] && [ -n "$port" ] && [ -n "$tok" ] || return 1
  if [ "$port" != "$remote_port" ] || ! alive "$pf_pid"; then
    remote_port="$port"
    [ -n "$lport" ] || lport="$port"
    start_forward || return 1
  fi
  if [ "$pid" != "$cur_pid" ] || [ "$epoch" != "$cur_epoch" ] || [ "$tok" != "$cur_token" ]; then
    cur_pid="$pid" cur_epoch="$epoch" cur_token="$tok"
    write_home || return 1
    say "mirror home written: pid $cur_pid, epoch $cur_epoch ($home)"
  fi
}

# reconcile, up to $retries attempts $retry_wait seconds apart (30 x 10 s = 5 minutes by default)
reconcile_retrying() {
  local n=0
  until reconcile; do
    n=$((n + 1))
    [ "$n" -le "$retries" ] || return 1
    say "pod restarting, reconnecting (attempt $n of $retries)"
    sleep "$retry_wait"
  done
  [ "$n" -eq 0 ] || say "reconnected to $pod"
}

reconcile_retrying || give_up "can't reach $pod in $ctx/$ns after $retries attempts (see $logs)"

AW_SINDRI_APP_NO_MCP=1 AW_SINDRI_HOME="$home" nohup "$ui" >> "$logs/ui.log" 2>&1 &
ui_pid=$!
say "app started (pid $ui_pid) on $home"
say "connected: $pod at http://127.0.0.1:$lport; Ctrl-C to stop"

if [ "$onyx" = 1 ]; then
  kubectl "${kargs[@]}" port-forward "pod/$pod" "$onyx_local:$onyx_remote" >> "$logs/onyx-port-forward.log" 2>&1 &
  onyx_pf_pid=$!
  say "Onyx's page: http://127.0.0.1:$onyx_local"
  attach=(env "AW_SINDRI_ONYX_FORWARD_URL=http://127.0.0.1:$onyx_local" sindri onyx attach)
  [ "$adopt" = 0 ] || attach+=(--adopt)
  trap ':' INT   # Ctrl-C reaches attach (and this shell) but ends only attach
  kc exec -it "$pod" -c sindri -- "${attach[@]}"
  rc=$?
  trap 'exit 0' INT
  case "$rc" in
    0) ;;
    130) say "attach stopped; the app stays connected; rerun connect --onyx to continue" ;;
    *) say "attach exited $rc; the app stays connected; rerun connect --onyx" ;;
  esac
fi

while alive "$ui_pid"; do
  sleep_for 1
  alive "$ui_pid" || break
  reconcile_retrying || give_up "lost $pod in $ctx/$ns after $retries attempts (see $logs)"
done
say "the app was closed"
exit 0
