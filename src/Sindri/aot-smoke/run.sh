#!/usr/bin/env bash
# Publish smoke for sindri (src/Sindri/Aw.Sindri.Host), CI's AOT-job entry for the host (engine spec rev 8 §11, heavy
# lane: "the framework-dependent publish of the Sindri host"). The host is framework-dependent, not NativeAOT: it hosts
# the Temporal worker through Aw.Sindri.Workflows and the Temporal .NET SDK has no trimming support
# (temporalio/sdk-dotnet#286; .agents/rules/csharp.md "AOT scope"). So this publishes the host framework-dependent and
# portable (no runtime identifier, no self-contained runtime) with warnings as errors, checks that the output is
# framework-dependent and carries the Temporal SDK with its native bridge, then drives the published apphost 'sindri'
# (DOTNET_ROOT set from the 'dotnet' on PATH when unset) through the S1 and API P1 journeys against a temp home (serve
# brings up the pinned Temporal dev server, so the temporal CLI must be installed: scripts/binaries-lock.sh fetch
# temporal): doctor with nothing running, start headless with an unapproved profile, a second serve refused by the lease,
# the approve preview and its non-terminal refusal, a session's typed approve refused (SND-AUTH-002), the human's typed
# approval, status (text and --json's apiVersion), /sindri/mcp's initialize and tools/list as a stateless MCP client,
# the views rebuild refused to a session and requested by the human, status's views and health lines, the item verbs (API P3: a human's item add, the queue and the item list, a session's item add refused, the human's item close, the done list and the queue again), the request verbs (API P4: a session's request, the needs-you list, a session's decide refused, the human's decide, the list again and the accepted requests), the ledger read (API P5: the human's lists command.handled, a session's doesn't, one item's events, and GET /events/catalog), the service and job verbs (API P6: the services list as JSON, a manual run of heartbeat followed to its end, a session's run refused, jobs show and the jobs list, pause and resume with a session's pause refused, and the services list after each), a session's stop refused, the human's stop, status and doctor again (doctor with HOME on an empty scratch dir, so its
# context:mcp-scope row never reads the real provider files);
# then recipe validate on the shipped recipes (their hashes; workflow.item reads its child beside it) and on an invalid
# fixture (RCP-REACH-001, exit 1). The normalized output (pids, ports, hashes, times, the host name and the temp path
# masked) must equal expected.txt.
# The apphost, not 'dotnet sindri.dll': start re-launches its own executable as 'serve' (Environment.ProcessPath), which
# under 'dotnet sindri.dll' is the dotnet muxer.
# Callers (API spec §6): the run unsets the agent variables (the pinned probe's markers, as sindri-prove.sh human does:
# CLAUDECODE, CLAUDE_CODE_ENTRYPOINT, CLAUDE_CODE_EXECPATH, CLAUDE_CODE_SESSION_ID, CLAUDE_PID, AW_SINDRI_CHILD, CODEX_SANDBOX, CURSOR_AGENT)
# and sets AW_SINDRI_PROVER=1, so the binary is the human; a "(human, terminal)" step runs under a pty
# (scripts/lib/with-tty.sh: CRs and terminal control sequences stripped, stderr merged), a "(session)" step sets CLAUDECODE=1 with stdin from /dev/null.
# Every invocation of the binary is bounded (30 s, perl's alarm); one that runs over prints 'exit 124'.
# The publish runs on a copy of the files it needs, in a temp dir under $TMPDIR that is deleted on exit, so the
# checkout's lock files and build output are never touched.
# Kept in artifacts/aot-sindri-smoke/ (gitignored): bin/ (the publish output with the apphost), publish.log, actual.txt.
set -euo pipefail
export AW_LOG_LEVEL=none

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
# shellcheck source=../../../scripts/lib/with-tty.sh
source "$root/scripts/lib/with-tty.sh"
unset CLAUDECODE CLAUDE_CODE_ENTRYPOINT CLAUDE_CODE_EXECPATH CLAUDE_CODE_SESSION_ID CLAUDE_PID AW_SINDRI_CHILD CODEX_SANDBOX CURSOR_AGENT
export AW_SINDRI_PROVER=1
out="$root/artifacts/aot-sindri-smoke"
mkdir -p "$out"
[ ! -d "$out/bin" ] || find "$out/bin" -delete
alarm=(perl -e 'alarm shift; exec @ARGV or die "exec $ARGV[0]: $!"' 30)   # SIGALRM after 30 s; a command, so with_tty can run it
bounded() { "${alarm[@]}" "$@"; }
bin="$out/bin/sindri"
scratch=""
work=""
# On any exit, a server this run started is stopped before its home is deleted: only once this run's home is exported
# (never the owner's default home), as the human at a terminal (a session's stop is refused, SND-AUTH-002), and, should
# that fail, by signalling the pid control.json still names, then waiting (bounded) for it to exit. The scratch tree is
# deleted on any exit, so the trap is set before it is made.
cleanup() {
  [ -n "$scratch" ] || return 0
  local control="$work/home/control.json" pid
  if [ -x "$bin" ] && [ "${AW_SINDRI_HOME:-}" = "$work/home" ] && [ -f "$control" ]; then
    with_tty "${alarm[@]}" "$bin" stop </dev/null >/dev/null 2>&1 || true
    pid="$(jq -r '.pid // empty' "$control" 2>/dev/null || true)"
    if [ -n "$pid" ]; then
      kill "$pid" 2>/dev/null || true
      for _ in 1 2 3 4 5 6 7 8 9 10; do kill -0 "$pid" 2>/dev/null || break; sleep 0.2; done
    fi
  fi
  find "$scratch" -delete
}
trap cleanup EXIT
# The physical path: macOS's $TMPDIR is under /var, a link to /private/var, and restore given both spellings of one
# project writes its props twice ("…nuget.g.props already exists").
scratch="$(cd "$(mktemp -d "${TMPDIR:-/tmp}/aot-sindri-smoke.XXXXXX")" && pwd -P)"
tree="$scratch/tree"
work="$scratch/run"
mkdir -p "$tree" "$work"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt context/onyx.lock \
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Context src/Judge src/Sindri | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
dotnet publish "$tree/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release --no-self-contained -warnaserror   -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }

