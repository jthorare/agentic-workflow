#!/usr/bin/env bash
# scripts/probe-linear-projects.sh — the projects page spec's Ph0 probe (docs/superpowers/specs/2026-10-29-sindri-projects-page-design.md
# §12 Ph0, §4.1, §4.4, §4.5). Owner-run, read-only: it only sends GraphQL queries to Linear and `gh api` GETs. It prints counts and
# whether a field is present, never record text (no names, titles, descriptions or urls) and never the key.
#
# Usage: scripts/probe-linear-projects.sh [--repo owner/name] [--window-days N]
#   --repo         the GitHub repo for the PR-link counts (default: the first of the profile's context.sources.github.repos)
#   --window-days  the issue window (default: the profile's context.index.windowDays, else 183)
# Key: AW_LINEAR_PROBE_KEY when set; else the profile's context.sources.linear.token pointer ($AW_SINDRI_HOME/profile/profile.yaml;
# env:NAME, file:/path, keychain:service/account, op:vault/item/field or command:argv). Teams: context.sources.linear.teams.
# Exit 0 ok, 1 a probe call failed (the "failed:" lines say which), 2 usage or no key.
set -uo pipefail

repo_arg="" window_arg=""
while [ $# -gt 0 ]; do
  case "$1" in
    --repo) repo_arg="${2:?--repo needs owner/name}"; shift 2 ;;
    --window-days) window_arg="${2:?--window-days needs a number}"; shift 2 ;;
    -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
    *) echo "usage: $0 [--repo owner/name] [--window-days N]" >&2; exit 2 ;;
  esac
done
case "$window_arg" in *[!0-9]*) echo "usage: --window-days takes a whole number" >&2; exit 2 ;; esac

command -v jq >/dev/null || { echo "probe: jq is required" >&2; exit 2; }
command -v curl >/dev/null || { echo "probe: curl is required" >&2; exit 2; }

state_dir="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
sindri_home="${AW_SINDRI_HOME:-$state_dir/sindri-app}"
profile="$sindri_home/profile/profile.yaml"

work="$(mktemp -d)"; chmod 700 "$work"
trap 'rm -rf "$work"' EXIT

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

key="${AW_LINEAR_PROBE_KEY:-}"
if [ -z "$key" ]; then
  pointer="$(profile_get context.sources.linear.token | head -n1)"
  [ -n "$pointer" ] && key="$(resolve_pointer "$pointer")"
fi
if [ -z "$key" ]; then
  echo "probe: no Linear key. Set AW_LINEAR_PROBE_KEY, or connect Linear (the profile at $profile needs context.sources.linear.token)." >&2
  exit 2
fi

teams=()   # a while loop, not mapfile: macOS ships bash 3.2, which has no mapfile
while IFS= read -r team; do [ -n "$team" ] && teams+=("$team"); done < <(profile_get context.sources.linear.teams)
repo="$repo_arg"
[ -n "$repo" ] || repo="$(profile_get context.sources.github.repos | head -n1)"
window="${window_arg:-$(profile_get context.index.windowDays | head -n1)}"
case "$window" in ''|*[!0-9]*) window=183 ;; esac
if [ "${#teams[@]}" -eq 0 ]; then echo "probe: the profile lists no context.sources.linear.teams" >&2; exit 2; fi
teams_json="$(printf '%s\n' "${teams[@]}" | jq -R . | jq -sc .)"
window_start="$(date -u -d "-${window} days" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -v-"${window}"d +%Y-%m-%dT%H:%M:%SZ)"

# The key goes to curl through a 0600 config file, never an argument.
printf 'header = "Authorization: %s"\nheader = "Content-Type: application/json"\n' "$key" > "$work/curl.cfg"
chmod 600 "$work/curl.cfg"

lines=() failures=()
emit() { lines+=("$1: $2"); printf '%s: %s\n' "$1" "$2"; }
failed() { failures+=("$1"); printf 'failed: %s\n' "$1" >&2; }

