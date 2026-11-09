#!/usr/bin/env bash
# scripts/sindri-prove.sh — run the C# Sindri CLI or its HTTP API the way a phase's prover must (API spec §12,
# plan P1 Unit E): against a guarded temp home only, as the calling agent or standing in for the owner.
#
#   sindri-prove.sh <verb> [args…]        $SINDRI <verb> as the calling agent (a session): the environment is untouched
#   sindri-prove.sh human [<verb> [args…]]
#                                         $SINDRI as the owner: the agent variables (the pinned probe's, engine
#                                         spec §6.2: CLAUDECODE, CLAUDE_CODE_ENTRYPOINT, CLAUDE_CODE_EXECPATH,
#                                         CLAUDE_CODE_SESSION_ID, CLAUDE_PID, CODEX_SANDBOX, CURSOR_AGENT,
#                                         AW_SINDRI_CHILD) unset, AW_SINDRI_PROVER=1, under a pty
#                                         (scripts/lib/with-tty.sh), stdin passed through, CRs and terminal control
#                                         sequences stripped; stdout and stderr arrive merged on stdout. Exit: the CLI's.
#                                         e.g. printf 'approve workflow.x 1\n' | sindri-prove.sh human recipe approve recipe.yaml
#   sindri-prove.sh curl [--stream] [--no-terminal] [--no-token | --token T] [-H 'Name: value']… <METHOD> <path> [json]
#                                         a human request to the running process: the URL from
#                                         $AW_SINDRI_HOME/control.json, headers X-Sindri-Caller: human,
#                                         X-Sindri-Terminal: 1 (unless --no-terminal), X-Sindri-Client: prover,
#                                         X-Sindri-Token from $AW_SINDRI_HOME/auth/human.token (unless --no-token or
#                                         --token T), Content-Type: application/json with a body. Prints the body on
#                                         stdout and "HTTP <status>" on stderr. Exit 0 on 2xx, 1 on another status,
#                                         3 when no response came. The body prints at the end; for the event stream
#                                         (GET /sindri/events) pass --stream: curl -N with the same human headers and
#                                         token, the body straight to stdout as it arrives (no buffer file, no status
#                                         line; exit 0 when it ends, 1 on an HTTP error status, 3 when no response came).
#   sindri-prove.sh url                   the base URL, for session requests written by hand (no caller header)
#   sindri-prove.sh publish [--session [--repo NAME]]
#                                         GET /sindri/context/map/publish (code index spec 6.4): how the current map was published, or that it
#                                         is held for the code index. As the owner by default (the curl mode's headers); --session sends no caller
#                                         header, as a session reads it (--repo sends X-Sindri-Repo): only that repo's lines, no tally. Prints the
#                                         JSON body (its "state" is held, overdue, published, published-without-waiting, published-stale or
#                                         published-by-hand); exit 0 on 2xx, 1 on another status, 3 when no response came.
#
# The guard runs first, for every mode: AW_SINDRI_HOME must be set and its real path strictly inside
# realpath(${TMPDIR:-/tmp}) or inside a proof folder, <AW_STATE_DIR or ~/.agentic-workflow>/<repo slug>/proof/…,
# a subset of the homes the server honours X-Sindri-Client: prover for (src/Sindri/Aw.Sindri.Infrastructure/ProverHome.cs):
# the guard also refuses a home whose deepest existing part is a dangling link or a file, which the server would accept.
# A home under the temp root gets the marker file .prover-scratch (setup-once spec §4 item 6), which lets human:prover confirm what the app confirms. Parts of the home that don't exist yet are judged as written. Anything else exits 2, so a prover can never reach the
# owner's real ledger. AW_SINDRI_PORT=0 is exported (a free port, recorded in control.json).
#
# SINDRI is the command line of the C# CLI, split on spaces. Default: dotnet run --project src/Sindri/Aw.Sindri.Host --
# (rebuilds per call, and a signal reaches dotnet, not sindri); a built binary is recommended for proofs. Never the
# `sindri` on PATH, which is the TS CLI.
#
# Seeding a profile: write profile.yaml, check it with `$SINDRI profile validate --json`; edits apply on save, there is no approval.
#
# Exit 2: the guard, a usage error, curl or url mode with no control.json, or curl mode with no token file (unless
# --no-token or --token).
# Tests: bash scripts/tests/sindri-prove.test.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=lib/with-tty.sh
source "$ROOT/scripts/lib/with-tty.sh"

