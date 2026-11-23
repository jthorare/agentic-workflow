#!/usr/bin/env bash
# scripts/skill-evals.sh — run `claude plugin eval` on the toolkit's skills from a clean staged copy, inside a scrubbed
# environment and a daily spend cap (skill-revamp spec 2026-10-25-skill-revamp-evals-design.md §4, §5, §6; issue #1107).
#
#   scripts/skill-evals.sh [--skills a,b | --changed | --all] [--kind routing|behaviour|both]
#                          [--runs N] [--ablation none|with-without] [--model M] [--judge-model M]
#                          [--threshold T] [--max-cost-usd N] [--jobs N] [--gate warn|block]
#                          [--family a,b] [--family-max-cost-usd N] [--preamble on|off] [--lane NAME] [--keep-temp] [--root DIR]
#                          [--dry-run | --selftest | --lint-descriptions | --status]
#
# Lane leads and the owner run the real thing; builders run --selftest, --dry-run and --lint-descriptions only (they call no
# model and cost nothing). A hand-typed `claude plugin eval` skips the tally and the environment below.
#
# What a run does (spec §5.2):
#   1 resolve the skill set (--changed = skills whose skills/<x>/ or evals/<x>/ differ from the merge base with origin/main,
#     plus every skill with evals when skills/_shared/**, skills/_preamble*.md or skills/_design-preamble*.md changed);
#   2 stage, per kind, into a fresh directory under $EVAL_HOME/cache/ outside any git repo (rsync -L; a link that resolves
#     outside this repo, a hooks/ dir or an .mcp.json other than evals/plugin/.mcp.json refuses the stage, exit 3): evals/plugin,
#     every skill's SKILL.md, the chosen skills' full directories, skills/_shared/, both preambles, config/voice/, the
#     config/agents/ files a chosen skill names, the chosen skills' evals/<skill>/<kind>/ and fixture.sh, _lib and mocks, and the stubs at the stage root (stubs/, outside the evals/ tree the agent is denied).
#     The STAGED copy is rewritten, never the source: the shell default of AW_PREAMBLE goes from on to off (kept on, and the
#     run passes AW_PREAMBLE=on, under --preamble on: the spec §8 5a baseline of the forced cases with the preamble), of AW_GIT_MODE
#     from full to readonly, of AW_RESULT from 0 to 1, of AW_RESULT_SKILL and AW_RESULT_FILE from empty to what the case's fixture left in .aw-eval/result-skill and result-file (nothing for a case that is not a Sindri block case), and the staged repo-slug.sh state default walks up from $PWD to the dir holding .aw-eval/ (the workspace root) and adds /.aw-state. The stubs get the same default rewrite.
#     The toolkit copy reaches $HOME/.agentic-workflow/toolkit (the default of AW_TOOLKIT) inside each run's temp HOME
#     through a scaffold script written into every staged case, which then runs the case's own scaffold.
#   3 check and reserve the day's spend (spec §6.4): $60 per day across all lanes, under one flock on $EVAL_HOME/spend.jsonl;
#     a run is refused (exit 2) when the day's total plus its --max-cost-usd would pass $60, or when the tally can't be read or written
#   4 run `claude plugin eval` per kind under `env -i` with an allowlist (PATH with the stubs first, TERM, LANG, HOME, and the
#     AW_* values); nothing else is forwarded, so an operator-shell GH_TOKEN, EVAL_* or any other variable never reaches it
#   5 scrub token patterns (and the values of the operator's token-named and EVAL_* variables) from what was saved, settle
#     the reserve with the real cost, print the WITH / W-OUT / Δ Markdown table and each failure's grader.
#
# Models (spec §6.3): the agent model is pinned here (default claude-sonnet-5-5, the per-PR choice; the nightly passes --model for
# the default Opus) and the judge is sonnet, not the Haiku default. The same --model runs both arms.
#
# Skill directories: a skill x lives in skills/<x>/ (SKILL.md inside); bootstrap lives in bootstrap/ (skill_dir resolves both, the
# stage copies it to skills/bootstrap/, --changed counts bootstrap/** as a change to the skill "bootstrap").
#
# Family sets (spec §3.1a): evals/_families/<family>/<member>/{prompt.md, graders/*.md} (an optional README.md per family). A
# <member> is a skill's dir name. --selftest walks every family case with the same lint and counts them in its case total. A real
# run also runs a family's whole set (--ablation none, own staged block "### family <name>" in the report) when a selected skill is a
# member and its SKILL.md description differs from the merge base with origin/main, or when --family a,b names it. The family set
# has its own per-run cap --family-max-cost-usd (default 12), reserved and settled as separate rows in the same tally (§6.4): when
# the day's $60 has no room for it, the family set is skipped ("FAMILY SKIPPED"), the skill run goes on, and the exit is 2.
#
# Regex graders are JavaScript (the tool runs new RegExp(pattern, flags).test(text); flags are dgimsuvy; count:N counts with g): --selftest
# runs them in node exactly as written and reports SELFTEST-REGEX when one does not compile ((?i), (?s) and \A are not JavaScript).
#
# Case files: <eval dir>/<skill>/{routing,behaviour}/<case>/{prompt.md|case.yaml, graders/*.md}. Conventions the script
# reads on top of the tool's format: a routing case's `skill-fired` grader (tool_used Skill) passing means the skill fired; a
# near-miss case uses `skill-not-fired` instead (passing means it did NOT fire). Cases named *-natural-N and *-near-miss feed
# the routing gate (spec §6.1). --selftest reads graders/_selftest/<grader>.pass.<ext> and .fail.<ext> (plus any extra <grader>.pass.<N>.<ext> and
# .fail.<N>.<ext>, each graded on its own), where <ext> is
# txt (the reply, or a file's contents), files (newline-separated created paths; file_exists and regex target files) or
# tools (one "ToolName<TAB>input" per call; tool_used), or trace (the eval tool's trace text: run.trace.map(JSON.stringify).join("\n"): one compact JSON message per line, a Skill call being an assistant
# message with a tool_use block {"name":"Skill","input":{"skill":"<name>"}} (the exact envelope is checked on the first real family run); regex with target: trace, as the
# family graders use to anchor on the first Skill call). --status reads evals/<skill>/status.md ("key: value" lines:
# fire_rate, delta, last_run YYYY-MM-DD, pr). --lint-descriptions reads the total-bytes baseline from
# evals/_lib/description-bytes.baseline (one number) when that file exists.
#
# Exit: 0 pass (also "no skills changed"), 1 below threshold (or a failed --selftest/--lint-descriptions), 2 budget refused
# or the ceiling left a case (or the CLI left a run) unrun, 3 environment (claude, jq, perl or rsync missing, not logged in, dirty or detached tree, a
# staged path outside the repo, an unreadable result), 4 routing gate missed (--gate block), 64 usage.
# When several apply the order is 3, 4, 1, 2, so a missed gate or a low score is never hidden behind a refused family set or
# a partial run. A case is never started when the money left is less than the costliest call so far ("stopped before"); one CLI
# call can still pass its cap, since the CLI checks it only between its runs: that is "over the ceiling by", not PARTIAL, and not exit 2.
# A --dry-run prints FAMILY SKIPPED when the family would be refused but exits 0.
# Bash 3.2 compatible.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CAP_USD=60
RESERVE_TTL=10800

SKILLS_ARG="" SELECT="" KIND=both RUNS="" ABLATION="" MODEL=claude-sonnet-5-5 JUDGE=sonnet THRESHOLD=0.8 MAX_COST=8 JOBS="" GATE=warn
FAMILY_ARG="" FAMILY_MAX=12 PREAMBLE=off
ACTION=run KEEP=0 LANE="" STAGE_LIST=""

usage() {
  sed -n '2,/^# Bash 3.2/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' | sed -n '1,12p' >&2
  echo "see the header of scripts/skill-evals.sh for the rest" >&2
}
die() { local rc="$1"; shift; echo "skill-evals: $*" >&2; exit "$rc"; }
note() { echo "skill-evals: $*" >&2; }

while [ $# -gt 0 ]; do
  case "$1" in
    --skills) SKILLS_ARG="${2:?--skills needs a list}"; SELECT=skills; shift 2 ;;
    --changed) SELECT=changed; shift ;;
    --all) SELECT=all; shift ;;
    --kind) KIND="${2:?--kind needs routing|behaviour|both}"; shift 2 ;;
    --runs) RUNS="${2:?--runs needs a number}"; shift 2 ;;
    --ablation) ABLATION="${2:?--ablation needs none|with-without}"; shift 2 ;;
    --model) MODEL="${2:?--model needs a value}"; shift 2 ;;
    --judge-model) JUDGE="${2:?--judge-model needs a value}"; shift 2 ;;
    --threshold) THRESHOLD="${2:?--threshold needs a number}"; shift 2 ;;
    --max-cost-usd) MAX_COST="${2:?--max-cost-usd needs a number}"; shift 2 ;;
    --jobs|-j) JOBS="${2:?--jobs needs a number}"; shift 2 ;;
    --gate) GATE="${2:?--gate needs warn|block}"; shift 2 ;;
    --family) FAMILY_ARG="${2:?--family needs a name}"; shift 2 ;;
    --family-max-cost-usd) FAMILY_MAX="${2:?--family-max-cost-usd needs a number}"; shift 2 ;;
    --preamble) PREAMBLE="${2:?--preamble needs on|off}"; shift 2 ;;
    --lane) LANE="${2:?--lane needs a name}"; shift 2 ;;
    --keep-temp) KEEP=1; shift ;;
    --root) ROOT="$(cd "${2:?--root needs a directory}" && pwd)" || exit 64; shift 2 ;;
    --dry-run) ACTION=dry; shift ;;
    --selftest) ACTION=selftest; shift ;;
    --lint-descriptions) ACTION=lint; shift ;;
    --status) ACTION=status; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "skill-evals: unknown argument: $1" >&2; usage; exit 64 ;;
  esac
done
case "$KIND" in routing|behaviour|both) ;; *) die 64 "--kind must be routing, behaviour or both" ;; esac
case "$GATE" in warn|block) ;; *) die 64 "--gate must be warn or block" ;; esac
case "$PREAMBLE" in on|off) ;; *) die 64 "--preamble must be on or off" ;; esac
case "$ABLATION" in ""|none|with-without) ;; *) die 64 "--ablation must be none or with-without" ;; esac
num() { grep -Eq '^[0-9]+(\.[0-9]+)?$' <<<"$1"; }
num "$THRESHOLD" || die 64 "--threshold must be a number"
num "$MAX_COST" || die 64 "--max-cost-usd must be a number"
num "$FAMILY_MAX" || die 64 "--family-max-cost-usd must be a number"
[ -z "$RUNS" ] || grep -Eq '^[0-9]+$' <<<"$RUNS" || die 64 "--runs must be a whole number"
[ -z "$JOBS" ] || grep -Eq '^[1-8]$' <<<"$JOBS" || die 64 "--jobs must be 1 to 8"
[ -n "$LANE" ] || LANE="${AW_META_LANE:-}"
[ -n "$LANE" ] || LANE=operator
grep -Eq '^[A-Za-z0-9._-]+$' <<<"$LANE" || die 64 "--lane must be letters, digits, . _ -"