# gql NAME QUERY VARS_JSON -> $work/NAME.json (body), $work/NAME.h (headers). Returns 0 ok, 3 GraphQL errors, 1 transport failure.
gql() {
  local name="$1" query="$2" vars="$3" code
  jq -n --arg q "$query" --argjson v "$vars" '{query:$q,variables:$v}' > "$work/$name.req"
  code="$(curl -sS -K "$work/curl.cfg" -X POST --max-time 60 -D "$work/$name.h" -o "$work/$name.json" -w '%{http_code}' \
    --data @"$work/$name.req" https://api.linear.app/graphql 2>/dev/null)" || return 1
  jq -e type "$work/$name.json" >/dev/null 2>&1 || return 1
  if jq -e '.errors != null' "$work/$name.json" >/dev/null 2>&1; then return 3; fi
  case "$code" in 2*) return 0 ;; *) return 1 ;; esac
}

# gql_count NAME QUERY FILTER_JSON: pages an `issues` connection of ids (250 a page, at most 20 pages). Returns gql's code; on 0
# the count is in $count_out.
count_out=0
gql_count() {
  local name="$1" query="$2" filter="$3" after="null" n=0 page rc
  for page in $(seq 1 20); do
    gql "$name" "$query" "$(jq -nc --argjson f "$filter" --argjson a "$after" '{filter:$f,first:250,after:$a}')"; rc=$?
    [ "$rc" -eq 0 ] || return "$rc"
    n=$((n + $(jq '[.data.issues.nodes[]?] | length' "$work/$name.json")))
    jq -e '.data.issues.pageInfo.hasNextPage == true' "$work/$name.json" >/dev/null || break
    after="$(jq -c '.data.issues.pageInfo.endCursor' "$work/$name.json")"
  done
  count_out=$n
}

# --- 1. ProjectsPull (spec 4.1) ---
projects_query() { # $1 = status fragment, $2 = lastUpdate fragment
  printf 'query ProjectsPull($filter: ProjectFilter, $first: Int, $after: String) { projects(filter: $filter, first: $first, after: $after, orderBy: updatedAt) { nodes { id updatedAt startedAt targetDate %s lead { id } %s } pageInfo { hasNextPage endCursor } } }' "$1" "$2"
}
pfilter="$(jq -nc --argjson t "$teams_json" '{accessibleTeams:{some:{key:{in:$t}}}}')"
status_field="status" update_frag='lastUpdate { health createdAt }'
pull_projects() { # collects every page of one query shape into $work/projects.ndjson; returns the first failing code
  local after="null" page rc q
  q="$(projects_query "$1" "$2")"
  : > "$work/projects.ndjson"
  for page in $(seq 1 20); do
    gql ProjectsPull "$q" "$(jq -nc --argjson f "$pfilter" --argjson a "$after" '{filter:$f,first:50,after:$a}')"; rc=$?
    [ "$rc" -eq 0 ] || return "$rc"
    jq -c '.data.projects.nodes[]?' "$work/ProjectsPull.json" >> "$work/projects.ndjson"
    jq -e '.data.projects.pageInfo.hasNextPage == true' "$work/ProjectsPull.json" >/dev/null || break
    after="$(jq -c '.data.projects.pageInfo.endCursor' "$work/ProjectsPull.json")"
  done
}
projects_ok=0
if pull_projects 'status { name type }' "$update_frag"; then projects_ok=1
elif pull_projects 'state' "$update_frag"; then projects_ok=1; status_field="state"; emit status "unsupported, using state"
elif pull_projects 'state' ''; then projects_ok=1; status_field="state"; update_frag=""; emit status "unsupported, using state"; emit lastUpdate unsupported
else failed "ProjectsPull (no query shape was accepted)"; fi

viewer_id=""
if [ "$projects_ok" -eq 1 ]; then
  jq -s . "$work/projects.ndjson" > "$work/projects.json"
  emit projects "$(jq length "$work/projects.json")"
  present() { emit "present.$1" "$(jq "[.[] | select($2 != null)] | length" "$work/projects.json")"; }
  if [ "$status_field" = status ]; then present status .status; present status.type .status.type; else present state .state; fi
  present startedAt .startedAt
  if [ -n "$update_frag" ]; then present lastUpdate .lastUpdate; present lastUpdate.health .lastUpdate.health; fi
  present targetDate .targetDate
  present lead .lead
