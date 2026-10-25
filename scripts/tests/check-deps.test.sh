#!/usr/bin/env bash
# Tests for scripts/check-deps.mjs using temp copies of a fixture repo.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
CHECKER="$HERE/../check-deps.mjs"
BASE="$HERE/fixtures/check-deps/base"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-deps-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# fixture <name> — a fresh copy of the base fixture repo; prints its path
fixture() { local d="$TMPROOT/$1"; cp -R "$BASE" "$d"; echo "$d"; }

# run <dir> [args...] — sets OUT (stdout+stderr) and CODE. The date is pinned (--today), so the
# exception fixtures' dates never expire under the suite.
TODAY="2026-10-10"
run() { local d="$1"; shift; set +e; OUT="$(node "$CHECKER" --root "$d" --today "$TODAY" "$@" 2>&1)"; CODE=$?; set -e; }

expect() { # expect <name> <code> <substring>...
  local name="$1" code="$2"; shift 2
  local good=1 s
  [ "$CODE" = "$code" ] || good=0
  for s in "$@"; do case "$OUT" in *"$s"*) ;; *) good=0 ;; esac; done
  if [ "$good" = 1 ]; then ok "$name"; else fail "$name (exit $CODE, want $code)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
}

refuse() { # refuse <name> <substring> — OUT must not contain it
  case "$OUT" in *"$2"*) fail "$1"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;; *) ok "$1" ;; esac
}

edge() { # edge <dir> <from> <to> <kind> <status> — append an edge
  local tmp="$1/deps.yaml.new"
  awk -v f="$2" -v t="$3" -v k="$4" -v s="$5" '
    /^exceptions:/ && !done { printf "  - from: %s\n    to: %s\n    kind: %s\n    status: %s\n    note: added by test\n", f, t, k, s; done=1 }
    { print }' "$1/deps.yaml" > "$tmp" && mv "$tmp" "$1/deps.yaml"
}

csproj() { # csproj <dir> <relpath> [ref-relpath...]
  local d="$1" p="$2"; shift 2
  mkdir -p "$d/$(dirname "$p")"
  { echo '<Project Sdk="Microsoft.NET.Sdk">'; echo '  <ItemGroup>'
    for r in "$@"; do echo "    <ProjectReference Include=\"$r\" />"; done
    echo '  </ItemGroup>'; echo '</Project>'; } > "$d/$p"
}

exc() { # exc <dir> <rule> <subject> <recorded> <expires> — record one exception
  perl -pi -e "s/^exceptions: \[\]\n/exceptions:\n  - rule: $2\n    subject: $3\n    reason: test\n    recorded: $4\n    expires: $5\n/" "$1/deps.yaml"
}

over() { # over <name> — Aw.Beta.Features holds 2 in-repo references under limits.projectFanOut 1 (DEPS-FANOUT-002); prints the path
  local d; d="$(fixture "$1")"; perl -pi -e 's/^  projectFanOut: 4\n/  projectFanOut: 1\n/' "$d/deps.yaml"
  perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"; edge "$d" Beta Alpha project current
  csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
  csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
  echo "$d"
}

echo "check-deps.mjs"

d="$(fixture clean)"; run "$d"
expect "clean fixture passes" 0 "deps: ok" "no C# projects yet — skipped, ok"
refuse "test files and comments are not scanned" "Delta"

d="$(fixture undeclared-cli)"
printf 'import { execFile } from "node:child_process";\nexecFile("beta", ["x"]);\n' > "$d/alpha/src/call.ts"
run "$d"
expect "undeclared cli edge fails with rule, measured value, limit and fix" 1 \
  "DEPS-EDGE-001" "Alpha -> Beta (cli)" "alpha/src/call.ts:2" "limit:" "fix:"

d="$(fixture undeclared-shell)"
printf '#!/usr/bin/env bash\nbeta report --json\n' > "$d/hooks/other.sh"
run "$d"
expect "undeclared cli edge from a shell script fails" 1 "DEPS-EDGE-001" "Hooks -> Beta (cli)" "hooks/other.sh:2"

d="$(fixture bin-helper)"
printf '#!/usr/bin/env bash\nbin="$(find_bin beta)" || exit 0\n' > "$d/hooks/status.sh"
run "$d"
expect "a *_bin helper naming another package's binary is a cli edge" 1 "DEPS-EDGE-001" "Hooks -> Beta (cli)" "hooks/status.sh:2"

d="$(fixture scan-file)"
perl -pi -e 's/^    scan: \[hooks\]\n/    scan: [hooks, statusline.sh]\n/' "$d/deps.yaml"
printf '#!/usr/bin/env bash\nbeta live --json\n' > "$d/statusline.sh"
run "$d"
expect "a single file in scan is scanned" 1 "DEPS-EDGE-001" "Hooks -> Beta (cli)" "statusline.sh:2"

d="$(fixture undeclared-file)"
printf 'const o = path.join(stateDir, "alpha", "outbox");\n' > "$d/delta/src/read.ts"
run "$d"
expect "undeclared file edge fails" 1 "DEPS-EDGE-001" "Delta -> Alpha (file)" "delta/src/read.ts:1"

d="$(fixture default-bin)"
printf 'export function realDeps(alphaBin = "alpha") { return alphaBin; }\n' > "$d/delta/src/deps.ts"
run "$d"
expect "a *Bin default naming another package's binary is a cli edge" 1 "DEPS-EDGE-001" "Delta -> Alpha (cli)"

d="$(fixture stale)"
printf 'export const dbPath = (s: string) => path.join(s, "alpha", "a.sqlite");\n' > "$d/beta/src/run.ts"
run "$d"
expect "declared current edge that the scan no longer finds is stale" 1 "DEPS-STALE-001" "Beta -> Alpha (cli)" "status: planned"

d="$(fixture cycle)"; edge "$d" Alpha Beta cli planned; run "$d"
expect "cycle fails with its path" 1 "DEPS-CYCLE-001" "Alpha -> Beta -> Alpha"

d="$(fixture same-tier)"; edge "$d" Beta Delta project planned; run "$d"
expect "same-tier project edge fails" 1 "DEPS-TIER-001" "Beta (tier 2) -> Delta (tier 2)"

d="$(fixture upward-db)"; edge "$d" Kernel Alpha db planned; run "$d"
expect "db edge to a higher tier fails" 1 "DEPS-TIER-002" "Kernel (tier 0) -> Alpha (tier 1)"

d="$(fixture same-tier-cli)"; edge "$d" Delta Beta cli planned; run "$d"
expect "same-tier cli edge is allowed (a contract, §4b.1a)" 0 "deps: ok"

d="$(fixture project-from-shell)"; edge "$d" Hooks Kernel project planned; run "$d"
expect "project edge from a shell node fails" 1 "DEPS-KIND-001" "Hooks -> Kernel (project)"

d="$(fixture file-into-shell)"; edge "$d" Beta Hooks file planned; run "$d"
expect "any edge into a shell node fails" 1 "DEPS-KIND-002" "Beta -> Hooks (file)"

d="$(fixture unknown)"; edge "$d" Beta Zeta cli planned; run "$d"
expect "unknown package fails" 1 "DEPS-REF-001" "Zeta"

d="$(fixture schema)"; perl -pi -e 's/^    tier: 2\n/    tier: 7\n/' "$d/deps.yaml"; run "$d"
expect "schema violation fails with its path" 1 "DEPS-SCHEMA-001" "/packages/2/tier"

d="$(fixture schema-required)"; perl -ni -e 'print unless /projectPrefix: Aw.Delta/' "$d/deps.yaml"; run "$d"
expect "package kind requires projectPrefix (if/then)" 1 "DEPS-SCHEMA-001" "/packages/3" "projectPrefix"

d="$(fixture yaml)"; perl -pi -e 's/^  - id: Alpha\n/  - &a id: Alpha\n/' "$d/deps.yaml"; run "$d"
expect "YAML outside the subset fails" 1 "DEPS-YAML-001" "anchor"

d="$(fixture yaml-dup)"; perl -pi -e 's/^    note: delta\n/    note: delta\n    note: again\n/' "$d/deps.yaml"; run "$d"
expect "duplicate key fails" 1 "DEPS-YAML-001" "duplicate key"

