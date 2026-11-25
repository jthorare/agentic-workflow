#!/usr/bin/env bash
# aw:done-gate — Stop hook (main sessions). RF-3 first: stop_hook_active
# means this is our own re-invocation from a prior "continue" — exit 0
# unconditionally before touching anything else, so this can never loop.
set -uo pipefail
# HOOK-FAILOPEN: record why this hook is failing open (config/lib/failopen.sh,
# installed at <hooks>/lib/failopen.sh). Logging only: it runs in a subshell
# with its output discarded, so it can never change this hook's exit code,
# stdout or stderr, whatever set -e/-u or the environment do to it.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen done-gate "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# HOOK-WATCHDOG (#3778): the host timeout is a backstop; the hook ends itself first. The whole script runs in a child bash
# that a watchdog kills after a WALL-CLOCK budget (AW_DONE_GATE_BUDGET_MS, default 10000 ms), counted from the hook's start and covering every
# command, not a count of sleep passes. On a spent budget it writes one failopen `timeout` row and the hook prints nothing.
# The output is printed only at the very end, so a kill never leaves a half answer. A normal run ends the watchdog.
if [ -z "${AW_HOOK_WD_INNER:-}" ] && [ -z "${AW_JUDGE_CHILD:-}" ]; then
  _aw_ms="${AW_DONE_GATE_BUDGET_MS:-10000}"; case "$_aw_ms" in '' | *[!0-9]*) _aw_ms=10000 ;; esac
  # Kill a process and everything under it, by pid only: _aw_kill_tree from config/lib/kill-tree.sh, installed beside
  # failopen.sh. Without the lib (an older install) the fallback below is the lib's line word for word
  # (config/lib/tests/kill-tree.test.sh), so a timed-out script still leaves no judge running.
  for _aw_f in "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/lib/kill-tree.sh" "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/../lib/kill-tree.sh"; do
    if [ -f "$_aw_f" ]; then . "$_aw_f" 2> /dev/null && break; fi
  done
  type _aw_kill_tree > /dev/null 2>&1 || _aw_kill_tree() { local _c _k; kill -STOP "$1" 2> /dev/null; _k="$(pgrep -P "$1" 2> /dev/null)"; kill -9 "$1" 2> /dev/null; for _c in $_k; do _aw_kill_tree "$_c"; done; return 0; }
  # The script's temp files go in a dir this bash owns and removes on every path: a killed script runs no EXIT trap.
  _aw_tmp="$(mktemp -d 2> /dev/null)" || _aw_tmp=""
  AW_HOOK_WD_INNER=1 AW_HOOK_WD_TMP="$_aw_tmp" "$BASH" "${BASH_SOURCE[0]}" "$@" 0<&0 &
  _aw_child=$!
  (
    set +eu
    sleep "$((_aw_ms / 1000)).$(printf '%03d' "$((_aw_ms % 1000))")" 2> /dev/null || exit 0
    [ -z "$_aw_tmp" ] || : > "$_aw_tmp/.wd-fired"
    failopen timeout
    _aw_kill_tree "$_aw_child"
  ) > /dev/null 2>&1 < /dev/null &
  _aw_wd=$!
  trap '_aw_kill_tree "$_aw_child"; kill "$_aw_wd" 2> /dev/null; [ -z "$_aw_tmp" ] || rm -rf "$_aw_tmp"; exit 0' TERM
  _aw_rc=0
  { wait "$_aw_child"; } 2> /dev/null || _aw_rc=$?
  # A watchdog that fired is left to finish killing the tree under the script; any other is ended here.
  if [ -z "$_aw_tmp" ] || [ ! -e "$_aw_tmp/.wd-fired" ]; then
    pkill -P "$_aw_wd" 2> /dev/null
    kill "$_aw_wd" 2> /dev/null
  fi
  [ -z "$_aw_tmp" ] || rm -rf "$_aw_tmp"
  if [ "$_aw_rc" -eq 137 ]; then _aw_rc=0; fi
  exit "$_aw_rc"
fi
# The images a hook sends the judge (config/lib/image-paths.sh, installed beside failopen.sh). Without it the hook sends none, as before.
for _aw_f in "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/lib/image-paths.sh" "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/../lib/image-paths.sh"; do
  if [ -f "$_aw_f" ]; then . "$_aw_f" 2> /dev/null && break; fi
done
type aw_images_attach > /dev/null 2>&1 || { aw_images_attach() { printf '%s' "$1"; }; aw_image_paths() { :; }; aw_image_files() { cat > /dev/null; }; }
# No EXIT trap: on bash 3.2 a trap defers SIGTERM until a running $(judge …)
# returns, so the host's timeout kill would take longer. A crash keeps its exit
# code and is not counted under Claude Code (see "Fail-open logging" in
# .agents/rules/hooks.md); under Codex/Cursor the adapter counts it.
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }

STOP_HOOK_ACTIVE="$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false')"
[ "$STOP_HOOK_ACTIVE" = "true" ] && exit 0

TRANSCRIPT="$(printf '%s' "$INPUT" | jq -r '.transcript_path // empty')"

if [ -z "$TRANSCRIPT" ] || [ ! -f "$TRANSCRIPT" ]; then
  # Provider adapters (config/hooks/adapters/) run this hook for hosts whose
  # transcript isn't Claude-shaped (Codex rollouts, Cursor agent transcripts).
  # They drop transcript_path and pass the final assistant text directly as
  # last_assistant_message (Codex's Stop input carries it natively). Claude
  # Code always sends a real transcript_path, so its path below is unchanged.
  CLAIM_TEXT="$(printf '%s' "$INPUT" | jq -r '.last_assistant_message // empty' 2>/dev/null)"
  if [ -z "$CLAIM_TEXT" ]; then
    # A done claim goes unchecked: say why, unless the payload simply has none.
    if [ -n "$TRANSCRIPT" ]; then failopen transcript-unreadable; else failopen input "$INPUT"; fi
    exit 0
  fi
else
# Last complete assistant text turn: grab every whole line whose top-level
# "type" is "assistant" (tail a generous window, not the whole file — a
# transcript can be large), then pick the last one and pull out its text.
# jq itself does the real JSON parse; grep only narrows which whole lines get
# handed to jq, it never extracts a sub-object by hand (never a `[^}]*`
# regex, which breaks on any nested object in the line).
LAST_ASSISTANT_LINE="$(tail -c 1048576 "$TRANSCRIPT" 2>/dev/null | grep '"type":"assistant"' | tail -n 1)"
if [ -z "$LAST_ASSISTANT_LINE" ]; then
  # Rare fallback: even a 1MB tail didn't contain an assistant line — a long
  # run of huge hook-attachment payloads (deferred-tool listings, full skill
  # text, etc.) after the last assistant turn can push it further back than
  # that (2026-09-27 finding: a real headless session did exactly this,
  # making this hook a silent no-op for a bare "Done." with no evidence).
  # Scan the whole file once rather than allowing a done-claim through
  # unevaluated; this path is rare, so the cost of a full scan is acceptable.
  LAST_ASSISTANT_LINE="$(grep '"type":"assistant"' "$TRANSCRIPT" 2>/dev/null | tail -n 1)"
fi
[ -n "$LAST_ASSISTANT_LINE" ] || { failopen no-assistant-line; exit 0; }
# message.content is real-world either a bare string or an array of blocks
# (real transcripts carry both shapes) —
# branch on its type rather than assuming array-shaped content.
CLAIM_TEXT="$(printf '%s' "$LAST_ASSISTANT_LINE" | jq -r '
  if (.message.content | type) == "string" then .message.content
  else ([.message.content[]? | select(.type=="text") | .text] | join("\n"))
  end
' 2>/dev/null)"
CLAIM_RC=$?
if [ -z "$CLAIM_TEXT" ]; then
  # A tool-use-only turn has no text and is fine; a line jq can't read is not.
  [ "$CLAIM_RC" -ne 0 ] && failopen transcript-line-unparsable
  exit 0
fi
fi

