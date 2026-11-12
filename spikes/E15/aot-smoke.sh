#!/usr/bin/env bash
# E15 spike (issue 3248): NativeAOT publish of spikes/E15/Aw.Spike.E15, failing on any IL2xxx/IL3xxx warning.
# A copy of src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh for the spike project. Publishes on a copy of the tree, so the
# RID's ILCompiler packs never reach the checkout's lock files. Output: artifacts/aot-spike-e15/ (publish.log, bin/).
# Prints the warning count (grep -cE 'IL[23][0-9]{3}') and the first lines; exit 0 only when the count is 0.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="$root/artifacts/aot-spike-e15"
mkdir -p "$out"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
bin="$out/bin"
[ ! -d "$bin" ] || find "$bin" -delete
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt \
  src/Aw.Analyzers spikes/E15 | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/spikes/E15/Aw.Spike.E15/Aw.Spike.E15.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
n="$(grep -cE 'IL[23][0-9]{3}' "$out/publish.log" || true)"
echo "aot-spike-e15: $n IL2xxx/IL3xxx warning line(s)"
if [ "$n" != 0 ]; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" | sort -u | head -20 >&2
  exit 1
fi
[ -x "$bin/spike-e15" ] || { echo "aot-spike-e15: no spike-e15 binary in $bin" >&2; exit 1; }
echo "aot-spike-e15: ok ($(du -h "$bin/spike-e15" | cut -f1) binary)"