# The per-package cap (DEPS-FANOUT-001) was removed by the owner, 2026-10-12: a package may declare project edges
# to any number of lower-tier packages; only the per-project limits remain.
d="$(fixture fanout-wide)"
for t in P1 P2 P3 P4; do perl -0pi -e "s/(  - id: Beta\n)/  - id: $t\n    kind: package\n    tier: 1\n    projectPrefix: Aw.$t\n    src: src\/$t\n    note: $t\n\$1/" "$d/deps.yaml"; done
for t in Alpha P1 P2 P3 P4; do edge "$d" Beta "$t" project planned; done; run "$d"
expect "a package with project edges to 6 packages passes" 0 "deps: ok"
refuse "  ... with no package fan-out rule" "DEPS-FANOUT-001"

d="$(over fanout-exc)"; exc "$d" DEPS-FANOUT-002 Beta 2026-10-10 2027-01-01; run "$d"
expect "an unexpired exception silences fan-out" 0 "deps: ok" "exception DEPS-FANOUT-002 Beta (expires 2027-01-01)"

d="$(over fanout-expired)"; exc "$d" DEPS-FANOUT-002 Beta 2020-01-01 2020-06-01; run "$d"
expect "an expired exception fails and no longer silences" 1 "DEPS-EXC-001" "DEPS-FANOUT-002" "Aw.Beta.Features references 2 in-repo projects"

d="$(fixture proj-undeclared)"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Delta/Aw.Delta.Core/Aw.Delta.Core.csproj
csproj "$d" src/Delta/Aw.Delta.Core/Aw.Delta.Core.csproj
run "$d"
expect "undeclared ProjectReference fails once src/ has projects" 1 "DEPS-PROJ-001" "Aw.Beta.Core -> Aw.Delta.Core"

d="$(fixture proj-declared)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Aw.Kernel/Aw.Kernel.csproj
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj
csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../Aw.Beta.Core/Aw.Beta.Core.csproj
run "$d"
expect "declared and same-package ProjectReferences pass" 0 "deps: ok" "checked 3 C# projects"

d="$(fixture proj-unowned)"; csproj "$d" src/Other/Aw.Other.csproj; run "$d"
expect "a C# project no package owns fails" 1 "DEPS-PROJ-002" "Aw.Other"

d="$(fixture mermaid)"; run "$d" --mermaid
expect "--mermaid prints a graph and the edge list" 0 '```mermaid' "flowchart TB" "Beta -->|project| Kernel" "Beta -.->|cli| Alpha" "edges (5):"

d="$(fixture mermaid-diff)"; cp "$d/deps.yaml" "$TMPROOT/base-deps.yaml"; edge "$d" Delta Beta cli planned; run "$d" --mermaid --base-file "$TMPROOT/base-deps.yaml"
expect "--base-file lists added and removed edges" 0 "Added edges (1):" "+ Delta -> Beta (cli, planned)" "Removed edges (0):"

d="$TMPROOT/empty"; mkdir -p "$d"; run "$d"
expect "missing deps.yaml is a tool error (exit 2)" 2 "deps.yaml not found"

run "$BASE" --bogus
expect "unknown flag is a tool error (exit 2)" 2 "unknown argument"

# ---- review run 1 (PR #92): scan shapes ----
d="$(fixture ts-shapes)"
cat > "$d/delta/src/calls.ts" <<'TS'
import { execSync, execFile } from "node:child_process";
export const a = () => execSync("alpha report --json");
export const b = (x: string) => execSync(`alpha report ${x}`);
export const c = () => execFileAsync("alpha", ["x"]);
export const d = () => execFile(
  "alpha",
  ["x"],
);
const cmd = process.env.ALPHA_BIN ?? "alpha";
export const e = () => execFile(cmd, ["health"]);
TS
run "$d"
expect "exec strings, *Async, wrapped calls and env fallbacks are cli edges" 1 \
  "Delta -> Alpha (cli): delta/src/calls.ts:2" "1 undeclared"

d="$(fixture ts-each)"
for shape in 'execSync(`alpha report ${x}`);' 'await execFileAsync("alpha", []);' $'execFile(\n  "alpha",\n  [],\n);' 'const c = process.env.ALPHA_BIN || "alpha";' 'spawn(process.env.ALPHA_BIN!, []);'; do
  printf '%s\n' "$shape" > "$d/delta/src/calls.ts"; run "$d"
  expect "ts shape is a cli edge: $(printf '%s' "$shape" | head -1)" 1 "Delta -> Alpha (cli)"
done

d="$(fixture ts-comments)"
printf 'export const x = 1; // execFile("alpha", [])\n/* spawn("alpha")\n   execSync("alpha x") */\nconst s = "see execFile(\\"alpha\\") in docs";\n' > "$d/delta/src/calls.ts"
run "$d"
expect "calls inside comments are not edges" 0 "deps: ok"

d="$(fixture sh-shapes)"
for shape in 'FOO=1 beta x' 'env FOO=1 beta x' 'sudo beta x' 'nohup beta x &' 'printf x | xargs beta' '! beta x' 'out=`beta x`' '"$DIR/beta" x' '$HOME/.local/bin/beta x' '"$BETA_BIN" x' 'which beta' 'exec beta x'; do
  printf '#!/usr/bin/env bash\n%s\n' "$shape" > "$d/hooks/other.sh"; run "$d"
  expect "shell shape is a cli edge: $shape" 1 "Hooks -> Beta (cli)"
done

d="$(fixture sh-prose)"
printf '#!/usr/bin/env bash\necho "then beta first"\nprintf "%%s\\n" "do beta"\ncat <<EOF\n  beta doctor\nEOF\ncat <<'"'"'X'"'"'\nbeta report\nX\nlog "if beta fails"\n' > "$d/hooks/other.sh"
run "$d"
expect "echo, printf, log lines and heredoc bodies are not launches" 0 "deps: ok"

d="$(fixture binpath)"; perl -pi -e 's/^    bins: \[beta\]\n/    bins: [beta]\n    binPaths: [beta\/dist\/bin.js]\n/' "$d/deps.yaml"
printf 'const p = path.join(root, "beta", "dist", "bin.js");\n' > "$d/delta/src/run.ts"; run "$d"
expect "a binPaths entry is a cli edge" 1 "Delta -> Beta (cli)" "launches beta/dist/bin.js"

d="$(fixture walk-skip)"; mkdir -p "$d/delta/src/node_modules/x" "$d/delta/src/dist"
printf 'execFile("alpha", []);\n' > "$d/delta/src/node_modules/x/i.ts"; cp "$d/delta/src/node_modules/x/i.ts" "$d/delta/src/dist/i.js"
printf '#!/usr/bin/env bash\nbeta x\n' > "$d/hooks/gate.test.sh"; run "$d"
expect "node_modules, dist and *.test.sh are not scanned" 0 "deps: ok"

# ---- namespaces ----
d="$(fixture ns-hook)"; printf '#!/usr/bin/env bash\nT="$AW_STATE_DIR/alpha/names.jsonl"\n' > "$d/hooks/names.sh"; run "$d"
expect "a hook using an unlisted file in alpha's namespace is the declared file edge" 0 "deps: ok"
d="$(fixture ns-ts)"; printf 'const c = path.join(stateDir, "alpha", "config.json");\n' > "$d/delta/src/cfg.ts"; run "$d"
expect "any file in another package's namespace is a file edge" 1 "Delta -> Alpha (file): delta/src/cfg.ts:1" "uses alpha/ (namespace)"
d="$(fixture ns-words)"; printf 'const words = ["alpha", "beta"];\nconst k = z.enum(["alpha", "test"]);\nconst p = path.join(root, "alpha", "outbox2");\n' > "$d/delta/src/words.ts"; run "$d"
expect "a namespace name in a word list is not a path; in a join it is" 1 "Delta -> Alpha (file): delta/src/words.ts:3"
d="$(fixture ns-db)"; run "$d"
refuse "a db path does not also count as a namespace file edge" "Beta -> Alpha (file)"

# ---- status both ways ----
d="$(fixture planned-found)"; edge "$d" Delta Alpha cli planned; printf 'execFile("alpha", []);\n' > "$d/delta/src/run.ts"; run "$d"
expect "a planned edge the scan finds must become current" 1 "DEPS-STALE-002" "Delta -> Alpha (cli)" "status: current"
d="$(fixture stale-db)"; printf 'export const ask = () => execFile("alpha", []);\n' > "$d/beta/src/run.ts"; run "$d"
expect "a current db edge the scan no longer finds is stale" 1 "DEPS-STALE-001" "Beta -> Alpha (db)"
d="$(fixture stale-file)"; perl -ni -e 'print unless /alpha\/outbox/' "$d/hooks/gate.sh"; run "$d"
expect "a current file edge the scan no longer finds is stale" 1 "DEPS-STALE-001" "Hooks -> Alpha (file)"
d="$(fixture proj-planned-real)"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; run "$d"
expect "a planned project edge with a real reference must become current" 1 "DEPS-STALE-002" "Beta -> Kernel (project)"
d="$(fixture proj-current-missing)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj; csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; run "$d"
expect "a current project edge without a reference is stale" 1 "DEPS-STALE-001" "Beta -> Kernel (project)"
d="$(fixture proj-current-noproj)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"; run "$d"
expect "a current project edge is stale while its package has no projects" 1 "DEPS-STALE-001" "Beta -> Kernel (project)"

