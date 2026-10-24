#!/usr/bin/env bash
# Tests that nothing which keeps running calls a judge verb that is not in this build yet (cutover spec §5.2, unit 6.0).
#
# The verbs of units 6.3 and 6.4 answer "judge: <verb> is not in this build yet" and exit 1 until they land. No hook, lib,
# script, skill (but skills/judge/), recipe or Sindri code may call one: an exit 1 there would be taken for a judge failure.
# skills/judge/ documents the verbs on purpose; the "Not in this build yet" marker of its text is unit 3.1's.
#
# STUBBED below is the list of those verbs. The PR that lands a verb deletes it from the list (and its route, its row of the
# "Not yet built" table and its per-verb test). The shell pattern matches an invocation of the judge binary (`judge`,
# `command judge`, `$JUDGE_BIN`, `${JUDGE_BIN}`, `$AW_JUDGE_BIN`, `${AW_JUDGE_BIN:-judge}`, quoted or not) followed by the verb as a
# word, `prompt-sort` only with a stubbed subcommand; never the bare words `eval` and `label`, which are shell words
# (config/lib/sys-pkg.sh). The C# pattern matches a "judge" argv element followed by a stubbed word and accepts false negatives
# (wrapper forms such as `exec judge`, `env judge` or `xargs judge`): the real-hook cases are the backstop for hooks, the P2 and P4
# exits for the rest. A positive control (fixtures/judge-stub-invocations/) must match, and its negatives must not.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="${JUDGE_STUB_ROOT:-$(cd "$HERE/../.." && pwd)}"
FIXTURES="$HERE/fixtures/judge-stub-invocations"
PASS=0
FAIL=0
ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# The verbs that answer the not-yet-built line (first word); prompt-sort has its own subcommands.
STUBBED='label|adjudicate|eval'
STUBBED_SORT='why|import|outcomes|adjudicate|eval|promote'

# The judge binary as a word: `judge` after a boundary (not aw-judge, not judge-eval, not a path segment), or the variable forms.
BIN='((^|[[:space:]"(;|&`])judge|"?\$\{?(AW_)?JUDGE_BIN(:-[^}]*)?\}?"?)'
END='([^[:alnum:]_-]|$)'
SHELL_PATTERN="${BIN}[[:space:]]+((${STUBBED})${END}|prompt-sort[[:space:]]+(${STUBBED_SORT})${END})"
CS_PATTERN="\"judge\"[[:space:]]*,[[:space:]]*(\"(${STUBBED})\"|\"prompt-sort\"[[:space:]]*,[[:space:]]*\"(${STUBBED_SORT})\")"

# shell_hits <file>...: lines that invoke a stubbed verb, comment lines left out.
shell_hits() { grep -nHE -e "$SHELL_PATTERN" "$@" 2>/dev/null | grep -vE '^[^:]+:[0-9]+:[[:space:]]*#' || true; }
cs_hits() { grep -nHE -e "$CS_PATTERN" "$@" 2>/dev/null || true; }
count() { if [ -z "$1" ]; then echo 0; else printf '%s\n' "$1" | wc -l | tr -d ' '; fi; }

# ---- the positive control and the negatives ----
lines=$(wc -l < "$FIXTURES/shell-positive.txt" | tr -d ' ')
hits=$(count "$(shell_hits "$FIXTURES/shell-positive.txt")")
[ "$hits" = "$lines" ] && ok "every positive shell line matches ($hits of $lines)" || { fail "positive shell lines matched: $hits of $lines"; shell_hits "$FIXTURES/shell-positive.txt"; }
extra=$(shell_hits "$FIXTURES/shell-negative.txt")
[ -z "$extra" ] && ok "no negative shell line matches" || { fail "a negative shell line matched"; printf '%s\n' "$extra"; }
lines=$(wc -l < "$FIXTURES/csharp-positive.txt" | tr -d ' ')
hits=$(count "$(cs_hits "$FIXTURES/csharp-positive.txt")")
[ "$hits" = "$lines" ] && ok "every positive C# line matches ($hits of $lines)" || { fail "positive C# lines matched: $hits of $lines"; cs_hits "$FIXTURES/csharp-positive.txt"; }
extra=$(cs_hits "$FIXTURES/csharp-negative.txt")
[ -z "$extra" ] && ok "no negative C# line matches" || { fail "a negative C# line matched"; printf '%s\n' "$extra"; }

# ---- the tree ----
# Tracked and not-ignored files, in the places that keep running; tests, fixtures and node_modules are not callers.
list() { # list <pathspec>...
  git -C "$ROOT" ls-files -z --cached --others --exclude-standard -- "$@" \
    | tr '\0' '\n' \
    | grep -vE '(^|/)(tests|node_modules|fixtures|bin|obj)/|\.test\.(sh|mjs|ts)$|\.spec\.ts$|(^|/)skills/judge/' || true
}
in_root() { while IFS= read -r f; do [ -f "$ROOT/$f" ] && printf '%s\n' "$ROOT/$f"; done; }

shell_files=$(list config/hooks config/lib scripts skills recipes src/Sindri | grep -E '\.(sh|bash|md|ya?ml|json)$|^config/(hooks|lib)/[^.]*$' | in_root || true)
cs_files=$(list src/Evidence src/BugFix src/Sindri | grep -E '\.cs$' | in_root || true)
bad=""
if [ -n "$shell_files" ]; then
  # shellcheck disable=SC2086
  bad=$(printf '%s\n' "$shell_files" | tr '\n' '\0' | xargs -0 grep -nHE -e "$SHELL_PATTERN" 2>/dev/null | grep -vE '^[^:]+:[0-9]+:[[:space:]]*#' || true)
fi
[ -z "$bad" ] && ok "no hook, lib, script, skill, recipe or Sindri file calls a stubbed verb (shell form)" || { fail "a stubbed verb is called (shell form)"; printf '%s\n' "$bad" | sed 's/^/       | /'; }
bad=""
if [ -n "$cs_files" ]; then
  bad=$(printf '%s\n' "$cs_files" | tr '\n' '\0' | xargs -0 grep -nHE -e "$CS_PATTERN" 2>/dev/null || true)
fi
[ -z "$bad" ] && ok "no Evidence, BugFix or Sindri source calls a stubbed verb (C# form)" || { fail "a stubbed verb is called (C# form)"; printf '%s\n' "$bad" | sed 's/^/       | /'; }

# The scan covers files at all (a typo in the pathspec would pass every check above).
n=$(count "$shell_files")
[ "$n" -gt 20 ] && ok "the shell scan reads $n files" || fail "the shell scan read only $n files"
n=$(count "$cs_files")
[ "$n" -gt 20 ] && ok "the C# scan reads $n files" || fail "the C# scan read only $n files"

echo "judge-stub-invocations: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
