#!/usr/bin/env bash
# scripts/linear-probe.sh — Ph0 of the Linear hub spec (docs/superpowers/specs/2026-11-04-sindri-linear-hub-design.md §12 Ph0, D2, D3,
# D12, §5.1, §5.2, §14 Q6): the owner-run go/no-go probe that finds the Linear API facts the spec marks "(Ph0 checks)". We only ever run
# it against a fake; the owner runs it on their work Linear.
#
# Owner-run commands:
#   scripts/linear-probe.sh                          # read-only: schema facts, key scope, rate-limit headers, mention syntax counts
#   scripts/linear-probe.sh --team ENG --write       # plus the write facts, in team ENG only, on issues labelled `sindri-probe`
# Usage: scripts/linear-probe.sh [--url URL] [--team KEY --write]
#   --url    the GraphQL endpoint (default https://api.linear.app/graphql); anything but api.linear.app must be loopback (the fake)
#   --team   the one team the write part may touch (and, alone, the team the mention scan reads; default: the profile's teams)
#   --write  run the write part; needs --team and a write key
# Keys: read key = AW_LINEAR_PROBE_KEY, else the profile's context.sources.linear.token pointer ($AW_SINDRI_HOME/profile/profile.yaml).
#   write key = AW_LINEAR_PROBE_WRITE_KEY, else context.sources.linear.writeToken: keychain: or op: only (D3); env:, file: and command:
#   pointers are accepted only with a loopback --url. A write key equal to the read key is refused (D2). Keys reach curl through a 0600
#   config file, never an argument, and are never printed.
# Write part guards: every issue it creates carries the team label `sindri-probe` (made when missing; the label stays); at the end (and
#   from the EXIT trap on any failure, a SIGINT too) it archives the labelled issues this run made, then re-queries. Before the first
#   create it reads the labelled non-archived issues as a baseline; ones already there are left alone (a warning counts them), and
#   a create whose answer was lost is still caught because it is not in the baseline. It never touches an issue without the label. Output is counts, booleans, error codes and header numbers: no titles, descriptions, names,
#   emails or urls from Linear.
# Exit 0 ok, 1 a probe call failed (the "failed:" lines say which; cleanup still ran), 2 usage, no key or a guard refused.
set -uo pipefail

url="https://api.linear.app/graphql" team_arg="" want_write=0
while [ $# -gt 0 ]; do
  case "$1" in
    --url) url="${2:?--url needs a value}"; shift 2 ;;
    --team) team_arg="${2:?--team needs a team key}"; shift 2 ;;
    --write) want_write=1; shift ;;
    -h|--help) sed -n '2,24p' "$0"; exit 0 ;;
    *) echo "usage: $0 [--url URL] [--team KEY --write]" >&2; exit 2 ;;
  esac
done
if [ "$want_write" -eq 1 ] && [ -z "$team_arg" ]; then echo "probe: --write needs --team KEY (the one team it may write in)" >&2; exit 2; fi
case "$team_arg" in *[!A-Za-z0-9_-]*) echo "probe: --team takes a team key like ENG" >&2; exit 2 ;; esac

command -v jq >/dev/null || { echo "probe: jq is required" >&2; exit 2; }
command -v curl >/dev/null || { echo "probe: curl is required" >&2; exit 2; }

# The url guard: api.linear.app over https, or a loopback host (the fake).
# The authority is parsed, not sliced: no userinfo (`@`), a bracketed IPv6 host with an optional port, or host with an optional port.
authority="${url#*://}"; authority="${authority%%[/?#]*}"
case "$authority" in *@*) echo "probe: --url must not carry user info (an @ in the host part)" >&2; exit 2 ;; esac
host="" port_ok=1
case "$authority" in
  \[*\]*)
    host="${authority%%\]*}]"; rest="${authority#*\]}"
    case "$rest" in '') ;; :*) case "${rest#:}" in ''|*[!0-9]*) port_ok=0 ;; esac ;; *) port_ok=0 ;; esac ;;
  *:*:*) port_ok=0 ;;
  *:*) host="${authority%%:*}"; case "${authority#*:}" in ''|*[!0-9]*) port_ok=0 ;; esac ;;
  *) host="$authority" ;;