# ---- tiers and sublevels ----
d="$(fixture sublevel)"; perl -pi -e 's/^    projectPrefix: Aw.Delta\n/    projectPrefix: Aw.Delta\n    sublevel: 1\n/' "$d/deps.yaml"; edge "$d" Delta Beta project planned; run "$d"
expect "sublevel outside tier 0 fails" 1 "DEPS-SCHEMA-001" "/packages/3"
refuse "sublevel outside tier 0 does not unlock a same-tier project edge" "deps: ok"

# ---- references and invariants ----
d="$(fixture dup-id)"; perl -0pi -e 's/  - id: Delta\n/  - id: Beta\n/' "$d/deps.yaml"; run "$d"
expect "duplicate package id fails" 1 "DEPS-REF-002" "Beta"
d="$(fixture dup-edge)"; edge "$d" Beta Alpha cli current; run "$d"
expect "duplicate edge fails" 1 "DEPS-REF-003" "Beta -> Alpha (cli)"
d="$(fixture dup-prefix)"; perl -pi -e 's/projectPrefix: Aw.Delta/projectPrefix: Aw.Beta/' "$d/deps.yaml"; run "$d"
expect "two packages with one projectPrefix fails" 1 "DEPS-REF-004" "Aw.Beta"
d="$(fixture exc-unknown)"; exc "$d" DEPS-FANOUT-002 Zeta 2026-10-10 2026-12-01; run "$d"
expect "an exception for an unknown package fails" 1 "DEPS-REF-001" "Zeta"
d="$(fixture self-loop)"; edge "$d" Delta Delta cli planned; run "$d"
expect "a self-loop is a cycle" 1 "DEPS-CYCLE-001" "Delta -> Delta"
d="$(fixture cycle3)"; edge "$d" Alpha Delta cli planned; edge "$d" Delta Beta cli planned; run "$d"
expect "a three-node cycle reports its path" 1 "DEPS-CYCLE-001" "Alpha -> Delta -> Beta -> Alpha"

# ---- exceptions ----
d="$(fixture exc-unused)"; exc "$d" DEPS-FANOUT-002 Beta 2026-10-10 2026-12-01; run "$d"
expect "an exception that suppresses nothing fails" 1 "DEPS-EXC-002" "DEPS-FANOUT-002 for Beta"
d="$(fixture exc-rule)"; perl -pi -e 's/^exceptions: \[\]\n/exceptions:\n  - rule: DEPS-TIER-001\n    subject: Beta\n    reason: test\n    recorded: 2026-10-10\n    expires: 2026-12-01\n/' "$d/deps.yaml"; run "$d"
expect "an exception for a rule that takes none fails the schema" 1 "DEPS-SCHEMA-001" "/exceptions/0/rule"
for dates in "2026-13-45 2026-12-01" "2026-12-01 2026-11-01" "2026-10-10 2027-10-10"; do
  set -- $dates; d="$(over "exc-dates-$1-$2")"; exc "$d" DEPS-FANOUT-002 Beta "$1" "$2"; run "$d"
  expect "bad exception dates fail (recorded $1, expires $2)" 1 "DEPS-EXC-003"
done

# ---- schema: consumer nodes ----
d="$(fixture shell-files)"; perl -pi -e 's/^    scan: \[hooks\]\n/    scan: [hooks]\n    files: [hooks-state]\n/' "$d/deps.yaml"; run "$d"
expect "a shell node cannot own bins, db or files" 1 "DEPS-SCHEMA-001" "/packages/4"

# ---- C# projects ----
d="$(fixture csproj-variants)"
mkdir -p "$d/src/Beta/Aw.Beta.Core" "$d/src/Delta/Aw.Delta.Core" "$d/src/Beta/tests/Aw.Beta.Tests" "$d/tests/Aw.Delta.Tests"
printf '<Project>\n  <ItemGroup>\n    <ProjectReference Condition="true" Include="..\\..\\Delta\\Aw.Delta.Core\\Aw.Delta.Core.csproj" />\n  </ItemGroup>\n</Project>\n' > "$d/src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj"
printf '<Project />\n' > "$d/src/Delta/Aw.Delta.Core/Aw.Delta.Core.csproj"
run "$d"
expect "Include after another attribute, with backslashes, is read" 1 "DEPS-PROJ-001" "Aw.Beta.Core -> Aw.Delta.Core"
printf "<Project><ItemGroup><ProjectReference Include='../../Delta/Aw.Delta.Core/Aw.Delta.Core.csproj'/></ItemGroup></Project>\n" > "$d/src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj"; run "$d"
expect "single-quoted Include is read" 1 "DEPS-PROJ-001" "Aw.Beta.Core -> Aw.Delta.Core"
printf '<Project><!-- <ProjectReference Include="../../Delta/Aw.Delta.Core/Aw.Delta.Core.csproj" /> --></Project>\n' > "$d/src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj"; run "$d"
expect "a commented-out ProjectReference is ignored" 0 "deps: ok"
printf '<Project><ItemGroup><ProjectReference Include="../../../Aw.Kernel/Aw.Kernel.csproj" /></ItemGroup></Project>\n' > "$d/src/Beta/tests/Aw.Beta.Tests/Aw.Beta.Tests.CSPROJ"; run "$d"
expect "projects under tests/ dirs and .CSPROJ are read; test projects may reference their owner's edges" 0 "checked 3 C# projects"
printf '<Project><ItemGroup><ProjectReference Include="../../src/Beta/tests/Aw.Beta.Tests/Aw.Beta.Tests.CSPROJ" /></ItemGroup></Project>\n' > "$d/tests/Aw.Delta.Tests/Aw.Delta.Tests.csproj"
printf '<Project><ItemGroup><ProjectReference Include="../../Beta/tests/Aw.Beta.Tests/Aw.Beta.Tests.CSPROJ" /></ItemGroup></Project>\n' > "$d/src/Delta/Aw.Delta.Core/Aw.Delta.Core.csproj"; run "$d"
expect "top-level tests/ is walked; nothing may reference a test project" 1 "DEPS-PROJ-003" "Aw.Delta.Core -> Aw.Beta.Tests" "checked 4 C# projects"
d="$(fixture csproj-dbp)"; csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj
printf '<Project><ItemGroup><ProjectReference Include="Aw.Kernel/Aw.Kernel.csproj" /></ItemGroup></Project>\n' > "$d/src/Directory.Build.props"; run "$d"
expect "a ProjectReference in Directory.Build.props fails" 1 "DEPS-PROJ-005" "src/Directory.Build.props"
d="$(fixture csproj-testing)"; perl -0pi -e 's/(  - id: Alpha\n)/  - id: KTest\n    kind: package\n    tier: 0\n    test: true\n    projectPrefix: Aw.KTest\n    src: src\/KTest\n    note: test support\n$1/' "$d/deps.yaml"
csproj "$d" src/KTest/Aw.KTest/Aw.KTest.csproj; csproj "$d" src/Beta/Aw.Beta.Tests/Aw.Beta.Tests.csproj ../../KTest/Aw.KTest/Aw.KTest.csproj; run "$d"
expect "a test project may reference a test-support package without a declared edge" 0 "deps: ok"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../KTest/Aw.KTest/Aw.KTest.csproj; run "$d"
expect "a production project referencing a test-support package fails" 1 "DEPS-PROJ-001" "Aw.Beta.Core -> Aw.KTest"

# ---- real per-project fan-out ----
d="$(fixture proj-fanout)"; perl -pi -e 's/^  projectFanOut: 4\n/  projectFanOut: 1\n/; s/thirdPartyFanOut: 6/thirdPartyFanOut: 1/' "$d/deps.yaml"
perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"; edge "$d" Beta Alpha project current
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj ../Aw.Beta.Core/Aw.Beta.Core.csproj
run "$d"
expect "a project over the in-repo fan-out fails; Host is exempt" 1 "DEPS-FANOUT-002" "Aw.Beta.Core references 2 in-repo projects"
refuse "Host projects are exempt from fan-out" "Aw.Beta.Host references"
printf '<Project><ItemGroup><PackageReference Include="A" /><PackageReference Include="B" /><PackageReference Include="An.Analyzer" PrivateAssets="all" /></ItemGroup></Project>\n' > "$d/src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj"; run "$d"
expect "a project over the third-party fan-out fails; analyzers excluded" 1 "DEPS-FANOUT-003" "Aw.Alpha.Core references 2 third-party packages (A, B)"

