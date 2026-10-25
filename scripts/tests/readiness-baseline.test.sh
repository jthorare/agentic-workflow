#!/usr/bin/env bash
# Tests for scripts/readiness-baseline.mjs (throwaway R0 scorer). Fixture trees only; no network (--no-remote).
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$DIR/../readiness-baseline.mjs"
TMP="$(mktemp -d)"
# The script scores no apps by default; the fixtures name six (alpha is the one the trees below build).
export READINESS_APPS="beta,alpha,gamma,delta,epsilon,apps/live"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $*"; exit 1; }

# A minimal tree: README, AGENTS.md, one app with a lockfile, strict tsconfig and a unit test.
make_tree() {
  local root="$1"
  mkdir -p "$root/alpha/tests" "$root/alpha/src"
  echo "# demo" > "$root/README.md"
  printf '# Agents\n\n## Commands\n\n```bash\ncd alpha && npm run build\n```\n' > "$root/AGENTS.md"
  echo '{"name":"alpha","scripts":{"build":"tsc","test":"vitest run"}}' > "$root/alpha/package.json"
  echo '{}' > "$root/alpha/package-lock.json"
  echo '{"compilerOptions":{"strict": true}}' > "$root/alpha/tsconfig.json"
  echo 'test("x", () => {})' > "$root/alpha/tests/a.test.ts"
}

row_state() { # row_state <file> <criterion-id> -> state column of that criterion's table row
  awk -F'|' -v id="$2" '$2 ~ "^ *"id" *$" { gsub(/ /, "", $6); print $6; exit }' "$1"
}

test_codeowners_flips_sec3() {
  local t="$TMP/co"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/before.md"
  [ "$(row_state "$t/before.md" SEC-3)" = "fail" ] || fail "SEC-3 should fail without CODEOWNERS"
  mkdir -p "$t/.github"; echo "* @owner" > "$t/.github/CODEOWNERS"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/after.md"
  [ "$(row_state "$t/after.md" SEC-3)" = "pass" ] || fail "SEC-3 should pass with .github/CODEOWNERS"
  echo "PASS: test_codeowners_flips_sec3"
}

test_env_template_flips_de2() {
  local t="$TMP/env"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/a.md"
  [ "$(row_state "$t/a.md" DE-2)" = "fail" ] || fail "DE-2 should fail without .env.example"
  echo "FOO=" > "$t/.env.example"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/b.md"
  [ "$(row_state "$t/b.md" DE-2)" = "pass" ] || fail "DE-2 should pass with .env.example"
  echo "PASS: test_env_template_flips_de2"
}

test_no_remote_marks_remote_unknown() {
  local t="$TMP/nr"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  for id in SEC-1 SEC-2 TD-2 BS-4; do
    [ "$(row_state "$t/o.md" "$id")" = "unknown" ] || fail "$id should be unknown with --no-remote"
  done
  echo "PASS: test_no_remote_marks_remote_unknown"
}

test_deterministic_for_fixed_date() {
  local t="$TMP/det"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/1.md"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/2.md"
  diff -u "$t/1.md" "$t/2.md" >/dev/null || fail "two runs with the same --date differ"
  echo "PASS: test_deterministic_for_fixed_date"
}

test_app_fraction_counts_present_apps() {
  local t="$TMP/frac"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  # TS-1 is per app over six apps; only alpha exists and has a test.
  grep -E '^\| TS-1 \|.*\| 1 / 6 \|' "$t/o.md" >/dev/null || fail "TS-1 should report 1 / 6"
  echo "PASS: test_app_fraction_counts_present_apps"
}

test_bad_root_exits_2() {
  local rc=0
  node "$SCRIPT" --root "$TMP/does-not-exist" --no-remote >/dev/null 2>"$TMP/err" || rc=$?
  [ "$rc" -eq 2 ] || fail "bad root should exit 2, got $rc"
  grep -q "does-not-exist" "$TMP/err" || fail "error should name the path"
  echo "PASS: test_bad_root_exits_2"
}

