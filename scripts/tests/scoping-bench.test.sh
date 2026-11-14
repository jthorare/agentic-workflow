#!/usr/bin/env bash
# Tests for scripts/scoping-bench.mjs (the scoping benchmark's runner, context scope spec §7): the repo's answer keys pass --check,
# --check finds a missing or repeated path, usage errors exit 2, and a stub sindri's answers are scored as the runner documents
# (item nodes, path steps, cite keys and refs, the map cite skipped, non-file keys skipped, touch recall apart from recall).
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
BENCH="$REPO/scripts/scoping-bench.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/scoping-bench-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT
export HOME="$TMPROOT/home"; mkdir -p "$HOME"

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# yaml is a dev dependency of scripts/ (scripts/package.json); installed once under a lock, since suites run in parallel.
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$BENCH" "$@" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_no() { if printf '%s\n' "$OUT" | has -F -- "$2"; then fail "$1: output has '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; else ok "$1"; fi; }

echo "the repo's keys"
run --questions "$REPO/tests/fixtures/scoping/agentic-workflow/questions.yaml" --check
expect_rc "this repo's answer keys name only files that exist" 0; expect_out "says ok" "scoping-bench: ok (5 question(s))"
run --questions "$REPO/tests/fixtures/scoping/effect/questions.yaml" --check --root "$TMPROOT/no-clone"
expect_rc "the Effect key against a root without the clone has findings" 1; expect_out "names a missing path" "effect-01: touch packages/sql/pg/src/PgClient.ts does not exist"

echo "--check"
mkdir -p "$TMPROOT/tree/src"; : > "$TMPROOT/tree/src/a.ts"; : > "$TMPROOT/tree/src/b.ts"
cat > "$TMPROOT/keys.yaml" <<'EOF'
repo: tiny
questions:
  - id: q1
    plain: "where is a"
    touch: [{path: src/a.ts, why: "it"}]
    read: [{path: src/b.ts, why: "it"}, {path: src/gone.ts, why: "moved"}]
    decoys: [{path: src/a.ts, why: "twice"}]
EOF
run --questions "$TMPROOT/keys.yaml" --check --root "$TMPROOT/tree"
expect_rc "a missing and a repeated path are findings" 1
expect_out "the missing one" "q1: read src/gone.ts does not exist"; expect_out "the repeated one" "q1: src/a.ts is listed twice"
expect_out "the count" "scoping-bench: 2 finding(s)"

echo "usage"
run; expect_rc "no --questions" 2; expect_out "says so" "--questions is required"
run --questions "$TMPROOT/keys.yaml" --k 0; expect_rc "--k 0" 2
run --questions "$TMPROOT/keys.yaml" --limit 0; expect_rc "--limit 0" 2; expect_out "says so" "--limit must be a whole number of 1 or more"
run --questions "$TMPROOT/keys.yaml" --field title; expect_rc "an unknown --field" 2
run --questions "$TMPROOT/keys.yaml" --only q1,q9; expect_rc "--only with an unknown id" 2; expect_out "names it" "unknown question(s): q9"
run --questions "$TMPROOT/nope.yaml"; expect_rc "a questions file that isn't there" 2
printf 'questions:\n  - id: q1\n    plain: "x"\n    read: [{path: a}]\n' > "$TMPROOT/notouch.yaml"
run --questions "$TMPROOT/notouch.yaml"; expect_rc "a question without touch files" 2; expect_out "says why" "q1: no touch files"
run --questions "$TMPROOT/keys.yaml" --bogus; expect_rc "an unknown argument" 2

echo "scoring (stub sindri)"
STUB="$TMPROOT/sindri"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
# A stub of `sindri context query <text> --json`: answers by the question text; logs its argv.
printf '%s\n' "$*" >> "${STUB_LOG:?}"
case "$3" in
  "where is a" | "Where is a, precisely?")
    cat <<'JSON'
{"sections":[{"items":[
  {"node":"feature:tiny.alpha","path":null,"cites":["c1"]},
  {"node":"code:src/a.ts","path":["feature:tiny.alpha","implements code:src/c.ts"],"cites":["c2","c3"]},
  {"node":null,"path":null,"cites":["c4"]}]}],
 "cites":{"c1":{"key":"map:tiny/v3","refs":[{"id":"tiny/v3"}]},
          "c2":{"key":"repo:tiny/docs/readme.md","refs":[{"id":"tiny/docs/readme.md#L1-L9"}]},
          "c3":{"key":"record:linear/ABC-1","refs":[{"id":"tiny/src/b.ts#L4"}]},
          "c4":{"key":"file:src/decoy.ts","refs":[]}}}