# The PNG/JPEG files the last assistant turn opened with Read, newest first (the same tail of the transcript as above; the turn starts after the last
# user line that isn't a tool result). Codex and Cursor send no transcript_path, so none there. Nothing when the transcript can't be read.
turn_images() {
  [ -n "$TRANSCRIPT" ] && [ -f "$TRANSCRIPT" ] || return 0
  # Only files under the checkout of the hook's cwd go out (a receipt the agent read in ~/Downloads does not); outside a checkout, none.
  local root; root="$(git rev-parse --show-toplevel 2> /dev/null)" || return 0
  [ -n "$root" ] || return 0
  tail -c 1048576 "$TRANSCRIPT" 2> /dev/null | jq -Rrn '
    [inputs | fromjson? | objects] as $l
    | ([$l | to_entries[] | select(.value.type == "user" and ((.value.message.content | type) != "array" or ([.value.message.content[] | select(.type? == "tool_result")] | length) == 0)) | .key] | last // -1) as $start
    | [$l[$start + 1:][] | select(.type == "assistant") | .message.content | arrays | .[] | select(.type? == "tool_use" and .name == "Read") | .input.file_path | strings | select(test("\\.(png|jpe?g)$"; "i"))]
    | reverse | .[]' 2> /dev/null | aw_image_files done-gate "$root"
}

# A done claim is an assertion of completion, not any occurrence of a claim word.
# Ignored: fenced code and table rows. Markdown emphasis/heading/quote marks and
# leading emoji are stripped first ('**Done.**', '## Done', '> Done.'); backticks stay. Sentences are split first, so a question
# ends at its own '?' ('All done. Should I open the PR?' still claims). A negation
# cancels a claim only when it appears before the claim word in the same clause
# (clauses end at a sentence mark or at ', and' / ', but' / ', so' / ' - ' / an em
# dash; bare commas do not split), including any word the noun pattern consumed ('It is not finished.' is no claim;
# 'Nothing, in short, is done.' is no claim; 'Done, no issues found.' still claims).
# Deterministic: awk only (BSD awk, POSIX classes, no \b). \047 is a single quote.
is_done_claim() {
  printf '%s\n' "$1" | awk '
    BEGIN {
      NEG = "[^[:alpha:]](not|nothing|no|yet|never|none)[^[:alpha:]]|n\047t"
      START = "^[[:space:]]*([-*+]|[0-9]+[.)])?[[:space:]]*(done|finished|shipped|merged|ready for review)[^[:alpha:]\047]"
      B = "[^[:alpha:]\047]"
      DONE = "(done|complete|completed|finished|merged|shipped|ready for review)"
      PAIR = "(((is|are|was|were|all|everything|now)|(it\047s|it is))[[:space:]]+(now[[:space:]]+)?" DONE "|(i|we)(\047ve|[[:space:]]+have)?[[:space:]]+(finished|completed|shipped|merged)|(has|have)[[:space:]]+been[[:space:]]+(merged|shipped|completed|finished)|(i|we)(\047m|[[:space:]]+am|\047re|[[:space:]]+are)[[:space:]]+(all[[:space:]]+|now[[:space:]]+)?(done|finished))"
      NOUN = "[[:alpha:]]+[[:space:]]+(complete|completed|finished|merged|shipped|ready for review)([[:space:]]*,|[.!;:]?[[:space:]]*$)"
      MID = B "(" PAIR B "|" NOUN ")"
      SEP = "\001"
      CLAUSE = ",[[:space:]]+(and|but|so)[[:space:]]|[[:space:]]+(-|\342\200\224)[[:space:]]+"
    }
    function is_claim(s,   t, rest, acc, pre, m, seg) {
      if (s ~ /\?[[:space:]]*$/) return 0
      t = " " s " "
      # Clause boundaries (", and", ", but", ", so", " - ", an em dash) become SEP
      # so a negation only cancels a claim in its own clause. Bare commas do not split.
      gsub(CLAUSE, SEP " ", t)
      if (t ~ START) return 1
      rest = t; acc = ""
      while (match(rest, MID)) {
        pre = acc substr(rest, 1, RSTART - 1)
        m = substr(rest, RSTART, RLENGTH)
        seg = " " pre " " m
        sub("^.*" SEP, "", seg)
        if (seg !~ NEG) return 1
        acc = acc substr(rest, 1, RSTART + RLENGTH - 1)
        rest = substr(rest, RSTART + RLENGTH)
      }
      return 0
    }
    /^[[:space:]]*```/ { f = !f; next }
    f { next }
    /^[[:space:]]*\|/ { next }
    {
      line = tolower($0)
      # Markdown decoration (emphasis, headings, quote marks, leading bullets and
      # emoji) is not part of the claim; backticks are kept so inline code stays inert.
      gsub(/[*_#]/, " ", line)
      sub(/^[^[:alnum:]`]*/, "", line)
      gsub(/[.!?;:][[:space:]]*/, "&\n", line)
      n = split(line, sent, "\n")
      for (i = 1; i <= n; i++) if (is_claim(sent[i])) found = 1
    }
    END { exit (found ? 0 : 1) }
  '
}

SESSION_ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty')"
SESSIONS_DIR="${AW_JUDGE_SESSIONS_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/judge/sessions}"