# ---- arguments ----
run "$BASE" --root
expect "--root without a value is a tool error" 2 "--root needs a value"
d="$(fixture args)"; run "$d" --mermaid --base-file
expect "--base-file without a value is a tool error" 2 "--base-file needs a value"
run "$d" --base-file "$d/deps.yaml"
expect "--base-file without --mermaid is a tool error" 2 "--base-file needs --mermaid"
run "$d" --mermaid --base-file "$TMPROOT/nope.yaml"
expect "a missing base file is a clean tool error" 2 "base file not found"
refuse "a missing base file prints no stack" "    at "
d="$(fixture mermaid-bad)"; perl -pi -e 's/^    tier: 2\n/    tier: 7\n/' "$d/deps.yaml"; run "$d" --mermaid
expect "--mermaid on an invalid deps.yaml skips the graph" 1 "DEPS-SCHEMA-001" "mermaid: skipped"

# ---- fix lines ----
d="$(fixture fixcmd)"; printf 'execFile("beta", []);\n' > "$d/alpha/src/call.ts"; run "$d"
expect "fix prints the YAML to paste and the rerun command" 1 "      - from: Alpha" "rerun: node scripts/check-deps.mjs"

# ---- review run 2 (PR #92) ----
d="$(over today)"; exc "$d" DEPS-FANOUT-002 Beta 2026-10-10 2026-12-01
TODAY=2026-11-30 run "$d"; expect "--today pins the date: the day before expiry passes" 0 "deps: ok"
TODAY=2026-12-02 run "$d"; expect "--today pins the date: the day after expiry fails" 1 "DEPS-EXC-001" "today 2026-12-02"
TODAY=2026-13-01 run "$d"; expect "--today must be a calendar date" 2 "--today needs a YYYY-MM-DD date"

d="$(fixture regex-backtick)"
printf 'export const clean = (s: string) => s.replace(/[`*]/g, "");\nexecFile("beta", ["x"]);\nexport const t = `later ${1}`;\n' > "$d/alpha/src/re.ts"; run "$d"
expect "a backtick inside a regex literal does not hide a later call" 1 "Alpha -> Beta (cli): alpha/src/re.ts:2"
d="$(fixture regex-slashes)"
printf 'const url = /https?:\\/\\//;\nconst x = a / b / c;\nexecFile("beta", ["x"]);\n' > "$d/alpha/src/re.ts"; run "$d"
expect "a regex with \\/\\/ is not a line comment, and division still works" 1 "Alpha -> Beta (cli): alpha/src/re.ts:3"
d="$(fixture regex-quote)"
printf 'const q = /["'"'"']/;\nconst r = (x) => x.split(/,/);\nexecFile("beta", ["x"]);\n' > "$d/alpha/src/re.ts"; run "$d"
expect "quotes inside a regex do not open strings" 1 "Alpha -> Beta (cli): alpha/src/re.ts:3"
d="$(fixture lex-open-template)"; printf 'const t = `never closed\nexecFile("beta", []);\n' > "$d/alpha/src/bad.ts"; run "$d"
expect "an unterminated template literal fails closed" 2 "alpha/src/bad.ts" "unterminated template"
d="$(fixture lex-open-comment)"; printf '/* never closed\nexecFile("beta", []);\n' > "$d/alpha/src/bad.ts"; run "$d"
expect "an unterminated block comment fails closed" 2 "alpha/src/bad.ts" "unterminated block comment"
d="$(fixture lex-jsx)"; printf 'export const V = () => <Box><Text>a/b</Text></Box>;\nexecFile("beta", []);\n' > "$d/alpha/src/v.tsx"; run "$d"
expect "JSX closing tags do not break the lexer" 1 "Alpha -> Beta (cli): alpha/src/v.tsx:2"

d="$(fixture sh-default)"
for shape in '"${BETA_BIN:-beta}" run' 'b=${BETA_BIN:-beta}; "$b" x' 'exec "${BETA_BIN:-beta}"' 'x="$(${TOOL:-beta} report)"'; do
  printf '#!/usr/bin/env bash\n%s\n' "$shape" > "$d/hooks/other.sh"; run "$d"
  expect "shell \${X:-bin} form is a cli edge: $shape" 1 "Hooks -> Beta (cli)"
done
d="$(fixture ts-env-index)"; printf 'const b = process.env["ALPHA_BIN"];\n' > "$d/delta/src/env.ts"; run "$d"
expect "process.env[\"X_BIN\"] is a cli edge" 1 "Delta -> Alpha (cli): delta/src/env.ts:1"
d="$(fixture sh-quoted-sep)"; printf '#!/usr/bin/env bash\necho "retry; beta x"\nprintf "a | beta"\nlog "x && beta y"\n' > "$d/hooks/other.sh"; run "$d"
expect "separators inside quotes do not start a command" 0 "deps: ok"
d="$(fixture sh-subst-in-quotes)"; printf '#!/usr/bin/env bash\nOUT="ok: $(beta report)"\n' > "$d/hooks/other.sh"; run "$d"
expect "a \$( ) inside double quotes is still a command" 1 "Hooks -> Beta (cli)"

d="$(fixture ns-dup)"; perl -pi -e 's/^    note: delta\n/    namespace: alpha\n    note: delta\n/' "$d/deps.yaml"; run "$d"
expect "two packages with one namespace fails" 1 "DEPS-REF-005" "alpha (Alpha, Delta)"
d="$(fixture ns-nested)"; perl -pi -e 's/^    note: delta\n/    files: [alpha\/extra]\n    note: delta\n/' "$d/deps.yaml"; run "$d"
expect "a file inside another package's namespace fails" 1 "DEPS-REF-005" "alpha/extra"

# ---- test code (*.Tests, *.Testing) follows its owner's rules ----
d="$(fixture testing-support)"; perl -0pi -e 's/(  - id: Alpha\n)/  - id: KTest\n    kind: package\n    tier: 0\n    test: true\n    projectPrefix: Aw.KTest\n    src: src\/KTest\n    note: test support\n$1/' "$d/deps.yaml"
perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
csproj "$d" src/KTest/Aw.KTest/Aw.KTest.csproj; csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Aw.Kernel/Aw.Kernel.csproj
csproj "$d" tests/Aw.Beta.Testing/Aw.Beta.Testing.csproj ../../src/KTest/Aw.KTest/Aw.KTest.csproj ../../src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../src/Aw.Kernel/Aw.Kernel.csproj
csproj "$d" tests/Aw.Beta.Tests/Aw.Beta.Tests.csproj ../Aw.Beta.Testing/Aw.Beta.Testing.csproj ../../src/KTest/Aw.KTest/Aw.KTest.csproj
run "$d"
expect "a package's *.Testing may reference test-support, its own package and its owner's edges; *.Tests may reference it" 0 "deps: ok" "checked 5 C# projects"
csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../../../tests/Aw.Beta.Testing/Aw.Beta.Testing.csproj; run "$d"
expect "production code may not reference a *.Testing project" 1 "DEPS-PROJ-003" "Aw.Beta.Host -> Aw.Beta.Testing"
d="$(fixture tests-tier)"; csproj "$d" src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj
csproj "$d" tests/Aw.Alpha.Tests/Aw.Alpha.Tests.csproj ../../src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj; run "$d"
expect "a tier-1 package's tests may not reference a tier-2 package" 1 "DEPS-PROJ-004" "Aw.Alpha.Tests -> Aw.Beta.Core (Alpha -> Beta)"
csproj "$d" tests/Aw.Alpha.Tests/Aw.Alpha.Tests.csproj ../../src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
csproj "$d" tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj ../../src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj ../../src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj; run "$d"
expect "an unowned cross-cutting test assembly may reference every package" 0 "deps: ok"
d="$(fixture tests-fanout)"; perl -pi -e 's/^  projectFanOut: 4\n/  projectFanOut: 1\n/' "$d/deps.yaml"
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj
csproj "$d" tests/Aw.Beta.Testing/Aw.Beta.Testing.csproj ../../src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../src/Aw.Kernel/Aw.Kernel.csproj; run "$d"
expect "*.Testing projects are exempt from fan-out and don't make edges current" 0 "deps: ok"

