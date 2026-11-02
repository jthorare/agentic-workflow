#!/usr/bin/env bash
# Tests for scripts/skill-evals.sh (#1107): the flags and exit codes 0-4, the staged rewrite (source untouched), the env allowlist
# and token scrub, the spend tally under two concurrent runs, budget refusal, --changed, a staged path outside the repo, --selftest,
# --lint-descriptions and --status, bootstrap/ as a skill dir, family sets (evals/_families) and their separate spend cap, and the
# fixture lint's comment lines. A stub `claude` stands in for the real one: no model, no network, no login.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REAL="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/skill-evals-test.XXXXXX")"
# why: the state dirs sit in /tmp, not under TMPDIR: the script refuses an eval temp dir too long for claude's sandbox socket (#1931)
SHORT="$(mktemp -d /tmp/se.XXXXXX)"
LIVE_PID=""
cleanup() { [ -z "$LIVE_PID" ] || kill "$LIVE_PID" 2>/dev/null || true; find "$TMPROOT" "$SHORT" -delete 2>/dev/null || true; }
trap cleanup EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
show() { printf '%s\n' "$OUT" | sed 's/^/      | /' | head -30; }

N=0
BIN="" REPO="" STATE=""

# mkrepo: a small git repo with the script, the real stubs/plugin/_lib/mocks, two skills and their cases; a fake origin/main
mkrepo() {
  N=$((N + 1))
  local base="$TMPROOT/t$N"
  REPO="$base/repo" STATE="$SHORT/$N" BIN="$base/bin"
  mkdir -p "$REPO/scripts" "$REPO/evals" "$REPO/skills/alpha" "$REPO/skills/beta" "$REPO/skills/_shared" "$REPO/config/voice" "$STATE" "$BIN"
  cp "$REAL/scripts/skill-evals.sh" "$REPO/scripts/"
  cp -a "$REAL/evals/plugin" "$REAL/evals/stubs" "$REAL/evals/_lib" "$REAL/evals/mocks" "$REPO/evals/"
  cp "$REAL/skills/_shared/repo-slug.sh" "$REPO/skills/_shared/"
  printf -- '---\nname: alpha\ndescription: Use when the cart total looks wrong after a bulk order. Not for new features.\n---\nState: ${AW_STATE_DIR:-$HOME/.agentic-workflow}\nMode: ${AW_GIT_MODE:-full}\n' >"$REPO/skills/alpha/SKILL.md"
  printf -- '---\nname: beta\ndescription: >-\n  Use BEFORE shipping a release to check every\n  deploy gate.\n---\nbeta body\n' >"$REPO/skills/beta/SKILL.md"
  printf 'Preamble ${AW_PREAMBLE:-on} and ${AW_RESULT:-0} and ${AW_GIT_MODE:-full}; ${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}\n' >"$REPO/skills/_preamble.md"
  echo "voice" >"$REPO/config/voice/joi.md"

  local r="$REPO/evals/alpha/routing" b="$REPO/evals/alpha/behaviour"
  mkdir -p "$r/alpha-natural-1/graders/_selftest" "$r/alpha-near-miss/graders/_selftest" "$b/alpha-forced/graders/_selftest"
  printf -- '---\nmax_turns: 5\ntimeout_seconds: 60\nallowed_tools: [Skill]\n---\nThe order total looks off after we ordered a dozen widgets.\n' >"$r/alpha-natural-1/prompt.md"
  printf -- '---\ntype: tool_used\ntool: Skill\ninput_match: alpha\narm: with-only\n---\n' >"$r/alpha-natural-1/graders/skill-fired.md"
  printf 'Skill\t{"skill":"alpha"}\n' >"$r/alpha-natural-1/graders/_selftest/skill-fired.pass.tools"
  printf 'Bash\tls\n' >"$r/alpha-natural-1/graders/_selftest/skill-fired.fail.tools"
  printf -- '---\nmax_turns: 5\ntimeout_seconds: 60\nallowed_tools: [Skill]\n---\nWrite a haiku about autumn.\n' >"$r/alpha-near-miss/prompt.md"
  printf -- '---\ntype: tool_used\ntool: Skill\ninput_match: alpha\nmin: 0\nmax: 0\narm: both\n---\n' >"$r/alpha-near-miss/graders/skill-not-fired.md"
  printf 'Bash\tls\n' >"$r/alpha-near-miss/graders/_selftest/skill-not-fired.pass.tools"
  printf 'Skill\t{"skill":"alpha"}\n' >"$r/alpha-near-miss/graders/_selftest/skill-not-fired.fail.tools"
  printf 'schema_version: "1.1"\nname: alpha-forced\ncontext:\n  scaffold_script: scaffold.sh\n' >"$b/alpha-forced/case.yaml"
  printf '#!/usr/bin/env bash\necho own-scaffold-ran > "$HOME/own-marker"\n' >"$b/alpha-forced/scaffold.sh"
  printf -- '---\nmax_turns: 5\ntimeout_seconds: 60\nallowed_tools: [Skill, Write]\n---\nUse the alpha skill on the cart.\n' >"$b/alpha-forced/prompt.md"
  printf -- '---\ntype: regex\n---\nALPHA-DONE\n' >"$b/alpha-forced/graders/has-done.md"
  printf 'all ALPHA-DONE\n' >"$b/alpha-forced/graders/_selftest/has-done.pass.txt"
  printf 'nothing here\n' >"$b/alpha-forced/graders/_selftest/has-done.fail.txt"
  printf '#!/usr/bin/env bash\nprintf "cart\\n" > cart.txt\n. "$(dirname "$0")/../_lib/fixture-lib.sh"\naw_fixture_repo init\n' >"$REPO/evals/alpha/fixture.sh"

  git -C "$REPO" init -q -b main
  git -C "$REPO" config user.email t@example.invalid
  git -C "$REPO" config user.name t
  git -C "$REPO" config commit.gpgsign false
  git -C "$REPO" add -A
  git -C "$REPO" commit -q -m base
  git -C "$REPO" update-ref refs/remotes/origin/main HEAD
  git -C "$REPO" checkout -q -b feat/test
}