test_only_read_only_gh_calls() {
  # The script must never write to GitHub: no method override and no field flags on gh api.
  if grep -nE "'(-X|--method|-f|-F|--field|--raw-field|--input)'" "$SCRIPT"; then
    fail "gh api write flags found"
  fi
  echo "PASS: test_only_read_only_gh_calls"
}

row() { # row <file> <criterion-id> -> the whole table row
  grep -E "^\| $2 \|" "$1" || true
}

# levelOf is exported; call it on synthetic rows. Prints JSON {level, cells}.
levels() { # levels '<rows json>'
  node --input-type=module -e "
    const { levelOf } = await import('$SCRIPT');
    const r = levelOf(JSON.parse(process.argv[1]));
    console.log(JSON.stringify({ level: r.level, skipped: r.byLevel.filter((b) => b.skipped).map((b) => b.L) }));
  " "$1"
}

test_level_walk() {
  # JSON lives in single-quoted variables: bash 3.2 brace-expands {…} written inside a quoted $( ) argument.
  local p='{"level":1,"state":"pass","credit":1}' f='{"level":1,"state":"fail","credit":0}'
  local frac='{"level":1,"state":"fail","credit":0.857}' p3='{"level":3,"state":"pass","credit":1}'
  [ "$(levels "[$p,$p,$p,$p,$f]")" = '{"level":2,"skipped":[]}' ] || fail "exactly 80% at L1 should unlock L2"
  [ "$(levels '[{"level":1,"state":"fail","credit":0.79}]')" = '{"level":1,"skipped":[]}' ] || fail "79% should block"
  # fractional app credit: 6/7 + a pass = 1.857 / 2 = 93%
  [ "$(levels "[$frac,$p]")" = '{"level":2,"skipped":[]}' ] || fail "fractional credit should pool"
  # a skipped level in the middle is walked past and reported as skipped
  [ "$(levels "[$p,$p3]")" = '{"level":4,"skipped":[2]}' ] || fail "empty L2 should be skipped and marked"
  # only empty levels below the reached level are skip (plus an empty L1, where every repo starts); L2-L3 stay unmarked
  local f4='{"level":4,"state":"fail","credit":0}'
  [ "$(levels "[$f4]")" = '{"level":1,"skipped":[1]}' ] || fail "empty L2-L3 above level 1 must not be skip: $(levels "[$f4]")"
  # n/a and unknown are not scored; nothing scored -> no level
  [ "$(levels '[{"level":1,"state":"unknown","credit":0},{"level":2,"state":"n/a","credit":0}]')" = '{"level":null,"skipped":[]}' ] \
    || fail "nothing scored should give level null"
  echo "PASS: test_level_walk"
}

test_unscored_pillar_and_skipped_cells_render() {
  local t="$TMP/render"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  # Product & Experimentation is scored at L4 only and stays level 1: empty L1 is skip, empty L2–L3 above it are '—'
  grep -E '^\| Product & Experimentation \| skip \| — \| — \| 0 / 2 \(0%\) \| — \| 1 \|$' "$t/o.md" >/dev/null \
    || fail "PE pillar row wrong: $(grep -E '^\| Product' "$t/o.md")"
  # Documentation: L1, L2 pass -> level 3; empty L3 is the reached level, not below it -> '—'
  grep -E '^\| Documentation \| 1 / 1 \(100%\) \| 1 / 1 \(100%\) \| — \| 0 / 1 \(0%\) \| — \| 3 \|$' "$t/o.md" >/dev/null \
    || fail "Documentation row wrong: $(grep -E '^\| Documentation' "$t/o.md")"
  echo "PASS: test_unscored_pillar_and_skipped_cells_render"
}

