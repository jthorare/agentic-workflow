#!/usr/bin/env bash
# NativeAOT smoke for bugfix-state (src/BugFix/Aw.BugFix.Host): publish it with PublishAot for the host RID, fail on any
# trim/AOT (IL2xxx/IL3xxx) warning, then run the native binary through a bug fix in a temp git repository: init,
# investigate, a ui-evidence run, a failing baseline run, reproduce, an attempt, a fixed candidate, its passing run, judge,
# report, resume, and a usage error. judge and ui-evidence are stub scripts in the temp directory (AW_JUDGE_BIN,
# AW_UI_EVIDENCE_BIN), so the adapters run for real against them. The normalized output must equal expected.txt.
# Output goes to artifacts/aot-bugfix-smoke/ (gitignored). The publish runs on a copy of the files it needs (the root
# build files, src/Aw.Kernel, src/Aw.Kernel.Telemetry and src/Redaction (the host starts telemetry, which scrubs through
# Redaction), src/Aw.Analyzers (every production project loads it), src/BugFix): PublishAot with a RID makes restore add
# the host RID's ILCompiler packs to the lock files, so it never runs on the checkout, whose lock files stay RID-free.
set -euo pipefail
# Log lines carry wall-clock timestamps: pin the level that writes none (docs/csharp/telemetry.md, the CLI stderr contract).
export AW_LOG_LEVEL=none

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../../artifacts/aot-bugfix-smoke"
mkdir -p "$out"

root="$(cd "$here/../../.." && pwd)"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/BugFix | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/src/BugFix/Aw.BugFix.Host/Aw.BugFix.Host.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

work="$(cd "$(mktemp -d "$out/run.XXXXXX")" && pwd -P)"
trap 'find "$work" -delete' EXIT
repo="$work/repo"
git_() { git -C "$1" -c user.name=smoke -c user.email=smoke@example.invalid "${@:2}"; }
mkdir -p "$repo/src" "$repo/tests"
printf 'drop cookie\n' >"$repo/src/app.txt"
printf '{"steps":[]}\n' >"$repo/tests/ui.json"
printf 'echo checking\ngrep -q keep src/app.txt\n' >"$repo/tests/bug.test.sh"
git_ "$repo" init -q -b main
git_ "$repo" add -A
git_ "$repo" commit -q -m base
printf '{"source":"text","id":null,"title":"Login","brief":"Login shows a blank page","expected":"The dashboard opens","actual":"Blank page"}' >"$work/ticket.json"
printf 'status: diagnosed\n\n## Hypotheses\n\n| # | Hypothesis | Cause-site files | Likelihood | Result |\n|---|---|---|---|---|\n| 1 | Cookie dropped | `src/app.txt` | high | confirmed |\n\n## Root Cause\n\nThe cookie is dropped.\n' >"$work/handoff.md"

# Stub judge: resolution-check keeps its stdin and decides resolved; why names the digest the real judge would store for that input.
mkdir -p "$work/stubs"
cat >"$work/stubs/judge" <<'STUB'
#!/bin/bash
here="$(cd "$(dirname "$0")" && pwd)"
case "$1" in
  resolution-check)
    cat >"$here/input"
    printf '{"decision":"resolved","confidence":0.9,"model":"jev","reasonCode":"judged","id":"d-1","reasons":["the page opens"]}\n' ;;
  why) printf '{"id":"%s","question":"resolution-check","decision":"resolved","input_digest":"%s"}\n' "$2" "$(sha256sum "$here/input" | cut -c1-16)" ;;
  *) exit 64 ;;
esac
STUB
# Stub ui-evidence: run <script> <run-dir> ... writes a one-step passing summary.
cat >"$work/stubs/ui-evidence" <<'STUB'
#!/bin/bash
mkdir -p "$3"
printf '{"scriptSha256":"%s","steps":[{"index":1,"status":"passed"}]}\n' "$(sha256sum "$2" | cut -d' ' -f1)" >"$3/summary.json"
STUB
chmod +x "$work/stubs/judge" "$work/stubs/ui-evidence"
export AW_JUDGE_BIN="$work/stubs/judge" AW_UI_EVIDENCE_BIN="$work/stubs/ui-evidence"

bin="$out/bin/bugfix-state"
state="$work/state"
step() { echo "## $*"; (cd "$repo" && "$bin" "$@") || echo "exit $?"; }
evidence() { sed -n 's/.*"evidence":"\([^"]*\)".*/\1/p' "$out/raw.txt" | tail -1; }
{
  step init --state "$state" --ticket "$work/ticket.json"
  step advance investigate --state "$state" --evidence "$work/handoff.md"
  step run-ui --state "$state" --check tests/ui.json
  step run-test --state "$state" --check tests/bug.test.sh -- bash tests/bug.test.sh
} >"$out/raw.txt" 2>&1
{
  step advance reproduce --state "$state" --evidence "$(evidence)" --check tests/bug.test.sh
  step start-attempt --state "$state" --mode A
  git_ "$repo" worktree add -q -b fix-a1 "$work/a1"
  printf 'keep cookie\n' >"$work/a1/src/app.txt"
  git_ "$work/a1" commit -q -am fix
  step record-candidate --state "$state" --branch fix-a1 --cwd "$work/a1"
  step run-test --state "$state" --check tests/bug.test.sh --cwd "$work/a1" -- bash tests/bug.test.sh
} >>"$out/raw.txt" 2>&1
{
  step record-run c1 --state "$state" --evidence "$(evidence)"
  step judge c1 --state "$state"
  step advance report --state "$state" --unresolved
  step resume --state "$state"
  step start-attempt --state "$state" --mode Z
} >>"$out/raw.txt" 2>&1
sed -E -e "s#$work#<tmp>#g" -e 's/[0-9a-f]{40}/<sha>/g' -e 's/[0-9]{8}T[0-9]{9}Z/<stamp>/g' "$out/raw.txt" >"$out/actual.txt"
if ! cmp -s "$out/actual.txt" "$here/expected.txt"; then
  diff "$here/expected.txt" "$out/actual.txt" | head -40 >&2
  exit 1
fi
echo "aot-bugfix-smoke: native binary passed ($(du -h "$bin" | cut -f1) bugfix-state)"