d="$(fixture scaffold-shape)"; perl -pi -e 's/thirdPartyFanOut: 6/thirdPartyFanOut: 1/' "$d/deps.yaml"
perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
perl -0pi -e 's/(  - id: Alpha\n)/  - id: KTest\n    kind: package\n    tier: 0\n    test: true\n    projectPrefix: Aw.KTest\n    src: src\/KTest\n    note: test support\n$1/' "$d/deps.yaml"
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/KTest/Aw.KTest/Aw.KTest.csproj
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Aw.Kernel/Aw.Kernel.csproj
mkdir -p "$d/tests/Aw.Beta.Testing" "$d/tests/Aw.Beta.Tests"
printf '<Project><ItemGroup><ProjectReference Include="../../src/KTest/Aw.KTest/Aw.KTest.csproj" /><ProjectReference Include="../../src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj" /><PackageReference Include="xunit.v3" /><PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" /></ItemGroup></Project>\n' > "$d/tests/Aw.Beta.Testing/Aw.Beta.Testing.csproj"
printf '<Project><ItemGroup><ProjectReference Include="../Aw.Beta.Testing/Aw.Beta.Testing.csproj" /><PackageReference Include="xunit.v3" /><PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="xunit.runner.visualstudio" /></ItemGroup></Project>\n' > "$d/tests/Aw.Beta.Tests/Aw.Beta.Tests.csproj"
run "$d"
expect "a scaffold-shaped package (Core -> Kernel current, Tests and Testing with xunit) passes" 0 "deps: ok" "checked 5 C# projects"

# ---- analyzers ----
d="$(fixture analyzers)"; perl -0pi -e 's/(  - id: Alpha\n)/  - id: Analyzers\n    kind: package\n    tier: 0\n    analyzer: true\n    projectPrefix: Aw.Analyzers\n    src: src\/Aw.Analyzers\n    note: build-time Roslyn rules\n$1/' "$d/deps.yaml"
perl -pi -e 's/^  projectFanOut: 4\n/  projectFanOut: 1\n/' "$d/deps.yaml"
perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
csproj "$d" src/Aw.Analyzers/Aw.Analyzers.csproj; csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj
mkdir -p "$d/src/Beta/Aw.Beta.Core"
printf '<Project><ItemGroup><ProjectReference Include="../../Aw.Kernel/Aw.Kernel.csproj" /><ProjectReference Include="../../Aw.Analyzers/Aw.Analyzers.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" /></ItemGroup></Project>\n' > "$d/src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj"
run "$d"
expect "an analyzer ProjectReference needs no edge and is not fan-out" 0 "deps: ok"
printf '<Project><ItemGroup><ProjectReference Include="$(MSBuildThisFileDirectory)Aw.Analyzers/Aw.Analyzers.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" /></ItemGroup></Project>\n' > "$d/src/Directory.Build.props"; run "$d"
expect "an analyzer ProjectReference in Directory.Build.props is allowed" 0 "deps: ok"
printf '<Project><ItemGroup><ProjectReference Include="../../Aw.Analyzers/Aw.Analyzers.csproj" /></ItemGroup></Project>\n' > "$d/src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj"; run "$d"
expect "a runtime reference to the analyzer project fails" 1 "DEPS-PROJ-006" "Aw.Beta.Core -> Aw.Analyzers"
printf '<Project><ItemGroup><ProjectReference Include="../../Aw.Kernel/Aw.Kernel.csproj" OutputItemType="Analyzer" /></ItemGroup></Project>\n' > "$d/src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj"; run "$d"
expect "an analyzer-style reference to a non-analyzer package fails" 1 "DEPS-PROJ-006" "Aw.Beta.Core -> Aw.Kernel"

# ---- review run 3 (PR #92) ----
d="$(fixture analyzer-tests)"; perl -0pi -e 's/(  - id: Alpha\n)/  - id: Analyzers\n    kind: package\n    tier: 0\n    analyzer: true\n    projectPrefix: Aw.Analyzers\n    src: src\/Aw.Analyzers\n    note: build-time Roslyn rules\n$1/' "$d/deps.yaml"
csproj "$d" src/Aw.Analyzers/Aw.Analyzers.csproj; csproj "$d" tests/Aw.Analyzers.Tests/Aw.Analyzers.Tests.csproj ../../src/Aw.Analyzers/Aw.Analyzers.csproj
csproj "$d" tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj ../../src/Aw.Analyzers/Aw.Analyzers.csproj; run "$d"
expect "test code may reference an analyzer normally" 0 "deps: ok"

d="$(fixture tests-dir)"; perl -0pi -e 's/(  - id: Alpha\n)/  - id: KTest\n    kind: package\n    tier: 0\n    test: true\n    projectPrefix: Aw.KTest\n    src: src\/KTest\n    note: test support\n$1/' "$d/deps.yaml"
perl -pi -e 's/^  projectFanOut: 4\n/  projectFanOut: 1\n/' "$d/deps.yaml"
csproj "$d" src/KTest/Aw.KTest/Aw.KTest.csproj; csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj
csproj "$d" tests/Aw.Beta.Tests.Integration/Aw.Beta.Tests.Integration.csproj ../../src/KTest/Aw.KTest/Aw.KTest.csproj ../../src/Aw.Kernel/Aw.Kernel.csproj
csproj "$d" tests/Aw.Beta.Benchmarks/Aw.Beta.Benchmarks.csproj ../../src/KTest/Aw.KTest/Aw.KTest.csproj ../../src/Aw.Kernel/Aw.Kernel.csproj; run "$d"
expect "every project under tests/ is test code, whatever its name" 0 "deps: ok"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../../tests/Aw.Beta.Benchmarks/Aw.Beta.Benchmarks.csproj; run "$d"
expect "nothing outside tests/ references a project under tests/" 1 "DEPS-PROJ-003" "Aw.Beta.Core -> Aw.Beta.Benchmarks"

d="$(fixture core-only)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"; edge "$d" Beta Alpha project current
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; csproj "$d" src/Alpha/Aw.Alpha.Infrastructure/Aw.Alpha.Infrastructure.csproj
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; run "$d"
expect "a cross-package reference to another package's Core (or a tier-0 package) passes" 0 "deps: ok"
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Infrastructure/Aw.Alpha.Infrastructure.csproj; run "$d"
expect "a cross-package reference to another package's non-Core project fails" 1 "DEPS-PROJ-007" "Aw.Beta.Features -> Aw.Alpha.Infrastructure"
csproj "$d" tests/Aw.Beta.Tests/Aw.Beta.Tests.csproj ../../src/Alpha/Aw.Alpha.Infrastructure/Aw.Alpha.Infrastructure.csproj
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; run "$d"
expect "test code also reaches other packages only through Core" 1 "DEPS-PROJ-007" "Aw.Beta.Tests -> Aw.Alpha.Infrastructure"

d="$(fixture core-only-exports)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/; s/(    projectPrefix: Aw.Alpha\n)/$1    exports: [Aw.Alpha.Infrastructure]\n/' "$d/deps.yaml"; edge "$d" Beta Alpha project current
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; csproj "$d" src/Alpha/Aw.Alpha.Infrastructure/Aw.Alpha.Infrastructure.csproj; csproj "$d" src/Alpha/Aw.Alpha.Features/Aw.Alpha.Features.csproj
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Infrastructure/Aw.Alpha.Infrastructure.csproj; run "$d"
expect "a project the owner lists under exports may be referenced" 0 "deps: ok"
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Features/Aw.Alpha.Features.csproj; run "$d"
expect "a project that is not listed under exports still fails" 1 "DEPS-PROJ-007" "Aw.Beta.Features -> Aw.Alpha.Features"

d="$(fixture regex-postfix)"
printf 'let n = 4, m = 2;\nconst a = n-- / 2; execFile("beta", []); const b = m++ / 3;\n' > "$d/alpha/src/p.ts"; run "$d"
expect "a / after postfix ++ or -- divides" 1 "Alpha -> Beta (cli): alpha/src/p.ts:2"
printf 'const r = a + /beta/.source;\nexecFile("beta", []);\n' > "$d/alpha/src/p.ts"; run "$d"
expect "a / after a binary + still starts a regex" 1 "Alpha -> Beta (cli): alpha/src/p.ts:2"

d="$(fixture fanout3-text)"; perl -pi -e 's/thirdPartyFanOut: 6/thirdPartyFanOut: 1/' "$d/deps.yaml"; mkdir -p "$d/src/Alpha/Aw.Alpha.Core"
printf '<Project><ItemGroup><PackageReference Include="A" /><PackageReference Include="B" /></ItemGroup></Project>\n' > "$d/src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj"; run "$d"
expect "the third-party fan-out limit names the Host and test exemptions" 1 "DEPS-FANOUT-003" "*.Host and test code exempt"