EVAL_HOME="${AW_STATE_DIR:-$HOME/.agentic-workflow}/skill-evals"
TALLY="$EVAL_HOME/spend.jsonl"
DAY="$(date +%F)"
RUN_ID="$(date +%Y%m%dT%H%M%S)-$$"

need() { command -v "$1" >/dev/null 2>&1 || die 3 "$1 is required"; }

# ---------------------------------------------------------------------------------------------------------------------
# Text helpers
# ---------------------------------------------------------------------------------------------------------------------

# fm_get FILE KEY: the value of a "key: value" line in FILE's leading --- block (one line; surrounding quotes stripped).
fm_get() {
  awk -v k="$2" -v q="'" 'NR==1 { if ($0 != "---") exit; next }
    /^---[ \t]*$/ { exit }
    index($0, k ":") == 1 { v = substr($0, length(k) + 2); sub(/^[ \t]+/, "", v); sub(/[ \t\r]+$/, "", v)
      if (length(v) > 1 && ((substr(v,1,1) == "\"" && substr(v,length(v),1) == "\"") || (substr(v,1,1) == q && substr(v,length(v),1) == q))) v = substr(v, 2, length(v) - 2)
      print v; exit }' "$1"
}
# fm_body FILE: everything after the leading --- block (the whole file when there is none).
fm_body() {
  awk 'NR==1 && $0 != "---" { all = 1 } all { print; next } NR==1 { next } done { print; next } /^---[ \t]*$/ { done = 1 }' "$1"
}
# description_of SKILL.md: the frontmatter description, folded onto one line (handles >, |, quoted and plain multi-line).
description_of() {
  awk -v q="'" '
    NR==1 { if ($0 != "---") exit; next }
    /^---[ \t]*$/ { exit }
    d && /^[ \t]+/ { l = $0; sub(/^[ \t]+/, "", l); out = out (out == "" ? "" : " ") l; next }
    d && /^[^ \t]/ { d = 0 }
    /^description:/ { d = 1; v = $0; sub(/^description:[ \t]*/, "", v)
      if (v ~ /^[>|][-+0-9]*[ \t]*$/) v = ""
      out = v; next }
    END { sub(/[ \t\r]+$/, "", out)
      if (length(out) > 1 && ((substr(out,1,1) == "\"" && substr(out,length(out),1) == "\"") || (substr(out,1,1) == q && substr(out,length(out),1) == q))) out = substr(out, 2, length(out) - 2)
      print out }' "$1"
}

# ---------------------------------------------------------------------------------------------------------------------
# Skill and case discovery
# ---------------------------------------------------------------------------------------------------------------------

# skill_dir X: the directory holding skill X's SKILL.md (skills/X, or bootstrap/ for bootstrap); non-zero when there is none
skill_dir() {
  if [ -f "$ROOT/skills/$1/SKILL.md" ]; then echo "$ROOT/skills/$1"
  elif [ "$1" = bootstrap ] && [ -f "$ROOT/bootstrap/SKILL.md" ]; then echo "$ROOT/bootstrap"
  else return 1; fi
}
all_skills() {
  local d
  for d in "$ROOT"/skills/*/; do [ -f "$d/SKILL.md" ] && basename "$d"; done
  [ ! -f "$ROOT/bootstrap/SKILL.md" ] || [ -f "$ROOT/skills/bootstrap/SKILL.md" ] || echo bootstrap
  return 0
}
has_evals() { [ -d "$ROOT/evals/$1" ]; }
case_dirs() { # skill kind -> case directories (a dir with prompt.md or case.yaml)
  local d
  for d in "$ROOT/evals/$1/$2"/*/; do
    [ -f "$d/prompt.md" ] || [ -f "$d/case.yaml" ] || continue
    printf '%s\n' "${d%/}"
  done
}

resolve_skills() { # prints one skill per line
  local s base mb files f all=0
  case "$SELECT" in
    skills)
      for s in $(printf '%s' "$SKILLS_ARG" | tr ',' ' '); do
        skill_dir "$s" >/dev/null || die 64 "unknown skill: $s"
        echo "$s"
      done ;;
    all) for s in $(all_skills); do has_evals "$s" && echo "$s"; done ;;
    changed)
      base=origin/main
      git -C "$ROOT" rev-parse --verify -q "$base^{commit}" >/dev/null 2>&1 || die 3 "--changed needs $base (git fetch origin main)"
      mb="$(git -C "$ROOT" merge-base "$base" HEAD 2>/dev/null)" || die 3 "--changed: no merge base with $base"
      files="$( { git -C "$ROOT" diff --name-only "$mb" -- skills evals config/voice bootstrap; git -C "$ROOT" ls-files --others --exclude-standard -- skills evals config/voice bootstrap; } | sort -u)"
      while IFS= read -r f; do
        case "$f" in
          skills/_shared/*|skills/_preamble*.md|skills/_design-preamble*.md) all=1 ;;
        esac
      done <<EOF
$files
EOF
      if [ "$all" = 1 ]; then
        for s in $(all_skills); do has_evals "$s" && echo "$s"; done
      else
        printf '%s\n' "$files" | awk -F/ '$1 == "bootstrap" && NF > 1 { print "bootstrap"; next }
          ($1 == "skills" || $1 == "evals") && $2 !~ /^_/ && $2 != "plugin" && $2 != "mocks" && $2 != "stubs" && $2 != "results" && NF > 2 { print $2 }' | sort -u |
          while IFS= read -r s; do skill_dir "$s" >/dev/null && echo "$s"; done
      fi ;;
    *) [ -n "$FAMILY_ARG" ] || die 64 "choose the skills: --skills a,b, --changed or --all" ;;
  esac
  return 0
}

kinds_for() { case "$KIND" in both) echo "routing behaviour" ;; *) echo "$KIND" ;; esac; }
ablation_for() { # kind -> the ablation mode (spec §3.1: routing reads the fire rate, behaviour needs a real delta)
  if [ -n "$ABLATION" ]; then echo "$ABLATION"; elif [ "$1" = routing ]; then echo none; else echo with-without; fi
}

# Family sets: evals/_families/<family>/<member>/ (prompt.md or case.yaml, graders/)
family_dirs() { local d; for d in "$ROOT"/evals/_families/*/; do [ -d "$d" ] && basename "$d"; done; return 0; }
family_case_dirs() { # family -> case directories
  local d
  for d in "$ROOT/evals/_families/$1"/*/; do
    [ -f "$d/prompt.md" ] || [ -f "$d/case.yaml" ] || continue
    printf '%s\n' "${d%/}"
  done
}
family_count() { family_case_dirs "$1" | grep -c . || true; }
is_family_kind() { case "$1" in family-*) return 0 ;; *) return 1 ;; esac; }

# desc_changed SKILL MERGE_BASE: 0 when SKILL's description differs from the merge base's (a skill new since then counts as changed)
desc_changed() {
  local s="$1" mb="$2" dir rel old tmp new
  dir="$(skill_dir "$s")" || return 1
  rel="${dir#"$ROOT"/}/SKILL.md"
  git -C "$ROOT" cat-file -e "$mb:$rel" 2>/dev/null || return 0
  tmp="$(mktemp "${TMPDIR:-/tmp}/skill-evals-desc.XXXXXX")" || return 1
  git -C "$ROOT" show "$mb:$rel" >"$tmp" 2>/dev/null
  old="$(description_of "$tmp")"; rm -f "$tmp"
  new="$(description_of "$dir/SKILL.md")"
  [ "$old" != "$new" ]
}

# pick_families SKILLS: prints "family<TAB>reason" lines: --family names, plus the families a selected skill belongs to whose
# description changed vs the merge base with origin/main
pick_families() {
  local skills="$1" f s mb="" have_mb=0 out=""
  if git -C "$ROOT" rev-parse --verify -q "origin/main^{commit}" >/dev/null 2>&1 &&
     mb="$(git -C "$ROOT" merge-base origin/main HEAD 2>/dev/null)" && [ -n "$mb" ]; then have_mb=1; fi
  for f in $(printf '%s' "$FAMILY_ARG" | tr ',' ' '); do
    [ -d "$ROOT/evals/_families/$f" ] || die 64 "unknown family: $f"
    out="$out$f\t--family
"
  done
  if [ "$have_mb" = 1 ]; then
    for f in $(family_dirs); do
      case " $(printf '%s' "$FAMILY_ARG" | tr ',' ' ') " in *" $f "*) continue ;; esac
      for s in $skills; do
        [ -d "$ROOT/evals/_families/$f/$s" ] || continue
        if desc_changed "$s" "$mb"; then out="$out$f\tdescription of $s changed
"; break; fi
      done
    done
  elif [ -n "$skills" ] && [ -n "$(family_dirs)" ]; then
    note "no origin/main merge base: families are chosen only by --family"
  fi
  printf '%b' "$out"
}

# ---------------------------------------------------------------------------------------------------------------------
# Graders (selftest)
# ---------------------------------------------------------------------------------------------------------------------

GRADER_TYPES="regex file_exists llm tool_used tool_order baseline"

# The tool's regex graders are JavaScript: new RegExp(pattern, flags).test(text), flags in dgimsuvy, `count:N` counted with g. So the
# selftest runs node with the pattern and flags exactly as written (a Perl-only construct such as (?i), (?s) or \A is an error).
# regex_error PATTERN FLAGS -> prints the problem (nothing when the regex is fine; a node failure is a problem too)
# The -- before the arguments keeps node from reading a pattern that starts with -- as one of its own options.
regex_error() {
  local out rc
  out="$(node -e '
    const [p, f] = process.argv.slice(1);
    if (!/^[dgimsuvy]*$/.test(f)) { console.log("flags \"" + f + "\" are not JavaScript RegExp flags (dgimsuvy)"); process.exit(0) }
    try { new RegExp(p, f) } catch (e) { console.log("pattern does not compile in JavaScript: " + e.message) }' -- "$1" "$2")" && rc=0 || rc=$?
  [ -z "$out" ] || printf '%s\n' "$out"
  [ "$rc" -eq 0 ] || printf 'node failed (exit %s) checking the pattern\n' "$rc"
  return 0
}
# regex_count PATTERN FLAGS < text -> number of matches (with the g flag added); exit 2 when it does not compile or node fails
regex_count() {
  local out rc
  out="$(node -e '
    const [p, f] = process.argv.slice(1);
    const t = require("fs").readFileSync(0, "utf8");
    let re; try { re = new RegExp(p, f.includes("g") ? f : f + "g") } catch (e) { console.log("bad"); process.exit(2) }
    console.log((t.match(re) || []).length)' -- "$1" "$2")" && rc=0 || rc=$?
  [ -z "$out" ] || printf '%s\n' "$out"
  [ "$rc" -eq 0 ] || return 2
}
# grader_pattern GRADER_MD: the regex grader's pattern (its pattern: line, else the body without trailing blank lines)
grader_pattern() {
  local pat
  pat="$(fm_get "$1" pattern)"; [ -n "$pat" ] || pat="$(fm_body "$1" | sed -e :a -e '/^\n*$/{$d;N;ba' -e '}')"
  printf '%s' "$pat"
}
# grader_input_matches GRADER_MD: every input_match value in the frontmatter, top level or nested (tool_order's before:/after:),
# one per line, quotes stripped. The runner compiles them as JavaScript; the selftest's tool_used check uses perl, which accepts more.
# Reads plain and single-quoted values exactly (every input_match in evals/ is single-quoted, none with ''); a double-quoted
# value's escapes are not decoded, and a block scalar (| or >) is not read at all.
grader_input_matches() {
  awk -v q="'" 'NR==1 { if ($0 != "---") exit; next }
    /^---[ \t]*$/ { exit }
    /^[ \t]*input_match:/ { v = $0; sub(/^[ \t]*input_match:[ \t]*/, "", v); sub(/[ \t\r]+$/, "", v)
      if (length(v) > 1 && ((substr(v,1,1) == "\"" && substr(v,length(v),1) == "\"") || (substr(v,1,1) == q && substr(v,length(v),1) == q))) v = substr(v, 2, length(v) - 2)
      if (v != "") print v }' "$1"
}
regex_verdict() { # match count -> 0 when the grader passes
  local match="$1" n="$2"
  case "$match" in
    contains|"") [ "$n" -ge 1 ] ;;
    not_contains) [ "$n" -eq 0 ] ;;
    count:*) [ "$n" -eq "${match#count:}" ] ;;
    *) return 2 ;;
  esac
}
# grader_passes GRADER_MD WHICH(pass|fail) [N] -> 0 the grader passes the reply, 1 fails it, 3 no reply file, 2 cannot evaluate.
# N picks an extra reply <grader>.<which>.<N>.<ext>; without it, the main reply <grader>.<which>.<ext>.
grader_passes() {
  local g="$1" which="$2" type name dir base pat flags match target tool im min max n f
  type="$(fm_get "$g" type)"; name="$(basename "$g" .md)"; dir="$(dirname "$g")/_selftest"; base="$dir/$name.$which${3:+.$3}"
  case "$type" in
    regex)
      pat="$(grader_pattern "$g")"
      flags="$(fm_get "$g" flags)"; match="$(fm_get "$g" match)"; target="$(fm_get "$g" target)"
      case "$target" in files) f="$base.files" ;; trace) f="$base.trace" ;; *) f="$base.txt" ;; esac
      [ -f "$f" ] || return 3
      [ -n "$pat" ] || return 2
      n="$(regex_count "$pat" "$flags" <"$f")" || return 2
      regex_verdict "$match" "$n" ;;
    file_exists)
      f="$base.files"; [ -f "$f" ] || return 3
      pat="$(fm_get "$g" path)"; [ -n "$pat" ] || return 2
      match=0
      while IFS= read -r n; do
        # shellcheck disable=SC2254
        case "$n" in $pat) match=1 ;; esac
      done <"$f"
      if [ "$(fm_get "$g" exists)" = false ]; then [ "$match" = 0 ]; else [ "$match" = 1 ]; fi ;;
    tool_used)
      f="$base.tools"; [ -f "$f" ] || return 3
      tool="$(fm_get "$g" tool)"; [ -n "$tool" ] || return 2
      im="$(fm_get "$g" input_match)"; min="$(fm_get "$g" min)"; max="$(fm_get "$g" max)"; [ -n "$min" ] || min=1
      n="$(perl -F'\t' -lane 'BEGIN { ($t, $im) = splice(@ARGV, 0, 2) } $c++ if $F[0] eq $t && ($im eq "" || (join("\t", @F[1..$#F]) =~ /$im/)); END { print $c + 0 }' "$tool" "$im" "$f")" || return 2
      [ "$n" -ge "$min" ] && { [ -z "$max" ] || [ "$n" -le "$max" ]; } ;;
    *) return 2 ;;
  esac
}

