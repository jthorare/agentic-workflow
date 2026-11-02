#!/usr/bin/env bash
# Tests for scripts/skill-evals.sh (#1107): the flags and exit codes 0-4, the staged rewrite (source untouched), the env allowlist
# and token scrub, the spend tally under two concurrent runs, budget refusal, --changed, a staged path outside the repo, --selftest,
# --lint-descriptions and --status. A stub `claude` stands in for the real one: no model, no network, no login.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REAL="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/skill-evals-test.XXXXXX")"
LIVE_PID=""
cleanup() { [ -z "$LIVE_PID" ] || kill "$LIVE_PID" 2>/dev/null || true; find "$TMPROOT" -delete 2>/dev/null || true; }
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
  REPO="$base/repo" STATE="$base/state" BIN="$base/bin"
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
  git -C "$REPO" checkout -q -b lane/test
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
out=""; prev=""
for a in "$@"; do [ "$prev" = "--json" ] && out="$a"; prev="$a"; done
[ ! -f "$d/sleep" ] || sleep "$(cat "$d/sleep")"
[ -f "$d/no-result" ] || { [ -z "$out" ] || { mkdir -p "$(dirname "$out")"; cp "$d/result.json" "$out"; }; }
exit "$(cat "$d/exit" 2>/dev/null || echo 0)"
EOF
  chmod +x "$BIN/claude"
}
# mkresult SCORE FIRED(true|false) COST [partial]
mkresult() {
  local run
  run="{\"score\":$1,\"passed\":true,\"costUsd\":0.2,\"judgeCostUsd\":0,\"graders\":[{\"name\":\"skill-fired\",\"passed\":$2,\"scored\":false,\"explanation\":\"saw $3\"}]}"
  cat >"$BIN/result.json" <<EOF
{"schemaVersion":1,"costUsd":$3,"partial":${4:-false},"cases":[{"name":"alpha-natural-1","dir":"evals/alpha/routing/alpha-natural-1","aggregates":{"score":$1,"passRate":1},
"arms":{"with":[$run,$run]}}],"aggregates":{"casesTotal":1}}
EOF
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
has -F -- 'evals/stubs/bin' <<<"$OUT" && ok "the stubs are first on PATH" || fail "stubs not on PATH"
[ -x "$STAGE_R/evals/stubs/bin/gh" ] && ok "the stub gh is executable in the stage" || fail "stub gh mode"
[ -d "$H/.aw-eval" ] || [ -d "$TMPROOT/.aw-eval" ] && ok "the scaffold creates .aw-eval/ in the workspace" || fail "no .aw-eval/"
grep -q 'AW_GIT_MODE:-readonly' "$STAGE_R/evals/stubs/bin/gh" && ok "the staged gh stub defaults to readonly" || fail "staged gh default"
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
has -E "^PATH=.*/evals/stubs/bin:" "$ENVF" && ok "PATH starts with the stub dir" || fail "PATH order"
[ "$(wc -l <"$ENVF")" -le 12 ] && ok "only the allowlist is forwarded ($(wc -l <"$ENVF") variables)" || { fail "env has too many variables"; cat "$ENVF"; }
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
sed -i "s/saw 0.5/leaked hunter22xyz and $FAKE_GH and Authorization: Bearer $FAKE_BEARER/" "$BIN/result.json"
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
printf '{"t":"lane","date":"%s","lane":"a"}\n{"t":"lane","date":"%s","lane":"b"}\n' "$(date +%F)" "$(date +%F)" >"$STATE/skill-evals/spend.jsonl"
run --skills alpha --kind routing --lane a --max-cost-usd 31
expect "with two lanes registered a lane's share is 30" 2 "lane a's share"
run --skills alpha --kind routing --lane a --max-cost-usd 29
expect "a run inside the lane's share passes" 0
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

echo
echo "skill-evals: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