test_pe_scored_against_w9_page() {
  local t="$TMP/pe"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/a.md"
  [ "$(row_state "$t/a.md" PE-1)" = "fail" ] || fail "PE-1 should fail without the W9 page"
  [ "$(row_state "$t/a.md" PE-2)" = "fail" ] || fail "PE-2 should fail without the W9 page"
  mkdir -p "$t/docs/readiness"; echo "# signals" > "$t/docs/readiness/product-signals.md"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/b.md"
  [ "$(row_state "$t/b.md" PE-1)" = "pass" ] || fail "PE-1 should pass with docs/readiness/product-signals.md"
  [ "$(row_state "$t/b.md" TD-4)" = "unknown" ] || fail "TD-4 (parallel decomposition) should be unknown"
  echo "PASS: test_pe_scored_against_w9_page"
}

test_app_na_leaves_denominator() {
  local t="$TMP/na"; make_tree "$t"
  mkdir -p "$t/apps/live/hooks"; echo 'export {}' > "$t/apps/live/hooks/index.ts"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  # BS-2: alpha passes, live is n/a (no package.json), the other four are absent -> 1 / 5
  row "$t/o.md" BS-2 | grep -E '\| 1 / 5 \|' >/dev/null || fail "BS-2 should be 1 / 5: $(row "$t/o.md" BS-2)"
  row "$t/o.md" BS-2 | grep -F 'n/a: apps/live (no package.json' >/dev/null || fail "BS-2 should list live as n/a"
  # BS-1: per app; alpha has scripts.build and AGENTS.md documents it
  row "$t/o.md" BS-1 | grep -E '\| app \|.*\| 1 / 5 \|' >/dev/null || fail "BS-1 should be app-scope 1 / 5: $(row "$t/o.md" BS-1)"
  echo "PASS: test_app_na_leaves_denominator"
}

test_arg_errors_exit_2() {
  local t="$TMP/args"; make_tree "$t"
  local args
  for args in "--bogus" "--out" "--out --no-remote" "--date 2026-13-45" "--date yesterday" "--out $TMP/no-such-dir/x.md" \
      "--repo a/b/../../x" "--repo ../.." "--repo ./x" "--repo o/.."; do
    local rc=0
    # shellcheck disable=SC2086
    node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 $args >/dev/null 2>"$TMP/err" || rc=$?
    [ "$rc" -eq 2 ] || fail "'$args' should exit 2, got $rc ($(head -c 300 "$TMP/err"))"
    grep -qE '^[[:space:]]+at |Error:' "$TMP/err" && fail "'$args' printed a stack trace"
  done
  [ ! -e "$t/--no-remote" ] || fail "--out took a flag as its value"
  local ms
  for ms in -1 1.5 abc 0; do
    local rc=0
    READINESS_GH_TIMEOUT_MS="$ms" node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 >/dev/null 2>"$TMP/err" || rc=$?
    [ "$rc" -eq 2 ] || fail "READINESS_GH_TIMEOUT_MS=$ms should exit 2, got $rc"
    grep -q 'READINESS_GH_TIMEOUT_MS' "$TMP/err" || fail "timeout error should name the variable"
  done
  echo "PASS: test_arg_errors_exit_2"
}

# A stub gh on PATH: logs argv, returns tiny payloads per path, sleeps when GH_STUB_SLEEP is set.
make_gh_stub() {
  local d="$1"; mkdir -p "$d"
  cat > "$d/gh" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$GH_LOG"
[ -n "${GH_STUB_SLEEP:-}" ] && exec sleep "$GH_STUB_SLEEP"
# GH_STUB_SHAPE=object: every call answers 200 with {} (an API change or a proxy)
[ -n "${GH_STUB_SHAPE:-}" ] && { echo '{}'; exit 0; }
# GH_STUB_BIG=1: releases padded past 1 MiB with JSON whitespace (in memory only; the stub ignores --jq)
if [ -n "${GH_STUB_BIG:-}" ] && [[ "$2" == repos/o/r/releases* ]]; then
  printf '['; head -c 1200000 /dev/zero | tr '\0' ' '; echo '{"published_at":"2026-10-01T00:00:00Z","draft":false}]'; exit 0
fi
case "$2" in
  repos/o/r) echo '{"default_branch":"trunk","security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"disabled"}}}' ;;
  repos/o/r/branches/trunk/protection) echo 'gh: Branch not protected (HTTP 404)' >&2; exit 1 ;;
  repos/o/r/rules/branches/trunk) echo '[]' ;;
  repos/o/r/labels*) echo 'gh: API rate limit exceeded (HTTP 403)' >&2; exit 1 ;;
  repos/o/r/releases*) echo '[{"published_at":"2026-10-01T00:00:00Z","created_at":"2026-10-01T00:00:00Z","draft":false},
    {"published_at":"2026-09-01T00:00:00Z","created_at":"2026-09-01T00:00:00Z","draft":false},
    {"published_at":null,"created_at":"2026-10-02T00:00:00Z","draft":true},
    {"published_at":"2026-10-20T00:00:00Z","created_at":"2026-10-20T00:00:00Z","draft":false}]' ;;
  *) echo 'gh: Not Found (HTTP 404)' >&2; exit 1 ;;