# ---- review run 4 (PR #92) ----
d="$(fixture tier0-whole)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Aw.Kernel/Aw.Kernel.Host.csproj
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj; run "$d"
expect "a tier-0 package's library projects are used whole" 0 "deps: ok"
for p in Host Infrastructure Features; do
  csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj "../../Aw.Kernel/Aw.Kernel.$p.csproj"; run "$d"
  expect "a tier-0 package's $p is not part of its contract" 1 "DEPS-PROJ-007" "Aw.Beta.Features -> Aw.Kernel.$p"
done

d="$(fixture source-role)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"; edge "$d" Beta Alpha project current
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Aw.Kernel/Aw.Kernel.csproj
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
csproj "$d" src/Beta/Aw.Beta.App/Aw.Beta.App.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; run "$d"
expect "Features, App and Host may hold another package's Core; any project may use tier 0" 0 "deps: ok"
for p in Core Infrastructure; do
  csproj "$d" "src/Beta/Aw.Beta.$p/Aw.Beta.$p.csproj" ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; run "$d"
  expect "a $p project may not reference another package's Core" 1 "DEPS-PROJ-008" "Aw.Beta.$p -> Aw.Alpha.Core"
  csproj "$d" "src/Beta/Aw.Beta.$p/Aw.Beta.$p.csproj" ../../Aw.Kernel/Aw.Kernel.csproj
done

d="$(fixture kernel-testing-tests)"; perl -0pi -e 's/(  - id: Alpha\n)/  - id: KTest\n    kind: package\n    tier: 0\n    sublevel: 2\n    test: true\n    projectPrefix: Aw.KTest\n    src: tests\/Aw.KTest.Testing\n    note: test support\n$1/' "$d/deps.yaml"
edge "$d" KTest Kernel project current
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" tests/Aw.KTest.Testing/Aw.KTest.Testing.csproj ../../src/Aw.Kernel/Aw.Kernel.csproj
csproj "$d" tests/Aw.Beta.Tests/Aw.Beta.Tests.csproj ../Aw.KTest.Testing/Aw.KTest.Testing.csproj; run "$d"
expect "a test-support package under tests/ makes its own edges current and is usable by any test code" 0 "deps: ok"

# ---- http edges: a C# client naming another package's routes (spec §4a "UI") ----
http_fixture() { # http_fixture <name> — Beta serves /beta/; a tier-4 Gamma client package lives inside Beta's src dir
  local d; d="$(fixture "$1")"
  perl -0pi -e 's/(    bins: \[beta\]\n)/$1    routes: [\/beta\/]\n/' "$d/deps.yaml"
  perl -0pi -e 's/(  - id: Hooks\n)/  - id: Gamma\n    kind: package\n    tier: 4\n    projectPrefix: Aw.Beta.Ui\n    src: src\/Beta\/Aw.Beta.Ui\n    note: the client app\n$1/' "$d/deps.yaml"
  csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj
  printf 'app.MapGet("/beta/status", () => 1);\n' > "$d/src/Beta/Aw.Beta.Host/Endpoints.cs"
  csproj "$d" src/Beta/Aw.Beta.Ui/Aw.Beta.Ui.csproj
  echo "$d"
}

d="$(http_fixture http-declared)"; edge "$d" Gamma Beta http current
printf 'var s = await http.GetAsync($"/beta/status?x={x}", ct);\n' > "$d/src/Beta/Aw.Beta.Ui/Client.cs"; run "$d"
expect "a declared http edge the C# source has passes; tier 4 is allowed; the server's own routes are no edge" 0 "deps: ok" "cli/db/file/http edges"

d="$(http_fixture http-undeclared)"
printf '// GET "/beta/status" is fine in a comment\n    /// <see cref="x"/> "/beta/x"\nconst string Path = "/beta/status";\n' > "$d/src/Beta/Aw.Beta.Ui/Client.cs"; run "$d"
expect "an undeclared http edge fails with where it was found" 1 "DEPS-EDGE-001" "Gamma -> Beta (http)" "src/Beta/Aw.Beta.Ui/Client.cs:3"
refuse "  ... and a quoted route in a whole-line // or /// comment is skipped" "Client.cs:1"
refuse "  ... (the /// line too)" "Client.cs:2"

d="$(http_fixture http-stale)"; edge "$d" Gamma Beta http current; run "$d"
expect "a current http edge with no route literal in the client is stale" 1 "DEPS-STALE-001" "Gamma -> Beta (http)" "no string literal naming Beta's routes (/beta/)"

d="$(http_fixture http-tests-only)"; edge "$d" Gamma Beta http planned
mkdir -p "$d/tests/Aw.Beta.Ui.Tests"; csproj "$d" tests/Aw.Beta.Ui.Tests/Aw.Beta.Ui.Tests.csproj
printf 'var url = "/beta/status";\n' > "$d/tests/Aw.Beta.Ui.Tests/T.cs"; run "$d"
expect "route literals in test projects are not edges" 0 "deps: ok"

d="$(http_fixture http-no-routes)"; edge "$d" Gamma Delta http planned; run "$d"
expect "an http edge to a package with no routes fails" 1 "DEPS-KIND-004" "Gamma -> Delta (http)"

# Review run 3 (kieran-typescript-reviewer-0): DEPS-KIND-004 depends only on the target, so an http edge from a shell
# node to a package with no routes fails too.
d="$(http_fixture http-no-routes-shell)"; edge "$d" Hooks Delta http planned; run "$d"
expect "an http edge from a shell node to a package with no routes fails" 1 "DEPS-KIND-004" "Hooks -> Delta (http)"

# Review run 4 (kieran-typescript-reviewer-0): no scan covers http calls from shell sources, so a current http
# edge from one can't be stale. It passes with an info line saying it isn't scanned, and nothing names C# projects for it.
d="$(http_fixture http-shell-current)"; edge "$d" Hooks Beta http current
printf 'curl -s "http://127.0.0.1:7480/beta/status"\n' >> "$d/hooks/gate.sh"; run "$d"
expect "a current http edge from a shell node passes with a not-scanned info line" 0 "deps: ok" "Hooks -> Beta (http): not scanned"
refuse "  ... and its text names no C# projects for the shell node" "Hooks's C# projects"

d="$(http_fixture http-cycle)"; edge "$d" Gamma Beta http planned; edge "$d" Beta Gamma cli planned; run "$d"
expect "http edges count in the cycle check" 1 "DEPS-CYCLE-001" "Beta -> Gamma -> Beta"

d="$(http_fixture http-shell-routes)"; perl -0pi -e 's/(    scan: \[hooks\]\n)/$1    routes: [\/hooks\/]\n/' "$d/deps.yaml"; run "$d"
expect "a shell node can't serve routes (schema)" 1 "DEPS-SCHEMA-001"

d="$(http_fixture http-nested-and-verbatim)"
mkdir -p "$d/src/Beta/Aw.Beta.Host/Ui"; csproj "$d" src/Beta/Aw.Beta.Host/Ui/Aw.Beta.Ui.Nested.csproj
printf 'var a = @"/beta/status";\n' > "$d/src/Beta/Aw.Beta.Host/Ui/Client.cs"; run "$d"
expect "a client project nested in the server's directory is its own (verbatim literal found)" 1 "DEPS-EDGE-001" "Gamma -> Beta (http)" "src/Beta/Aw.Beta.Host/Ui/Client.cs:1"

# ---- C# file and db edges: every segment of another package's state path as a string literal in one file ----
d="$(http_fixture cs-file-undeclared)"
printf 'var p = Path.Combine(home, "alpha", "outbox");\n' > "$d/src/Beta/Aw.Beta.Ui/Paths.cs"; run "$d"
expect "a C# project naming another package's state file is an undeclared file edge" 1 "DEPS-EDGE-001" "Gamma -> Alpha (file)" "src/Beta/Aw.Beta.Ui/Paths.cs"

d="$(http_fixture cs-file-declared)"; edge "$d" Gamma Alpha file current
printf 'var p = Path.Combine(home, "alpha");\nvar f = Path.Combine(p, "outbox");\n' > "$d/src/Beta/Aw.Beta.Ui/Paths.cs"; run "$d"
expect "a declared C# file edge passes when the segments sit on separate lines of one file" 0 "deps: ok"

d="$(http_fixture cs-file-whole-path)"
printf 'var p = Path.Combine(home, "alpha/outbox");\n' > "$d/src/Beta/Aw.Beta.Ui/Paths.cs"; run "$d"
expect "a single literal holding the whole state path is a file edge" 1 "DEPS-EDGE-001" "Gamma -> Alpha (file)"