fi

# --- 2. viewer ---
if gql Viewer 'query Viewer { viewer { id name } }' '{}'; then
  viewer_id="$(jq -r '.data.viewer.id // empty' "$work/Viewer.json")"
  emit viewer ok
  if [ "$projects_ok" -eq 1 ]; then emit projects.led "$(jq --arg v "$viewer_id" '[.[] | select(.lead.id == $v)] | length' "$work/projects.json")"; fi
else failed "viewer"; fi

# --- 3. the widened issue filter (spec 4.4) ---
open_state='{"type":{"nin":["completed","canceled"]}}'
count_query='query IssueCount($filter: IssueFilter, $first: Int, $after: String) { issues(filter: $filter, first: $first, after: $after) { nodes { id } pageInfo { hasNextPage endCursor } } }'
widened() { # $1 = the project clause
  jq -nc --argjson t "$teams_json" --argjson s "$open_state" --argjson p "$1" --arg w "$window_start" \
    '{team:{key:{in:$t}},state:$s,project:$p,updatedAt:{lt:$w}}'
}
gql_count Widened "$count_query" "$(widened '{"status":{"type":{"nin":["completed","canceled"]}}}')"; rc=$?
if [ "$rc" -eq 0 ]; then
  emit filter.project.status.type accepted:yes
  emit issues.open_old_in_open_projects "$count_out"
elif [ "$rc" -eq 3 ]; then
  emit filter.project.status.type accepted:no
  if gql_count Widened "$count_query" "$(widened '{"state":{"nin":["completed","canceled"]}}')"; then
    emit filter.project.state accepted:yes
    emit issues.open_old_in_open_projects "$count_out"
  else emit filter.project.state accepted:no; failed "widened issue filter (neither project.status nor project.state)"; fi
else failed "widened issue filter"; fi

# --- 4. IssuesPull with attachments at 25 a page ---
issues_query='query IssuesPull($filter: IssueFilter, $first: Int, $after: String) { issues(filter: $filter, first: $first, after: $after, orderBy: updatedAt) { nodes { id identifier attachments(first: 10) { nodes { url } } } pageInfo { hasNextPage endCursor } } }'
ifilter="$(jq -nc --argjson t "$teams_json" '{team:{key:{in:$t}}}')"
gql IssuesPull "$issues_query" "$(jq -nc --argjson f "$ifilter" '{filter:$f,first:25,after:null}')"; rc=$?
if [ "$rc" -eq 0 ]; then
  emit issues.attachments25 accepted:yes
  emit issues.page "$(jq '[.data.issues.nodes[]?] | length' "$work/IssuesPull.json")"
  while IFS=: read -r hname hval; do
    hval="$(printf '%s' "$hval" | tr -d '[:space:]')"
    if [ -n "$hname" ]; then emit "ratelimit.$(printf '%s' "$hname" | tr 'A-Z' 'a-z')" "$hval"; fi
  done < <(tr -d '\r' < "$work/IssuesPull.h" | grep -iE '^(x-ratelimit|x-complexity)[a-z-]*:' || true)
elif [ "$rc" -eq 3 ]; then emit issues.attachments25 accepted:no
else failed "IssuesPull with attachments"; fi

# --- 5. per led project: stored-window open count vs Linear's own ---
if [ "$projects_ok" -eq 1 ] && [ -n "$viewer_id" ]; then
  n=0
  while read -r pid; do
    n=$((n + 1))
    in_window="$(jq -nc --arg p "$pid" --arg w "$window_start" --argjson s "$open_state" '{project:{id:{eq:$p}},state:$s,updatedAt:{gte:$w}}')"
    all_open="$(jq -nc --arg p "$pid" --argjson s "$open_state" '{project:{id:{eq:$p}},state:$s}')"
    if gql_count LedWindow "$count_query" "$in_window"; then inwin=$count_out; else failed "led project $n window count"; continue; fi
    if gql_count LedAll "$count_query" "$all_open"; then emit "project $n" "$inwin vs $count_out"; else failed "led project $n Linear count"; fi
  done < <(jq -r --arg v "$viewer_id" '.[] | select(.lead.id == $v) | .id' "$work/projects.json")