esac
STUB
  chmod +x "$d/gh"
}

test_gh_stub_states_and_get_only() {
  local t="$TMP/gh"; make_tree "$t"; make_gh_stub "$TMP/stub"
  export GH_LOG="$TMP/gh.log"; : > "$GH_LOG"
  local rc=0
  PATH="$TMP/stub:$PATH" node "$SCRIPT" --root "$t" --repo o/r --date 2026-10-10 --out "$t/o.md" 2>"$TMP/err" || rc=$?
  [ "$rc" -eq 0 ] || fail "stubbed run should exit 0, got $rc"
  row "$t/o.md" SEC-1 | grep -E '\| fail \|.*trunk' >/dev/null || fail "SEC-1 should fail on the default branch trunk: $(row "$t/o.md" SEC-1)"
  [ "$(row_state "$t/o.md" SEC-2)" = "pass" ] || fail "SEC-2 should pass"
  row "$t/o.md" TD-2 | grep -E '\| unknown \|.*403' >/dev/null || fail "TD-2 should be unknown on 403: $(row "$t/o.md" TD-2)"
  # drafts and releases after --date don't count: 2 in the window -> fail
  row "$t/o.md" BS-4 | grep -E '\| fail \|.*2 releases' >/dev/null || fail "BS-4 should count 2: $(row "$t/o.md" BS-4)"
  # every call is `api <path>` (optionally with --jq), never a write flag
  if grep -vE '^api repos/o/r[^ ]*( --jq .*)?$' "$GH_LOG"; then fail "unexpected gh argv above"; fi
  grep -q 'branches/main' "$GH_LOG" && fail "SEC-1 should not hardcode main"
  echo "PASS: test_gh_stub_states_and_get_only"
}

test_gh_timeout_is_unknown() {
  local t="$TMP/gto"; make_tree "$t"; make_gh_stub "$TMP/stub"
  export GH_LOG="$TMP/gto.log"; : > "$GH_LOG"
  local rc=0
  GH_STUB_SLEEP=3 READINESS_GH_TIMEOUT_MS=200 PATH="$TMP/stub:$PATH" \
    node "$SCRIPT" --root "$t" --repo o/r --date 2026-10-10 --out "$t/o.md" 2>/dev/null || rc=$?
  [ "$rc" -eq 0 ] || fail "timed-out run should exit 0, got $rc"
  for id in SEC-1 SEC-2 TD-2 BS-4; do
    [ "$(row_state "$t/o.md" "$id")" = "unknown" ] || fail "$id should be unknown when gh times out"
  done
  # discriminating: the reason must be the timeout, not an empty-output parse error after the sleep
  row "$t/o.md" BS-4 | grep -F 'timed out after 200 ms' >/dev/null || fail "BS-4 should say it timed out: $(row "$t/o.md" BS-4)"
  echo "PASS: test_gh_timeout_is_unknown"
}

