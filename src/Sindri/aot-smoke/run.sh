#!/usr/bin/env bash
# NativeAOT smoke for sindri (src/Sindri/Aw.Sindri.Host): publish it with PublishAot for the host RID, fail on any
# trim/AOT (IL2xxx/IL3xxx) warning, then drive the native binary through the S1 journeys against a temp home: doctor
# with nothing running, start headless with an unapproved profile, a second serve refused by the lease, the approve
# preview and its non-terminal refusal, status, stop, and doctor again; then recipe validate on the shipped recipes (their hashes; workflow.item reads its child beside it)
# and on an invalid fixture (RCP-REACH-001, exit 1), the first verbs through the YAML reader and the recipe binding. The normalized output (pids, ports, hashes, times,
# the host name and the temp path masked) must equal expected.txt.
# Every invocation of the binary is bounded (30 s, perl's alarm); one that runs over prints 'exit 124'.
# Output goes to artifacts/aot-sindri-smoke/ (gitignored). The publish runs on a copy of the files it needs: PublishAot
# with a RID makes restore add the host RID's ILCompiler packs to the lock files, so it never runs on the checkout.
set -euo pipefail
export AW_LOG_LEVEL=none

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
out="$root/artifacts/aot-sindri-smoke"
mkdir -p "$out"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' BannedSymbols.txt CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Sindri | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

work="$(mktemp -d "$out/run.XXXXXX")"
bin="$out/bin/sindri"
export AW_SINDRI_HOME="$work/home" AW_SINDRI_PORT=0
bounded() { perl -e 'alarm shift; exec @ARGV or die "exec $ARGV[0]: $!"' 30 "$@"; }   # SIGALRM after 30 s
cleanup() { bounded "$bin" stop >/dev/null 2>&1 || true; find "$work" -delete; }
trap cleanup EXIT
mkdir -p "$AW_SINDRI_HOME/profile"
printf 'schemaVersion: 1\nuser: smoke\nservices:\n  heartbeat: { every: 5s }\n' >"$AW_SINDRI_HOME/profile/profile.yaml"

run() {
  echo "## sindri $*"
  local rc=0
  bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  [ "$rc" -ne 142 ] || rc=124   # killed by the alarm
  [ "$rc" -eq 0 ] || echo "exit $rc"
}
{
  run doctor
  run --headless
  run serve
  run profile approve
  hash="$({ bounded "$bin" profile validate --json </dev/null || true; } | sed -E 's/.*"hash":"([0-9a-f]{64})".*/\1/')"
  run profile approve "$hash"
  run status
  run stop
  run status
  run doctor
  run recipe validate "$root/recipes/sindri/workflow.step.yaml"
  run recipe validate "$root/recipes/sindri/workflow.item.yaml"
  run recipe validate "$root/tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml"
} >"$out/actual.raw"

sed -E -e "s#$work#WORK#g" -e "s#$root#ROOT#g" -e "s#held by $(hostname -s)[^/ ]*#held by HOST#g" -e 's/[0-9a-f]{64}/HASH/g' -e 's/hash [0-9a-f]{6}/hash HASH6/g' \
  -e 's/pid [0-9]+/pid PID/g' -e 's#HOST/[0-9]+#HOST/PID#g' -e 's/127\.0\.0\.1:[0-9]+/127.0.0.1:PORT/g' \
  -e 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]{8}Z/TIME/g' -e 's/\(last [0-9:]{8}Z\)/(last TIME)/g' "$out/actual.raw" >"$out/actual.txt"
diff -u "$here/expected.txt" "$out/actual.txt"
echo "aot-sindri-smoke: ok ($(du -h "$bin" | cut -f1) binary)"