lint_case() { # case dir -> prints problems, returns the count via the exit status (0 = clean)
  local c="$1" rel g type name seen="" bad=0 free=0 gcount=0 tools to nf rerr x xw xn xrc
  rel="${c#"$ROOT"/}"
  if [ ! -f "$c/prompt.md" ] && ! grep -Eq '^[[:space:]]*prompt:' "$c/case.yaml" 2>/dev/null; then
    echo "SELFTEST-CASE: $rel: no prompt.md and no execution.prompt"; bad=1
  elif [ -f "$c/prompt.md" ] && [ -z "$(fm_body "$c/prompt.md" | tr -d '[:space:]')" ]; then
    echo "SELFTEST-CASE: $rel/prompt.md: empty prompt"; bad=1
  fi
  { [ -f "$c/prompt.md" ] && [ -n "$(fm_get "$c/prompt.md" timeout_seconds)" ]; } || grep -Eq "^[[:space:]]+timeout_seconds:" "$c/case.yaml" 2>/dev/null ||
    note "warning: $rel has no timeout_seconds in prompt.md or case.yaml's execution: (set it on every case)"
  # the run grants a case exactly these tools (#2023), so a case without the list would run with almost none
  [ -n "$(case_tools "$c")" ] || { echo "SELFTEST-CASE: $rel: no allowed_tools in prompt.md or case.yaml's execution: (the run grants a case only these)"; bad=1; }
  if [ -f "$c/case.yaml" ] && [ -n "$(awk '/scaffold_script:/ { sub(/.*scaffold_script:[ \t]*/, ""); print; exit }' "$c/case.yaml")" ]; then
    to="$(awk '/scaffold_script:/ { sub(/.*scaffold_script:[ \t]*/, ""); gsub(/["\047]/, ""); print; exit }' "$c/case.yaml")"
    [ -f "$c/$to" ] || { echo "SELFTEST-CASE: $rel/case.yaml: scaffold_script $to is not in the case directory"; bad=1; }
    case "$to" in *..*|/*) echo "SELFTEST-CASE: $rel/case.yaml: scaffold_script must sit inside the case directory"; bad=1 ;; esac
  fi
  for g in "$c"/graders/*.md; do
    [ -f "$g" ] || continue
    gcount=$((gcount + 1)); name="$(basename "$g" .md)"
    case " $seen " in *" $name "*) echo "SELFTEST-GRADER: $rel/graders/$name.md: duplicate name"; bad=1 ;; esac
    seen="$seen $name"
    type="$(fm_get "$g" type)"
    case " $GRADER_TYPES " in *" $type "*) ;; *) echo "SELFTEST-GRADER: $rel/graders/$name.md: type '$type' is not one of $GRADER_TYPES"; bad=1; continue ;; esac
    if [ "$type" = regex ]; then
      rerr="$(regex_error "$(grader_pattern "$g")" "$(fm_get "$g" flags)")"
      if [ -n "$rerr" ]; then echo "SELFTEST-REGEX: $rel/graders/$name.md: $rerr"; bad=1; free=1; continue; fi
    fi
    if [ "$type" = tool_used ] || [ "$type" = tool_order ]; then
      rerr=""
      while IFS= read -r x; do
        rerr="$(regex_error "$x" "")"; [ -z "$rerr" ] || break
      done < <(grader_input_matches "$g")
      if [ -n "$rerr" ]; then echo "SELFTEST-REGEX: $rel/graders/$name.md: input_match ${rerr#pattern }"; bad=1; free=1; continue; fi
    fi
    case "$type" in
      regex|file_exists|tool_used)
        free=1
        grader_passes "$g" pass; case $? in
          0) ;; 3) echo "SELFTEST-GRADER: $rel/graders/$name.md: no graders/_selftest/$name.pass.* reply"; bad=1 ;;
          *) echo "SELFTEST-GRADER: $rel/graders/$name.md: does not pass its pass reply"; bad=1 ;; esac
        grader_passes "$g" fail; case $? in
          1) ;; 3) echo "SELFTEST-GRADER: $rel/graders/$name.md: no graders/_selftest/$name.fail.* reply"; bad=1 ;;
          *) echo "SELFTEST-GRADER: $rel/graders/$name.md: does not fail its fail reply (a grader that cannot fail)"; bad=1 ;; esac
        # extra replies <grader>.pass.<N>.<ext> / .fail.<N>.<ext>, each graded on its own: one wording that passes
        # never hides another that fails, as it would inside one reply file
        for x in "$(dirname "$g")/_selftest/$name".pass.[0-9]*.* "$(dirname "$g")/_selftest/$name".fail.[0-9]*.*; do
          [ -f "$x" ] || continue
          xw="${x##*/}"; xw="${xw#"$name".}"; xn="${xw#*.}"; xn="${xn%%.*}"; xw="${xw%%.*}"
          grader_passes "$g" "$xw" "$xn"; xrc=$?
          case "$xw:$xrc" in
            pass:0|fail:1) ;;
            *:3) echo "SELFTEST-GRADER: $rel/graders/$name.md: ${x##*/} is not a reply this grader reads"; bad=1 ;;
            pass:*) echo "SELFTEST-GRADER: $rel/graders/$name.md: does not pass its pass reply ${x##*/}"; bad=1 ;;
            fail:*) echo "SELFTEST-GRADER: $rel/graders/$name.md: does not fail its fail reply ${x##*/}"; bad=1 ;;
          esac
        done ;;
      llm)
        [ -n "$(fm_body "$g" | tr -d '[:space:]')" ] || { echo "SELFTEST-GRADER: $rel/graders/$name.md: llm grader without a rubric"; bad=1; } ;;
    esac
    # selftest grades no tool_order or llm reply, so an extra next to one would sit there unchecked: say so
    case "$type" in
      regex|file_exists|tool_used) ;;
      *)
        for x in "$(dirname "$g")/_selftest/$name".pass.[0-9]*.* "$(dirname "$g")/_selftest/$name".fail.[0-9]*.*; do
          [ -f "$x" ] || continue
          echo "SELFTEST-GRADER: $rel/graders/$name.md: ${x##*/} is never graded (selftest grades only regex, file_exists and tool_used replies)"; bad=1
        done ;;
    esac
  done
  [ "$gcount" -gt 0 ] || { echo "SELFTEST-CASE: $rel: no graders/*.md"; bad=1; }
  case "$c" in
    */routing/*)
      nf=0; [ ! -f "$c/graders/skill-fired.md" ] || nf=$((nf + 1)); [ ! -f "$c/graders/skill-not-fired.md" ] || nf=$((nf + 1))
      [ "$nf" -eq 1 ] || { echo "SELFTEST-CASE: $rel: a routing case needs exactly one of graders/skill-fired.md and graders/skill-not-fired.md (found $nf)"; bad=1; } ;;
  esac
  [ "$gcount" -eq 0 ] || [ "$free" = 1 ] || { echo "SELFTEST-CASE: $rel: needs at least one regex, file_exists or tool_used grader"; bad=1; }
  tools=""; : "$tools"
  return "$bad"
}

lint_fixture() { # fixture.sh -> problems on stdout; 0 clean
  local f="$1" rel bad=0 hit
  rel="${f#"$ROOT"/}"
  hit="$(grep -nE '(^|[;&|[:space:]])(rm|mv|cp|rmdir|chmod|chown)[[:space:]]+(-[A-Za-z]+[[:space:]]+)*(/|~|\$HOME|\.\.)|\bsudo\b|(^|[[:space:]])cd[[:space:]]+(/|~|\.\.)|>>?[[:space:]]*(/[a-z]|~|\$HOME)|git[[:space:]]+(push|remote[[:space:]]+add)' "$f" | grep -vE '>>?[[:space:]]*/dev/null' | grep -vE '^[0-9]+:[[:space:]]*#' || true)"
  if [ -n "$hit" ]; then
    while IFS= read -r l; do echo "SELFTEST-FIXTURE: $rel:${l%%:*}: deletes or writes outside \$PWD: ${l#*:}"; done <<EOF