test_gh_large_payload_is_scored() {
  local t="$TMP/big"; make_tree "$t"; make_gh_stub "$TMP/stub"
  export GH_LOG="$TMP/big.log"; : > "$GH_LOG"
  GH_STUB_BIG=1 PATH="$TMP/stub:$PATH" node "$SCRIPT" --root "$t" --repo o/r --date 2026-10-10 --out "$t/o.md" 2>/dev/null \
    || fail "large payload run should exit 0"
  # over Node's 1 MiB default maxBuffer: scored (1 release in the window -> fail), not unknown/ENOBUFS
  row "$t/o.md" BS-4 | grep -E '\| fail \|.*1 releases' >/dev/null || fail "BS-4 should be scored from a >1 MiB reply: $(row "$t/o.md" BS-4)"
  echo "PASS: test_gh_large_payload_is_scored"
}

test_unexpected_shapes_and_fs_errors_are_unknown() {
  local t="$TMP/shape"; make_tree "$t"; make_gh_stub "$TMP/stub"
  export GH_LOG="$TMP/shape.log"; : > "$GH_LOG"
  mkdir -p "$t/beta"; echo '{"name":"beta"}' > "$t/beta/package.json"; echo 'not a dir' > "$t/beta/src"
  mkdir -p "$t/.github"; echo 'not a dir' > "$t/.github/workflows"
  local rc=0
  GH_STUB_SHAPE=object PATH="$TMP/stub:$PATH" node "$SCRIPT" --root "$t" --repo o/r --date 2026-10-10 --out "$t/o.md" 2>"$TMP/err" || rc=$?
  [ "$rc" -eq 0 ] || fail "unexpected shapes / fs errors should not crash, got $rc ($(head -c 300 "$TMP/err"))"
  for id in BS-4 TD-2 SEC-1; do
    row "$t/o.md" "$id" | grep -E '\| unknown \|.*unexpected response shape' >/dev/null || fail "$id should be unknown on a {} reply: $(row "$t/o.md" "$id")"
  done
  echo "PASS: test_unexpected_shapes_and_fs_errors_are_unknown"
}

test_malformed_package_json_fails() {
  local t="$TMP/badpkg"; make_tree "$t"
  echo '{bad' > "$t/alpha/package.json"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  for id in BS-1 BS-2; do
    row "$t/o.md" "$id" | grep -E 'fail: .*alpha \(package.json is not valid JSON\)' >/dev/null || fail "$id should fail alpha on bad JSON: $(row "$t/o.md" "$id")"
  done
  echo "PASS: test_malformed_package_json_fails"
}

test_runtime_pin_flips_bs5() {
  local t="$TMP/pin"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/a.md"
  row "$t/a.md" BS-5 | grep -E '\| fail \|.*no \.nvmrc' >/dev/null || fail "BS-5 should fail without a runtime pin: $(row "$t/a.md" BS-5)"
  echo "20.18.0" > "$t/.nvmrc"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/b.md"
  [ "$(row_state "$t/b.md" BS-5)" = "pass" ] || fail "BS-5 should pass with .nvmrc"
  echo "PASS: test_runtime_pin_flips_bs5"
}

test_dependency_bot_flips_sec4() {
  local t="$TMP/bot"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/a.md"
  [ "$(row_state "$t/a.md" SEC-4)" = "fail" ] || fail "SEC-4 should fail without a dependency bot config"
  mkdir -p "$t/.github"; echo 'version: 2' > "$t/.github/dependabot.yml"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/b.md"
  [ "$(row_state "$t/b.md" SEC-4)" = "pass" ] || fail "SEC-4 should pass with .github/dependabot.yml"
  echo "PASS: test_dependency_bot_flips_sec4"
}

test_walk_sorted_and_skips_symlinks() {
  local t="$TMP/walk"; make_tree "$t"
  mkdir -p "$t/.github/workflows"
  echo 'name: z flaky quarantine' > "$t/.github/workflows/z.yml"
  echo 'name: a flaky quarantine' > "$t/.github/workflows/a.yml"
  mkdir "$t/.github/workflows/dir.yml"
  ln -s "$t/missing-target" "$t/alpha/src/broken-link.ts"
  ln -s "$t/alpha" "$t/alpha/src/cycle"
  local rc=0
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md" 2>"$TMP/err" || rc=$?
  [ "$rc" -eq 0 ] || fail "broken symlink / cycle / *.yml dir should not crash ($(head -c 300 "$TMP/err"))"
  row "$t/o.md" TS-5 | grep -F '.github/workflows/a.yml' >/dev/null || fail "TS-5 should name the first sorted workflow: $(row "$t/o.md" TS-5)"
  echo "PASS: test_walk_sorted_and_skips_symlinks"
}