JSON
    ;;
  "where is b") echo 'SND-CONTEXT-011 no source answered' >&2; exit 1 ;;
  *) echo '{"sections":[],"cites":{}}' ;;
esac
EOF
chmod +x "$STUB"
export STUB_LOG="$TMPROOT/stub.log"
cat > "$TMPROOT/score.yaml" <<'EOF'
repo: tiny
questions:
  - id: q1
    plain: "where is a"
    question: "Where is a, precisely?"
    touch: [{path: src/a.ts}, {path: src/never.ts}]
    read: [{path: src/b.ts}, {path: src/c.ts}]
    decoys: [{path: src/decoy.ts}]
  - id: q2
    plain: "where is nothing"
    touch: [{path: src/a.ts}]
EOF
run --questions "$TMPROOT/score.yaml" --sindri "$STUB"
expect_rc "every question answered" 0
expect_out "files in answer order: node, path step, cite key, cite ref; map and record keys dropped" \
  "top 5: src/a.ts, src/c.ts, docs/readme.md, src/b.ts, src/decoy.ts"
expect_out "touch recall counts apart from read" "touch 1/2  read 2/2  recall=0.75  P@25=0.60  decoys 1"
expect_out "the missed touch file is named" "missed touch: src/never.ts"
expect_out "the decoy is named" "decoys hit: src/decoy.ts"
expect_no "the map version cite is not a file" "v3"
expect_out "an empty answer scores zero" "q2"
expect_out "totals" "== 2/2 answered · touch recall 0.25 · recall 0.38 · P@25 0.30 · decoys 1"
if has -Fx -- "context query where is a --json" "$STUB_LOG"; then ok "the plain text is asked, with --json"; else fail "the plain text is asked, with --json"; cat "$STUB_LOG"; fi

: > "$STUB_LOG"
run --questions "$TMPROOT/score.yaml" --sindri "$STUB" --field question --only q1 --k 2 --json
expect_rc "--field question --only --k --json" 0
if has -Fx -- "context query Where is a, precisely? --json" "$STUB_LOG"; then ok "--field question asks the precise text"; else fail "--field question asks the precise text"; cat "$STUB_LOG"; fi
if [ "$(printf '%s' "$OUT" | jq -r '[(.rows | length), .rows[0].touch.hit, .rows[0].precision, .totals.k] | join(" ")')" = "1 1 1 2" ]; then
  ok "--json: one row, top 2 = src/a.ts src/c.ts, both relevant"
else fail "--json rows and totals"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi

: > "$STUB_LOG"
run --questions "$TMPROOT/score.yaml" --sindri "$STUB" --only q1 --limit 25
expect_rc "--limit" 0
if has -Fx -- "context query where is a --limit 25 --json" "$STUB_LOG"; then ok "--limit asks sindri for that window"; else fail "--limit asks sindri for that window"; cat "$STUB_LOG"; fi
expect_out "the header names the window" "reads 25"

printf 'questions:\n  - id: qb\n    plain: "where is b"\n    touch: [{path: src/b.ts}]\n  - id: q1\n    plain: "where is a"\n    touch: [{path: src/a.ts}]\n' > "$TMPROOT/fails.yaml"
run --questions "$TMPROOT/fails.yaml" --sindri "$STUB" --out "$TMPROOT/raw"
expect_rc "a failed query" 1; expect_out "is an ERROR row with its first stderr line" "qb  " ; expect_out "the reason" "ERROR SND-CONTEXT-011 no source answered"
expect_out "the others still score" "== 1/2 answered"
if [ -f "$TMPROOT/raw/qb.json" ] && [ "$(jq -r .rc "$TMPROOT/raw/qb.json")" = 1 ] && [ -f "$TMPROOT/raw/q1.json" ]; then ok "--out keeps every raw answer, failed ones too"; else fail "--out keeps every raw answer"; fi
run --questions "$TMPROOT/fails.yaml" --sindri "$TMPROOT/no-such-sindri"
expect_rc "a sindri that can't start fails every question" 1; expect_out "says why" "ENOENT"

echo
echo "scoping-bench: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