# stub claude: control files next to it (logged-out, exit, sleep, result.json); logs args and env per call
mkclaude() {
  cat >"$BIN/claude" <<'EOF'
#!/usr/bin/env bash
d="$(cd "$(dirname "$0")" && pwd)"
if [ "${1:-}" = auth ]; then [ ! -f "$d/logged-out" ]; exit $?; fi
[ "${1:-} ${2:-}" = "plugin eval" ] || exit 9
echo "$*" >"$d/args.$$.txt"
env >"$d/env.$$.txt"
echo "$PWD" >"$d/cwd.$$.txt"
if [ -n "${TMPDIR:-}" ] && [ -d "$TMPDIR" ] && [ -w "$TMPDIR" ]; then echo writable >"$d/tmpdir-ok.$$.txt"; fi
if [ -n "${TMPDIR:-}" ] && [ -w "$TMPDIR" ]; then mkdir -p "$TMPDIR/sealed" && echo left >"$TMPDIR/sealed/leftover" && chmod 500 "$TMPDIR/sealed"; fi
out=""; prev=""
for a in "$@"; do [ "$prev" = "--json" ] && out="$a"; prev="$a"; done
[ ! -f "$d/sleep" ] || sleep "$(cat "$d/sleep")"
[ -f "$d/no-result" ] || { [ -z "$out" ] || { mkdir -p "$(dirname "$out")"; cp "$d/result.json" "$out"; }; }
exit "$(cat "$d/exit" 2>/dev/null || echo 0)"
EOF
  chmod +x "$BIN/claude"
}
# The result files below are cut from the pilot's real claude results (jthorare/feat/skill-evals-pilot): pilot-graded.json is
# evals-pilot/pilot-results.json (case rc-explicit-invoke, its real graders incl. skill-fired), pilot-errored.json is
# evals-pilot/evals/results/2026-10-25T14-12-24-101Z/aggregate-result.json (case smoke-skill-visible, a run that errored: graders []).
FIX="$REAL/scripts/tests/fixtures/skill-evals"
# mkresult SCORE FIRED(true|false) COST [partial]: the real graded case, renamed to alpha's natural routing case, 2 runs
mkresult() {
  jq --argjson sc "$1" --argjson fired "$2" --argjson cost "$3" --argjson part "${4:-false}" '
    .costUsd = $cost | .partial = $part
    | .cases = [.cases[0] | .name = "alpha-natural-1" | .dir = "evals/alpha/routing/alpha-natural-1" | .aggregates.score = $sc
      | .arms = {with: [.arms.with[0], .arms.with[0]] | map(.costUsd = 0.2 | .judgeCostUsd = 0
          | .graders = [.graders[] | select(.name == "skill-fired") | .passed = $fired])}]' "$FIX/pilot-graded.json" >"$BIN/result.json"
}
# mkresult_errored: the real errored run, renamed the same way
mkresult_errored() {
  jq '.cases[0] |= (.name = "alpha-natural-1" | .dir = "evals/alpha/routing/alpha-natural-1")' "$FIX/pilot-errored.json" >"$BIN/result.json"
}

run() { # args... : run the script in $REPO with a clean env and the stub claude first on PATH
  set +e
  OUT="$(cd "$REPO" && env -i PATH="$BIN:$PATH" HOME="$TMPROOT/home" TERM=dumb LANG=C.UTF-8 AW_STATE_DIR="$STATE" ${EXTRA_ENV:-} bash scripts/skill-evals.sh "$@" 2>&1)"
  RC=$?
  set -e
}
expect() { # label rc [regex]
  if [ "$RC" -ne "$2" ]; then fail "$1: exit $RC, want $2"; show; return; fi
  if [ -n "${3:-}" ] && ! printf '%s\n' "$OUT" | has -E -- "$3"; then fail "$1: output lacks /$3/"; show; return; fi
  ok "$1"
}
lacks() { # label regex : the output must not match
  if printf '%s\n' "$OUT" | has -E -- "$2"; then fail "$1: output has /$2/"; show; else ok "$1"; fi
}
mkdir -p "$TMPROOT/home"
# token-shaped values are built at run time so no secret scanner trips on this file
FAKE_GH="gh""p_$(printf 'a%.0s' $(seq 1 30))"
FAKE_BEARER="$(printf 'b%.0s' $(seq 1 30))"

echo "usage and refusals"
mkrepo; mkclaude
run
expect "no skill selector is a usage error" 64 'choose the skills'
run --bogus
expect "an unknown flag is a usage error" 64 'unknown argument'
run --skills nope --dry-run
expect "an unknown skill is a usage error" 64 'unknown skill: nope'
run --skills alpha --kind sideways --dry-run
expect "a bad --kind is a usage error" 64 '--kind must be'
run --skills alpha --preamble sometimes --dry-run
expect "a bad --preamble is a usage error" 64 '--preamble must be'

echo "--preamble on (spec §8 5a: the forced cases once with the preamble on)"
mkrepo
run --skills alpha --kind behaviour --preamble on --dry-run --keep-temp
expect "--preamble on dry run passes" 0 'AW_PREAMBLE=on'
lacks "--preamble on passes no AW_PREAMBLE=off" 'AW_PREAMBLE=off'
STAGE_P="$(printf '%s\n' "$OUT" | sed -n 's/^skill-evals: staged behaviour: //p')"
has -F -- '${AW_PREAMBLE:-on}' "$STAGE_P/skills/_preamble.md" && has -F -- '${AW_GIT_MODE:-readonly}' "$STAGE_P/skills/_preamble.md" \
  && ok "--preamble on keeps the preamble default and still rewrites the git mode" || { fail "--preamble on stage"; cat "$STAGE_P/skills/_preamble.md"; }

echo "--changed"
mkrepo; mkclaude
run --changed --dry-run
expect "no skills changed exits 0" 0 '^no skills changed'
printf 'edit\n' >>"$REPO/skills/alpha/SKILL.md"
run --changed --dry-run
expect "a changed skill is picked" 0 'skills: alpha'
git -C "$REPO" checkout -q -- skills/alpha/SKILL.md
printf 'edit\n' >>"$REPO/skills/_shared/repo-slug.sh"
run --changed --dry-run
expect "a _shared change picks every skill with evals" 0 'skills: alpha '

