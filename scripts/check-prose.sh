#!/usr/bin/env bash
# scripts/check-prose.sh — run Vale (vale.sh) with our own style (.vale.ini, .vale/styles/Aw) on tracked Markdown
# and print one line per hit (issue 2919).
#
# Usage: scripts/check-prose.sh [--base REF] [--strict] [--counts] [path...]
#   path      Markdown files, or directories whose *.md files git tracks or doesn't ignore. Default: the tracked Markdown
#             in docs/ planning/ skills/ bootstrap/ config/voice/, AGENTS.md and README.md.
#   --base R  only hits on lines added or changed since this branch left R (`git diff $(git merge-base R HEAD)`,
#             the working tree included), in the Markdown files changed since then. CI runs this on every PR,
#             so new slop can't land.
#   --strict  warnings fail too (the 80% rules: sentence and paragraph length, passive voice, noun stacks).
#   --counts  print hits per directory ("<dir> <errors> errors, <warnings> warnings") instead of each hit.
# Output: '<file>:<line>:<col>: <error|warning> <Rule>: <message>' sorted by file and line; a file whose front
#         matter Vale can't parse is one 'error Aw.FrontMatter' hit on line 1.
# Exit: 0 no error hits (warnings are printed), 1 an error hit (or a warning under --strict), 2 usage, vale
#       missing or vale failed.
# Vale: $AW_VALE_BIN, else the binaries.lock pin under ${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/vale-<pin>/
#       (scripts/binaries-lock.sh fetch vale; scripts/install-deps.sh runs it), else `vale` on PATH.
# Bash 3.2 compatible (macOS /bin/bash). Needs git and jq.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCOPE=(docs planning skills bootstrap config/voice AGENTS.md README.md)

usage() { awk 'NR < 4 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "${BASH_SOURCE[0]}" >&2; exit 2; }

base="" strict=0 counts=0
paths=()
while [ $# -gt 0 ]; do
  case "$1" in
    --base) [ $# -ge 2 ] && [ -n "$2" ] || usage; base="$2"; shift 2 ;;
    --strict) strict=1; shift ;;
    --counts) counts=1; shift ;;
    -h|--help) usage ;;
    --) shift; while [ $# -gt 0 ]; do paths+=("$1"); shift; done ;;
    -*) echo "check-prose: unknown option $1" >&2; usage ;;
    *) paths+=("$1"); shift ;;
  esac
done

command -v jq >/dev/null 2>&1 || { echo "check-prose: jq is missing" >&2; exit 2; }

find_vale() {
  if [ -n "${AW_VALE_BIN:-}" ]; then
    [ -x "$AW_VALE_BIN" ] && { echo "$AW_VALE_BIN"; return 0; }
    echo "check-prose: AW_VALE_BIN=$AW_VALE_BIN is not an executable" >&2; return 1
  fi
  local pin cached
  pin="$(awk '!/^[[:space:]]*(#|$)/ && $1 == "vale" { print $2; exit }' "$ROOT/binaries.lock" 2>/dev/null || true)"
  cached="${AW_STATE_DIR:-$HOME/.agentic-workflow}/cache/bin/vale-$pin/vale"
  if [ -n "$pin" ] && [ -x "$cached" ]; then echo "$cached"; return 0; fi
  command -v vale 2>/dev/null && return 0
  echo "check-prose: vale is missing; run scripts/binaries-lock.sh fetch vale (or scripts/install-deps.sh)" >&2
  return 1
}
VALE="$(find_vale)" || exit 2

cd "$ROOT"
[ "${#paths[@]}" -gt 0 ] || paths=("${SCOPE[@]}")

if [ -n "$base" ]; then
  git rev-parse --verify --quiet "$base^{commit}" >/dev/null || { echo "check-prose: --base $base is not a commit" >&2; exit 2; }
  # Diff from where this branch left REF, so commits REF gained since then never count as this change's lines.
  base="$(git merge-base "$base" HEAD 2>/dev/null || echo "$base")"
fi

# The files: the *.md under each path that git tracks or doesn't ignore (or changed since --base); a named file
# is taken as is.
files_list="$(mktemp "${TMPDIR:-/tmp}/check-prose.XXXXXX")"
trap 'rm -f "$files_list" "$files_list".*' EXIT
for p in "${paths[@]}"; do
  if [ -n "$base" ]; then
    git diff --name-only --diff-filter=ACMR "$base" -- "$p" | grep '\.md$' || true
    git ls-files --others --exclude-standard -- "$p" | grep '\.md$' || true
  elif [ -f "$p" ]; then
    echo "$p"   # a file named on purpose is checked, tracked or not
  else
    git ls-files --cached --others --exclude-standard -- "$p" | grep '\.md$' || true
  fi