test_unreadable_files_have_their_own_reason() {
  local t="$TMP/perm"; make_tree "$t"
  chmod 000 "$t/AGENTS.md" "$t/alpha/package.json"
  local rc=0
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md" 2>"$TMP/err" || rc=$?
  chmod 644 "$t/AGENTS.md" "$t/alpha/package.json"
  [ "$rc" -eq 0 ] || fail "unreadable files should not abort the run, got $rc ($(head -c 300 "$TMP/err"))"
  row "$t/o.md" DOC-1 | grep -E '\| unknown \|.*AGENTS.md is not readable' >/dev/null || fail "DOC-1 should be unknown: $(row "$t/o.md" DOC-1)"
  for id in BS-1 BS-2; do
    row "$t/o.md" "$id" | grep -E 'fail: .*alpha \(package.json is not readable' >/dev/null || fail "$id should say not readable: $(row "$t/o.md" "$id")"
    row "$t/o.md" "$id" | grep -F 'not valid JSON' >/dev/null && fail "$id should not claim bad JSON"
  done
  # only AGENTS.md unreadable: rows that read it name that cause, not "not documented"
  chmod 000 "$t/AGENTS.md"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/p.md" 2>/dev/null || true
  chmod 644 "$t/AGENTS.md"
  row "$t/p.md" BS-1 | grep -E 'fail: .*alpha \(AGENTS.md is not readable \(EACCES\)\)' >/dev/null || fail "BS-1 should blame the unreadable AGENTS.md: $(row "$t/p.md" BS-1)"
  row "$t/p.md" BS-3 | grep -F 'AGENTS.md is not readable (EACCES)' >/dev/null || fail "BS-3 should blame the unreadable AGENTS.md: $(row "$t/p.md" BS-3)"
  echo "PASS: test_unreadable_files_have_their_own_reason"
}

test_bs1_needs_the_commands_block() {
  local t="$TMP/bs1"; make_tree "$t"
  # alpha named only in a directory tree; a build mention elsewhere; no Commands block -> not documented
  printf '# Agents\n\n```\nalpha/   # the alpha CLI\n```\n\nRun npm run build in a package.\n' > "$t/AGENTS.md"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/a.md"
  row "$t/a.md" BS-1 | grep -E 'fail: .*alpha \(scripts.build not documented' >/dev/null || fail "BS-1 should not credit a tree mention: $(row "$t/a.md" BS-1)"
  # the package line sits in the Commands block under a comment that names build -> documented
  printf '# Agents\n\n## Commands\n\n```bash\n# TypeScript packages (each has test, typecheck, build)\ncd alpha && npm test\n```\n' > "$t/AGENTS.md"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/b.md"
  row "$t/b.md" BS-1 | grep -E 'pass: alpha \(heuristic: ' >/dev/null || fail "BS-1 should credit the Commands block: $(row "$t/b.md" BS-1)"
  echo "PASS: test_bs1_needs_the_commands_block"
}

test_sv2_reads_compiler_options() {
  local t="$TMP/sv2"; make_tree "$t"
  # strict only in a comment and in a nested object -> not strict
  printf '{\n  // "strict": true,\n  "compilerOptions": { "paths": { "x": ["src/**/*"] }, "plugins": [{ "strict": true }] },\n  "include": ["src/**/*"]\n}\n' > "$t/alpha/tsconfig.json"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/a.md"
  row "$t/a.md" SV-2 | grep -E 'fail: .*alpha \(' >/dev/null || fail "SV-2 should not credit a comment or nested key: $(row "$t/a.md" SV-2)"
  # strict inherited through a relative extends -> strict
  echo '{ "compilerOptions": { "strict": true, }, }' > "$t/alpha/tsconfig.base.json"
  printf '{\n  /* base */ "extends": "./tsconfig.base.json",\n  "compilerOptions": { "outDir": "dist" }\n}\n' > "$t/alpha/tsconfig.json"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/b.md"
  row "$t/b.md" SV-2 | grep -F 'pass: alpha (compilerOptions.strict via ./tsconfig.base.json)' >/dev/null || fail "SV-2 should follow extends: $(row "$t/b.md" SV-2)"
  echo "PASS: test_sv2_reads_compiler_options"
}