echo "--dry-run and the staged rewrite"
mkrepo
before="$(cksum <"$REPO/skills/_preamble.md") $(cksum <"$REPO/skills/alpha/SKILL.md") $(cksum <"$REPO/skills/_shared/repo-slug.sh")"
run --skills alpha --dry-run --keep-temp
expect "dry run passes with no claude on PATH" 0 'dry run ok'
has -F -- 'not calling claude' <<<"$OUT" && ok "dry run prints the command it would run" || { fail "dry run prints the command"; show; }
has -E 'env -i .*TMPDIR=' <<<"$OUT" && ok "eval child: the dry-run line shows TMPDIR=" || { fail "eval child: the dry-run line shows TMPDIR="; show; }
STAGE_R="$(printf '%s\n' "$OUT" | sed -n 's/^skill-evals: staged routing: //p')"
STAGE_B="$(printf '%s\n' "$OUT" | sed -n 's/^skill-evals: staged behaviour: //p')"
[ -d "$STAGE_R" ] && [ -d "$STAGE_B" ] && ok "both kinds staged" || fail "stage dirs: '$STAGE_R' '$STAGE_B'"
case "$STAGE_R" in "$STATE"/skill-evals/cache/*) ok "the stage is under EVAL_HOME/cache" ;; *) fail "stage path $STAGE_R" ;; esac
(git -C "$STAGE_R" rev-parse --git-dir >/dev/null 2>&1) && fail "the stage is inside a git repo" || ok "the stage is outside any git repo"
if has -F -- '${AW_PREAMBLE:-off}' "$STAGE_R/skills/_preamble.md" && has -F -- '${AW_RESULT:-1}' "$STAGE_R/skills/_preamble.md" &&
   has -F -- '${AW_GIT_MODE:-readonly}' "$STAGE_R/skills/_preamble.md" && has -F -- '${AW_GIT_MODE:-readonly}' "$STAGE_R/skills/alpha/SKILL.md" &&
   grep -q "aw-eval" "$STAGE_R/skills/_shared/repo-slug.sh"; then ok "the four defaults are rewritten in the stage"; else fail "stage rewrite"; cat "$STAGE_R/skills/_preamble.md"; fi
has -F -- '${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}' "$STAGE_R/skills/_preamble.md" && ok "AW_TOOLKIT's default stays" || fail "AW_TOOLKIT default was touched"
after="$(cksum <"$REPO/skills/_preamble.md") $(cksum <"$REPO/skills/alpha/SKILL.md") $(cksum <"$REPO/skills/_shared/repo-slug.sh")"
[ "$before" = "$after" ] && has -F -- '${AW_PREAMBLE:-on}' "$REPO/skills/_preamble.md" && ok "the source tree is untouched" || fail "the source tree changed"
[ -z "$(find "$STAGE_B" -name _selftest)" ] && ok "_selftest replies are not staged" || fail "_selftest was staged"
[ -f "$STAGE_B/skills/beta/SKILL.md" ] && [ ! -e "$STAGE_B/evals/beta" ] && ok "every skill is staged, evals only for the chosen" || fail "stage contents"
has -F -- 'scaffold_script: _aw-scaffold.sh' "$STAGE_B/evals/alpha/behaviour/alpha-forced/case.yaml" && ok "the case scaffold is pointed at the toolkit scaffold" || fail "case.yaml scaffold"
has -F -- 'scaffold_script: _aw-scaffold.sh' "$STAGE_R/evals/alpha/routing/alpha-natural-1/case.yaml" && ok "a prompt-only case gets a case.yaml with the scaffold" || fail "routing case.yaml"
H="$TMPROOT/h$N"; mkdir -p "$H"
(cd "$TMPROOT" && env -i PATH="$PATH" HOME="$H" bash "$STAGE_B/evals/alpha/behaviour/alpha-forced/_aw-scaffold.sh")
[ -f "$H/.agentic-workflow/toolkit/skills/_preamble.md" ] && [ -f "$H/.agentic-workflow/toolkit/skills/_shared/repo-slug.sh" ] && [ -f "$H/.agentic-workflow/toolkit/config/voice/joi.md" ] &&
  [ "$(cat "$H/own-marker")" = own-scaffold-ran ] && ok "the scaffold puts the toolkit at the default path, then chains the case's own" || fail "scaffold run"
# claude plugin eval denies the agent every read under the stage's evals/ (so it can't see the graders), and a stub there is
# invisible to its shell: the stubs are staged at the stage root instead.
has -E -- "PATH=[^ ]*/stage-[^/ ]*/stubs/bin:" <<<"$OUT" && ! has -F -- 'evals/stubs/bin' <<<"$OUT" && ok "the stubs are first on PATH, outside the stage's evals/" || { fail "stubs not first on PATH, or under evals/"; show; }
[ -x "$STAGE_R/stubs/bin/gh" ] && [ ! -e "$STAGE_R/evals/stubs" ] && ok "the stub gh is executable in the stage" || fail "stub gh mode"
[ -d "$H/.aw-eval" ] || [ -d "$TMPROOT/.aw-eval" ] && ok "the scaffold creates .aw-eval/ in the workspace" || fail "no .aw-eval/"
[ -f "$H/.bashrc" ] && [ ! -s "$H/.bashrc" ] && ok "eval child: the scaffold leaves an empty .bashrc in the run's HOME" || fail "eval child: no empty .bashrc in the run's HOME"
H2="$TMPROOT/h2$N"; mkdir -p "$H2"; echo 'own rc' >"$H2/.bashrc"
(cd "$TMPROOT" && env -i PATH="$PATH" HOME="$H2" bash "$STAGE_B/evals/alpha/behaviour/alpha-forced/_aw-scaffold.sh")
[ "$(cat "$H2/.bashrc")" = 'own rc' ] && ok "eval child: the scaffold keeps an existing .bashrc" || fail "eval child: the scaffold replaced an existing .bashrc"
grep -q 'AW_GIT_MODE:-readonly' "$STAGE_R/stubs/bin/gh" && ok "the staged gh stub defaults to readonly" || fail "staged gh default"
# repo-slug.sh sourced from the workspace root and from a subdirectory gives one state root (the slug below it is repo-slug.sh's own business)
WS="$TMPROOT/ws$N"; mkdir -p "$WS/.aw-eval" "$WS/src/deep"
slug() { (cd "$1" && env -i PATH="$PATH" HOME="$H" bash -c '. "$0"; printf %s "$AW_DIR"' "$STAGE_R/skills/_shared/repo-slug.sh"); }
r1="$(dirname "$(slug "$WS")")"; r2="$(dirname "$(slug "$WS/src/deep")")"
[ -n "$r1" ] && [ "$r1" = "$r2" ] && [ "$r1" = "$WS/.aw-state" ] && ok "the staged state root is the same from a subdirectory ($r1)" || fail "AW_DIR differs: '$r1' vs '$r2'"
[ ! -s "$STATE/skill-evals/spend.jsonl" ] && ok "a dry run writes no tally row" || fail "dry run wrote the tally"

echo "a staged path outside the repo"
mkrepo
ln -s /etc "$REPO/skills/alpha/evil"
run --skills alpha --dry-run
expect "a link that resolves outside the repo is refused" 3 'resolves outside the repo'
mkrepo
mkdir -p "$REPO/skills/alpha/hooks"; : >"$REPO/skills/alpha/hooks/h.sh"
run --skills alpha --dry-run
expect "a hooks/ directory in the stage is refused" 3 'hooks/ directory'
mkrepo
echo '{}' >"$REPO/skills/alpha/.mcp.json"
run --skills alpha --dry-run
expect "an .mcp.json other than the mocks' is refused" 3 '\.mcp\.json other than'

echo "--selftest"
mkrepo
run --skills alpha --selftest
expect "a good case set passes" 0 'selftest: 3 case\(s\), 0 problem'
printf -- '---\ntype: regex\n---\n.*\n' >"$REPO/evals/alpha/behaviour/alpha-forced/graders/has-done.md"
run --skills alpha --selftest
expect "a grader that cannot fail is caught" 1 'cannot fail'
git -C "$REPO" checkout -q -- evals
rm "$REPO/evals/alpha/behaviour/alpha-forced/graders/_selftest/has-done.fail.txt"
run --skills alpha --selftest
expect "a grader without a fail reply is caught" 1 'no graders/_selftest/has-done.fail'
git -C "$REPO" checkout -q -- evals
printf 'rm -rf /tmp/some-dir\n' >>"$REPO/evals/alpha/fixture.sh"
run --skills alpha --selftest
expect "a fixture that deletes outside \$PWD is caught" 1 'SELFTEST-FIXTURE'
git -C "$REPO" checkout -q -- evals
printf 'exit 3\n' >>"$REPO/evals/alpha/fixture.sh"
run --skills alpha --selftest
expect "a fixture that fails is caught" 1 'SELFTEST-FIXTURE.*exit 3'
git -C "$REPO" checkout -q -- evals
rm -r "$REPO/evals/alpha/behaviour/alpha-forced/graders"
mkdir "$REPO/evals/alpha/behaviour/alpha-forced/graders"
run --skills alpha --selftest
expect "a case with no graders is caught" 1 'no graders'