esac
[ "$port_ok" -eq 1 ] || { echo "probe: --url has a malformed host or port" >&2; exit 2; }
loopback=0
case "$host" in 127.0.0.1|localhost|'[::1]') loopback=1 ;; esac
case "$url" in
  https://api.linear.app/*|https://api.linear.app) ;;
  http://*|https://*) [ "$loopback" -eq 1 ] || { echo "probe: --url must be https://api.linear.app or a loopback address" >&2; exit 2; } ;;
  *) echo "probe: --url must be an http(s) url" >&2; exit 2 ;;
esac

state_dir="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
sindri_home="${AW_SINDRI_HOME:-$state_dir/sindri-app}"
profile="$sindri_home/profile/profile.yaml"

work="$(mktemp -d)"; chmod 700 "$work"
cleanup_armed=0 cleanup_done=0
# The one trap: the write part's cleanup (when armed; defined further down) and the temp dir, on any exit, SIGINT and SIGTERM included.
on_exit() { local rc=$?; if declare -F cleanup >/dev/null; then cleanup; fi; rm -rf "$work"; exit "$rc"; }
trap on_exit EXIT
trap 'exit 130' INT TERM

# profile_get a.b.c: the scalar or list at that path, one value per line. A small indentation-based read for the documented block
# shape (`key: value`, `key: [a, b]`, `key:` then `- item` lines); not a YAML parser.
profile_get() {
  [ -f "$profile" ] || return 0
  python3 -I - "$profile" "$1" <<'PY'
import re, sys
path = sys.argv[2].split(".")
def unq(s):
    s = s.strip()
    return s[1:-1] if len(s) > 1 and s[0] == s[-1] and s[0] in "'\"" else s
def strip_comment(s):
    return re.sub(r"\s+#.*$", "", s).rstrip()
stack, out, collecting, base = [], [], False, 0
for raw in open(sys.argv[1], encoding="utf-8"):
    line = strip_comment(raw.rstrip("\n"))
    if not line.strip() or line.lstrip().startswith("#"):
        continue
    indent = len(line) - len(line.lstrip())
    body = line.strip()
    if collecting:
        if body.startswith("- ") and indent >= base:
            out.append(unq(body[2:])); continue
        collecting = False
    m = re.match(r"^([A-Za-z0-9_.-]+):\s*(.*)$", body)
    if not m:
        continue
    while stack and stack[-1][0] >= indent:
        stack.pop()
    stack.append((indent, m.group(1)))
    if [k for _, k in stack] == path:
        val = m.group(2)
        if val.startswith("["):
            out += [unq(x) for x in val.strip("[]").split(",") if x.strip()]
        elif val:
            out.append(unq(val))
        else:
            collecting, base = True, indent
print("\n".join(out))
PY
}

resolve_pointer() {
  local p="$1" rest name
  case "$p" in
    env:*) name="${p#env:}"; printf '%s' "${!name:-}" ;;
    file:*) head -n1 "${p#file:}" 2>/dev/null | tr -d '[:space:]' ;;
    keychain:*)
      rest="${p#keychain:}"
      if [ "$(uname)" = Darwin ] && command -v security >/dev/null; then security find-generic-password -s "${rest%%/*}" -a "${rest#*/}" -w 2>/dev/null
      else secret-tool lookup service "${rest%%/*}" account "${rest#*/}" 2>/dev/null; fi ;;
    op:*) op read "op://${p#op:}" 2>/dev/null ;;
    command:*) python3 -I -c 'import shlex,subprocess,sys; print(subprocess.run(shlex.split(sys.argv[1]),capture_output=True,text=True,timeout=30).stdout.strip())' "${p#command:}" 2>/dev/null ;;
  esac
}


# --- keys ---
read_key="${AW_LINEAR_PROBE_KEY:-}"
if [ -z "$read_key" ]; then
  pointer="$(profile_get context.sources.linear.token | head -n1)"
  [ -n "$pointer" ] && read_key="$(resolve_pointer "$pointer")"
fi
if [ -z "$read_key" ]; then
  echo "probe: no Linear key. Set AW_LINEAR_PROBE_KEY, or connect Linear (the profile at $profile needs context.sources.linear.token)." >&2
  exit 2