die() { echo "sindri-prove: $*" >&2; exit 2; }
usage() { sed -n '5,31p' "$0" | sed 's/^# \{0,1\}//'; }

case "${1:-}" in
  -h | --help) usage; exit 0 ;;
  "") usage >&2; exit 2 ;;
esac

# lexical PATH — absolute, with . and .. collapsed (as Path.GetFullPath does before links are read).
lexical() {
  local p="$1" part out=""
  case "$p" in /*) ;; *) p="$PWD/$p" ;; esac
  local IFS=/
  # shellcheck disable=SC2086 # split on / on purpose
  set -f; set -- $p; set +f
  for part in "$@"; do
    case "$part" in
      "" | .) ;;
      ..) out="${out%/*}" ;;
      *) out="$out/$part" ;;
    esac
  done
  printf '%s\n' "${out:-/}"
}

# real PATH — like realpath(3), but parts that don't exist yet are kept as written (as the server's RealPath does).
# Fails when the deepest existing part can't be resolved (a dangling link, a file).
real() {
  local full rest="" head
  full="$(lexical "$1")"
  head="$full"
  while [ "$head" != "/" ] && [ ! -e "$head" ] && [ ! -L "$head" ]; do
    rest="/${head##*/}$rest"
    head="${head%/*}"
    [ -n "$head" ] || head="/"
  done
  head="$(cd -P "$head" 2>/dev/null && pwd -P)" || return 1
  [ "$head" = "/" ] && head=""
  printf '%s\n' "${head}${rest}"
}

# inside PATH ROOT — PATH is strictly below ROOT.
inside() {
  case "$1" in "${2%/}"/?*) return 0 ;; *) return 1 ;; esac
}

# The marker only this script writes (setup-once spec §4 item 6): a home under the temp root that the prover uses is a scratch home, and only there may
# human:prover confirm what the app confirms (the server checks the file). The owner's default home is never under the temp root, so it never carries it.
mark_scratch() {
  mkdir -p "$1" && : > "$1/.prover-scratch" || die "can't mark $1 as a scratch home"
}

guard() {
  local given="${AW_SINDRI_HOME:-}" home temp state below slug
  local msg="AW_SINDRI_HOME must be under \$TMPDIR or a proof folder (got ${given:-nothing})"
  [ -n "$given" ] || die "$msg"
  home="$(real "$given")" || die "$msg"
  temp="$(real "${TMPDIR:-/tmp}")" || die "$msg"
  state="$(real "${AW_STATE_DIR:-$HOME/.agentic-workflow}")" || die "$msg"
  if inside "$home" "$temp"; then mark_scratch "$home"; return 0; fi
  if inside "$home" "$state"; then
    below="${home#"${state%/}"/}"   # <slug>/proof/<more…>
    slug="${below%%/*}"
    case "${below#"$slug"/}" in proof/?*) return 0 ;; esac
  fi
  die "$msg"
}

guard
export AW_SINDRI_PORT=0
read -r -a SINDRI_CMD <<< "${SINDRI:-dotnet run --project $ROOT/src/Sindri/Aw.Sindri.Host --}"

base_url() {
  local file="$AW_SINDRI_HOME/control.json" url
  [ -f "$file" ] || die "no control.json in $AW_SINDRI_HOME: sindri isn't running there (start it: sindri-prove.sh human --headless)"
  url="$(jq -r '.url // empty' "$file" 2>/dev/null)"
  [ -n "$url" ] || die "$file has no url"
  printf '%s\n' "${url%/}"
}