echo "--lint-descriptions"
mkrepo
run --lint-descriptions
expect "short, distinct descriptions pass" 0 'lint-descriptions: ok'
long="$(printf 'word%.0s ' $(seq 1 120))"
printf -- '---\nname: alpha\ndescription: %s\n---\n' "$long" >"$REPO/skills/alpha/SKILL.md"
run --lint-descriptions
expect "a description over 400 characters fails" 1 'over|max 400'
git -C "$REPO" checkout -q -- skills
printf -- '---\nname: alpha\ndescription: Use when the order total looks off after we ordered a dozen widgets today.\n---\n' >"$REPO/skills/alpha/SKILL.md"
run --lint-descriptions
expect "a 5-word phrase from a case prompt fails" 1 'shares the phrase'
git -C "$REPO" checkout -q -- skills
echo 100 >"$REPO/evals/_lib/description-bytes.baseline"
run --lint-descriptions
expect "total bytes over the baseline + 25% fails" 1 'over the baseline'

echo "--selftest: every case sets timeout_seconds and allowed_tools"
mkrepo
A="$REPO/evals/alpha/behaviour/alpha-forced"
printf -- '---\nmax_turns: 5\nallowed_tools: [Skill, Write]\n---\nUse the alpha skill on the cart.\n' >"$A/prompt.md"
run --skills alpha --selftest
expect "a case.yaml with no execution: block does not hide a missing timeout_seconds" 0 'warning: evals/alpha/behaviour/alpha-forced has no timeout_seconds'
printf '%s\n' 'execution:' '  timeout_seconds: 60' >>"$A/case.yaml"
run --skills alpha --selftest
lacks "timeout_seconds under case.yaml's execution: counts" 'alpha-forced has no timeout_seconds'

echo "--selftest and description lint"
mkrepo
long="$(printf 'word%.0s ' $(seq 1 120))"
printf -- '---\nname: alpha\ndescription: %s\n---\nbody\n' "$long" >"$REPO/skills/alpha/SKILL.md"
run --skills alpha --selftest
expect "--selftest: an over-400 description is a warning and exit stays 0" 0 'LINT-DESC: skills/alpha: description is .* \(warning\)'
run --lint-descriptions
expect "--lint-descriptions alone still fails on the long one" 1 'skills/alpha: description is'
mkrepo
printf -- '---\nname: beta\ndescription: %s\n---\nbody\n' "$long" >"$REPO/skills/beta/SKILL.md"
run --skills alpha --selftest
expect "--selftest --skills alpha does not lint beta" 0 'selftest: 3 case'
lacks "no beta lint line" 'skills/beta'

echo "--status"
mkrepo
printf 'fire_rate: 3/3\ndelta: 0.18\nlast_run: %s\npr: 1110\n' "$(date +%F)" >"$REPO/evals/alpha/status.md"
run --status
expect "a fresh row has no stale mark" 0 '\| alpha \| 3/3 \| 0.18 \|'
lacks "a fresh row is not stale" '\| alpha .*stale'
printf 'fire_rate: 3/3\ndelta: 0.18\nlast_run: 2020-01-01\npr: 1110\n' >"$REPO/evals/alpha/status.md"
run --status
expect "an old row is stale" 0 '\| alpha .*stale'
expect_beta="$(printf '%s\n' "$OUT" | grep -c '| beta .*stale' || true)"
[ "$expect_beta" = 1 ] && ok "a skill never measured is stale" || fail "beta row"

echo "a real run (stub claude)"
mkrepo; mkclaude; mkresult 1 true 1.5
EXTRA_ENV="EVAL_SECRET=hunter22xyz GH_TOKEN=$FAKE_GH AW_FORGE_TOKEN=tok-forge-123456"
run --skills alpha --kind routing --runs 2
expect "a passing run exits 0" 0 '\| alpha-natural-1 \| 1 \|'
has -F -- '| Case | WITH | W-OUT | Δ | Skill fired | Cost |' <<<"$OUT" && ok "the WITH / W-OUT / Δ table is printed" || { fail "table header"; show; }
has -F -- '| 2/2 |' <<<"$OUT" && ok "the Skill-fired rate is read from the skill-fired grader" || { fail "fired rate"; show; }
ENVF="$(ls "$BIN"/env.*.txt | head -1)"
if grep -qE '^(EVAL_SECRET|GH_TOKEN|AW_FORGE_TOKEN)=' "$ENVF"; then fail "a secret reached the stub's env"; else ok "EVAL_SECRET, GH_TOKEN and AW_FORGE_TOKEN never reach claude"; fi
if grep -qE "hunter22xyz|$FAKE_GH|tok-forge" "$ENVF"; then fail "a secret value is in the env dump"; else ok "no secret value anywhere in claude's env"; fi
has -E '^AW_PREAMBLE=off$' "$ENVF" && has -E '^AW_GIT_MODE=readonly$' "$ENVF" && has -E '^AW_RESULT=1$' "$ENVF" && ok "AW_* values are in the allowlist" || fail "AW_* values"
has -E "^PATH=[^:]*/stage-[^/:]*/stubs/bin:" "$ENVF" && ok "PATH starts with the stub dir" || fail "PATH order"
[ "$(wc -l <"$ENVF")" -le 12 ] && ok "only the allowlist is forwarded ($(wc -l <"$ENVF") variables)" || { fail "env has too many variables"; cat "$ENVF"; }
has -E "^TMPDIR=$STATE/skill-evals/tmp/[A-Za-z0-9]{4}\$" "$ENVF" && ok "eval child: TMPDIR is a short per-run dir (4 characters) under the eval home's tmp" || { fail "eval child: TMPDIR is a short per-run dir (4 characters) under the eval home's tmp"; grep '^TMPDIR=' "$ENVF" || echo "      | (no TMPDIR in the child's env)"; }
RUN_TMP="$(sed -n 's/^TMPDIR=//p' "$ENVF")"
[ -n "$RUN_TMP" ] && [ ! -e "$RUN_TMP" ] && ok "eval child: a normal run removes its TMPDIR, read-only leftovers included" || fail "eval child: TMPDIR '$RUN_TMP' is still there after a normal run"
[ -n "$(ls "$BIN"/tmpdir-ok.*.txt 2>/dev/null)" ] && ok "eval child: TMPDIR exists and is writable when claude runs" || fail "eval child: TMPDIR exists and is writable when claude runs"
ARGS="$(cat "$BIN"/args.*.txt | head -1)"
for f in '--trust-plugin' '--scaffold' '--no-publish' '--ablation none' '--runs 2' '--max-cost-usd 8' '--allow-tools Bash Write Edit'; do
  case "$ARGS" in *"$f"*) ok "claude got $f" ;; *) fail "claude args lack $f: $ARGS" ;; esac
