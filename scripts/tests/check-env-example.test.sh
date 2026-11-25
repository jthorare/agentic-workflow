#!/usr/bin/env bash
# Tests for scripts/check-env-example.sh using temp-dir fixture trees.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

SCRIPT="$(cd "$(dirname "$0")/.." && pwd)/check-env-example.sh"
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-env-example-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# run <dir> → sets OUT and RC
run() { set +e; OUT="$(bash "$SCRIPT" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -E -- "$2"; then ok "$1"; else fail "$1: output lacks /$2/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_no_out() { if printf '%s\n' "$OUT" | has -E -- "$2"; then fail "$1: output has /$2/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; else ok "$1"; fi; }

# A minimal compliant tree: one read per language, each documented.
fixture() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/judge/src" "$d/src/Aw.Demo" "$d/config/hooks" "$d/scripts"
  printf 'export const dir = process.env.AW_STATE_DIR ?? "x";\nconst p = process.env["AW_PORT"];\n' > "$d/judge/src/config.ts"
  printf 'export function key(deps: { env: NodeJS.ProcessEnv }) { return deps.env.AW_KEY_NAME; }\n' > "$d/judge/src/keys.ts"
  printf 'var v = Environment.GetEnvironmentVariable("AW_CS_VAR");\nvar w = environment("AW_CS_LAMBDA");\n' > "$d/src/Aw.Demo/Settings.cs"
  printf '#!/usr/bin/env bash\nDIR="${AW_HOOK_DIR:-/tmp}"\necho "${HOME:-}"\n' > "$d/config/hooks/demo.sh"
  printf 'HOME # set by the OS\n' > "$d/scripts/env-scan-ignore.txt"
  cat > "$d/.env.example" <<'EOF'
# State root.
# AW_STATE_DIR=~/.agentic-workflow
# Port. test-ports: ok: an env value in a fixture .env.example, nothing binds it
AW_PORT=3100
# Key name.
# AW_KEY_NAME=
# C# setting.
# AW_CS_VAR=
# C# lambda read.
# AW_CS_LAMBDA=
# Hook dir.
# AW_HOOK_DIR=
EOF
  printf '%s' "$d"
}

echo "check-env-example"

d="$(fixture clean)"; run "$d"
expect_rc "compliant tree passes" 0
expect_out "reports how many names it scanned" 'ENV-EXAMPLE: [0-9]+ names read, all documented'

d="$(fixture missing-ts)"; printf 'const x = process.env.AW_T10_PROBE;\n' >> "$d/judge/src/config.ts"; run "$d"
expect_rc "undocumented TS read fails" 1
expect_out "names rule, var and file:line" 'ENV-EXAMPLE-001: AW_T10_PROBE .*judge/src/config.ts:3'
expect_out "prints the fix" 'fix: add'

d="$(fixture missing-cs)"; printf 'var z = Environment.GetEnvironmentVariable("AW_T10_CS");\n' >> "$d/src/Aw.Demo/Settings.cs"; run "$d"
expect_rc "undocumented C# read fails" 1
expect_out "names the C# var" 'ENV-EXAMPLE-001: AW_T10_CS .*src/Aw.Demo/Settings.cs:3'

d="$(fixture missing-sh)"; printf 'X="${AW_T10_SH:-x}"\n' >> "$d/config/hooks/demo.sh"; run "$d"
expect_rc "undocumented shell default read fails" 1
expect_out "names the shell var" 'ENV-EXAMPLE-001: AW_T10_SH .*config/hooks/demo.sh:4'

d="$(fixture documented)"; printf 'const x = process.env.AW_T10_PROBE;\n' >> "$d/judge/src/config.ts"
printf '# Probe.\n# AW_T10_PROBE=\n' >> "$d/.env.example"; run "$d"
expect_rc "documenting the read fixes it" 0

d="$(fixture stale)"; printf '# Nobody reads this.\n# AW_T10_STALE=\n' >> "$d/.env.example"; run "$d"
expect_rc "a documented name nobody reads fails" 1
expect_out "stale rule id" 'ENV-EXAMPLE-002: AW_T10_STALE'

d="$(fixture indirect)"; printf '# Read through a configured name (indirect).\n# AW_T10_INDIRECT=\n' >> "$d/.env.example"; run "$d"
expect_rc "an entry marked (indirect) is not stale" 0

d="$(fixture indirect-after)"; printf '\n# Thing.\n# AW_T10_BLOCK=\n# (indirect: read by a tool)\n' >> "$d/.env.example"; run "$d"
expect_rc "an (indirect marker anywhere in the block covers every entry in it" 0

d="$(fixture tests-skipped)"; mkdir -p "$d/judge/tests" "$d/config/tests"
printf 'process.env.AW_ONLY_IN_TEST = "1";\n' > "$d/judge/tests/a.test.ts"
printf 'process.env.AW_ONLY_IN_TEST2 = "1";\n' > "$d/judge/src/b.test.ts"
printf 'X="${AW_ONLY_IN_TEST3:-}"\n' > "$d/config/tests/c.test.sh"; run "$d"
expect_rc "reads in test files are not scanned" 0

d="$(fixture node-modules)"; mkdir -p "$d/judge/node_modules/pkg" "$d/judge/dist"
printf 'process.env.AW_VENDOR;\n' > "$d/judge/node_modules/pkg/i.js"
printf 'process.env.AW_BUILT;\n' > "$d/judge/dist/i.js"; run "$d"
expect_rc "node_modules and dist are not scanned" 0

d="$(fixture ignore-no-reason)"; printf 'PATH\n' >> "$d/scripts/env-scan-ignore.txt"; run "$d"
expect_rc "an ignore entry without a reason fails" 1
expect_out "ignore rule id" 'ENV-EXAMPLE-003: PATH'

d="$(fixture ignore-dup-no-reason)"; printf 'HOME\n' >> "$d/scripts/env-scan-ignore.txt"; run "$d"
expect_rc "a reasonless duplicate of a reasoned ignore entry still fails" 1
expect_out "names the reasonless entry" 'ENV-EXAMPLE-003: HOME .*no reason'

d="$(fixture problems-first)"; printf 'const x = process.env.AW_T10_PROBE;\n' >> "$d/judge/src/config.ts"; run "$d"
expect_out "the summary line comes last" '^ENV-EXAMPLE: 1 problem\(s\); [0-9]+ names read$'
if [ "$(printf '%s\n' "$OUT" | tail -1 | cut -c1-12)" = "ENV-EXAMPLE:" ]; then ok "summary is the last line"; else fail "summary is the last line"; fi

d="$(fixture ignore-and-doc)"; printf 'AW_PORT # also documented\n' >> "$d/scripts/env-scan-ignore.txt"; run "$d"
expect_rc "a name both ignored and documented fails" 1
expect_out "conflict rule id" 'ENV-EXAMPLE-003: AW_PORT'

d="$(fixture secret-value)"; printf '# A key.\nAW_T10_API_KEY=abc123\n' >> "$d/.env.example"
printf 'const k = process.env.AW_T10_API_KEY;\n' >> "$d/judge/src/config.ts"; run "$d"
expect_rc "a value on a secret-named entry fails" 1
expect_out "secret rule id" 'ENV-EXAMPLE-004: AW_T10_API_KEY'
expect_no_out "never prints the value" 'abc123'

d="$(fixture lowercase)"; printf 'for f in "${files[@]:-}"; do :; done\necho "${foo:-bar}"\n' >> "$d/config/hooks/demo.sh"; run "$d"
expect_rc "lowercase shell locals are not env reads" 0

d="$(fixture tsx-env-get)"; mkdir -p "$d/mods/demo"; printf "const b = await \$.env.get('AW_T10_TSX');\n" > "$d/mods/demo/register.tsx"; run "$d"
expect_rc "a .tsx env.get('NAME') read is scanned" 1
expect_out "names the .tsx read" 'ENV-EXAMPLE-001: AW_T10_TSX .*mods/demo/register.tsx:1'

d="$(fixture mjs-single-quote)"; printf "const v = deps.env['AW_T10_SQ'];\n" > "$d/scripts/x.mjs"; run "$d"
expect_out "a single-quoted bracket read in .mjs is scanned" 'ENV-EXAMPLE-001: AW_T10_SQ .*scripts/x.mjs:1'

d="$(fixture shell-forms)"; printf 'A="${AW_T10_ASSIGN:=x}"\nB="${AW_T10_PLUS:+y}"\nC="${AW_T10_DASH-z}"\n' >> "$d/config/hooks/demo.sh"; run "$d"
expect_out "shell := form is scanned" 'ENV-EXAMPLE-001: AW_T10_ASSIGN'
expect_out "shell :+ form is scanned" 'ENV-EXAMPLE-001: AW_T10_PLUS'
expect_out "shell - form is scanned" 'ENV-EXAMPLE-001: AW_T10_DASH'

d="$(fixture first-line)"; for i in 1 2 3 4 5 6 7 8; do printf '// filler\n' >> "$d/judge/src/lines.ts"; done
printf 'const a = process.env.AW_T10_LINE;\n' >> "$d/judge/src/lines.ts"
printf 'const b = process.env.AW_T10_LINE;\n' >> "$d/judge/src/lines.ts"; run "$d"
expect_out "reports the first read by line number, not text order" 'ENV-EXAMPLE-001: AW_T10_LINE .*judge/src/lines.ts:9 '

d="$(fixture no-env-example)"; unlink "$d/.env.example"; run "$d"
expect_rc "a missing .env.example is a tool error" 2

run "$REPO"
expect_rc "the real repo passes" 0

echo
echo "check-env-example: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