fi

# --- 6. PR links: the two sources over the last 50 merged PRs (spec 4.5) ---
if [ -z "$repo" ]; then
  emit pr.links "skipped (no repo: the profile lists no context.sources.github.repos and no --repo)"
elif ! command -v gh >/dev/null; then failed "gh is not installed"
else
  : > "$work/prs.ndjson"
  for page in 1 2 3 4 5; do
    gh api "repos/$repo/pulls?state=closed&sort=updated&direction=desc&per_page=100&page=$page" > "$work/pulls.json" 2>/dev/null || { failed "gh api pulls"; break; }
    jq -c '.[] | select(.merged_at != null) | {n:.number, text:((.title // "") + " " + (.body // ""))}' "$work/pulls.json" >> "$work/prs.ndjson"
    if [ "$(wc -l < "$work/prs.ndjson")" -ge 50 ] || [ "$(jq length "$work/pulls.json")" -lt 100 ]; then break; fi
  done
  head -n 50 "$work/prs.ndjson" > "$work/prs50.ndjson"
  emit pr.merged_checked "$(wc -l < "$work/prs50.ndjson")"
  att_query='query PrAttachments($filter: AttachmentFilter, $first: Int, $after: String) { attachments(filter: $filter, first: $first, after: $after) { nodes { url } pageInfo { hasNextPage endCursor } } }'
  afilter="$(jq -nc --arg r "github.com/$repo/pull/" '{url:{contains:$r}}')"
  after="null"; : > "$work/att.txt"; att_ok=1
  for page in $(seq 1 10); do
    if ! gql PrAttachments "$att_query" "$(jq -nc --argjson f "$afilter" --argjson a "$after" '{filter:$f,first:250,after:$a}')"; then att_ok=0; failed "PR attachments"; break; fi
    jq -r '.data.attachments.nodes[]?.url' "$work/PrAttachments.json" >> "$work/att.txt"
    jq -e '.data.attachments.pageInfo.hasNextPage == true' "$work/PrAttachments.json" >/dev/null || break
    after="$(jq -c '.data.attachments.pageInfo.endCursor' "$work/PrAttachments.json")"
  done
  if [ "$att_ok" -eq 1 ]; then
    jq -s --slurpfile urls <(jq -R . "$work/att.txt") --arg repo "$repo" --argjson teams "$teams_json" '
      ($teams | map(. + "-[0-9]+") | join("|")) as $keys
      | ($repo | gsub("\\."; "\\.")) as $r
      | map(. as $pr | {a: ([$urls[] | select(test("^https://github\\.com/" + $r + "/pull/" + ($pr.n | tostring) + "(/|$)"; "i"))] | length > 0),
                        k: ($pr.text | test("(^|[^A-Za-z0-9])(" + $keys + ")([^0-9]|$)"))})
      | {source1_attachment: (map(select(.a)) | length), source2_key_in_text: (map(select(.k)) | length),
         both: (map(select(.a and .k)) | length), either: (map(select(.a or .k)) | length), neither: (map(select((.a or .k) | not)) | length)}' \
      "$work/prs50.ndjson" > "$work/pr-counts.json"
    while IFS=$'\t' read -r k v; do emit "pr.$k" "$v"; done < <(jq -r 'to_entries[] | [.key, .value] | @tsv' "$work/pr-counts.json")
  fi
fi

echo
echo "paste this into the Ph1/Ph6 PR"
echo '```'
printf '%s\n' ${lines[@]+"${lines[@]}"}   # bash 3.2 calls an empty array unbound under set -u
echo '```'
if [ "${#failures[@]}" -gt 0 ]; then
  printf 'probe: %d call(s) failed: %s\n' "${#failures[@]}" "$(IFS=';'; echo "${failures[*]}")" >&2
  exit 1
fi
exit 0