done
case "$(cat "$BIN"/cwd.*.txt | head -1)" in "$STATE"/skill-evals/cache/stage-*) ok "claude runs inside the stage" ;; *) fail "claude cwd" ;; esac
ROWS="$STATE/skill-evals/spend.jsonl"
[ "$(jq -s '[.[] | select(.t == "reserve")] | length' "$ROWS")" = 1 ] && [ "$(jq -s '[.[] | select(.t == "settle") | .usd] | add * 1' "$ROWS")" = 1.5 ] && ok "the tally has a reserve row and a settle row with the real cost" || { fail "tally rows"; cat "$ROWS"; }
has -E 'spend: this run \$1.50, today \$1.50 of \$60.00' <<<"$OUT" && ok "the spend line is printed" || { fail "spend line"; show; }
[ -z "$(find "$STATE/skill-evals/cache" -maxdepth 1 -name 'stage-*')" ] && ok "the stage is removed afterwards" || fail "stage left behind"
SAVED="$(find "$STATE/skill-evals" -name results.json | head -1)"
[ -s "$SAVED" ] && ok "results.json is saved under EVAL_HOME" || fail "no saved results.json"

echo "the token scrub"
mkrepo; mkclaude; mkresult 1 true 0.5
jq --arg t "leaked hunter22xyz and $FAKE_GH and Authorization: Bearer $FAKE_BEARER" '.cases[0].arms.with[0].graders[0].explanation = $t' "$BIN/result.json" >"$BIN/r2.json" && mv "$BIN/r2.json" "$BIN/result.json"
EXTRA_ENV="EVAL_SECRET=hunter22xyz"
run --skills alpha --kind routing
expect "a run whose output leaks a token still passes" 0
SAVED="$(find "$STATE/skill-evals" -name results.json | head -1)"
if grep -qE "hunter22xyz|$FAKE_GH|$FAKE_BEARER" "$SAVED"; then fail "a secret is in the saved results"; cat "$SAVED"; else ok "token patterns and the operator's EVAL_* value are scrubbed from the saved results"; fi
has -F -- '[REDACTED]' "$SAVED" && ok "the scrub leaves a marker" || fail "no marker"
EXTRA_ENV=""

echo "exit codes"
mkrepo; mkclaude; mkresult 0.5 true 1
run --skills alpha --kind routing
expect "below the threshold exits 1" 1 'FAIL|\| alpha-natural-1 \| 0.5'
mkresult 1 true 1 true; echo 2 >"$BIN/exit"
run --skills alpha --kind routing
expect "a run that hit its cost ceiling exits 2" 2 'PARTIAL'
rm -f "$BIN/exit"; mkresult 1 false 1
run --skills alpha --kind routing
expect "a missed routing gate under warn prints the line and exits 0" 0 'ROUTING GATE MISSED: alpha fired 0/2'
run --skills alpha --kind routing --gate block
expect "a missed routing gate under block exits 4" 4 'ROUTING GATE MISSED: alpha'
touch "$BIN/no-result"
run --skills alpha --kind routing
expect "no readable result exits 3" 3 'left no readable'
[ "$(jq -s '[.[] | select(.t == "settle")] | last | .usd * 1' "$STATE/skill-evals/spend.jsonl")" = 8 ] && ok "an unreadable result settles at the reserve (\$8), not \$0" || { fail "unreadable result settle"; cat "$STATE/skill-evals/spend.jsonl"; }
rm -f "$BIN/no-result"
touch "$BIN/logged-out"
run --skills alpha --kind routing
expect "not logged in exits 3" 3 'not logged in'
rm -f "$BIN/logged-out"
printf 'dirty\n' >>"$REPO/skills/alpha/SKILL.md"
run --skills alpha --kind routing
expect "uncommitted changes under skills/ exit 3" 3 'uncommitted changes'
git -C "$REPO" checkout -q -- skills
(cd "$REPO" && set +e; OUT="$(env -i PATH="/usr/bin:/bin" HOME="$TMPROOT/home" AW_STATE_DIR="$STATE" bash scripts/skill-evals.sh --skills alpha --kind routing 2>&1)"; echo "$?" >"$TMPROOT/rc"; echo "$OUT" >"$TMPROOT/out")
RC="$(cat "$TMPROOT/rc")"; OUT="$(cat "$TMPROOT/out")"
expect "claude missing exits 3" 3 'claude is not on PATH'

echo "the spend tally"
mkrepo; mkclaude; mkresult 1 true 1
run --skills alpha --kind routing --max-cost-usd 70
expect "a budget over the daily cap is refused with exit 2" 2 'refused'
[ ! -e "$BIN"/args.*.txt ] 2>/dev/null && ok "claude was not called when refused" || { ls "$BIN"/args.* >/dev/null 2>&1 && fail "claude was called" || ok "claude was not called when refused"; }
mkdir -p "$STATE/skill-evals"; echo 'not json' >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --kind routing
expect "an unreadable tally fails closed with exit 2" 2 'unreadable or malformed'
printf '{"t":"settle","id":"x","date":"%s","lane":"operator","usd":55}\n' "$(date +%F)" >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --kind routing
expect "the day's settled spend counts (55 + 8 passes 60)" 2 'refused'
NOW="$(date +%s)"
printf '{"t":"reserve","id":"dead","date":"%s","lane":"operator","pid":999999,"ts":%s,"usd":50}\n' "$(date +%F)" "$NOW" >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --kind routing
expect "a reserve whose process is gone counts as zero" 0
printf '{"t":"reserve","id":"old","date":"%s","lane":"operator","pid":%s,"ts":%s,"usd":50}\n' "$(date +%F)" "$$" "$((NOW - 4 * 3600))" >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --kind routing
expect "a reserve older than 3 hours counts as zero" 0
sleep 60 & LIVE_PID=$!
printf '{"t":"reserve","id":"live","date":"%s","lane":"operator","pid":%s,"ts":%s,"usd":55}\n' "$(date +%F)" "$LIVE_PID" "$NOW" >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --kind routing
expect "a live reserve counts in full" 2 'refused'
kill "$LIVE_PID" 2>/dev/null || true; LIVE_PID=""
mkrepo; mkclaude; mkresult 1 true 1
echo 2 >"$BIN/sleep"
EXTRA_ENV=""
( set +e; cd "$REPO"; env -i PATH="$BIN:$PATH" HOME="$TMPROOT/home" AW_STATE_DIR="$STATE" bash scripts/skill-evals.sh --skills alpha --kind routing --lane x --max-cost-usd 40 >"$TMPROOT/c1.out" 2>&1; echo $? >"$TMPROOT/c1.rc" ) &
P1=$!
( set +e; cd "$REPO"; env -i PATH="$BIN:$PATH" HOME="$TMPROOT/home" AW_STATE_DIR="$STATE" bash scripts/skill-evals.sh --skills alpha --kind routing --lane x --max-cost-usd 40 >"$TMPROOT/c2.out" 2>&1; echo $? >"$TMPROOT/c2.rc" ) &
P2=$!
wait "$P1" "$P2"
codes="$(sort "$TMPROOT/c1.rc" "$TMPROOT/c2.rc" | tr '\n' ' ')"
[ "$codes" = "0 2 " ] && ok "two concurrent runs of \$40 cannot both fit under \$60 (exit codes: $codes)" || { fail "concurrent runs exited: $codes"; grep -h "^spend:" "$TMPROOT/c1.out" "$TMPROOT/c2.out"; cat "$STATE/skill-evals/spend.jsonl"; }
[ "$(jq -s '[.[] | select(.t == "reserve")] | length' "$STATE/skill-evals/spend.jsonl")" = 1 ] && ok "only one reserve row was written" || fail "reserve rows"