test_ts2_evidence_says_heuristic() {
  local t="$TMP/ts2"; make_tree "$t"
  echo '{"name":"alpha","scripts":{"build":"tsc","test":"vitest run","test:heavy":"vitest run heavy"}}' > "$t/alpha/package.json"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  row "$t/o.md" TS-2 | grep -F 'alpha (heuristic: matched script test:heavy)' >/dev/null || fail "TS-2 evidence should say heuristic: $(row "$t/o.md" TS-2)"
  echo "PASS: test_ts2_evidence_says_heuristic"
}

test_non_github_origin_is_not_sent_to_gh() {
  local t="$TMP/gl"; make_tree "$t"; make_gh_stub "$TMP/stub"
  git -C "$t" init -q && git -C "$t" remote add origin https://gitlab.example.com/team/proj.git
  export GH_LOG="$TMP/gl.log"; : > "$GH_LOG"
  local rc=0
  PATH="$TMP/stub:$PATH" node "$SCRIPT" --root "$t" --date 2026-10-10 --out "$t/o.md" 2>"$TMP/err" || rc=$?
  [ "$rc" -eq 0 ] || fail "non-GitHub origin run should exit 0, got $rc"
  [ ! -s "$GH_LOG" ] || fail "gh should not be called for a non-GitHub origin: $(head -c 300 "$GH_LOG")"
  row "$t/o.md" SEC-1 | grep -E '\| unknown \|.*origin is not a GitHub URL \(gitlab.example.com\)' >/dev/null || fail "SEC-1 should say why: $(row "$t/o.md" SEC-1)"
  echo "PASS: test_non_github_origin_is_not_sent_to_gh"
}

test_outside_a_repo_prints_only_our_error() {
  mkdir -p "$TMP/norepo"
  local rc=0
  (cd "$TMP/norepo" && GIT_CEILING_DIRECTORIES="$TMP" node "$SCRIPT" --no-remote >/dev/null 2>"$TMP/err") || rc=$?
  [ "$rc" -eq 2 ] || fail "outside a repo should exit 2, got $rc"
  [ "$(wc -l < "$TMP/err" | tr -d ' ')" = "1" ] || fail "stderr should be one line: $(head -c 300 "$TMP/err")"
  grep -q '^readiness-baseline: not in a git repo' "$TMP/err" || fail "stderr should be our message: $(head -c 300 "$TMP/err")"
  echo "PASS: test_outside_a_repo_prints_only_our_error"
}

test_codeowners_flips_sec3
test_env_template_flips_de2
test_no_remote_marks_remote_unknown
test_deterministic_for_fixed_date
test_app_fraction_counts_present_apps
test_bad_root_exits_2
test_only_read_only_gh_calls
test_level_walk
test_unscored_pillar_and_skipped_cells_render
test_pe_scored_against_w9_page
test_app_na_leaves_denominator
test_arg_errors_exit_2
test_gh_stub_states_and_get_only
test_gh_timeout_is_unknown
test_walk_sorted_and_skips_symlinks
test_gh_large_payload_is_scored
test_unexpected_shapes_and_fs_errors_are_unknown
test_malformed_package_json_fails
test_runtime_pin_flips_bs5
test_dependency_bot_flips_sec4
test_unreadable_files_have_their_own_reason
test_bs1_needs_the_commands_block
test_sv2_reads_compiler_options
test_ts2_evidence_says_heuristic
test_non_github_origin_is_not_sent_to_gh
test_outside_a_repo_prints_only_our_error
