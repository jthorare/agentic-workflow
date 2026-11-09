#!/usr/bin/env bash
# scripts/probe-meeting-apps.sh — the Meetings block's probe (docs/superpowers/specs/2026-11-07-sindri-agent-first/prs/pr-10d.md).
# Owner-run, read-only: it records which API each note app exposes, an MCP server (initialize + tools/list) or a REST API (one GET).
# It prints one line per app: the kind of API, how many tools an MCP server lists and their names, or the HTTP status of the GET.
# It never prints a meeting, a note, a transcript or a key. Run it before GranolaMeetingSource / WisprFlowMeetingSource are written.
#
# Usage: scripts/probe-meeting-apps.sh [--app granola|wispr-flow]
# Keys (an access token from a signed-in connection, or an API key; at least one is needed):
#   AW_GRANOLA_PROBE_KEY      for granola       AW_GRANOLA_PROBE_URL      its endpoint (default https://mcp.granola.ai/mcp)
#   AW_WISPR_FLOW_PROBE_KEY   for wispr-flow    AW_WISPR_FLOW_PROBE_URL   its endpoint (required: no endpoint is confirmed yet)
# An app without a key prints "skipped". Exit 0 ok, 1 a call failed (the "failed:" lines say which), 2 usage or no key at all.
set -uo pipefail

only=""
while [ $# -gt 0 ]; do
  case "$1" in
    --app) only="${2:?--app needs granola or wispr-flow}"; shift 2 ;;
    -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
    *) echo "usage: $0 [--app granola|wispr-flow]" >&2; exit 2 ;;
  esac
done
case "$only" in ''|granola|wispr-flow) ;; *) echo "usage: --app takes granola or wispr-flow" >&2; exit 2 ;; esac

command -v jq >/dev/null || { echo "probe: jq is required" >&2; exit 2; }
command -v curl >/dev/null || { echo "probe: curl is required" >&2; exit 2; }

work="$(mktemp -d)"; chmod 700 "$work"
trap 'rm -rf "$work"' EXIT

failures=() lines=() any_key=0
emit() { lines+=("$1: $2"); printf '%s: %s\n' "$1" "$2"; }
failed() { failures+=("$1"); printf 'failed: %s\n' "$1" >&2; }

# post NAME URL BODY -> $work/NAME.json; returns 0 on a 2xx JSON answer. The key goes to curl through a 0600 config file, never an argument.
post() {
  local name="$1" url="$2" body="$3" code
  code="$(curl -sS -K "$work/curl.cfg" -X POST --max-time 30 -o "$work/$name.raw" -w '%{http_code}' --data "$body" "$url" 2>/dev/null)" || return 1
  case "$code" in 2*) ;; *) return 1 ;; esac
  # an MCP server may answer as a single JSON document or as one server-sent event; take the first JSON payload
  sed -n 's/^data: //p' "$work/$name.raw" | head -n1 > "$work/$name.json"
  [ -s "$work/$name.json" ] || cp "$work/$name.raw" "$work/$name.json"
  jq -e type "$work/$name.json" >/dev/null 2>&1
}

probe_app() { # app key url
  local app="$1" key="$2" url="$3" code n names
  if [ -z "$key" ]; then emit "$app" "skipped (no key)"; return; fi
  any_key=1
  if [ -z "$url" ]; then failed "$app (no endpoint: set AW_$(printf '%s' "$app" | tr 'a-z-' 'A-Z_')_PROBE_URL)"; return; fi
  printf 'header = "Authorization: Bearer %s"\nheader = "Content-Type: application/json"\nheader = "Accept: application/json, text/event-stream"\n' "$key" > "$work/curl.cfg"
  chmod 600 "$work/curl.cfg"
  if post init "$url" '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"aw-probe","version":"1"}}}' \
     && jq -e '.result.protocolVersion != null' "$work/init.json" >/dev/null 2>&1; then
    if post tools "$url" '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' && jq -e '.result.tools | type == "array"' "$work/tools.json" >/dev/null 2>&1; then
      n="$(jq '.result.tools | length' "$work/tools.json")"
      names="$(jq -r '[.result.tools[].name] | join(",")' "$work/tools.json")"
      emit "$app" "mcp tools=$n names=$names"
    else
      failed "$app tools/list"
    fi
    return
  fi
  code="$(curl -sS -K "$work/curl.cfg" --max-time 30 -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || { failed "$app (no answer from the endpoint)"; return; }
  case "$code" in 2*) emit "$app" "rest status=$code" ;; *) failed "$app (the endpoint answered $code)" ;; esac
}

if [ -z "$only" ] || [ "$only" = granola ]; then probe_app granola "${AW_GRANOLA_PROBE_KEY:-}" "${AW_GRANOLA_PROBE_URL:-https://mcp.granola.ai/mcp}"; fi
if [ -z "$only" ] || [ "$only" = wispr-flow ]; then probe_app wispr-flow "${AW_WISPR_FLOW_PROBE_KEY:-}" "${AW_WISPR_FLOW_PROBE_URL:-}"; fi

if [ "$any_key" -eq 0 ] && [ "${#failures[@]}" -eq 0 ]; then
  echo "probe: no key. Set AW_GRANOLA_PROBE_KEY and/or AW_WISPR_FLOW_PROBE_KEY (and AW_WISPR_FLOW_PROBE_URL)." >&2
  exit 2
fi

echo
echo "paste this into the PR 10d mappings PR"
echo '```'
printf '%s\n' ${lines[@]+"${lines[@]}"}   # bash 3.2 calls an empty array unbound under set -u
echo '```'
if [ "${#failures[@]}" -gt 0 ]; then
  printf 'probe: %d call(s) failed: %s\n' "${#failures[@]}" "$(IFS=';'; echo "${failures[*]}")" >&2
  exit 1
fi
exit 0