echo "the routing gate has something to judge"
mkrepo; mkclaude; mkresult_errored
run --skills alpha --kind routing --gate block
expect "an all-errored run (real errored result) is a MISS, exit 4" 4 'ROUTING GATE MISSED: alpha/alpha-natural-1: no judged runs'
mkresult 1 true 1
jq '.cases[0].arms.with |= map(.graders |= map(select(.name != "skill-fired")))' "$BIN/result.json" >"$BIN/r2.json" && mv "$BIN/r2.json" "$BIN/result.json"
run --skills alpha --kind routing --gate block
expect "a case whose runs lack the fired grader is a MISS, exit 4" 4 'no judged runs'
mkresult 1 true 1
jq '.cases[0].arms.with = []' "$BIN/result.json" >"$BIN/r2.json" && mv "$BIN/r2.json" "$BIN/result.json"
run --skills alpha --kind routing --gate block
expect "a case with no runs at all is a MISS, exit 4" 4 'no judged runs'
mkresult 1 true 1
jq '.cases[0].dir = null' "$BIN/result.json" >"$BIN/r2.json" && mv "$BIN/r2.json" "$BIN/result.json"
run --skills alpha --kind routing --gate block
expect "a jq error in the gate is a MISS with a clear line, never a pass" 4 'gate could not be evaluated'
mkresult 1 true 1
run --skills alpha --kind routing --gate block
expect "a result with fired runs still passes the gate" 0
lacks "no gate line on a good result" 'ROUTING GATE MISSED'
mkrepo
G="$REPO/evals/alpha/routing/alpha-natural-1/graders"
mv "$G/skill-fired.md" "$TMPROOT/sf.md"
run --skills alpha --selftest
expect "--selftest: a routing case without a fired grader is caught" 1 'needs exactly one of graders/skill-fired.md'
mv "$TMPROOT/sf.md" "$G/skill-fired.md"
cp "$G/skill-fired.md" "$G/skill-not-fired.md"
run --skills alpha --selftest
expect "--selftest: a routing case with both fired graders is caught" 1 'found 2'

echo "the --scaffold branch guard"
mkrepo; mkclaude; mkresult 1 true 1
for b in main fix/x bugfix/x docs/x test/x chore/x refactor/x; do
  git -C "$REPO" checkout -q -B "$b"
  run --skills alpha --kind routing
  expect "branch $b is allowed" 0
done
for b in champion/pr-5 lane/test wip release/1; do
  git -C "$REPO" checkout -q -B "$b"
  run --skills alpha --kind routing
  expect "branch $b is refused" 3 'is not one of main, feat'
done

echo "model pins, tally and stage cleanup"
mkrepo
run --skills alpha --kind routing --dry-run
has -E -- '--model claude-sonnet-5-5( |$)' <<<"$OUT" && has -E -- '--judge-model sonnet( |$)' <<<"$OUT" && ok "the default agent model and judge are pinned in the dry run" || { fail "model pins"; show; }
run --skills alpha --kind routing --dry-run --model claude-opus-5-5 --judge-model haiku
has -E -- '--model claude-opus-5-5( |$)' <<<"$OUT" && has -E -- '--judge-model haiku( |$)' <<<"$OUT" && ok "--model and --judge-model override the pins" || { fail "model override"; show; }
mkclaude; mkresult 1 true 1
find "$STATE/skill-evals/spend.jsonl" -delete 2>/dev/null; mkdir -p "$STATE/skill-evals/spend.jsonl"
run --skills alpha --kind routing
expect "an unwritable tally exits 2, not 3" 2 'fail closed'
find "$STATE/skill-evals/spend.jsonl" -delete
mkdir -p "$REPO/skills/alpha/hooks"; : >"$REPO/skills/alpha/hooks/h.sh"
git -C "$REPO" add -A; git -C "$REPO" commit -q -m hooks
run --skills alpha --kind routing
expect "a refusal mid-stage exits 3" 3 'hooks/ directory'
[ -z "$(find "$STATE/skill-evals/cache" -name 'stage-*' 2>/dev/null)" ] && ok "a refusal mid-stage leaves nothing in cache/" || fail "stage left in cache/"
mkrepo
printf 'x\n' >"$REPO/skills/_preamble-extended.md"
run --changed --dry-run
expect "--changed: an edit to skills/_preamble-extended.md picks every skill with evals" 0 'skills: alpha '
run --skills alpha --kind routing --dry-run --keep-temp
SR="$(printf '%s\n' "$OUT" | sed -n 's/^skill-evals: staged routing: //p')"
[ -f "$SR/skills/_preamble-extended.md" ] && ok "the staged copy includes skills/_preamble-extended.md" || fail "extended preamble not staged"

echo "the real skills/ stage"
mkrepo
cp -a "$REAL/skills/." "$REPO/skills/"
printf -- '---\nname: alpha\ndescription: Use when the cart total looks wrong after a bulk order. Not for new features.\n---\nState: ${AW_STATE_DIR:-$HOME/.agentic-workflow}\nMode: ${AW_GIT_MODE:-full}\n' >"$REPO/skills/alpha/SKILL.md"
run --skills alpha --kind routing --dry-run
n_of() { printf '%s\n' "$OUT" | sed -n "s/^skill-evals:   rewrite $1 default [a-z0-9]* -> [a-z0-9]*: \([0-9]*\) file.*/\1/p" | head -1; }
for v in AW_PREAMBLE AW_GIT_MODE; do
  n="$(n_of $v)"; [ -n "$n" ] && [ "$n" -gt 0 ] && ok "$v's default is rewritten in $n real staged file(s)" || { fail "$v rewrote '$n' files"; show; }
done
# TODO(#1897): AW_RESULT has no skill text on main until 0d lands; then assert its count is > 0 like the two above.
n="$(n_of AW_RESULT)"
if [ "$n" = 0 ]; then has -E 'warning: no staged file holds .*AW_RESULT' <<<"$OUT" && ok "AW_RESULT rewriting 0 files prints a warning" || { fail "no AW_RESULT warning"; show; }
else ok "AW_RESULT is rewritten in $n file(s)"; fi

# trace_reply SKILL: what the eval tool hands a `target: trace` grader for a run whose Skill call names SKILL: run.trace.map(JSON.stringify)
# joined by newlines (claude 2.1.312 bundle, grader module near byte 283.43M). The exact message envelope is checked on the first real family run.
trace_reply() {
  printf '{"type":"system","subtype":"init","skills":["alpha","beta"],"tools":["Skill"]}\n'
  printf '{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Skill","input":{"skill":"%s"}}]}}\n' "$1"
}