fi
write_key="${AW_LINEAR_PROBE_WRITE_KEY:-}"
if [ -z "$write_key" ]; then
  wpointer="$(profile_get context.sources.linear.writeToken | head -n1)"
  if [ -n "$wpointer" ]; then
    case "$wpointer" in
      keychain:*|op:*) ;;
      *) if [ "$loopback" -ne 1 ]; then
           echo "probe: the write key pointer must be keychain: or op: (D3); env:, file: and command: only work with a loopback --url" >&2; exit 2
         fi ;;
    esac
    write_key="$(resolve_pointer "$wpointer")"
  fi
fi
if [ "$want_write" -eq 1 ] && [ -z "$write_key" ]; then
  echo "probe: --write needs a write key. Set AW_LINEAR_PROBE_WRITE_KEY, or context.sources.linear.writeToken (keychain: or op:) in $profile." >&2
  exit 2
fi
if [ -n "$write_key" ] && [ "$write_key" = "$read_key" ]; then
  echo "probe: the write key is the same secret as the read key (D2 keeps them apart)" >&2; exit 2
fi

teams=()   # a while loop, not mapfile: macOS ships bash 3.2, which has no mapfile
if [ -n "$team_arg" ]; then teams+=("$team_arg")
else while IFS= read -r t; do [ -n "$t" ] && teams+=("$t"); done < <(profile_get context.sources.linear.teams); fi

# Keys go to curl through 0600 config files, never an argument.
mkcfg() { printf 'header = "Authorization: %s"\nheader = "Content-Type: application/json"\n' "$2" > "$work/$1.cfg"; chmod 600 "$work/$1.cfg"; }
mkcfg read "$read_key"
if [ -n "$write_key" ]; then mkcfg write "$write_key"; fi
unset read_key write_key

lines=() failures=()
emit() { lines+=("$1: $2"); printf '%s: %s\n' "$1" "$2"; }
failed() { failures+=("$1"); printf 'failed: %s\n' "$1" >&2; }

# gql KEY NAME QUERY VARS_JSON -> $work/NAME.json (body), $work/NAME.h (headers). 0 ok, 3 GraphQL errors, 1 transport failure.
gql() {
  local cfg="$1" name="$2" query="$3" vars="$4" code
  jq -n --arg q "$query" --argjson v "$vars" '{query:$q,variables:$v}' > "$work/$name.req"
  code="$(curl -sS -K "$work/$cfg.cfg" -X POST --max-time 60 -D "$work/$name.h" -o "$work/$name.json" -w '%{http_code}' \
    --data @"$work/$name.req" "$url" 2>/dev/null)" || return 1
  jq -e type "$work/$name.json" >/dev/null 2>&1 || return 1
  if jq -e '.errors != null' "$work/$name.json" >/dev/null 2>&1; then return 3; fi
  case "$code" in 2*) return 0 ;; *) return 1 ;; esac
}
# errcode NAME: the first error's code or type, reduced to [A-Za-z0-9_-] (never a message).
errcode() {
  jq -rj '(.errors[0].extensions.code // .errors[0].extensions.type // "error") | tostring' "$work/$1.json" 2>/dev/null \
    | tr -c 'A-Za-z0-9_-' '_' | cut -c1-40
}
hdrnum() { # FILE NAME -> the digits of that header, or empty
  tr -d '\r' < "$1" | grep -i "^$2:" | head -n1 | cut -d: -f2 | tr -cd '0-9'
}

# --- 1. introspection ---
introspect='query Introspect { m: __type(name: "Mutation") { fields { name } } ic: __type(name: "IssueCreateInput") { inputFields { name } }
  cc: __type(name: "CommentCreateInput") { inputFields { name } } iu: __type(name: "IssueUpdateInput") { inputFields { name } }
  q: __type(name: "Query") { fields { name } } ak: __type(name: "ApiKey") { fields { name } } }'