# Framework-dependent: the runtimeconfig names the shared framework and no runtime ships beside the host.
pub="$out/bin"
grep -q '"name": "Microsoft.AspNetCore.App"' "$pub/sindri.runtimeconfig.json" || { echo "aot-sindri-smoke: sindri.runtimeconfig.json names no shared framework" >&2; exit 1; }
[ ! -e "$pub/System.Private.CoreLib.dll" ] || { echo "aot-sindri-smoke: the publish is self-contained (System.Private.CoreLib.dll beside the host)" >&2; exit 1; }
# The Temporal SDK and its native core bridge for this platform are in the publish output.
[ -f "$pub/Temporalio.dll" ] || { echo "aot-sindri-smoke: Temporalio.dll is missing from the publish output" >&2; exit 1; }
os="$(uname -s)"; arch="$(uname -m)"
case "$os/$arch" in
  Darwin/arm64) bridge=runtimes/osx-arm64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Darwin/x86_64) bridge=runtimes/osx-x64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Linux/x86_64) bridge=runtimes/linux-x64/native/libtemporalio_sdk_core_c_bridge.so ;;
  Linux/aarch64) bridge=runtimes/linux-arm64/native/libtemporalio_sdk_core_c_bridge.so ;;
  *) bridge="" ;;
esac
[ -z "$bridge" ] || [ -f "$pub/$bridge" ] || { echo "aot-sindri-smoke: the Temporal core bridge $bridge is missing from the publish output" >&2; exit 1; }
[ -x "$bin" ] || { echo "aot-sindri-smoke: the apphost 'sindri' is missing from the publish output" >&2; exit 1; }
if [ -z "${DOTNET_ROOT:-}" ]; then
  DOTNET_ROOT="$(cd "$(dirname "$(perl -MCwd=abs_path -e 'print abs_path(shift)' "$(command -v dotnet)")")" && pwd)"
  export DOTNET_ROOT
fi

export AW_SINDRI_HOME="$work/home" AW_SINDRI_PORT=0
mkdir -p "$AW_SINDRI_HOME/profile"
printf 'schemaVersion: 1\nuser: smoke\nservices:\n  heartbeat: { every: 5s }\n' >"$AW_SINDRI_HOME/profile/profile.yaml"

