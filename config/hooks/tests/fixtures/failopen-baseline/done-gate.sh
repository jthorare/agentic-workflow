#!/usr/bin/env bash
# aw:done-gate — Stop hook (main sessions). RF-3 first: stop_hook_active
# means this is our own re-invocation from a prior "continue" — exit 0
# unconditionally before touching anything else, so this can never loop.
set -uo pipefail
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || exit 0

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
  [ -n "$CLAIM_TEXT" ] || exit 0
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
[ -n "$LAST_ASSISTANT_LINE" ] || exit 0
# message.content is real-world either a bare string or an array of blocks
# (scorer/src/transcript/parse-line.ts's own schema confirms both shapes) —
# branch on its type rather than assuming array-shaped content.
CLAIM_TEXT="$(printf '%s' "$LAST_ASSISTANT_LINE" | jq -r '
  if (.message.content | type) == "string" then .message.content
  else ([.message.content[]? | select(.type=="text") | .text] | join("\n"))
  end
' 2>/dev/null)"
[ -n "$CLAIM_TEXT" ] || exit 0
fi

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
  ASK_INPUT="$(jq -nc --arg t "$CLAIM_TEXT" '{transcriptTail: $t}')"
  set +e
  printf '%s' "$ASK_INPUT" | AW_SESSION_ID="$SESSION_ID" judge ask-check > /dev/null 2>&1
  ASK_RC=$?
  set -e
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
if grep -qE '^[A-Za-z0-9._-]{1,80}$' <<<"$SESSION_ID" && [ "${SESSION_ID#*..}" = "$SESSION_ID" ]; then
  SORT_FILE="$SESSIONS_DIR/$SESSION_ID.sort.json"
  if [ -f "$SORT_FILE" ] && [ "$(jq -r '.requirements.uiEvidence // false' "$SORT_FILE" 2>/dev/null)" = "true" ]; then
    if ! grep -qiE '(screenshot|ui-evidence|playwright|verify-web|verify-ios|snapshot_ui|\.png\b)' <<<"$CLAIM_TEXT"; then
      echo "This session changes the UI (judged from your prompt). Show UI evidence (a screenshot, a Playwright/ui-evidence run or an iOS snapshot) before claiming done." >&2
      exit 2
    fi
    jq 'del(.requirements.uiEvidence)' "$SORT_FILE" > "$SORT_FILE.tmp" 2>/dev/null && mv "$SORT_FILE.tmp" "$SORT_FILE" || rm -f "$SORT_FILE.tmp"
  fi
fi

BRIEF_JSON="$(judge brief get "task:$SESSION_ID" 2>/dev/null || true)"

if [ -z "$BRIEF_JSON" ]; then
  # RF-2: no brief at all — only check that SOME evidence is mentioned.
  if grep -qiE '(https?://|npm (run|test)|pytest|vitest|PR #[0-9]+)' <<<"$CLAIM_TEXT"; then
    exit 0
  fi
  echo "Claiming done with no evidence mentioned (no command output, no PR link, no test run). Show the proof." >&2
  exit 2
fi

ACCEPTANCE="$(printf '%s' "$BRIEF_JSON" | jq -r '.acceptanceCriteria // empty')"
if [ -n "$ACCEPTANCE" ] && ! grep -qiF "$(printf '%s' "$ACCEPTANCE" | cut -c1-40)" <<<"$CLAIM_TEXT"; then
  echo "Claiming done, but the acceptance criteria (\"$ACCEPTANCE\") doesn't appear addressed in the claim. Show how it's met." >&2
  exit 2
fi
exit 0