if gql read Introspect "$introspect" '{}'; then
  for m in issueCreate issueUpdate commentCreate commentUpdate attachmentLinkURL issueArchive; do
    emit "mutation.$m" "$(jq -r --arg m "$m" 'if ([.data.m.fields[]?.name] | index($m)) != null then "yes" else "no" end' "$work/Introspect.json")"
  done
  emit schema.IssueCreateInput.id "$(jq -r 'if ([.data.ic.inputFields[]?.name] | index("id")) != null then "yes" else "no" end' "$work/Introspect.json")"
  emit schema.CommentCreateInput.id "$(jq -r 'if ([.data.cc.inputFields[]?.name] | index("id")) != null then "yes" else "no" end' "$work/Introspect.json")"
  cond="$(jq -r '[.data.iu.inputFields[]?.name | select(test("^(ifMatch|ifUnmodifiedSince|expected|conditional|version|if[A-Z])"; "i"))] | join(",")' "$work/Introspect.json")"
  emit schema.IssueUpdateInput.conditional "${cond:-none}"
  # key scope: a query that lists API keys with a scope-like field, if the schema has one
  scope="unknown"
  scope_field="$(jq -r '[.data.ak.fields[]?.name | select(test("scope|permission"; "i"))] | first // empty' "$work/Introspect.json")"
  if jq -e '[.data.q.fields[]?.name] | index("apiKeys") != null' "$work/Introspect.json" >/dev/null && [ -n "$scope_field" ]; then
    if gql read KeyScope "query KeyScope { apiKeys { nodes { $scope_field } } }" '{}'; then
      s="$(jq -rj --arg f "$scope_field" '[.data.apiKeys.nodes[]?[$f] | if type == "array" then .[] else . end | tostring] | unique | join(",")' "$work/KeyScope.json" | tr -c 'A-Za-z0-9_,:-' '_')"
      [ -n "$s" ] && scope="$s"
    fi
  fi
  emit key.scope "$scope"
else failed "introspection"; fi