exit_line() { # $1 = exit code
  local rc="$1"
  [ "$rc" -ne 142 ] || rc=124   # killed by the alarm
  [ "$rc" -eq 0 ] || echo "exit $rc"
}
run() { # the human, no terminal
  echo "## sindri $*"
  local rc=0
  bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  exit_line "$rc"
}
human() { # the human at a terminal; stdin passed through (the typed confirmation)
  echo "## sindri $* (human, terminal)"
  local rc=0
  with_tty "${alarm[@]}" "$bin" "$@" || rc=$?
  exit_line "$rc"
}
session() { # an agent session (CLAUDECODE set), no terminal
  echo "## sindri $* (session)"
  local rc=0
  CLAUDECODE=1 bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  exit_line "$rc"
}
doctor() { # doctor reads the providers' user-wide MCP files: point it at an empty scratch home, never the real one
  HOME="$work/fakehome" CLAUDE_CONFIG_DIR="" CODEX_HOME="" run doctor
}
mcp() { # $1 = JSON-RPC method, $2 = params: one stateless /sindri/mcp call as an MCP client sends it (no Origin)
  local url
  url="$(jq -r '.url' "$AW_SINDRI_HOME/control.json")"
  curl -s -m 30 -X POST "${url%/}/sindri/mcp" -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
    -H 'X-Sindri-Repo: smoke' -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"$1\",\"params\":$2}" | sed -n 's/^data: //p'
}
settle() { # the projector has caught up and isn't rebuilding (it polls once a second): status's views line is then stable.
  # Bounded by wall time, not a count, so a loaded box gets the same 60 s; a timeout prints a named line into the transcript
  # (so the diff says why) instead of falling through to a snapshot taken mid-rebuild.
  local deadline=$((SECONDS + 60))
  while ((SECONDS < deadline)); do
    jq -e '.views | (.cursor == .ledgerHead and (.rebuilding | not))' <<<"$(bounded "$bin" status --json </dev/null 2>/dev/null)" >/dev/null 2>&1 && return 0
    sleep 0.25
  done
  echo "## settle: the views were still rebuilding or behind after 60 s"
}
items_in() { # $1 = state, $2 = count: the items view lists that many in that state (the projector polls once a second);
  # bounded by wall time like settle, with a named line on a timeout
  local deadline=$((SECONDS + 60))
  while ((SECONDS < deadline)); do
    jq -e --argjson n "$2" '.items | length == $n' <<<"$(bounded "$bin" items --state "$1" --json </dev/null 2>/dev/null)" >/dev/null 2>&1 && return 0
    sleep 0.25
  done
  echo "## items_in: the items view didn't list $2 '$1' item(s) after 60 s"
}
session_tty() { # an agent session at a terminal (the CLI's own terminal check passes, so the server decides)
  echo "## sindri $* (session, terminal)"
  local rc=0
  with_tty env CLAUDECODE=1 "${alarm[@]}" "$bin" "$@" || rc=$?
  exit_line "$rc"
}
{
  doctor
  human --headless </dev/null
  run serve
  human profile approve </dev/null
  hash="$({ bounded "$bin" profile validate --json </dev/null || true; } | sed -E 's/.*"hash":"([0-9a-f]{64})".*/\1/')"
  run profile approve "$hash"
  printf '%s\n' "approve profile ${hash:0:6}" | session_tty profile approve "$hash"
  printf '%s\n' "approve profile ${hash:0:6}" | human profile approve "$hash"
  settle
  run status
  echo "## sindri status --json | jq '{running, apiVersion}'"
  json="$({ bounded "$bin" status --json </dev/null 2>&1 || true; })"
  jq -c '{running, apiVersion}' <<<"$json" 2>/dev/null || printf '%s\n' "$json"
  echo "## /sindri/mcp initialize, then tools/list (stateless)"
  mcp initialize '{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"1"}}' | jq -c '.result.serverInfo.name'
  mcp tools/list '{}' | jq -c '[.result.tools[].name]'
  session views rebuild
  human views rebuild </dev/null
  settle
  run status
  human item add "Smoke item" </dev/null
  items_in new 1
  run queue
  run items --state new
  session item add "Refused by the desk"
  item="$(bounded "$bin" items --json </dev/null | jq -r '.items[0].id')"
  human item close "$item" </dev/null
  items_in done 1
  run items --state done
  run queue
  session request note "Smoke request"
  settle
  run needs-you
  decision="$(bounded "$bin" needs-you --json </dev/null | jq -r '.rows[0].subject.id')"
  session decide "$decision" accept
  human decide "$decision" accept </dev/null
  settle
  run needs-you
  run requests --state accepted
  echo "## sindri ledger --json --after 0 | jq (human: command.handled is listed)"
  with_tty "${alarm[@]}" "$bin" ledger --json --after 0 </dev/null | jq -c '[.events[].type] | any(. == "command.handled")'
  echo "## sindri ledger --json --after 0 | jq (session: public types only)"
  CLAUDECODE=1 bounded "$bin" ledger --json --after 0 </dev/null | jq -c '[.events[].type] | any(. == "command.handled")'
  echo "## sindri ledger --item <closed item> --json | jq (the item's events)"
  with_tty "${alarm[@]}" "$bin" ledger --item "$item" --type 'item.*' --json </dev/null | jq -c '[.events[].type]'
  echo "## GET /sindri/events/catalog | jq (public types, their invalidates)"
  curl -s -m 30 "$(jq -r '.url' "$AW_SINDRI_HOME/control.json" | sed 's|/$||')/sindri/events/catalog" -H 'X-Sindri-Repo: smoke' \
    | jq -c '{commandHandled: any(.types[]; .type == "command.handled"), itemCreated: (.types[] | select(.type == "item.created") | .invalidates)}'
  echo "## sindri services --json | jq (name, every, paused, job)"
  bounded "$bin" services --json </dev/null | jq -c '[.services[] | {name, every, paused, job}]'
  human services run heartbeat </dev/null
  settle
  session services run heartbeat
  job="$(bounded "$bin" jobs --json </dev/null | jq -r '.jobs[0].id')"
  run jobs show "$job"
  run jobs --state succeeded
  human services pause heartbeat </dev/null
  session services pause heartbeat
  settle
  run services
  human services resume heartbeat </dev/null
  settle
  run services
  session stop
  human stop </dev/null
  run status
  doctor
  run recipe validate "$root/recipes/sindri/workflow.step.yaml"
  run recipe validate "$root/recipes/sindri/workflow.item.yaml"
  run recipe validate "$root/tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml"
} >"$out/actual.raw"

bash "$here/normalize.sh" "$work" "$root" <"$out/actual.raw" >"$out/actual.txt"
diff -u "$here/expected.txt" "$out/actual.txt"
echo "aot-sindri-smoke: ok (framework-dependent publish, $(du -sh "$pub" | cut -f1))"