prove_curl() {
  local terminal=1 token_mode=file token="" extra=() stream=0
  while [ $# -gt 0 ]; do
    case "$1" in
      --stream) stream=1; shift ;;
      --no-terminal) terminal=0; shift ;;
      --no-token) token_mode=none; shift ;;
      --token) [ $# -ge 2 ] || die "--token needs a value"; token_mode=given; token="$2"; shift 2 ;;
      -H) [ $# -ge 2 ] || die "-H needs a header"; extra+=(-H "$2"); shift 2 ;;
      -*) die "unknown curl flag $1 (usage: sindri-prove.sh curl [--stream] [--no-terminal] [--no-token | --token T] [-H 'Name: value']… <METHOD> <path> [json])" ;;
      *) break ;;
    esac
  done
  [ $# -ge 2 ] && [ $# -le 3 ] || die "usage: sindri-prove.sh curl [flags] <METHOD> <path> [json]"
  local method="$1" path="$2" body="${3-}" has_body=0
  [ $# -eq 3 ] && has_body=1
  case "$path" in /*) ;; *) die "the path must start with / (got $path)" ;; esac
  case "$body" in @*) die "the json body can't start with @ (curl would read a file)" ;; esac

  local url
  url="$(base_url)" || exit 2
  if [ "$token_mode" = file ]; then
    local token_file="$AW_SINDRI_HOME/auth/human.token"
    [ -f "$token_file" ] || die "no $token_file (sindri mints it on start; use --no-token to send none)"
    token="$(tr -d '\r\n' < "$token_file")"
  fi

  local args=(-sS -X "$method" -H "X-Sindri-Caller: human")
  [ "$terminal" = 1 ] && args+=(-H "X-Sindri-Terminal: 1")
  args+=(-H "X-Sindri-Client: prover")
  [ "$token_mode" != none ] && args+=(-H "X-Sindri-Token: $token")
  [ "$has_body" = 1 ] && args+=(-H "Content-Type: application/json")
  [ ${#extra[@]} -gt 0 ] && args+=("${extra[@]}")
  [ "$has_body" = 1 ] && args+=(--data-binary "$body")

  if [ "$stream" = 1 ]; then
    # the body goes to stdout as it arrives: -N turns curl's buffering off; an HTTP error status still prints its body, then exits 22
    curl "${args[@]}" -N --fail-with-body "$url$path"
    case $? in 0) exit 0 ;; 22) exit 1 ;; *) exit 3 ;; esac
  fi

  local out status rc
  out="$(mktemp "${TMPDIR:-/tmp}/sindri-prove.XXXXXX")" || die "no temp file"
  status="$(curl "${args[@]}" -o "$out" -w '%{http_code}' "$url$path")"
  rc=$?
  cat "$out"
  rm -f "$out"
  echo "HTTP ${status:-000}" >&2
  [ "$rc" -eq 0 ] || exit 3
  case "$status" in 2??) exit 0 ;; *) exit 1 ;; esac
}

# publish [--session [--repo NAME]]: the map's publish state, as the owner (the curl mode) or as a session (no caller header).
prove_publish() {
  local path=/sindri/context/map/publish session=0 repo=""
  while [ $# -gt 0 ]; do
    case "$1" in
      --session) session=1; shift ;;
      --repo) [ $# -ge 2 ] || die "--repo needs a repo name"; repo="$2"; shift 2 ;;
      *) die "usage: sindri-prove.sh publish [--session [--repo NAME]]" ;;
    esac
  done
  [ -z "$repo" ] || [ "$session" = 1 ] || die "--repo belongs to --session: sindri-prove.sh publish --session --repo NAME"
  if [ "$session" = 0 ]; then prove_curl GET "$path"; fi
  local url args=(-sS -X GET) status rc out
  url="$(base_url)" || exit 2
  [ -z "$repo" ] || args+=(-H "X-Sindri-Repo: $repo")
  out="$(mktemp "${TMPDIR:-/tmp}/sindri-prove.XXXXXX")" || die "no temp file"
  status="$(curl "${args[@]}" -o "$out" -w '%{http_code}' "$url$path")"
  rc=$?
  cat "$out"
  rm -f "$out"
  echo "HTTP ${status:-000}" >&2
  [ "$rc" -eq 0 ] || exit 3
  case "$status" in 2??) exit 0 ;; *) exit 1 ;; esac
}

case "$1" in
  url) [ $# -eq 1 ] || die "usage: sindri-prove.sh url"; base_url ;;
  curl) shift; prove_curl "$@" ;;
  publish) shift; prove_publish "$@" ;;
  human)
    shift
    with_tty env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT -u CLAUDE_CODE_EXECPATH -u CLAUDE_CODE_SESSION_ID -u CLAUDE_PID -u AW_SINDRI_CHILD -u CODEX_SANDBOX -u CURSOR_AGENT AW_SINDRI_PROVER=1 \
      "${SINDRI_CMD[@]}" "$@"
    ;;
  *) "${SINDRI_CMD[@]}" "$@" ;;
esac