# --- 2. rate limits: read key, write key, and whether they share a budget ---
ping='query Ping { viewer { id } }'
emit_rl() { # LABEL HEADERFILE
  local pair v
  for pair in requests_limit:x-ratelimit-requests-limit requests_remaining:x-ratelimit-requests-remaining requests_reset:x-ratelimit-requests-reset \
    complexity_limit:x-ratelimit-complexity-limit complexity_remaining:x-ratelimit-complexity-remaining complexity_reset:x-ratelimit-complexity-reset complexity:x-complexity; do
    v="$(hdrnum "$2" "${pair#*:}")"; emit "ratelimit.$1.${pair%%:*}" "${v:-unknown}"
  done
}
shared="unknown"
if gql read PingRead1 "$ping" '{}'; then
  emit_rl read "$work/PingRead1.h"
  if [ -f "$work/write.cfg" ]; then
    if gql write PingWrite "$ping" '{}'; then
      emit_rl write "$work/PingWrite.h"
      # Two rounds of read, write, read: a read-to-read drop of 2 means the write call spent the read key's budget, a drop of 1 means it
      # did not. Other traffic on the workspace can add to a drop, so the answer is yes/no only when both rounds agree; the raw numbers print.
      if gql read PingRead2 "$ping" '{}' && gql write PingWrite2 "$ping" '{}' && gql read PingRead3 "$ping" '{}'; then
        a="$(hdrnum "$work/PingRead1.h" x-ratelimit-requests-remaining)"; b="$(hdrnum "$work/PingRead2.h" x-ratelimit-requests-remaining)"
        c="$(hdrnum "$work/PingRead3.h" x-ratelimit-requests-remaining)"
        emit ratelimit.read.remaining_before "${a:-unknown} ${b:-unknown}"
        emit ratelimit.read.remaining_after "${b:-unknown} ${c:-unknown}"
        emit ratelimit.write.calls_between "1 1"
        if [ -n "$a" ] && [ -n "$b" ] && [ -n "$c" ]; then
          if [ $((a - b)) -ge 2 ] && [ $((b - c)) -ge 2 ]; then shared=yes
          elif [ $((a - b)) -eq 1 ] && [ $((b - c)) -eq 1 ]; then shared=no; fi
        fi
      else failed "rate limit comparison"; fi
    else failed "rate limit (write key)"; fi
  else emit ratelimit.write "skipped (no write key)"; fi
else failed "rate limit (read key)"; fi
emit ratelimit.shared_between_keys "$shared"

# --- 3. mention syntax: counts of body patterns over the newest 100 comments in the teams ---
if [ "${#teams[@]}" -eq 0 ]; then
  emit mention "skipped (no team: the profile lists no context.sources.linear.teams and no --team)"
else
  teams_json="$(printf '%s\n' "${teams[@]}" | jq -R . | jq -sc .)"
  cq='query Comments($filter: CommentFilter, $first: Int) { comments(filter: $filter, first: $first, orderBy: updatedAt) { nodes { body } } }'
  if gql read Comments "$cq" "$(jq -nc --argjson t "$teams_json" '{filter:{issue:{team:{key:{in:$t}}}},first:100}')"; then
    jq -r '[.data.comments.nodes[]? | (.body // "")] as $b
      | ([$b[] | select(test("(^|\\s)@[A-Za-z0-9._-]+"))] | length) as $at
      | ([$b[] | select(test("https?://linear\\.app/[^/ ]+/profiles/"))] | length) as $pu
      | ([$b[] | select(contains("@") and (test("(^|\\s)@[A-Za-z0-9._-]+") | not))] | length) as $ot
      | "mention.scanned: \($b | length)\nmention.at_name: \($at)\nmention.profile_url: \($pu)\nmention.other: \($ot)"' "$work/Comments.json" > "$work/mention.txt"
    while IFS= read -r l; do lines+=("$l"); printf '%s\n' "$l"; done < "$work/mention.txt"
  else failed "mention scan"; fi
fi

# --- 4. the write part ---
label_name="sindri-probe"
team_id="" label_id="" baseline_json="[]" archived_sample="" label_made=0
cnt_query='query ProbeIssues($filter: IssueFilter, $first: Int) { issues(filter: $filter, first: $first, includeArchived: false) { nodes { id } } }'
probe_filter() { jq -nc --arg t "$team_arg" --arg l "$label_name" '{team:{key:{eq:$t}},labels:{some:{name:{eq:$l}}}}'; }

# fresh_ids: the labelled non-archived issues of the last ProbeIssues answer that were not in the baseline, one id per line.
fresh_ids() { jq -r --argjson b "$baseline_json" '.data.issues.nodes[]?.id | select(. as $i | ($b | index($i)) == null)' "$work/ProbeIssues.json"; }

# baseline: the labelled non-archived issues that exist before this run's first create; cleanup leaves them alone.
read_baseline() {
  gql write ProbeIssues "$cnt_query" "$(jq -nc --argjson f "$(probe_filter)" '{filter:$f,first:250}')" || return 1
  baseline_json="$(jq -c '[.data.issues.nodes[]?.id]' "$work/ProbeIssues.json")"
  local n; n="$(jq 'length' <<<"$baseline_json")"
  if [ "$n" -gt 0 ]; then echo "warning: $n $label_name issues existed before this run; left alone" >&2; fi
}

# cleanup: archive the labelled issues in the team that this run made (every labelled issue not in the baseline, so a create whose answer
# was lost is caught too), then re-query. Never touches an issue without the label: the filter is the only way an id gets in, and a
# baseline issue is never archived. Runs once, from the main flow or the EXIT trap.
cleanup() {
  [ "$cleanup_armed" -eq 1 ] && [ "$cleanup_done" -eq 0 ] || return 0
  cleanup_done=1
  local found=0 done_n=0 round round_n id left
  for round in 1 2 3 4 5; do
    gql write ProbeIssues "$cnt_query" "$(jq -nc --argjson f "$(probe_filter)" '{filter:$f,first:250}')" || { failed "cleanup query"; break; }
    fresh_ids > "$work/fresh.txt"
    [ -s "$work/fresh.txt" ] || break
    round_n=0
    while IFS= read -r id; do
      found=$((found + 1))
      if gql write IssueArchive 'mutation IssueArchive($id: String!) { issueArchive(id: $id) { success } }' "$(jq -nc --arg id "$id" '{id:$id}')" \
         && jq -e '.data.issueArchive.success == true' "$work/IssueArchive.json" >/dev/null; then
        done_n=$((done_n + 1)); round_n=$((round_n + 1)); [ -n "$archived_sample" ] || archived_sample="$id"
      else failed "cleanup archive"; fi
    done < "$work/fresh.txt"
    [ "$round_n" -gt 0 ] || break
  done
  emit cleanup "archived $done_n of $found"
  if gql write ProbeIssues "$cnt_query" "$(jq -nc --argjson f "$(probe_filter)" '{filter:$f,first:250}')"; then
    left="$(fresh_ids | wc -l | tr -d ' ')"
    emit cleanup.left "$left"
    [ "$left" -eq 0 ] || failed "cleanup left $left labelled issue(s)"
  else emit cleanup.left unknown; failed "cleanup re-query"; fi
  archive_visibility
}

# archive_visibility: read-only, on an issue the cleanup just archived: is an archived issue hidden from `issues` unless includeArchived?
archive_visibility() {
  [ -n "$archived_sample" ] || return 0
  local vars hidden shown
  vars="$(jq -nc --arg id "$archived_sample" '{f:{id:{eq:$id}}}')"
  if gql write ArchivedDefault 'query ArchivedDefault($f: IssueFilter) { issues(filter: $f, first: 1) { nodes { id } } }' "$vars" \
     && gql write ArchivedIncluded 'query ArchivedIncluded($f: IssueFilter) { issues(filter: $f, first: 1, includeArchived: true) { nodes { id } } }' "$vars"; then
    hidden="$(jq '[.data.issues.nodes[]?] | length' "$work/ArchivedDefault.json")"; shown="$(jq '[.data.issues.nodes[]?] | length' "$work/ArchivedIncluded.json")"
    if [ "$hidden" -eq 0 ] && [ "$shown" -eq 1 ]; then emit archived.hidden_by_default yes
    elif [ "$hidden" -eq 1 ]; then emit archived.hidden_by_default no
    else emit archived.hidden_by_default unknown; fi
  else failed "archive visibility probe"; fi
}

uuid() { python3 -I -c 'import uuid; print(uuid.uuid4())'; }
create_issue() { # NAME TITLE DESCRIPTION [CLIENT_ID]
  gql write "$1" 'mutation IssueCreate($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { id } } }' \
    "$(jq -nc --arg team "$team_id" --arg l "$label_id" --arg t "$2" --arg d "$3" --arg id "${4:-}" \
      '{input: ({teamId:$team, labelIds:[$l], title:$t, description:$d} + (if $id != "" then {id:$id} else {} end))}')"
}
issue_read() { # ID -> $work/IssueRead.json holds updatedAt and the attachment ids
  gql write IssueRead 'query IssueRead($id: String!) { issue(id: $id) { updatedAt attachments { nodes { id } } } }' "$(jq -nc --arg id "$1" '{id:$id}')"
}
stamp() { jq -r '.data.issue.updatedAt // ""' "$work/IssueRead.json"; }
bumped() { if [ "$1" != "$2" ]; then echo yes; else echo no; fi; }
comment_create='mutation CommentCreate($input: CommentCreateInput!) { commentCreate(input: $input) { success comment { id } } }'
issue_update='mutation IssueUpdate($id: String!, $input: IssueUpdateInput!) { issueUpdate(id: $id, input: $input) { success } }'

write_part() {
  local rc cid1 cid2 a_id second u0 u1 u2 u3 n suffix t300 d70k link link_vars
  suffix="$(uuid | cut -c1-8)"
  gql write TeamId 'query TeamId($k: String!) { teams(filter: {key: {eq: $k}}) { nodes { id } } }' "$(jq -nc --arg k "$team_arg" '{k:$k}')" || { failed "team lookup"; return; }
  team_id="$(jq -r '.data.teams.nodes[0].id // empty' "$work/TeamId.json")"
  [ -n "$team_id" ] || { failed "team $team_arg not visible to the write key"; return; }
  gql write ProbeLabel 'query ProbeLabel($t: ID!, $n: String!) { issueLabels(filter: {team: {id: {eq: $t}}, name: {eq: $n}}) { nodes { id } } }' \
    "$(jq -nc --arg t "$team_id" --arg n "$label_name" '{t:$t,n:$n}')" || { failed "label lookup"; return; }
  label_id="$(jq -r '.data.issueLabels.nodes[0].id // empty' "$work/ProbeLabel.json")"
  if [ -n "$label_id" ]; then emit label existing
  else
    gql write LabelCreate 'mutation LabelCreate($input: IssueLabelCreateInput!) { issueLabelCreate(input: $input) { success issueLabel { id } } }' \
      "$(jq -nc --arg t "$team_id" --arg n "$label_name" '{input:{teamId:$t,name:$n}}')"; rc=$?
    if [ "$rc" -eq 3 ]; then emit write.allowed "no ($(errcode LabelCreate))"; return; fi
    [ "$rc" -eq 0 ] || { failed "label create"; return; }
    label_id="$(jq -r '.data.issueLabelCreate.issueLabel.id // empty' "$work/LabelCreate.json")"
    [ -n "$label_id" ] || { failed "label create (no id)"; return; }
    emit label created; label_made=1
  fi
  # the baseline comes before the first create; a label made just now has no issues, so there is nothing to read
  if [ "$label_made" -eq 0 ]; then read_baseline || { failed "baseline query"; return; }; fi
  cleanup_armed=1   # from here on the label exists and the cleanup has something to find

  # issue create with a client id
  cid1="$(uuid)"
  create_issue CreateA "sindri-probe $suffix a" "probe" "$cid1"; rc=$?
  if [ "$rc" -eq 3 ]; then
    emit create.client_id refused
    emit create.repeat_client_id "n/a (client id refused)"
    create_issue CreateA "sindri-probe $suffix a" "probe"; rc=$?
    if [ "$rc" -eq 3 ]; then emit write.allowed "no ($(errcode CreateA))"; return; fi
    cid1=""
  fi
  [ "$rc" -eq 0 ] || { failed "issue create"; return; }
  a_id="$(jq -r '.data.issueCreate.issue.id // empty' "$work/CreateA.json")"
  [ -n "$a_id" ] || { failed "issue create (no id)"; return; }
  emit write.allowed yes
  if [ -n "$cid1" ]; then
    if [ "$a_id" = "$cid1" ]; then emit create.client_id accepted; else emit create.client_id ignored; fi
    create_issue CreateRepeat "sindri-probe $suffix a" "probe" "$cid1"; rc=$?
    if [ "$rc" -eq 3 ]; then emit create.repeat_client_id "$(errcode CreateRepeat)"
    elif [ "$rc" -eq 0 ]; then
      second="$(jq -r '.data.issueCreate.issue.id // empty' "$work/CreateRepeat.json")"
      if [ "$second" = "$a_id" ]; then emit create.repeat_client_id same-issue; else emit create.repeat_client_id second-issue; fi
    else failed "repeat issue create"; fi
  fi

  # the baseline updatedAt, then a comment with a client id
  issue_read "$a_id" || { failed "issue read"; return; }
  u0="$(stamp)"
  cid2="$(uuid)"
  gql write CommentA "$comment_create" "$(jq -nc --arg i "$a_id" --arg id "$cid2" '{input:{issueId:$i,body:"probe comment",id:$id}}')"; rc=$?
  if [ "$rc" -eq 3 ]; then
    emit comment.client_id refused
    emit comment.repeat_client_id "n/a (client id refused)"
    gql write CommentA "$comment_create" "$(jq -nc --arg i "$a_id" '{input:{issueId:$i,body:"probe comment"}}')" || { failed "comment create"; return; }
  elif [ "$rc" -eq 0 ]; then
    if [ "$(jq -r '.data.commentCreate.comment.id // empty' "$work/CommentA.json")" = "$cid2" ]; then
      emit comment.client_id accepted
      gql write CommentRepeat "$comment_create" "$(jq -nc --arg i "$a_id" --arg id "$cid2" '{input:{issueId:$i,body:"probe comment",id:$id}}')"; rc=$?
      if [ "$rc" -eq 3 ]; then emit comment.repeat_client_id "$(errcode CommentRepeat)"
      elif [ "$rc" -eq 0 ]; then
        if [ "$(jq -r '.data.commentCreate.comment.id // empty' "$work/CommentRepeat.json")" = "$cid2" ]; then emit comment.repeat_client_id same-comment
        else emit comment.repeat_client_id second-comment; fi
      else failed "repeat comment create"; fi
    else emit comment.client_id ignored; emit comment.repeat_client_id second-comment; fi
  else failed "comment create"; return; fi
  issue_read "$a_id" || { failed "issue read"; return; }
  u1="$(stamp)"; emit updatedAt.bumped_by.comment "$(bumped "$u0" "$u1")"

  # attachmentLinkURL twice with the same url
  link='mutation AttachLink($issueId: String!, $url: String!, $title: String) { attachmentLinkURL(issueId: $issueId, url: $url, title: $title) { success } }'
  link_vars="$(jq -nc --arg i "$a_id" '{issueId:$i,url:"https://example.invalid/sindri-probe",title:"Sindri probe"}')"
  gql write AttachLink1 "$link" "$link_vars" && gql write AttachLink2 "$link" "$link_vars" || { failed "attachmentLinkURL"; return; }
  issue_read "$a_id" || { failed "issue read"; return; }
  u2="$(stamp)"; n="$(jq '[.data.issue.attachments.nodes[]?] | length' "$work/IssueRead.json")"
  if [ "$n" -le 1 ]; then emit attachment.repeat_url one; else emit attachment.repeat_url two; fi
  emit updatedAt.bumped_by.attachment "$(bumped "$u1" "$u2")"

  # an update
  gql write IssueUpdate "$issue_update" "$(jq -nc --arg i "$a_id" '{id:$i,input:{description:"probe update"}}')" || { failed "issue update"; return; }
  issue_read "$a_id" || { failed "issue read"; return; }
  u3="$(stamp)"; emit updatedAt.bumped_by.update "$(bumped "$u2" "$u3")"

  # input caps
  t300="$(head -c 300 /dev/zero | tr '\0' 't')"; d70k="$(head -c 70000 /dev/zero | tr '\0' 'd')"
  create_issue CapTitle "$t300" "probe"; rc=$?
  if [ "$rc" -eq 0 ] && jq -e '.data.issueCreate.success == true' "$work/CapTitle.json" >/dev/null; then emit cap.title_300 ok
  elif [ "$rc" -eq 3 ] || [ "$rc" -eq 0 ]; then emit cap.title_300 rejected
  else failed "title cap probe"; fi
  gql write CapDesc "$issue_update" "$(jq -nc --arg i "$a_id" --arg d "$d70k" '{id:$i,input:{description:$d}}')"; rc=$?
  if [ "$rc" -eq 0 ] && jq -e '.data.issueUpdate.success == true' "$work/CapDesc.json" >/dev/null; then emit cap.description_70k ok
  elif [ "$rc" -eq 3 ] || [ "$rc" -eq 0 ]; then emit cap.description_70k rejected
  else failed "description cap probe"; fi

  # paging: two pages of one issue in the team (read-only); the second must start after the first
  local pq p1 p2 cursor
  pq='query IssuePage($f: IssueFilter, $after: String) { issues(filter: $f, first: 1, after: $after) { nodes { id } pageInfo { hasNextPage endCursor } } }'
  if gql write IssuePage1 "$pq" "$(jq -nc --arg t "$team_arg" '{f:{team:{key:{eq:$t}}}}')"; then
    cursor="$(jq -r '.data.issues.pageInfo.endCursor // empty' "$work/IssuePage1.json")"; p1="$(jq -r '.data.issues.nodes[0].id // empty' "$work/IssuePage1.json")"
    if [ "$(jq -r '.data.issues.pageInfo.hasNextPage' "$work/IssuePage1.json")" != true ]; then emit pagination.after_cursor "not probed (one issue in the team)"
    elif [ -n "$cursor" ] && gql write IssuePage2 "$pq" "$(jq -nc --arg t "$team_arg" --arg a "$cursor" '{f:{team:{key:{eq:$t}}},after:$a}')"; then
      p2="$(jq -r '.data.issues.nodes[0].id // empty' "$work/IssuePage2.json")"
      if [ -n "$p2" ] && [ "$p2" != "$p1" ]; then emit pagination.after_cursor ok; else emit pagination.after_cursor failed; failed "pagination probe"; fi
    else emit pagination.after_cursor failed; failed "pagination probe"; fi
  else emit pagination.after_cursor failed; failed "pagination probe"; fi
  emit team_move.old_key_resolves "not probed"   # a team move is not something the probe does
}

if [ "$want_write" -eq 1 ]; then write_part; fi
cleanup   # before the paste block so its lines are in it; the EXIT trap runs it only when something died before this point

echo
echo "paste this into the Ph0 PR"
echo '```'
printf '%s\n' ${lines[@]+"${lines[@]}"}   # bash 3.2 calls an empty array unbound under set -u
echo '```'
if [ "${#failures[@]}" -gt 0 ]; then
  printf 'probe: %d call(s) failed: %s\n' "${#failures[@]}" "$(IFS=';'; echo "${failures[*]}")" >&2
  exit 1
fi
exit 0
