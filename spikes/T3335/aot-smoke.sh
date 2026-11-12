#!/usr/bin/env bash
# T3335 spike (issue 3335): NativeAOT publish of spikes/T3335/Aw.Spike.T3335, failing on any IL2xxx/IL3xxx warning.
# A copy of src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh for the spike project. Publishes on a copy of the tree, so the
# RID's ILCompiler packs never reach the checkout's lock files. Output: artifacts/aot-spike-t3335/ (publish.log, bin/).
# Prints the warning count (grep -cE 'IL[23][0-9]{3}') and the first lines; exit 0 only when the count is 0.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="$root/artifacts/aot-spike-t3335"
mkdir -p "$out"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
bin="$out/bin"
[ ! -d "$bin" ] || find "$bin" -delete
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt \
  src/Aw.Analyzers spikes/T3335 | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/spikes/T3335/Aw.Spike.T3335/Aw.Spike.T3335.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
n="$(grep -cE 'IL[23][0-9]{3}' "$out/publish.log" || true)"
echo "aot-spike-t3335: $n IL2xxx/IL3xxx warning line(s)"
if [ -f "$bin/spike-t3335" ]; then echo "aot-spike-t3335: binary $(stat -c %s "$bin/spike-t3335") bytes; publish dir $(find "$bin" -type f -printf '%s\n' | awk '{s+=$1} END {print s}') bytes"; fi
if [ "$n" != 0 ]; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" | sort -u | head -20 >&2
  exit 1
fi
[ -x "$bin/spike-t3335" ] || { echo "aot-spike-t3335: no spike-t3335 binary in $bin" >&2; exit 1; }
echo "aot-spike-t3335: ok ($(du -h "$bin/spike-t3335" | cut -f1) binary)"