if ! is_done_claim "$CLAIM_TEXT"; then
  # Not a done claim — ask whether auto-continue is already authorized
  # (Task 4, N2/R2). judge ask-check's own CLI contract is inverted from
  # every other subcommand here: exit 2 means "continue" (don't actually
  # stop), exit 0 means "ask" (a real stop is fine).
  ASK_INPUT="$(jq -nc --rawfile t <(printf '%s' "$CLAIM_TEXT") '{transcriptTail: $t}')"
  ASK_INPUT="$(aw_images_attach "$ASK_INPUT" "$(turn_images)")"
  set +e
  printf '%s' "$ASK_INPUT" | AW_SESSION_ID="$SESSION_ID" judge ask-check > /dev/null 2>&1
  ASK_RC=$?
  set -e
  # 0 = ask, 2 = continue; anything else is judge failing (auto-continue is lost).
  case "$ASK_RC" in 0|2) ;; *) failopen "judge-ask-check-exit-$ASK_RC" ;; esac
  if [ "$ASK_RC" -eq 2 ] && [ -n "$SESSION_ID" ]; then
    mkdir -p "$SESSIONS_DIR"
    jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" '{auto_continued_at: $ts}' > "$SESSIONS_DIR/$SESSION_ID.json" 2>/dev/null || true
    echo "Next step already authorized by the brief or plan — continuing without asking." >&2
    exit 2
  fi
  exit 0
fi

# Prompt sorter requirement (Plan C): the sorter judged this session's work to
# touch the UI and wrote requirements.uiEvidence into <sid>.sort.json. A done
# claim must then mention UI evidence. stop_hook_active already exited above,
# so this can block at most once per stop, never loop. SESSION_ID is only used
# as a file name when it matches the same character set the sorter enforces.
if printf '%s' "$SESSION_ID" | grep -qE '^[A-Za-z0-9._-]{1,80}$' && [ "${SESSION_ID#*..}" = "$SESSION_ID" ]; then
  SORT_FILE="$SESSIONS_DIR/$SESSION_ID.sort.json"
  if [ -f "$SORT_FILE" ] && [ "$(jq -r '.requirements.uiEvidence // false' "$SORT_FILE" 2>/dev/null)" = "true" ]; then
    if ! printf '%s' "$CLAIM_TEXT" | grep -qiE '(screenshot|ui-evidence|playwright|verify-web|verify-ios|snapshot_ui|\.png\b)'; then
      echo "This session changes the UI (judged from your prompt). Show UI evidence (a screenshot, a Playwright/ui-evidence run or an iOS snapshot) before claiming done." >&2
      exit 2
    fi
    jq 'del(.requirements.uiEvidence)' "$SORT_FILE" > "$SORT_FILE.tmp" 2>/dev/null && mv "$SORT_FILE.tmp" "$SORT_FILE" || rm -f "$SORT_FILE.tmp"
  fi
fi

# judge says "no brief for …" (exit 1) when there simply is none. Any other
# failure falls to the RF-2 evidence check below.
# judge runs as a DIRECT child of this bash (in the background, output to a file), so the watchdog's tree kill reaches it
# on a spent budget. Inside $( … ) it can be a grandchild (bash 3.2 always forks one) and outlive the hook (#3778).
BRIEF_ERR="$(mktemp "${AW_HOOK_WD_TMP:-${TMPDIR:-/tmp}}/aw-hook.XXXXXX" 2>/dev/null)" || BRIEF_ERR=/dev/null
BRIEF_OUT="$(mktemp "${AW_HOOK_WD_TMP:-${TMPDIR:-/tmp}}/aw-hook.XXXXXX" 2>/dev/null)" || BRIEF_OUT=""
BRIEF_RC=0
if [ -n "$BRIEF_OUT" ]; then
  judge brief get "task:$SESSION_ID" > "$BRIEF_OUT" 2>"$BRIEF_ERR" &
  { wait "$!"; } 2>/dev/null || BRIEF_RC=$?
  BRIEF_JSON="$(cat "$BRIEF_OUT" 2>/dev/null)"
  rm -f "$BRIEF_OUT"
else
  BRIEF_JSON="$(judge brief get "task:$SESSION_ID" 2>"$BRIEF_ERR")" || BRIEF_RC=$?
fi
if [ "$BRIEF_RC" -ne 0 ] && ! grep -q '^no brief for' "$BRIEF_ERR" 2>/dev/null; then
  failopen "judge-brief-exit-$BRIEF_RC"
fi
[ "$BRIEF_ERR" = /dev/null ] || rm -f "$BRIEF_ERR"

if [ -z "$BRIEF_JSON" ]; then
  # RF-2: no brief at all — only check that SOME evidence is mentioned.
  if printf '%s' "$CLAIM_TEXT" | grep -qiE '(https?://|npm (run|test)|pytest|vitest|PR #[0-9]+|dotnet (test|build|run)|cargo (test|build|nextest)|go (test|build|vet)|xunit|[0-9]+ passed|passed!)'; then
    exit 0
  fi
  echo "Claiming done with no evidence mentioned (no command output, no PR link, no test run). Show the proof." >&2
  exit 2
fi

# A brief exists: the claim need not repeat its acceptance criteria (q93 JG-05: the brief is the whole prompt, so a prefix is boilerplate).
exit 0