$hit
EOF
    bad=1
  fi
  return "$bad"
}

run_fixture() { # fixture.sh -> runs it in a scrubbed temp dir; 0 ok
  local f="$1" t rc
  t="$(mktemp -d "${TMPDIR:-/tmp}/skill-evals-fixture.XXXXXX")" || return 1
  mkdir -p "$t/home" "$t/ws"
  (cd "$t/ws" && env -i PATH="$PATH" HOME="$t/home" TERM="${TERM:-dumb}" LANG="${LANG:-C.UTF-8}" bash "$f" >"$t/out" 2>&1)
  rc=$?
  [ "$rc" -eq 0 ] || { echo "SELFTEST-FIXTURE: ${f#"$ROOT"/}: exit $rc"; sed 's/^/    | /' "$t/out" | head -10; }
  find "$t" -delete 2>/dev/null
  return "$rc"
}

# lint_descriptions: lints $lint_skills (default: every skill). lint_len_warn=1 (--selftest) makes an over-400 description a warning on
# stderr, not a problem, and skips the all-skills byte baseline (it only means something over the whole set).
lint_descriptions() { # -> 0 clean
  local s d len total=0 bad=0 base prompts p shared baseline limit all_listed
  all_listed="${lint_skills:-$(all_skills)}"
  for s in $all_listed; do
    d="$(description_of "$(skill_dir "$s")/SKILL.md")"
    len=${#d}
    total=$((total + $(printf '%s' "$d" | wc -c)))
    [ -n "$d" ] || { echo "LINT-DESC: skills/$s/SKILL.md: no description"; bad=1; continue; }
    if [ "$len" -gt 400 ]; then
      if [ "${lint_len_warn:-0}" = 1 ]; then echo "LINT-DESC: skills/$s: description is $len characters (max 400) (warning)" >&2
      else echo "LINT-DESC: skills/$s: description is $len characters (max 400)"; bad=1; fi
    fi
    has_evals "$s" || continue
    prompts="$(find "$ROOT/evals/$s" \( -name prompt.md -o -name case.yaml \) -type f 2>/dev/null | sort)"
    [ -n "$prompts" ] || continue
    printf '%s' "$d" >"$LINT_TMP/desc"
    # shellcheck disable=SC2086
    shared="$(echo "$prompts" | xargs perl -e '
      sub words { my $t = lc shift; $t =~ s/[^a-z0-9\x27 ]+/ /g; return split " ", $t }
      my $df = shift; open my $h, "<", $df or exit 2; local $/; my @d = words(scalar <$h>); my %g;
      for my $i (0 .. $#d - 4) { $g{join " ", @d[$i .. $i + 4]} = 1 }
      for my $p (@ARGV) { open my $f, "<", $p or next; my @w = words(scalar <$f>);
        for my $i (0 .. $#w - 4) { my $k = join " ", @w[$i .. $i + 4]; if ($g{$k}) { print "$p\t$k\n"; last } } }' "$LINT_TMP/desc")"
    if [ -n "$shared" ]; then
      while IFS="$(printf '\t')" read -r p phrase; do
        case "$p" in
          */_heldout/*) echo "LINT-DESC: skills/$s: the description shares a 5-word phrase with ${p#"$ROOT"/} (held out, phrase not shown)" ;;
          *) echo "LINT-DESC: skills/$s: the description shares the phrase \"$phrase\" with ${p#"$ROOT"/}" ;;
        esac
      done <<EOF
$shared
EOF
      bad=1
    fi
  done
  baseline="$ROOT/evals/_lib/description-bytes.baseline"
  if [ "${lint_len_warn:-0}" = 1 ]; then :
  elif [ -f "$baseline" ]; then
    base="$(tr -d '[:space:]' <"$baseline")"
    if grep -Eq '^[0-9]+$' <<<"$base"; then
      limit=$((base + base / 4))
      if [ "$total" -gt "$limit" ]; then echo "LINT-DESC: all descriptions total $total bytes, over the baseline $base + 25% ($limit)"; bad=1; fi
    else
      echo "LINT-DESC: $baseline is not a number"; bad=1
    fi
  else
    note "descriptions total $total bytes (no evals/_lib/description-bytes.baseline yet)"
  fi
  return "$bad"
}

do_selftest() {
  local skills s k c f errs=0 cases=0 out
  skills="$(resolve_skills)" || exit $?
  if [ -z "$skills" ] && [ "$SELECT" = changed ] && [ -z "$(family_dirs)" ]; then echo "no skills changed"; exit 0; fi
  need perl; need jq; need node
  LINT_TMP="$(mktemp -d "${TMPDIR:-/tmp}/skill-evals-lint.XXXXXX")" || die 3 "no temp dir"
  trap 'find "$LINT_TMP" -delete 2>/dev/null' EXIT
  for s in $skills; do
    has_evals "$s" || { note "$s: no evals/$s yet"; continue; }
    f="$ROOT/evals/$s/fixture.sh"
    if [ -f "$f" ]; then
      out="$(lint_fixture "$f")" || { echo "$out"; errs=$((errs + 1)); }
      run_fixture "$f" || errs=$((errs + 1))
    fi
    for k in $(kinds_for); do
      for c in $(case_dirs "$s" "$k"); do
        cases=$((cases + 1))
        out="$(lint_case "$c")" || { echo "$out"; errs=$((errs + 1)); }
      done
    done
  done
  for s in $(family_dirs); do
    for c in $(family_case_dirs "$s"); do
      cases=$((cases + 1))
      out="$(lint_case "$c")" || { echo "$out"; errs=$((errs + 1)); }
    done
  done
  lint_skills="$skills" lint_len_warn=1 lint_descriptions || errs=$((errs + 1))
  echo "selftest: $cases case(s), $errs problem(s)"
  [ "$errs" -eq 0 ]
}

do_lint() {
  need perl
  LINT_TMP="$(mktemp -d "${TMPDIR:-/tmp}/skill-evals-lint.XXXXXX")" || die 3 "no temp dir"
  trap 'find "$LINT_TMP" -delete 2>/dev/null' EXIT
  if lint_descriptions; then echo "lint-descriptions: ok"; else echo "lint-descriptions: problems"; return 1; fi
}

# ---------------------------------------------------------------------------------------------------------------------
# Status
# ---------------------------------------------------------------------------------------------------------------------

day_epoch() { perl -MTime::Local=timegm -e '($y, $m, $d) = split /-/, $ARGV[0]; print timegm(0, 0, 0, $d, $m - 1, $y)' "$1" 2>/dev/null; }

do_status() {
  local s f lr fire delta pr age stale now
  need perl
  now="$(date +%s)"
  echo "| Skill | Fire rate | Behaviour Δ | Last run | PR | |"
  echo "|---|---|---|---|---|---|"
  for s in $(all_skills); do
    f="$ROOT/evals/$s/status.md"
    if [ ! -f "$f" ]; then echo "| $s | - | - | never | - | stale |"; continue; fi
    lr="$(awk -F': *' '$1 == "last_run" { print $2; exit }' "$f")"
    fire="$(awk -F': *' '$1 == "fire_rate" { print $2; exit }' "$f")"
    delta="$(awk -F': *' '$1 == "delta" { print $2; exit }' "$f")"
    pr="$(awk -F': *' '$1 == "pr" { print $2; exit }' "$f")"
    stale=stale
    if grep -Eq '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' <<<"$lr"; then
      age="$(day_epoch "$lr")"
      if [ -n "$age" ] && [ $(( (now - age) / 86400 )) -le 7 ]; then stale=""; fi
    fi
    echo "| $s | ${fire:--} | ${delta:--} | ${lr:-never} | ${pr:--} | $stale |"
  done
  if [ -r "$TALLY" ] && command -v jq >/dev/null 2>&1; then
    jq -s -r --arg d "$DAY" '"spend today: $" + ([.[] | select(.t == "settle" and .date == $d) | .usd] | add // 0 | tostring) + " of $'"$CAP_USD"'"' "$TALLY" 2>/dev/null || echo "spend: tally unreadable"
  fi
}

# ---------------------------------------------------------------------------------------------------------------------
# Spend tally (spec §6.4): one flock'd jsonl; rows are lane, reserve, settle
# ---------------------------------------------------------------------------------------------------------------------

# locked CMD...: run CMD holding an exclusive lock on the tally (perl's flock works the same on Linux and macOS)
locked() {
  perl -MFcntl=:flock -e '$^F = 255; open(my $f, ">>", shift) or exit 3; flock($f, LOCK_EX) or exit 3; exec @ARGV or exit 3' "$TALLY" "$@"
}

# tally_op: MODE is reserve (check and write), peek (check only) or settle (replace the reserve with the real cost)
tally_op() {
  local rows pending live spent total all_total
  [ -f "$TALLY" ] || : >"$TALLY"
  jq -e -s 'all(.[]; type == "object" and (.t | IN("lane", "reserve", "settle")) and (.date | type == "string") and (.lane | type == "string")
      and (.t == "lane" or ((.usd | type == "number") and (.id | type == "string")))
      and (.t != "reserve" or ((.ts | type == "number") and (.pid | type == "number"))))' "$TALLY" >/dev/null 2>&1 ||
    { echo "the spend tally is unreadable or malformed ($TALLY); refusing to start (fix or remove it)"; return 2; }
  if [ "$TALLY_MODE" = settle ]; then
    jq -nc --arg id "$TID" --arg d "$DAY" --arg l "$LANE" --argjson u "$SETTLE_USD" --argjson ts "$NOW" --arg c "$CMD_DESC" \
      '{t: "settle", id: $id, date: $d, lane: $l, usd: $u, ts: $ts, cmd: $c}' >>"$TALLY"
    spent="$(jq -s -r --arg d "$DAY" '[.[] | select(.t == "settle" and .date == $d) | .usd] | add // 0' "$TALLY")"
    all_total="$(jq -s -r '[.[] | select(.t == "settle") | .usd] | add // 0' "$TALLY")"
    awk -v r="$SETTLE_USD" -v t="$spent" -v c="$CAP_USD" -v a="$all_total" 'BEGIN { printf "spend: this run $%.2f, today $%.2f of $%.2f, all runs $%.2f\n", r, t, c, a }'
    return 0
  fi
  pending="$(jq -s -r --arg d "$DAY" --argjson now "$NOW" --argjson ttl "$RESERVE_TTL" '
      (map(select(.t == "settle") | .id)) as $s
      | .[] | select(.t == "reserve" and .date == $d and ($now - .ts) <= $ttl and ((.id | IN($s[])) | not))
      | "\(.pid)\t\(.lane)\t\(.usd)"' "$TALLY")"
  live=""
  while IFS="$(printf '\t')" read -r pid plane pusd; do
    [ -n "$pid" ] || continue
    if kill -0 "$pid" 2>/dev/null; then live="$live$plane $pusd
"; fi
  done <<EOF
$pending
EOF
  rows="$(jq -s -r --arg d "$DAY" '.[] | select(.t == "settle" and .date == $d) | "\(.lane) \(.usd)"' "$TALLY")"
  total="$(printf '%s\n%s\n' "$rows" "$live" | awk 'NF { s += $2 } END { printf "%.6f", s + 0 }')"
  awk -v t="$total" -v c="$CAP_USD" -v m="$MAXC" 'BEGIN { printf "spend: today $%.2f of $%.2f; this run reserves $%.2f\n", t, c, m }'
  if awk -v m="$MAXC" -v t="$total" -v c="$CAP_USD" 'BEGIN { exit !(t + m > c + 1e-9) }'; then
    echo "refused: today's \$$total plus --max-cost-usd $MAXC would pass the \$$CAP_USD daily cap."; return 2
  fi
  if [ "$TALLY_MODE" = reserve ]; then
    jq -nc --arg id "$TID" --arg d "$DAY" --arg l "$LANE" --argjson pid "$SE_PID" --argjson ts "$NOW" --argjson u "$MAXC" --arg c "$CMD_DESC" \
      '{t: "reserve", id: $id, date: $d, lane: $l, pid: $pid, ts: $ts, usd: $u, cmd: $c}' >>"$TALLY"
  fi
  return 0
}
export -f tally_op

tally_run() { # MODE [settle usd [row id [ceiling usd]]]; prints the tally's message, returns its status (the family set uses its own id and ceiling)
  mkdir -p "$EVAL_HOME" || return 3
  export TALLY RUN_ID DAY LANE CAP_USD RESERVE_TTL MAXC="${4:-$MAX_COST}" TID="${3:-$RUN_ID}" SE_PID="$$" NOW CMD_DESC TALLY_MODE="$1" SETTLE_USD="${2:-0}"
  NOW="$(date +%s)"
  locked bash -c tally_op
}

# ---------------------------------------------------------------------------------------------------------------------
# Scrub (spec §5.4): token patterns and the operator's secret-looking values, from what was saved
# ---------------------------------------------------------------------------------------------------------------------

scrub_paths() { # files or directories
  local lits n v
  [ $# -gt 0 ] || return 0
  lits="$(mktemp "${TMPDIR:-/tmp}/skill-evals-lits.XXXXXX")" || return 1
  chmod 600 "$lits"
  for n in $(compgen -e); do
    case "$n" in
      EVAL_*|*TOKEN*|*SECRET*|*PASSWORD*|*PASSWD*|*CREDENTIAL*|*API_KEY*|*APIKEY*|*PRIVATE_KEY*) ;;
      *) continue ;;
    esac
    v="${!n}"
    [ "${#v}" -ge 6 ] || continue
    case "$v" in *$'\n'*) continue ;; esac
    printf '%s\n' "$v" >>"$lits"
  done
  find "$@" -type f \( -name '*.json' -o -name '*.jsonl' -o -name '*.html' -o -name '*.txt' -o -name '*.log' -o -name '*.md' \) -print 2>/dev/null |
    while IFS= read -r f; do
      SCRUB_LITS="$lits" perl -i -pe '
        BEGIN { if (open(my $h, "<", $ENV{SCRUB_LITS})) { while (<$h>) { chomp; push @L, $_ if length } } }
        for my $l (@L) { s/\Q$l\E/[REDACTED]/g }
        s/gh[pousr]_[A-Za-z0-9]{20,}/[REDACTED]/g; s/github_pat_[A-Za-z0-9_]{20,}/[REDACTED]/g;
        s/sk-ant-[A-Za-z0-9_-]{20,}/[REDACTED]/g; s/\bsk-[A-Za-z0-9]{32,}/[REDACTED]/g;
        s/xox[baprs]-[A-Za-z0-9-]{10,}/[REDACTED]/g; s/\bAKIA[0-9A-Z]{16}\b/[REDACTED]/g;
        s/eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{5,}/[REDACTED]/g;
        s/(authorization:\s*bearer\s+)[A-Za-z0-9._~+\/=-]{16,}/$1\[REDACTED]/ig;' "$f"
    done
  rm -f "$lits"
}

# ---------------------------------------------------------------------------------------------------------------------
# Stage (spec §5.2 step 2)
# ---------------------------------------------------------------------------------------------------------------------

ROOT_REAL=""
real_of() { perl -MCwd=realpath -e 'my $p = realpath($ARGV[0]); print defined $p ? $p : ""' "$1"; }

check_links() { # paths... : refuse a link that resolves outside this repo (or nowhere)
  local p l t bad=0
  for p in "$@"; do
    [ -e "$p" ] || [ -L "$p" ] || continue
    while IFS= read -r l; do
      [ -n "$l" ] || continue
      t="$(real_of "$l")"
      case "$t" in
        "$ROOT_REAL"/*) ;;
        *) echo "skill-evals: $l resolves outside the repo (${t:-nowhere}); not staging it" >&2; bad=1 ;;
      esac
    done <<EOF
$(find "$p" -type l 2>/dev/null)
EOF
  done
  return "$bad"
}

rewrite_default() { # STAGE NAME FROM TO: the shell default of NAME (the text after ":-") from FROM to TO, in the staged skills and config
  local S="$1" name="$2" from="$3" to="$4" f n=0 a b
  a="\${$name:-$from}"; b="\${$name:-$to}"
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    FROM="$a" TO="$b" perl -i -pe 'BEGIN { $f = quotemeta($ENV{FROM}); $t = $ENV{TO} } s/$f/$t/g' "$f"
    n=$((n + 1))
  done <<EOF
$(grep -rlF -- "$a" "$S/skills" "$S/config" "$S/stubs" 2>/dev/null)
EOF
  echo "$name default $from -> $to: $n file(s)" >>"$S/.stage-rewrites"
  [ "$n" -gt 0 ] || note "warning: no staged file holds $a, so the $name default was not rewritten (a skill that reads it will run with the default $from)"
}

rewrite_stage() { # STAGE: the staged copy only, never the source tree
  local S="$1" n=0 a b
  : >"$S/.stage-rewrites"
  [ "$PREAMBLE" = on ] || rewrite_default "$S" AW_PREAMBLE on off
  rewrite_default "$S" AW_GIT_MODE full readonly
  rewrite_default "$S" AW_RESULT 0 1
  # Sindri block mode (AW_RESULT_SKILL / AW_RESULT_FILE): the env cannot reach the agent, so the staged gate reads them from the
  # workspace root's .aw-eval/result-skill and result-file, which only a Sindri case's fixture writes; every other case finds no
  # file, gets an empty value and stays in the normal mode.
  local walk='d=$PWD; while [ ! -d "$d/.aw-eval" ] && [ "$d" != / ]; do d=${d%/*}; [ -n "$d" ] || d=/; done'
  rewrite_default "$S" AW_RESULT_SKILL "" "\$($walk; cat \"\$d/.aw-eval/result-skill\" 2>/dev/null)"
  rewrite_default "$S" AW_RESULT_FILE "" "\$($walk; cat \"\$d/.aw-eval/result-file\" 2>/dev/null)"
  a="\${AW_STATE_DIR:-\$HOME/.agentic-workflow}"
  b='${AW_STATE_DIR:-$(d=$PWD; while [ ! -d "$d/.aw-eval" ] && [ "$d" != / ]; do d=${d%/*}; [ -n "$d" ] || d=/; done; [ -d "$d/.aw-eval" ] || d=$PWD; printf %s "$d")/.aw-state}'
  if [ -f "$S/skills/_shared/repo-slug.sh" ] && grep -qF -- "$a" "$S/skills/_shared/repo-slug.sh"; then
    FROM="$a" TO="$b" perl -i -pe 'BEGIN { $f = quotemeta($ENV{FROM}); $t = $ENV{TO} } s/$f/$t/g' "$S/skills/_shared/repo-slug.sh"
    n=1
  fi
  echo "repo-slug.sh state default -> workspace root .aw-state: $n file(s)" >>"$S/.stage-rewrites"
}

# inject_scaffold CASE_DIR UPS: write _aw-scaffold.sh and point the staged case.yaml at it
inject_scaffold() {
  local c="$1" ups="$2" own="" y="$1/case.yaml" name
  name="$(basename "$c")"
  if [ -f "$y" ]; then own="$(awk '/^[ \t]*scaffold_script:/ { sub(/^[ \t]*scaffold_script:[ \t]*/, ""); gsub(/["\047]/, ""); sub(/[ \t\r]+$/, ""); print; exit }' "$y")"; fi
  {
    echo '#!/usr/bin/env bash'
    echo '# Written by scripts/skill-evals.sh: put the staged toolkit at the default ${AW_TOOLKIT:-...} path inside this run'"'"'s HOME,'
    echo '# then run the case'"'"'s own scaffold script (if any) in the same working directory.'
    echo 'set -eu'
    echo 'here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"'
    echo "stage=\"\$(cd \"\$here/$ups\" && pwd)\""
    echo 'dest="$HOME/.agentic-workflow/toolkit"'
    echo 'mkdir -p "$dest" "$HOME/.agentic-workflow" .aw-eval'
    echo '# an empty .bashrc: the sandbox hides a missing one behind an unreadable stand-in, and every shell then prints "Permission denied" (#1924)'
    echo '[ -e "$HOME/.bashrc" ] || : >"$HOME/.bashrc"'
    echo 'cp -R "$stage/skills" "$dest/"'
    echo '[ ! -d "$stage/config" ] || cp -R "$stage/config" "$dest/"'
    echo 'echo "claude $HOME/.claude/skills" > "$HOME/.agentic-workflow/providers"'
    if [ -n "$own" ]; then echo "exec bash \"\$here/$own\""; fi
  } >"$c/_aw-scaffold.sh"
  chmod +x "$c/_aw-scaffold.sh"
  if [ ! -f "$y" ]; then
    printf 'schema_version: "1.1"\nname: %s\ncontext:\n  scaffold_script: _aw-scaffold.sh\n' "$name" >"$y"
  elif [ -n "$own" ]; then
    awk '/^[ \t]*scaffold_script:/ && !done { sub(/scaffold_script:.*/, "scaffold_script: _aw-scaffold.sh"); done = 1 } { print }' "$y" >"$y.new" && mv "$y.new" "$y"
  elif grep -qE '^context:[ \t]*$' "$y"; then
    awk '{ print } /^context:[ \t]*$/ && !done { print "  scaffold_script: _aw-scaffold.sh"; done = 1 }' "$y" >"$y.new" && mv "$y.new" "$y"
  else
    printf 'context:\n  scaffold_script: _aw-scaffold.sh\n' >>"$y"
  fi
}

# stage_build KIND SKILLS -> STAGE (a fresh directory); returns 3 when it must be refused
stage_build() {
  local kind="$1" skills="$2" S s d n a src c rel ups
  S="$EVAL_HOME/cache/stage-$RUN_ID-$kind"
  STAGE_LIST="$STAGE_LIST $kind=$S"
  mkdir -p "$S" || die 3 "cannot create $S"
  if git -C "$S" rev-parse --git-dir >/dev/null 2>&1; then die 3 "the stage $S is inside a git repo (the eval refuses plugins in repos with many worktrees); point AW_STATE_DIR elsewhere"; fi
  STAGE="$S"

  local srcs="$ROOT/skills $ROOT/bootstrap $ROOT/evals/_families $ROOT/evals/plugin $ROOT/evals/_lib $ROOT/evals/mocks $ROOT/evals/stubs $ROOT/config/voice $ROOT/config/agents"
  for s in $skills; do srcs="$srcs $ROOT/evals/$s"; done
  # shellcheck disable=SC2086
  check_links $srcs || die 3 "a staged path resolves outside the repo"

  rsync -a --exclude skills "$ROOT/evals/plugin/" "$S/" || die 3 "rsync of evals/plugin failed"
  mkdir -p "$S/skills" "$S/evals" "$S/config"
  for n in $(all_skills); do
    mkdir -p "$S/skills/$n"; rsync -aL "$(skill_dir "$n")/SKILL.md" "$S/skills/$n/" || die 3 "rsync of skill $n failed"
  done
  for s in $skills; do rsync -aL "$(skill_dir "$s")/" "$S/skills/$s/" || die 3 "rsync of skill $s failed"; done
  [ ! -d "$ROOT/skills/_shared" ] || rsync -aL "$ROOT/skills/_shared/" "$S/skills/_shared/"
  for a in "$ROOT"/skills/_preamble*.md "$ROOT"/skills/_design-preamble*.md; do [ ! -f "$a" ] || rsync -aL "$a" "$S/skills/"; done
  [ ! -d "$ROOT/config/voice" ] || rsync -aL "$ROOT/config/voice/" "$S/config/voice/"
  if [ -d "$ROOT/config/agents" ]; then
    for a in "$ROOT"/config/agents/*.md; do
      [ -f "$a" ] || continue
      n="$(basename "$a" .md)"
      for s in $skills; do
        if grep -rqw -- "$n" "$(skill_dir "$s")" 2>/dev/null; then
          mkdir -p "$S/config/agents" "$S/agents"; rsync -aL "$a" "$S/config/agents/"; rsync -aL "$a" "$S/agents/"; break
        fi
      done
    done
  fi
  for d in _lib mocks; do [ ! -d "$ROOT/evals/$d" ] || rsync -aL "$ROOT/evals/$d/" "$S/evals/$d/"; done
  # the stubs go to the stage root: claude plugin eval denies the agent every read under evals/, which hides a stub there
  # from its shell, so the real tool would answer instead
  [ ! -d "$ROOT/evals/stubs" ] || rsync -aL "$ROOT/evals/stubs/" "$S/stubs/"
  for s in $skills; do
    mkdir -p "$S/evals/$s"
    [ ! -f "$ROOT/evals/$s/fixture.sh" ] || rsync -aL "$ROOT/evals/$s/fixture.sh" "$S/evals/$s/"
    if [ -d "$ROOT/evals/$s/$kind" ]; then rsync -aL --exclude _selftest "$ROOT/evals/$s/$kind/" "$S/evals/$s/$kind/"; fi
  done
  if is_family_kind "$kind"; then
    mkdir -p "$S/evals/_families/${kind#family-}"
    rsync -aL --exclude _selftest --exclude README.md "$ROOT/evals/_families/${kind#family-}/" "$S/evals/_families/${kind#family-}/" || die 3 "rsync of the family ${kind#family-} failed"
  fi

  # refused: a hooks/ dir, or an .mcp.json other than evals/plugin/.mcp.json at the stage root
  if [ -n "$(find "$S" -name hooks -type d -print -quit)" ]; then die 3 "the stage holds a hooks/ directory ($(find "$S" -name hooks -type d -print -quit))"; fi
  while IFS= read -r a; do
    [ "$a" = "$S/.mcp.json" ] && cmp -s "$a" "$ROOT/evals/plugin/.mcp.json" && continue
    die 3 "the stage holds an .mcp.json other than evals/plugin/.mcp.json ($a)"
  done <<EOF
$(find "$S" -name .mcp.json -print)
EOF

  rewrite_stage "$S"
  for s in $skills; do
    [ -d "$S/evals/$s/$kind" ] || continue
    for c in $(find "$S/evals/$s/$kind" -type f \( -name prompt.md -o -name case.yaml \) -exec dirname {} \; | sort -u); do
      rel="${c#"$S"/}"; ups="$(echo "$rel" | awk -F/ '{ s = ".."; for (i = 2; i <= NF; i++) s = s "/.."; print s }')"
      inject_scaffold "$c" "$ups"
    done
  done
  if is_family_kind "$kind"; then
    for c in $(find "$S/evals/_families/${kind#family-}" -type f \( -name prompt.md -o -name case.yaml \) -exec dirname {} \; | sort -u); do
      rel="${c#"$S"/}"; ups="$(echo "$rel" | awk -F/ '{ s = ".."; for (i = 2; i <= NF; i++) s = s "/.."; print s }')"
      inject_scaffold "$c" "$ups"
    done
  fi
  find "$S" -name '*.new' -delete 2>/dev/null
  return 0
}

# ---------------------------------------------------------------------------------------------------------------------
# Results
# ---------------------------------------------------------------------------------------------------------------------

JQ_FIRED='def fired($run): ($run.graders // []) as $g
  | ([$g[] | select(.name == "skill-fired")] | first) as $f
  | ([$g[] | select(.name == "skill-not-fired")] | first) as $n
  | if $f != null then ($f.passed // false) elif $n != null then (($n.passed // false) | not) else null end;'

print_table() { # results.json
  echo "| Case | WITH | W-OUT | Δ | Skill fired | Cost |"
  echo "|---|---|---|---|---|---|"
  jq -r "$JQ_FIRED"'
    def r2: if . == null then "-" else ((. * 100 | round) / 100 | tostring) end;
    .cases[]
    | ((.arms.with // []) | map(fired(.)) | map(select(. != null))) as $fl
    | ([(.arms.with // [])[], (.arms.without // [])[]] | map((.costUsd // 0) + (.judgeCostUsd // 0)) | add // 0) as $cost
    | "| \(.name) | \(.aggregates.score | r2) | \(.aggregates.scoreWithout | r2) | \(.aggregates.delta | r2) | \(if ($fl | length) == 0 then "n/a" else "\($fl | map(select(. == true)) | length)/\($fl | length)" end) | $\(($cost * 100 | round) / 100) |"' "$1"
}

print_failures() { # results.json threshold
  jq -r --argjson th "$2" '
    .cases[] | select((.aggregates.score // 0) < $th) as $c
    | [($c.arms.with // [])[] | .graders[]? | select((.scored // true) and (.passed == false))] | group_by(.name)[]
    | "FAIL \($c.name) / \(.[0].name): failed in \(length) run(s): \((.[0].explanation // .[0].evidence // "") | tostring | .[0:200])"' "$1"
  # why: a with-only check is never in the score, so the FAIL lines above skip it; a broken one would never show (#2024).
  # skill-fired and skill-not-fired are left out: the Skill fired column and the routing gate already report them.
  jq -r '
    .cases[] as $c
    | [($c.arms.with // [])[] | .graders[]? | select((.withOnly == true) and (.passed == false) and ((.name | test("^skill-(not-)?fired$")) | not))]
    | group_by(.name)[]
    | "NOT SCORED \($c.name) / \(.[0].name): failed in \(length) run(s), with the skill only, not in the score: \((.[0].explanation // .[0].evidence // "") | tostring | .[0:200])"' "$1"
  jq -r '.cases[] | .arms.with[]? | select((.error // "") != "") | "ERROR \(.error | .[0:200])"' "$1" | sort -u
}

gate_lines() { # results.json -> lines "ROUTING GATE MISSED: ..."; nothing when the gate holds; non-zero when it cannot be judged
  local out filter names name sd slash="" sj
  # why: a slash-only skill (front matter disable-model-invocation: true) is never picked by the model, so the natural-case rule skips it
  names="$(jq -r '[.cases[] | .dir | split("/")[1]] | unique[]' "$1" 2>/dev/null)" || names=""
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    sd="$(skill_dir "$name")" || continue
    if awk '/^---[ \t]*$/ { n++; next } n == 1 && /^disable-model-invocation:[ \t]*["\x27]?true["\x27]?[ \t]*$/ { f = 1 } n >= 2 { exit } END { exit !f }' "$sd/SKILL.md"; then
      slash="$slash$(printf '%s' "$name" | jq -R .),"
    fi
  done <<<"$names"
  sj="[${slash%,}]"
  filter="$JQ_FIRED"'
    [ .cases[] | (.dir | split("/")) as $p | ($p | last) as $b
      | {skill: ($p[1] // ""), case: $b, nat: ($b | test("natural-[0-9]+$")), near: ($b | test("near-miss$")),
         f: ((.arms.with // []) | map(fired(.)) | map(select(. != null)))} ] as $rows
    | ($rows[] | select((.nat or .near) and (.f | length) == 0)
        | "ROUTING GATE MISSED: \(.skill)/\(.case): no judged runs (no run has a skill-fired or skill-not-fired grader result)"),
      (($rows | map(.skill) | unique[]) as $s
        | ([$rows[] | select(.skill == $s and .nat) | .f[]]) as $nat
        | ([$rows[] | select(.skill == $s and .near) | .f[]]) as $near
        | (if ($slash | index($s)) then empty
           elif ($nat | length) > 0 and (($nat | map(select(. == true)) | length) * 2 < ($nat | length))
             then "ROUTING GATE MISSED: \($s) fired \($nat | map(select(. == true)) | length)/\($nat | length) on its natural cases (needs 50%)" else empty end),
          (if ($near | map(select(. == true)) | length) > 0
             then "ROUTING GATE MISSED: \($s) fired \($near | map(select(. == true)) | length)/\($near | length) on its near-miss case (needs 0)" else empty end))'
  # stdout only: a jq warning on stderr must never read as a gate line; on failure, run it again for the message
  out="$(jq -r --argjson slash "$sj" "$filter" "$1" 2>/dev/null)" || {
    echo "ROUTING GATE MISSED: the gate could not be evaluated (jq failed on $1: $(jq -r --argjson slash "$sj" "$filter" "$1" 2>&1 >/dev/null | head -c 200))"
    return 1
  }
  [ -z "$out" ] || printf '%s\n' "$out"
  return 0
}

# ---------------------------------------------------------------------------------------------------------------------
# The run
# ---------------------------------------------------------------------------------------------------------------------

shq() { printf '%q ' "$@"; }

# Tool grants (#2023). claude plugin eval gives every case in one call the same tools: its --allow-tools grant plus every
# built-in, whatever the case's allowed_tools says. So the run calls it once per case name with that case's own grant:
# --allow-tools the gated tools the list names, --disallow-tools the built-ins it doesn't. NotebookEdit counts only when named
# (a Write grant unlocks it too); ToolSearch is never kept back, a deferred tool can't load without it.
EVAL_BUILTINS="Agent Bash Edit Glob Grep NotebookEdit Read Skill TaskStop WebFetch Write"
stage_case_dirs() { # stage -> its case directories (evals/<skill>/<kind>/<case>, evals/_families/<family>/<case>)
  find "$1/evals" -mindepth 4 -maxdepth 4 -type f \( -name prompt.md -o -name case.yaml \) \
    ! -path "$1/evals/_lib/*" ! -path "$1/evals/mocks/*" -exec dirname {} \; | sort -u
}
case_tools() { # case dir -> the tools its allowed_tools names (prompt.md front matter and case.yaml), one per line
  local c="$1"
  { [ ! -f "$c/prompt.md" ] || fm_get "$c/prompt.md" allowed_tools | tr -d '[]' | tr ',' '\n'
    [ ! -f "$c/case.yaml" ] || awk '
      /^[ \t]+allowed_tools:/ { v = $0; sub(/^[^:]*:[ \t]*/, "", v)
        if (v != "") { gsub(/[][]/, "", v); n = split(v, a, ","); for (i = 1; i <= n; i++) print a[i]; next }
        on = 1; next }
      on && /^[ \t]*- / { v = $0; sub(/^[ \t]*- /, "", v); print v; next }
      { on = 0 }' "$c/case.yaml"
  } | sed "s/^[ \"']*//; s/[ \"']*\$//" | grep -v '^$' | sort -u
}
case_grant() { # tools (one per line) -> GRANT_ALLOW (the gated ones) and GRANT_DENY (the built-ins it doesn't name)
  local t b named
  GRANT_ALLOW=() GRANT_DENY=()
  while IFS= read -r t; do
    case "$t" in Bash|Bash\(*|Write|Write\(*|Edit|Edit\(*|WebFetch|WebFetch\(*|mcp__*) GRANT_ALLOW+=("$t") ;; esac
  done <<EOF
$1
EOF
  for b in $EVAL_BUILTINS; do
    named=0
    while IFS= read -r t; do
      case "$t" in "$b"|"$b("*) named=1 ;; Task|Task\(*) [ "$b" != Agent ] || named=1 ;; esac
    done <<EOF
$1
EOF
    [ "$named" = 1 ] || GRANT_DENY+=("$b")
  done
}
# Mock servers (#2042). --disallow-tools refuses MCP names, so a case's grant can't keep a mock server's tools out of its
# list. Record mode (the default) starts no server that has no evals/mocks/<server>/, so each call moves the mock dirs of
# the servers its case names no tool of to evals/.mocks-off/ (still under evals/, which the agent can't read) and puts them
# back after the call; cleanup puts them back too, so a killed run leaves a kept stage whole.
mocks_stage=
mocks_off() { # stage, tools (one per line) -> mocks_moved: the servers moved aside for this call
  local m s t named
  mocks_stage="$1" mocks_moved=
  for m in "$1"/evals/mocks/*/; do
    [ -d "$m" ] || continue
    s="$(basename "$m")" named=0
    while IFS= read -r t; do
      case "$t" in "mcp__plugin_aw-evals_$s"|"mcp__plugin_aw-evals_$s"__*) named=1 ;; esac
    done <<EOF
$2
EOF
    [ "$named" = 0 ] || continue
    mkdir -p "$1/evals/.mocks-off" && mv "$1/evals/mocks/$s" "$1/evals/.mocks-off/$s" || die 3 "cannot move the $s mocks aside"
    mocks_moved="${mocks_moved:+$mocks_moved }$s"
  done
}
mocks_on() { # the servers mocks_off moved aside go back to evals/mocks/
  local m
  [ -n "$mocks_stage" ] && [ -d "$mocks_stage/evals/.mocks-off" ] || { mocks_stage=; return 0; }
  for m in "$mocks_stage"/evals/.mocks-off/*/; do
    [ ! -d "$m" ] || mv "${m%/}" "$mocks_stage/evals/mocks/$(basename "$m")"
  done
  rmdir "$mocks_stage/evals/.mocks-off" 2>/dev/null
  mocks_stage=
}

do_run() {
  local skills s k c kinds gl branch cases total_cases=0 dry=0 rc_final=0 below=0 partial=0 gate_missed=0 envfail=0
  local kcap kspent left cmax out_dir results cmd_rc ab pass_env cost run_cost=0 clog tool rest budget_left skipped
  local famlines fams f fam_cost=0 fam_left="$FAMILY_MAX" fam_skipped=0 fam_refused=0 isfam bl fid n_fam
  [ "$ACTION" = dry ] && dry=1
  need jq; need perl; need rsync
  ROOT_REAL="$(real_of "$ROOT")"
  skills="$(resolve_skills)" || exit $?
  famlines="$(pick_families "$skills")" || exit $?
  fams="$(printf '%s\n' "$famlines" | cut -f1 | awk 'NF')"
  if [ -z "$skills" ] && [ -z "$fams" ]; then
    if [ "$SELECT" = changed ]; then echo "no skills changed"; exit 0; fi
    echo "no skills with evals to run"; exit 0
  fi
  kinds="$(kinds_for)"
  for s in $skills; do for k in $kinds; do total_cases=$((total_cases + $(case_dirs "$s" "$k" | grep -c .))); done; done
  [ -z "$skills" ] || note "skills: $(echo $skills | tr ' ' ',') ($total_cases case(s), kind $KIND)"
  n_fam=0
  for f in $fams; do n_fam=$((n_fam + $(family_count "$f"))); done
  total_cases=$((total_cases + n_fam))
  if [ -n "$fams" ]; then
    printf '%s\n' "$famlines" | while IFS="$(printf '\t')" read -r f why; do
      [ -n "$f" ] && note "family plan: $f ($(family_count "$f") case(s), ablation none, cap \$$FAMILY_MAX): $why"
    done
  fi
  if [ "$total_cases" -eq 0 ]; then echo "no cases to run"; exit 0; fi

  if [ "$dry" = 0 ]; then
    CLAUDE_BIN="$(command -v claude 2>/dev/null)" || die 3 "claude is not on PATH"
    git -C "$ROOT" rev-parse --git-dir >/dev/null 2>&1 || die 3 "$ROOT is not a git checkout"
    branch="$(git -C "$ROOT" symbolic-ref -q --short HEAD 2>/dev/null)" || die 3 "detached HEAD: --scaffold runs the cases' bash, so it runs only on a branch of this repo"
    case "$branch" in
      main|feat/*|fix/*|bugfix/*|docs/*|test/*|chore/*|refactor/*) ;;
      *) die 3 "branch $branch is not one of main, feat/*, fix/*, bugfix/*, docs/*, test/*, chore/*, refactor/*: --scaffold runs author-written bash, so it runs only on our own branches (not champion/pr-N)" ;;
    esac
    if [ -n "$(git -C "$ROOT" status --porcelain -- evals skills bootstrap 2>/dev/null)" ]; then
      die 3 "uncommitted changes under evals/ or skills/: commit them first (--scaffold runs author-written bash as you)"
    fi
    env -i PATH="$PATH" HOME="$HOME" "$CLAUDE_BIN" auth status >/dev/null 2>&1 || die 3 "claude is not logged in (run: claude auth login)"
  else
    CLAUDE_BIN="$(command -v claude 2>/dev/null || echo claude)"
    git -C "$ROOT" status --porcelain -- evals skills bootstrap 2>/dev/null | grep -q . && note "dry run: uncommitted changes under evals/ or skills/ (a real run refuses these)"
  fi

  mkdir -p "$EVAL_HOME/cache" || die 3 "cannot create $EVAL_HOME"
  # one per run: lanes don't share it, and cleanup empties it. Its name stays 4 characters: claude's sandbox binds
  # <TMPDIR>/claude-eval-XXXXXX/tmp/srt-mux-<pid>-<n>.sock (46 bytes more with a 7-digit pid, claude 2.1.312), and a Unix
  # socket path must fit in 108 bytes with its NUL, so a longer name fails every Bash call in the run (#1931)
  EVAL_TMP="$EVAL_HOME/tmp/XXXX"
  [ $(( ${#EVAL_TMP} + 46 )) -le 107 ] ||
    die 3 "the eval temp dir $EVAL_TMP is ${#EVAL_TMP} bytes, so claude's sandbox socket under it would pass the 108-byte Unix socket limit and every Bash call would fail; set AW_STATE_DIR to a path at least $(( ${#EVAL_TMP} + 46 - 107 )) bytes shorter"
  [ "$dry" = 1 ] || { mkdir -p "$EVAL_HOME/tmp" && EVAL_TMP="$(mktemp -d "$EVAL_HOME/tmp/XXXX")"; } || die 3 "cannot create $EVAL_HOME/tmp/XXXX"
  case "$EVAL_TMP" in
    /tmp/* | /var/tmp/* | /dev/shm/*) note "the eval temp dir $EVAL_TMP is under a system temp dir; claude's sandbox denies writes there, so Bash writes in eval runs will fail (set AW_STATE_DIR elsewhere)" ;;
  esac
  cleanup() {
    local kv
    mocks_on
    [ "$KEEP" = 0 ] || return 0
    for kv in $STAGE_LIST; do case "${kv#*=}" in "$EVAL_HOME"/cache/stage-*) find "${kv#*=}" -delete 2>/dev/null ;; esac; done
    # what the agent's tools left in TMPDIR; claude leaves its sealed dirs read-only, so open them first
    [ ! -d "$EVAL_TMP" ] || { chmod -R u+rwx "$EVAL_TMP" 2>/dev/null; find "$EVAL_TMP" -delete 2>/dev/null; }
  }
  trap cleanup EXIT
  trap 'exit 130' INT TERM
  if [ -n "$skills" ]; then
    for k in $kinds; do
      stage_build "$k" "$skills" || exit $?
      note "staged $k: $STAGE"
      sed 's/^/skill-evals:   rewrite /' "$STAGE/.stage-rewrites" >&2
    done

    CMD_DESC="skill-evals --kind $KIND $(echo $skills | tr ' ' ',')"
    if [ "$dry" = 1 ]; then
      tally_run peek; rc_final=$?
      case $rc_final in 0) ;; *) die 2 "the spend tally could not be checked (fail closed)" ;; esac
    else
      tally_run reserve; rc_final=$?
      case $rc_final in 0) ;; *) die 2 "the spend tally could not be written (fail closed)" ;; esac
    fi
  fi
  if [ -n "$fams" ]; then
    # the family set: its own ceiling, its own reserve row (id <run>-family) in the same tally
    fid="$RUN_ID-family"
    CMD_DESC="skill-evals family $(echo $fams | tr ' ' ',')"
    if [ "$dry" = 1 ]; then tally_run peek 0 "$fid" "$FAMILY_MAX"; rc_final=$?; else tally_run reserve 0 "$fid" "$FAMILY_MAX"; rc_final=$?; fi
    case $rc_final in
      0) for f in $fams; do
           stage_build "family-$f" "$(for c in $(family_case_dirs "$f"); do skill_dir "$(basename "$c")" >/dev/null && basename "$c"; done)" || exit $?
           note "staged family-$f: $STAGE"
         done ;;
      2) fam_refused=1; fam_skipped=1; echo "FAMILY SKIPPED: the family set ($FAMILY_MAX) was refused by the daily cap; the skill run goes on" ;;
      *) die 2 "the spend tally could not be written (fail closed)" ;;
    esac
  fi

  budget_left="$MAX_COST"
  for kv in $STAGE_LIST; do
    k="${kv%%=*}"; STAGE="${kv#*=}"; ab="$(ablation_for "$k")"
    isfam=0; bl="$budget_left"
    if is_family_kind "$k"; then isfam=1; ab=none; bl="$fam_left"; fi
    out_dir="$EVAL_HOME/$DAY/$RUN_ID-$k"
    results="$out_dir/results.json"
    local base args name co cres crc cbl ccost cost nores dirs tools
    base=(plugin eval . --trust-plugin --scaffold --no-publish --ablation "$ab")
    [ -z "$RUNS" ] || base+=(--runs "$RUNS")
    [ -z "$MODEL" ] || base+=(--model "$MODEL")
    [ -z "$JUDGE" ] || base+=(--judge-model "$JUDGE")
    [ -z "$JOBS" ] || base+=(-j "$JOBS")
    [ "$KEEP" = 0 ] || base+=(--keep-temp)
    pass_env=(PATH="$STAGE/stubs/bin:$PATH" TERM="${TERM:-dumb}" LANG="${LANG:-C.UTF-8}" HOME="$HOME"
              AW_STATE_DIR=.aw-state AW_PREAMBLE="$PREAMBLE" AW_GIT_MODE=readonly AW_RESULT=1)
    pass_env+=(TMPDIR="$EVAL_TMP")
    if [ "$dry" = 1 ]; then echo "dry run, not calling claude (one call per case, each with its own tools):"
    else mkdir -p "$out_dir/cases"; note "running $k cases (ablation $ab, ceiling \$$bl)"; fi
    # one call per case name (every skill's held-out is named _heldout: one call runs them all, with their lists' union)
    dirs="$(stage_case_dirs "$STAGE")"
    cmd_rc=0 cost=0 nores=0 cbl="$bl" callfail=0 ceil=0 cmax=0 files=()
    if [ "$isfam" = 1 ]; then kcap="$FAMILY_MAX" kspent="$fam_cost"; else kcap="$MAX_COST" kspent="$run_cost"; fi
    for name in $(printf '%s\n' "$dirs" | sed 's|.*/||' | sort -u); do
      # never start a case the money left can't pay for (the costliest finished call is the estimate)
      left="$(awk -v m="$kcap" -v s="$kspent" -v c="$cost" 'BEGIN { v = m - s - c; printf "%.2f", (v < 0 ? 0 : v) }')"
      if [ "$dry" = 0 ] && awk -v l="$left" -v x="$cmax" 'BEGIN { exit !(l <= 0 || l < x) }'; then
        echo "stopped before $name: \$$left left$(awk -v x="$cmax" 'BEGIN { if (x > 0) printf ", a case cost $%.2f", x }')"
        ceil=1; break
      fi
      tools="$(for c in $dirs; do [ "${c##*/}" != "$name" ] || case_tools "$c"; done | sort -u)"
      case_grant "$tools"
      co="$out_dir/cases/$name" cres="$out_dir/cases/$name/results.json"
      args=("${base[@]}" --case "$name" --output-dir "$co" --json "$cres" --report "$co/report.html" --max-cost-usd "$cbl")
      [ "${#GRANT_ALLOW[@]}" = 0 ] || args+=(--allow-tools "${GRANT_ALLOW[@]}")
      [ "${#GRANT_DENY[@]}" = 0 ] || args+=(--disallow-tools "${GRANT_DENY[@]}")
      mocks_off "$STAGE" "$tools"
      if [ "$dry" = 1 ]; then
        echo "  cd $STAGE && env -i $(shq "${pass_env[@]}") $(shq "$CLAUDE_BIN" "${args[@]}")${mocks_moved:+  # mocks off: $mocks_moved}"
        mocks_on
        continue
      fi
      (cd "$STAGE" && env -i "${pass_env[@]}" "$CLAUDE_BIN" "${args[@]}") >&2
      crc=$?
      mocks_on
      # whatever the tool saved goes through the scrub before anything else reads it
      scrub_paths "$co"
      if [ ! -s "$cres" ] || ! jq -e '.cases' "$cres" >/dev/null 2>&1; then
        note "claude plugin eval exited $crc and left no readable $cres"
        cost="$(awk -v a="$cost" -v b="$cbl" 'BEGIN { printf "%.6f", a + b }')"   # unknown spend settles at what was left
        callfail=1; break   # no more calls; the cases that did finish still count
      fi
      ccost="$(jq -r '(.costUsd // 0) as $c | if $c > 0 then $c else ([.cases[] | (.arms.with // [])[], (.arms.without // [])[] | (.costUsd // 0) + (.judgeCostUsd // 0)] | add // 0) end' "$cres")"
      cost="$(awk -v a="$cost" -v b="$ccost" 'BEGIN { printf "%.6f", a + b }')"
      cmax="$(awk -v a="$cmax" -v b="$ccost" 'BEGIN { printf "%.6f", (b > a ? b : a) }')"
      cbl="$(awk -v m="$bl" -v c="$cost" 'BEGIN { v = m - c; printf "%.2f", (v < 0.01 ? 0.01 : v) }')"
      # a readable file with no case is a failed call (refused or failed to load); later calls still run
      jq -e '.cases | length > 0' "$cres" >/dev/null 2>&1 || { note "claude plugin eval ran no case for $name (refused or failed to load)"; envfail=1; }
      case $crc in 0|1) ;; *) case $cmd_rc in 0|1) cmd_rc=$crc ;; esac ;; esac
      [ "$crc" != 2 ] || break   # the ceiling: the cases after this one stay unrun ("skipped (no result)")
    done
    [ "$dry" = 0 ] || continue
    for f in "$out_dir"/cases/*/results.json; do
      [ ! -s "$f" ] || ! jq -e '.cases | length > 0' "$f" >/dev/null 2>&1 || files+=("$f")
    done
    if [ "${#files[@]}" -gt 0 ]; then
      jq -s --argjson cost "$cost" '{costUsd: $cost, partial: (map(.partial // false) | any), cases: (map(.cases // []) | add)}' \
        "${files[@]}" >"$results" || nores=1
    else
      jq -n --argjson cost "$cost" '{costUsd: $cost, partial: false, cases: []}' >"$results" || nores=1
      note "no plugin eval call ran a case (refused or failed to load)"; envfail=1
    fi
    [ "$ceil" = 0 ] || partial=1
    [ "$callfail" = 0 ] || envfail=1
    if [ "$nores" = 1 ]; then
      if [ "$isfam" = 1 ]; then fam_cost="$(awk -v a="$fam_cost" -v b="$cost" 'BEGIN { printf "%.6f", a + b }')"
      else run_cost="$(awk -v a="$run_cost" -v b="$cost" 'BEGIN { printf "%.6f", a + b }')"; fi
      envfail=1; continue
    fi
    if [ "$isfam" = 1 ]; then
      fam_cost="$(awk -v a="$fam_cost" -v b="$cost" 'BEGIN { printf "%.6f", a + b }')"
      fam_left="$(awk -v m="$fam_left" -v c="$cost" 'BEGIN { v = m - c; printf "%.2f", (v < 0.01 ? 0.01 : v) }')"
    else
      run_cost="$(awk -v a="$run_cost" -v b="$cost" 'BEGIN { printf "%.6f", a + b }')"
      budget_left="$(awk -v m="$budget_left" -v c="$cost" 'BEGIN { v = m - c; printf "%.2f", (v < 0.01 ? 0.01 : v) }')"
    fi
    case $cmd_rc in 0|1) ;; 2) partial=1 ;; *) note "claude plugin eval exited $cmd_rc"; envfail=1 ;; esac
    [ "$(jq -r '.partial // false' "$results")" != true ] || partial=1
    echo
    if [ "$isfam" = 1 ]; then echo "### family ${k#family-} ($(basename "$out_dir"), ablation none)"; else echo "### $k ($(basename "$out_dir"))"; fi
    print_table "$results"
    skipped="$(jq -r '[.cases[].name] | join(" ")' "$results")"
    if [ "$isfam" = 1 ]; then
      for c in $(family_case_dirs "${k#family-}"); do
        case " $skipped " in *" $(basename "$c") "*) ;; *) echo "skipped (no result): family ${k#family-} / $(basename "$c")" ;; esac
      done
    else
      for s in $skills; do
        for c in $(case_dirs "$s" "$k"); do
          case " $skipped " in *" $(basename "$c") "*) ;; *) echo "skipped (no result): $s / $(basename "$c")" ;; esac
        done
      done
    fi
    print_failures "$results" "$THRESHOLD" | while IFS= read -r l; do echo "$l"; done
    if jq -e --argjson th "$THRESHOLD" '[.cases[] | (.aggregates.score // 0) < $th] | any' "$results" >/dev/null; then below=1; fi
    if [ "$k" = routing ]; then
      gl="$(gate_lines "$results")" || gate_missed=1
      if [ -n "$gl" ]; then
        gate_missed=1; echo "$gl"
      fi
    fi
    if [ "$KEEP" = 1 ]; then
      # one file per run, named for what results.json says it is: traces/<case>-<arm>-<run>.jsonl (never one shared name)
      jq -r '.cases[] | .name as $c | ["with", "without"][] as $a | (.arms[$a] // []) | to_entries[]
          | select((.value.tracePath // "") != "") | [$c, $a, (.key + 1 | tostring), .value.tracePath] | @tsv' "$results" 2>/dev/null |
        while IFS="$(printf '\t')" read -r tc ta tn tp; do
          [ -e "$tp" ] || continue
          mkdir -p "$out_dir/traces"
          if [ -d "$tp" ]; then cp -R "$tp" "$out_dir/traces/$tc-$ta-$tn" 2>/dev/null; else cp "$tp" "$out_dir/traces/$tc-$ta-$tn.jsonl" 2>/dev/null; fi
        done
      scrub_paths "$out_dir/traces"
      [ ! -d "$out_dir/traces" ] || echo "transcripts: $out_dir/traces"
    fi
  done

  if [ "$dry" = 1 ]; then
    echo "dry run ok"
    exit 0
  fi
  if [ -n "$skills" ]; then
    CMD_DESC="skill-evals --kind $KIND $(echo $skills | tr ' ' ',')"
    tally_run settle "$run_cost" || note "could not settle the spend tally (the reserve stays until its process is gone)"
  fi
  if [ -n "$fams" ] && [ "$fam_refused" = 0 ]; then
    CMD_DESC="skill-evals family $(echo $fams | tr ' ' ',')"
    tally_run settle "$fam_cost" "$fid" || note "could not settle the family spend (the reserve stays until its process is gone)"
  fi
  if [ "$partial" = 0 ]; then
    awk -v c="$run_cost" -v m="$MAX_COST" 'BEGIN { if (c > m) printf "over the ceiling by $%.2f ($%.2f of $%.2f): every case ran; the CLI checks its own cap only between its runs\n", c - m, c, m }'
    awk -v c="$fam_cost" -v m="$FAMILY_MAX" 'BEGIN { if (c > m) printf "family over the ceiling by $%.2f ($%.2f of $%.2f): every case ran; the CLI checks its own cap only between its runs\n", c - m, c, m }'
  else echo "PARTIAL: the cost ceiling left cases or runs unrun; the table above is not a full measurement"
  fi
  [ "$envfail" = 0 ] || exit 3
  if [ "$gate_missed" = 1 ] && [ "$GATE" = block ]; then exit 4; fi
  [ "$below" = 0 ] || exit 1
  [ "$fam_skipped" = 0 ] && [ "$partial" = 0 ] || exit 2
  exit 0
}

case "$ACTION" in
  selftest) do_selftest; exit $? ;;
  lint) do_lint; exit $? ;;
  status) do_status; exit $? ;;
  run|dry) do_run ;;
esac