done | awk 'NF && !seen[$0]++' | while IFS= read -r f; do [ -f "$f" ] && [ ! -L "$f" ] && echo "$f"; done >"$files_list"

if [ ! -s "$files_list" ]; then
  if [ "$counts" = 1 ]; then echo "no Markdown to check"; fi
  exit 0
fi

# Vale on every file: JSON alerts on stdout, one JSON error object per unreadable file on stderr.
tr '\n' '\0' <"$files_list" | xargs -0 "$VALE" --config "$ROOT/.vale.ini" --no-exit --output JSON \
  >"$files_list.out" 2>"$files_list.err" || true
if ! jq -s -e 'all(type == "object")' "$files_list.out" >/dev/null 2>&1; then
  echo "check-prose: vale failed:" >&2; cat "$files_list.err" "$files_list.out" >&2; exit 2
fi
jq -s -r 'add // {} | to_entries[] | .key as $f | .value[]
  | [$f, .Line, .Span[0], .Severity, .Check, .Message] | @tsv' "$files_list.out" >"$files_list.hits"
# A file whose front matter isn't YAML: vale reports it as {"Path": ..., "Code": "E201", "Text": ...} on stderr.
if [ -s "$files_list.err" ]; then
  if jq -s -e 'all(type == "object" and has("Path"))' "$files_list.err" >/dev/null 2>&1; then
    jq -s -r --arg root "$ROOT/" '.[] | [(.Path | ltrimstr($root)), 1, 1, "error", "Aw.FrontMatter",
      ("Vale cannot parse this front matter (" + .Text + "). Quote the value.")] | @tsv' \
      "$files_list.err" >>"$files_list.hits"
  else
    echo "check-prose: vale failed:" >&2; cat "$files_list.err" >&2; exit 2
  fi
fi

# --base: keep hits on added lines only (the + side of `git diff -U0`); a new or untracked file keeps every line.
if [ -n "$base" ]; then
  while IFS= read -r f; do
    if git cat-file -e "$base:$f" 2>/dev/null; then
      git diff -U0 --no-color "$base" -- "$f" | awk -v f="$f" '
        /^@@/ { split($3, a, ","); s = substr(a[1], 2) + 0; n = (a[2] == "" ? 1 : a[2] + 0)
                for (i = s; i < s + n; i++) print f "\t" i }'
    else
      awk -v f="$f" '{ print f "\t" NR } END { if (NR == 0) print f "\t1" }' "$f"
    fi
  done <"$files_list" >"$files_list.lines"
  awk -F'\t' 'NR == FNR { keep[$1 "\t" $2] = 1; next } ($1 "\t" $2) in keep' \
    "$files_list.lines" "$files_list.hits" >"$files_list.kept"
  mv "$files_list.kept" "$files_list.hits"
fi

sort -t$'\t' -k1,1 -k2,2n -k3,3n "$files_list.hits" -o "$files_list.hits"
if [ "$counts" = 1 ]; then
  # The directory, two levels deep at most (docs/superpowers, skills/review); a root file counts as ".".
  awk -F'\t' '{ n = split($1, p, "/"); d = (n == 1 ? "." : (n == 2 ? p[1] : p[1] "/" p[2]))
                if ($4 == "error") e[d]++; else w[d]++; seen[d] = 1 }
              END { for (d in seen) printf "%s %d errors, %d warnings\n", d, e[d], w[d] }' "$files_list.hits" | sort
else
  awk -F'\t' '{ printf "%s:%s:%s: %s %s: %s\n", $1, $2, $3, $4, $5, $6 }' "$files_list.hits"
fi

errors="$(awk -F'\t' '$4 == "error"' "$files_list.hits" | wc -l | tr -d ' ')"
warnings="$(awk -F'\t' '$4 != "error"' "$files_list.hits" | wc -l | tr -d ' ')"
echo "check-prose: $(wc -l <"$files_list" | tr -d ' ') files, $errors errors, $warnings warnings" >&2
if [ "$errors" -gt 0 ] || { [ "$strict" = 1 ] && [ "$warnings" -gt 0 ]; }; then exit 1; fi
exit 0