# mkfamily: evals/_families/grp/{alpha,beta}: a first-Skill-call regex grader on the trace, committed
mkfamily() {
  local F="$REPO/evals/_families/grp" m
  for m in alpha beta; do
    mkdir -p "$F/$m/graders/_selftest"
    printf -- '---\nmax_turns: 5\ntimeout_seconds: 60\nallowed_tools: [Skill]\n---\nA request that only %s should handle.\n' "$m" >"$F/$m/prompt.md"
    cat >"$F/$m/graders/first-skill.md" <<'EOF'
---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"skill"\s*:)[\s\S])*"skill"\s*:\s*"(?:[\w-]+:)?MEMBER"'
---

The first Skill call names MEMBER.
EOF
    sed -i "s/MEMBER/$m/g" "$F/$m/graders/first-skill.md"
    trace_reply "aw-evals:$m" >"$F/$m/graders/_selftest/first-skill.pass.trace"
    { trace_reply zzz; trace_reply "$m"; } >"$F/$m/graders/_selftest/first-skill.fail.trace"
  done
  printf 'family grp\n' >"$F/README.md"
  git -C "$REPO" add -A; git -C "$REPO" commit -q -m families
}

echo "bootstrap/ is a skill dir"
mkrepo; mkclaude
mkdir -p "$REPO/bootstrap" "$REPO/evals/bootstrap/routing/bootstrap-natural-1/graders/_selftest"
printf -- '---\nname: bootstrap\ndescription: Use when a repo lacks its planning docs and needs them generated.\n---\nbootstrap body\n' >"$REPO/bootstrap/SKILL.md"
BC="$REPO/evals/bootstrap/routing/bootstrap-natural-1"
printf -- '---\nmax_turns: 5\ntimeout_seconds: 60\nallowed_tools: [Skill]\n---\nSet this repo up with the docs it is missing.\n' >"$BC/prompt.md"
printf -- '---\ntype: tool_used\ntool: Skill\ninput_match: bootstrap\narm: with-only\n---\n' >"$BC/graders/skill-fired.md"
printf 'Skill\t{"skill":"bootstrap"}\n' >"$BC/graders/_selftest/skill-fired.pass.tools"
printf 'Bash\tls\n' >"$BC/graders/_selftest/skill-fired.fail.tools"
git -C "$REPO" add -A; git -C "$REPO" commit -q -m bootstrap
run --skills bootstrap --selftest
expect "--skills bootstrap resolves bootstrap/SKILL.md and --selftest passes" 0 'selftest: 1 case\(s\), 0 problem'
run --changed --dry-run
expect "--changed picks bootstrap from a bootstrap/ diff" 0 'skills: bootstrap'
git -C "$REPO" update-ref refs/remotes/origin/main HEAD
run --changed --dry-run
expect "nothing differs from the merge base: no skills changed" 0 '^no skills changed'
printf 'more\n' >>"$REPO/bootstrap/SKILL.md"
run --changed --dry-run
expect "--changed counts a bootstrap/ body change" 0 'skills: bootstrap'
run --skills bootstrap --kind routing --dry-run --keep-temp
SR="$(printf '%s\n' "$OUT" | sed -n 's/^skill-evals: staged routing: //p')"
[ -f "$SR/skills/bootstrap/SKILL.md" ] && ok "the stage copies bootstrap/ to skills/bootstrap/" || { fail "bootstrap not staged"; show; }
run --skills bootstrap --lint-descriptions
expect "--lint-descriptions reads bootstrap's description" 0 'lint-descriptions: ok'

echo "family sets"
mkrepo; mkclaude; mkfamily
run --skills alpha --selftest
expect "--selftest walks the family cases and counts them" 0 'selftest: 5 case\(s\), 0 problem'
rm "$REPO/evals/_families/grp/beta/graders/_selftest/first-skill.fail.trace"
run --skills alpha --selftest
expect "a family grader without a fail reply is caught" 1 'beta/graders/first-skill.md: no graders/_selftest/first-skill.fail'
trace_reply beta >"$REPO/evals/_families/grp/beta/graders/_selftest/first-skill.fail.trace"
run --skills alpha --selftest
expect "a trace grader that passes its fail reply is caught" 1 'does not fail its fail reply'
trace_reply zzz >"$REPO/evals/_families/grp/beta/graders/_selftest/first-skill.fail.trace"
run --skills alpha --selftest
expect "a trace grader that fails its fail reply passes" 0 'selftest: 5 case'

mkrepo; mkclaude; mkfamily
run --changed --dry-run
expect "no change: no skills and no family" 0 '^no skills changed'
printf 'body edit\n' >>"$REPO/skills/alpha/SKILL.md"
run --changed --dry-run
expect "a body-only change picks the skill" 0 'skills: alpha'
lacks "a body-only change does not pull in its family" 'family plan'
git -C "$REPO" checkout -q -- skills/alpha/SKILL.md
sed -i 's/^description: Use when the cart total looks wrong/description: Use when the cart total looks off/' "$REPO/skills/alpha/SKILL.md"
run --changed --dry-run
expect "a description change pulls in the member's family" 0 'family plan: grp \(2 case\(s\), ablation none, cap \$12\): description of alpha changed'
has -E 'staged family-grp' <<<"$OUT" && has -E -- '--ablation none .*--max-cost-usd 12' <<<"$OUT" && ok "the dry run shows the family stage, ablation none and the \$12 ceiling" || { fail "family dry-run command"; show; }
git -C "$REPO" checkout -q -- skills/alpha/SKILL.md
run --skills alpha --dry-run
lacks "--skills without a changed description runs no family" 'family plan'
run --family grp --dry-run
expect "--family alone needs no skill selector" 0 'family plan: grp .*--family'
run --family nope --dry-run
expect "an unknown family is a usage error" 64 'unknown family: nope'
run --skills alpha --family grp --kind routing --dry-run --family-max-cost-usd 5
expect "--family-max-cost-usd sets the family ceiling" 0 'cap \$5'
has -E -- '--max-cost-usd 5 ' <<<"$OUT" && ok "the family stage gets the \$5 ceiling" || { fail "family ceiling"; show; }
run --skills alpha --family grp --kind routing --dry-run --family-max-cost-usd many
expect "a bad --family-max-cost-usd is a usage error" 64 'must be a number'

echo "the family cap refuses separately"
mkrepo; mkclaude; mkfamily; mkresult 1 true 1
mkdir -p "$STATE/skill-evals"
printf '{"t":"settle","id":"x","date":"%s","lane":"l","usd":50}\n' "$(date +%F)" >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --family grp --kind routing --dry-run
expect "dry run: the skill run fits (50 + 8) and the family set (+ 12) does not" 0 'FAMILY SKIPPED'
has -E 'dry run ok' <<<"$OUT" && has -E 'plugin eval' <<<"$OUT" && ok "the skill run is still planned" || { fail "skill plan lost"; show; }
run --skills alpha --family grp --kind routing
expect "a real run: the skill run goes on, the family set is skipped, exit 2" 2 'FAMILY SKIPPED'
has -E '### routing' <<<"$OUT" && ok "the skill table is printed" || { fail "no skill table"; show; }
lacks "no family block when the family set was refused" '### family'
[ "$(grep -c -- '-family' "$STATE/skill-evals/spend.jsonl" || true)" = 0 ] && ok "a refused family set leaves no reserve row" || fail "family row written"
mkresult 1 false 1
run --skills alpha --family grp --kind routing --gate block
expect "a missed gate under block still exits 4 when the family set was refused" 4 'ROUTING GATE MISSED: alpha'
has -E 'FAMILY SKIPPED' <<<"$OUT" && ok "the refused family set is still reported" || { fail "family skip line lost"; show; }
mkresult 0.5 true 1
run --skills alpha --family grp --kind routing
expect "a score below the threshold still exits 1 when the family set was refused" 1 'FAMILY SKIPPED'
mkresult 1 true 1
printf '{"t":"settle","id":"x","date":"%s","lane":"l","usd":50}\n' "$(date +%F)" >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --family grp --kind routing --family-max-cost-usd 1
expect "a smaller family ceiling fits" 0 '### family grp'
[ "$(grep -c -- '-family' "$STATE/skill-evals/spend.jsonl" || true)" = 2 ] && ok "the family set has its own reserve and settle rows" || { fail "family tally rows"; cat "$STATE/skill-evals/spend.jsonl"; }