d="$(http_fixture cs-file-commented)"
printf '// var p = Path.Combine(home, "alpha", "outbox");\n    /// Path.Combine(home, "alpha", "outbox")\n' > "$d/src/Beta/Aw.Beta.Ui/Paths.cs"; run "$d"
expect "a state path in whole-line // and /// comments is no file edge" 0 "deps: ok"

d="$(http_fixture cs-file-partial)"
printf 'var p = "outbox";\n' > "$d/src/Beta/Aw.Beta.Ui/Paths.cs"; run "$d"
expect "one segment alone is not a file edge" 0 "deps: ok"

# ---- the compose edge kind (context spec §4, stage S-1C): a Host calls a lower package's Add…() methods ----
composed() { # composed <name> — Beta reaches Alpha by project (current) and compose (current); Alpha has every layer
  local d; d="$(fixture "$1")"
  perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
  edge "$d" Beta Alpha project current; edge "$d" Beta Alpha compose current
  csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj
  for p in Core Features Infrastructure; do csproj "$d" "src/Alpha/Aw.Alpha.$p/Aw.Alpha.$p.csproj"; done
  csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj
  csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj \
    ../../Alpha/Aw.Alpha.Features/Aw.Alpha.Features.csproj ../../Alpha/Aw.Alpha.Infrastructure/Aw.Alpha.Infrastructure.csproj
  echo "$d"
}

d="$(fixture compose-planned)"; edge "$d" Beta Alpha project planned; edge "$d" Beta Alpha compose planned; run "$d" --mermaid
expect "a planned compose edge beside a project edge passes and is drawn" 0 "deps: ok" "Beta -->|compose| Alpha"

d="$(fixture compose-alone)"; edge "$d" Beta Alpha compose planned; run "$d"
expect "a compose edge without a project edge to the same package fails" 1 "DEPS-COMPOSE-001" "Beta -> Alpha (compose)" "no project edge" "fix:"

d="$(fixture compose-up)"; edge "$d" Alpha Beta compose planned; run "$d"
expect "a compose edge points down the tiers" 1 "DEPS-TIER-001" "compose edge does not point to a lower tier" "Alpha (tier 1) -> Beta (tier 2)"
refuse "a misplaced compose edge isn't also told to add a project edge" "DEPS-COMPOSE-001"

d="$(fixture compose-shell)"; edge "$d" Hooks Alpha compose planned; run "$d"
expect "a compose edge from a shell node fails" 1 "DEPS-KIND-001" "Hooks -> Alpha (compose)"
refuse "a compose edge from a shell node isn't also told to add a project edge" "DEPS-COMPOSE-001"

d="$(fixture compose-tier0)"; edge "$d" Beta Kernel compose planned; run "$d"
expect "a compose edge into a tier-0 package fails" 1 "DEPS-COMPOSE-001" "compose edge into a tier-0 package" "Beta -> Kernel (compose)"

d="$(composed compose-good)"; run "$d"
expect "a Host may reference a compose target's Features and Infrastructure" 0 "deps: ok"

d="$(composed compose-undeclared)"; perl -0pi -e 's/  - from: Beta\n    to: Alpha\n    kind: compose\n    status: current\n    note: added by test\n//' "$d/deps.yaml"; run "$d"
expect "a Host reference into another package's Features without a compose edge fails" 1 "DEPS-COMPOSE-001" "Aw.Beta.Host -> Aw.Alpha.Features" "Aw.Beta.Host -> Aw.Alpha.Infrastructure" "kind: compose"
refuse "the Host's undeclared compose reference is not also DEPS-PROJ-007" "DEPS-PROJ-007"

d="$(composed compose-host-only)"; perl -0pi -e 's/  - from: Beta\n    to: Alpha\n    kind: compose\n    status: current\n    note: added by test\n//; s/  - from: Beta\n    to: Alpha\n    kind: project\n    status: current\n    note: added by test\n//' "$d/deps.yaml"; run "$d"
expect "with neither edge declared, a Host's Features reference is DEPS-COMPOSE-001 only" 1 "DEPS-COMPOSE-001" "Aw.Beta.Host -> Aw.Alpha.Features"
refuse "  ... and not DEPS-PROJ-007" "DEPS-PROJ-007"

d="$(composed compose-no-core)"
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj
csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../../Alpha/Aw.Alpha.Features/Aw.Alpha.Features.csproj; run "$d"
expect "a Host's compose reference doesn't make the project edge current: it needs a Core reference" 1 "DEPS-STALE-001" "Beta -> Alpha (project)"
refuse "  ... while the compose edge itself is found" "Beta -> Alpha (compose)"

d="$(composed compose-app)"
for p in App Host; do csproj "$d" "src/Alpha/Aw.Alpha.$p/Aw.Alpha.$p.csproj"; done
csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj ../../Alpha/Aw.Alpha.Features/Aw.Alpha.Features.csproj ../../Alpha/Aw.Alpha.App/Aw.Alpha.App.csproj ../../Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj; run "$d"
expect "a compose edge opens Features and Infrastructure only, never App or Host" 1 "DEPS-PROJ-007" "Aw.Beta.Host -> Aw.Alpha.App" "Aw.Beta.Host -> Aw.Alpha.Host"

d="$(fixture compose-up-ref)"; perl -0pi -e 's/kind: project\n    status: planned/kind: project\n    status: current/' "$d/deps.yaml"
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj; csproj "$d" src/Delta/Aw.Delta.Features/Aw.Delta.Features.csproj
csproj "$d" src/Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj ../../Delta/Aw.Delta.Features/Aw.Delta.Features.csproj; run "$d"
expect "a Host reference up the tiers into Features is DEPS-PROJ-007, not a compose suggestion" 1 "DEPS-PROJ-007" "Aw.Alpha.Host -> Aw.Delta.Features"
refuse "  ... with no DEPS-COMPOSE-001" "DEPS-COMPOSE-001"

d="$(fixture compose-tier0-current)"; edge "$d" Beta Kernel compose current; run "$d"
expect "a current compose edge into tier 0 fails DEPS-COMPOSE-001 only" 1 "compose edge into a tier-0 package"
refuse "  ... without a staleness fix that can't apply" "DEPS-STALE-001"

d="$(composed compose-features)"
csproj "$d" src/Beta/Aw.Beta.Features/Aw.Beta.Features.csproj ../../Aw.Kernel/Aw.Kernel.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj ../../Alpha/Aw.Alpha.Features/Aw.Alpha.Features.csproj; run "$d"
expect "the compose carve-out is for the Host only" 1 "DEPS-PROJ-007" "Aw.Beta.Features -> Aw.Alpha.Features"

d="$(composed compose-stale)"; csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../../Alpha/Aw.Alpha.Core/Aw.Alpha.Core.csproj; run "$d"
expect "a current compose edge with no Host reference into Features or Infrastructure is stale" 1 "DEPS-STALE-001" "Beta -> Alpha (compose)"

d="$(composed compose-early)"; perl -0pi -e 's/kind: compose\n    status: current/kind: compose\n    status: planned/' "$d/deps.yaml"; run "$d"
expect "a planned compose edge the Host already uses fails" 1 "DEPS-STALE-002" "Beta -> Alpha (compose)"

d="$(fixture exc-001)"; exc "$d" DEPS-FANOUT-001 Beta 2026-10-10 2026-12-01; run "$d"
expect "the removed package fan-out rule takes no exception" 1 "DEPS-SCHEMA-001" "/exceptions/0/rule"

d="$(over exc-edge)"; perl -pi -e "s/^exceptions: \[\]\n/exceptions:\n  - rule: DEPS-FANOUT-002\n    subject: Beta\n    edge: Alpha\n    reason: test\n    recorded: 2026-10-10\n    expires: 2026-12-01\n/" "$d/deps.yaml"; run "$d"
expect "an exception names no edge" 1 "DEPS-SCHEMA-001" "/exceptions/0" "edge"

# ---- cycle exceptions: a DEPS-CYCLE-001 exception excuses one declared cli or http edge and the one cycle it closes ----
cyc() { # cyc <dir> <subject> <from> <to> <kind> [recorded expires] — a DEPS-CYCLE-001 exception excusing from -> to (kind)
  perl -pi -e "s/^exceptions: \[\]\n/exceptions:\n  - rule: DEPS-CYCLE-001\n    subject: $2\n    excuses:\n      from: $3\n      to: $4\n      kind: $5\n    reason: test\n    recorded: ${6:-2026-10-10}\n    expires: ${7:-2026-12-01}\n/" "$1/deps.yaml"
}
d="$(fixture cycle-excepted)"; edge "$d" Alpha Beta cli planned; cyc "$d" Alpha Alpha Beta cli; run "$d"
expect "a cycle exception excusing the cli edge that closes the cycle passes" 0 "deps: ok" "exception DEPS-CYCLE-001 Alpha -> Beta (cli) (expires 2026-12-01)"