echo "regex graders are JavaScript"
mkrepo
G="$REPO/evals/alpha/behaviour/alpha-forced/graders"
printf -- '---\ntype: regex\n---\n(?i)alpha-done\n' >"$G/has-done.md"
run --skills alpha --selftest
expect "a Perl-only (?i) is reported as invalid in JavaScript" 1 'SELFTEST-REGEX: .*has-done.md: pattern does not compile in JavaScript'
printf -- '---\ntype: regex\nflags: i\n---\nalpha-done\n' >"$G/has-done.md"
run --skills alpha --selftest
expect "flags: i matches case-insensitively" 0 'selftest: 3 case\(s\), 0 problem'
printf -- '---\ntype: regex\n---\nalpha-done\n' >"$G/has-done.md"
run --skills alpha --selftest
expect "without flags the same pattern is case-sensitive and fails its pass reply" 1 'does not pass its pass reply'
printf -- '---\ntype: regex\nflags: x\n---\nALPHA-DONE\n' >"$G/has-done.md"
run --skills alpha --selftest
expect "flags outside dgimsuvy are a selftest problem" 1 'SELFTEST-REGEX: .*are not JavaScript RegExp flags'
printf 'ALPHA ALPHA\n' >"$G/_selftest/has-done.pass.txt"
printf -- '---\ntype: regex\nmatch: count:2\n---\nALPHA\n' >"$G/has-done.md"
run --skills alpha --selftest
expect "count:N counts every match (g)" 0 'selftest: 3 case\(s\), 0 problem'
printf -- '---\ntype: regex\nmatch: count:3\n---\nALPHA\n' >"$G/has-done.md"
run --skills alpha --selftest
expect "a wrong count:N fails the pass reply" 1 'does not pass its pass reply'

echo "kept traces: one file per run"
mkrepo; mkclaude; mkresult 1 true 1
mkdir -p "$TMPROOT/tr/1" "$TMPROOT/tr/2" "$TMPROOT/tr/3"   # every run's trace is called trace.jsonl, as in the real tool
echo '{"type":"system"}' >"$TMPROOT/tr/1/trace.jsonl"; echo '{"type":"assistant"}' >"$TMPROOT/tr/2/trace.jsonl"; echo '{"type":"result"}' >"$TMPROOT/tr/3/trace.jsonl"
jq --arg a "$TMPROOT/tr/1/trace.jsonl" --arg b "$TMPROOT/tr/2/trace.jsonl" --arg c "$TMPROOT/tr/3/trace.jsonl" '
  .cases = [.cases[0], (.cases[0] | .name = "alpha-natural-2" | .dir = "evals/alpha/routing/alpha-natural-2")]
  | .cases[0].arms.with[0].tracePath = $a | .cases[0].arms.with[1].tracePath = $b
  | .cases[1].arms.with[0].tracePath = $c | .cases[1].arms.with[1].tracePath = null' "$BIN/result.json" >"$BIN/r2.json" && mv "$BIN/r2.json" "$BIN/result.json"
run --skills alpha --kind routing --keep-temp
TD="$(find "$STATE/skill-evals" -type d -name traces | head -1)"
[ "$(ls "$TD" 2>/dev/null | wc -l)" = 3 ] && ok "three traced runs of two cases leave three files" || { fail "trace files: $(ls "$TD" 2>/dev/null | tr '\n' ' ')"; show; }
[ -f "$TD/alpha-natural-1-with-1.jsonl" ] && [ -f "$TD/alpha-natural-1-with-2.jsonl" ] && [ -f "$TD/alpha-natural-2-with-1.jsonl" ] && ok "traces are named <case>-<arm>-<run>.jsonl" || { fail "trace names"; ls "$TD"; }
grep -q assistant "$TD/alpha-natural-1-with-2.jsonl" && ok "each file holds its own run's trace" || fail "trace content"
KEPT_TMP="$(sed -n 's/^TMPDIR=//p' "$BIN"/env.*.txt | head -1)"
[ -n "$KEPT_TMP" ] && [ -d "$KEPT_TMP" ] && ok "eval child: --keep-temp keeps the run's TMPDIR" || fail "eval child: --keep-temp lost TMPDIR '$KEPT_TMP'"

echo "an eval temp dir too long for claude's sandbox socket (#1931)"
mkrepo; mkclaude; mkresult 1 true 1
STATE="$STATE/$(printf 'd%.0s' $(seq 40))"; mkdir -p "$STATE"
run --skills alpha --kind routing
expect "a deep state dir refuses before claude runs" 3 'socket'
has -F -- '108' <<<"$OUT" && has -F -- 'AW_STATE_DIR' <<<"$OUT" && ok "the refusal names the 108-byte socket limit and AW_STATE_DIR" || { fail "the refusal names the 108-byte socket limit and AW_STATE_DIR"; show; }
[ -z "$(ls "$BIN"/args.*.txt 2>/dev/null)" ] && ok "claude never ran" || fail "claude ran despite the long temp dir"
[ -z "$(ls -A "$STATE/skill-evals/tmp" 2>/dev/null)" ] && ok "the refused run leaves no temp dir behind" || { fail "the refused run left $(ls -A "$STATE/skill-evals/tmp")"; }

echo "fixture lint ignores comment lines"
mkrepo
printf '#!/usr/bin/env bash\n# echo x > /home/foo (a comment)\n  # git push origin main (so is this)\nprintf "cart\\n" > cart.txt\n' >"$REPO/evals/alpha/fixture.sh"
run --skills alpha --selftest
expect "commented-out redirects and pushes are not flagged" 0 'selftest: 3 case\(s\), 0 problem'
printf 'git push origin main\n' >>"$REPO/evals/alpha/fixture.sh"
run --skills alpha --selftest
expect "a real push is still flagged" 1 'SELFTEST-FIXTURE: .*deletes or writes outside'
printf '#!/usr/bin/env bash\necho x > /home/foo\n' >"$REPO/evals/alpha/fixture.sh"
run --skills alpha --selftest
expect "a real redirect outside is still flagged" 1 'SELFTEST-FIXTURE'

echo
echo "skill-evals: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