d="$(fixture cycle-excepted-other)"; edge "$d" Alpha Beta cli planned; edge "$d" Alpha Delta cli planned; edge "$d" Delta Beta cli planned; cyc "$d" Alpha Alpha Beta cli; run "$d"
expect "a cycle exception covers its one cycle: another cycle still fails" 1 "DEPS-CYCLE-001" "Alpha -> Delta -> Beta -> Alpha"
refuse "  ... and the excused cycle is not reported" "Alpha -> Beta -> Alpha"

d="$(fixture cycle-excepted-two)"; edge "$d" Alpha Beta cli planned; edge "$d" Beta Delta cli planned; edge "$d" Delta Alpha cli planned; cyc "$d" Alpha Alpha Beta cli; run "$d"
expect "a cycle exception whose edge closes two cycles covers neither" 1 "DEPS-CYCLE-001" "Alpha -> Beta -> Alpha" "Alpha -> Beta -> Delta -> Alpha" "DEPS-EXC-002" "2 cycles pass through Alpha -> Beta (cli)" "fix: break the other cycle through Alpha -> Beta (cli)"
refuse "  ... and its fix isn't to delete the exception" "fix: delete the DEPS-CYCLE-001 exception"

# Review run 3 (kieran-typescript-reviewer-1): two exceptions on the same cycle (one per direction): either alone removes
# the cycle, so the second suppresses nothing and fails DEPS-EXC-002 with its own measurement and fix.
d="$(fixture cycle-excepted-both)"; edge "$d" Alpha Delta cli planned; edge "$d" Delta Alpha cli planned; cyc "$d" Alpha Alpha Delta cli
perl -pi -e "s/^exceptions:\n/exceptions:\n  - rule: DEPS-CYCLE-001\n    subject: Delta\n    excuses:\n      from: Delta\n      to: Alpha\n      kind: cli\n    reason: test\n    recorded: 2026-10-10\n    expires: 2026-12-01\n/" "$d/deps.yaml"; run "$d"
expect "a second exception on an already covered cycle suppresses nothing" 1 "DEPS-EXC-002" "is already covered by another DEPS-CYCLE-001 exception" "fix: delete one of the two exceptions on"
refuse "  ... and the cycle itself stays covered" "FAIL DEPS-CYCLE-001 the declared graph has a cycle"

d="$(fixture cycle-excepted-none)"; edge "$d" Delta Alpha cli planned; cyc "$d" Delta Delta Alpha cli; run "$d"
expect "a cycle exception whose cycle doesn't exist suppresses nothing" 1 "DEPS-EXC-002" "DEPS-CYCLE-001 for Delta: no cycle passes through Delta -> Alpha (cli)" "fix: delete the DEPS-CYCLE-001 exception for Delta from deps.yaml"

d="$(fixture cycle-excepted-parallel)"; edge "$d" Alpha Beta cli planned; edge "$d" Alpha Beta file planned; cyc "$d" Alpha Alpha Beta cli; run "$d"
expect "a cycle exception covers nothing when another edge of the same pair still closes the cycle" 1 "DEPS-CYCLE-001" "Alpha -> Beta -> Alpha" "DEPS-EXC-002" "every cycle through Alpha -> Beta (cli) also has another Alpha -> Beta edge" "fix: remove the other Alpha -> Beta edge"
refuse "  ... and its fix isn't to delete the exception" "fix: delete the DEPS-CYCLE-001 exception"

d="$(fixture cycle-excepted-missing)"; edge "$d" Alpha Beta cli planned; cyc "$d" Alpha Alpha Beta http; run "$d"
expect "a cycle exception excusing an edge that isn't declared fails, and the cycle with it" 1 "DEPS-EXC-005" "Alpha -> Beta (http) is not a declared edge" "DEPS-CYCLE-001" "Alpha -> Beta -> Alpha"

d="$(fixture cycle-excepted-subject)"; edge "$d" Alpha Beta cli planned; cyc "$d" Beta Alpha Beta cli; run "$d"
expect "a cycle exception excuses an edge leaving its own subject" 1 "DEPS-EXC-005" "Alpha -> Beta (cli) does not leave Beta" "DEPS-CYCLE-001" "Alpha -> Beta -> Alpha"

d="$(fixture cycle-excepted-file)"; edge "$d" Alpha Beta file planned; cyc "$d" Alpha Alpha Beta file; run "$d"
expect "a cycle exception excuses a cli or http edge, not a db or file edge" 1 "DEPS-SCHEMA-001" "/exceptions/0/excuses/kind"

d="$(fixture cycle-excepted-expired)"; edge "$d" Alpha Beta cli planned; cyc "$d" Alpha Alpha Beta cli 2020-01-01 2020-06-01; run "$d"
expect "an expired cycle exception fails and no longer covers the cycle" 1 "DEPS-EXC-001" "DEPS-CYCLE-001" "Alpha -> Beta -> Alpha"
for dates in "2026-13-45 2026-12-01" "2026-12-01 2026-11-01" "2026-10-10 2027-10-10"; do
  set -- $dates; d="$(fixture "cycle-dates-$1-$2")"; edge "$d" Alpha Beta cli planned; cyc "$d" Alpha Alpha Beta cli "$1" "$2"; run "$d"
  expect "bad cycle exception dates fail (recorded $1, expires $2)" 1 "DEPS-EXC-003" "Alpha -> Beta -> Alpha"
done

d="$(fixture cycle-excepted-unnamed)"; exc "$d" DEPS-CYCLE-001 Alpha 2026-10-10 2026-12-01; run "$d"
expect "a cycle exception names the edge it excuses" 1 "DEPS-SCHEMA-001" "/exceptions/0: missing required property excuses"
d="$(over fanout-excuses)"; perl -pi -e "s/^exceptions: \[\]\n/exceptions:\n  - rule: DEPS-FANOUT-002\n    subject: Beta\n    excuses:\n      from: Beta\n      to: Alpha\n      kind: cli\n    reason: test\n    recorded: 2026-10-10\n    expires: 2026-12-01\n/" "$d/deps.yaml"; run "$d"
expect "only a cycle exception names an excused edge" 1 "DEPS-SCHEMA-001" "/exceptions/0"

# ---- build-order references (ReferenceOutputAssembly="false", not an analyzer) ----
bo() { printf '<Project><ItemGroup><ProjectReference Include="%s" ReferenceOutputAssembly="false" /></ItemGroup></Project>\n' "$2" > "$1"; }
d="$(fixture build-order)"; mkdir -p "$d/tests/Aw.Beta.Tests" "$d/src/Beta/Aw.Beta.Core"
csproj "$d" src/Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj
bo "$d/tests/Aw.Beta.Tests/Aw.Beta.Tests.csproj" ../../src/Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj; run "$d"
expect "test code's build-order reference passes when its owner has a cli edge to the target" 0 "deps: ok"
d="$(fixture build-order-no-edge)"; mkdir -p "$d/tests/Aw.Delta.Tests"
csproj "$d" src/Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj
bo "$d/tests/Aw.Delta.Tests/Aw.Delta.Tests.csproj" ../../src/Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj; run "$d"
expect "a build-order reference without the owner's cli edge fails" 1 "DEPS-PROJ-009" "Aw.Delta.Tests -> Aw.Alpha.Host"
d="$(fixture build-order-prod)"; mkdir -p "$d/src/Beta/Aw.Beta.Host"
csproj "$d" src/Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj
bo "$d/src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj" ../../Alpha/Aw.Alpha.Host/Aw.Alpha.Host.csproj; run "$d"
expect "a production project's build-order reference fails even with a cli edge" 1 "DEPS-PROJ-009" "Aw.Beta.Host -> Aw.Alpha.Host"

# ---- parser, validator and schema drift (node unit table) ----
set +e; UNIT="$(node "$HERE/check-deps.unit.mjs" 2>&1)"; UCODE=$?; set -e
if [ "$UCODE" = 0 ]; then ok "parser/validator unit table ($(printf '%s' "$UNIT" | tail -1))"; else fail "parser/validator unit table"; printf '%s\n' "$UNIT" | sed 's/^/       | /'; fi

echo
echo "check-deps: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
