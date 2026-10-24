#!/usr/bin/env bash
# NativeAOT publish check for the Sindri app's desktop head (src/Sindri/Aw.Sindri.Ui.Desktop, docs/csharp/sindri-ui.md):
# publish sindri-ui with PublishAot for the host RID and fail on any trim/AOT (IL2xxx/IL3xxx) warning or a missing
# binary. It doesn't open a window (CI has no display); the hand test and the headless view tests cover what it shows.
# CI's AOT step runs it (the src/*/*.AotSmoke/run.sh glob). Output goes to artifacts/aot-sindri-ui/ (gitignored); the
# publish runs on a copy, like src/Sindri/aot-smoke/run.sh, so the RID's ILCompiler
# packs never reach the checkout's lock files. Pass a directory to also copy sindri-ui there: the publish directory of a
# sindri (NativeAOT today, framework-dependent from engine rev 8's Temporal adapter; csharp.md "AOT scope"), where
# bare `sindri` looks for it.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
out="$root/artifacts/aot-sindri-ui"
mkdir -p "$out"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
# The publish output starts empty too: `dotnet publish -o` keeps what an earlier run left, so the binary check and the
# <dir> copy could use an old sindri-ui or another version's native libraries (review run 4, code-reviewer-0).
bin="$out/bin"
[ ! -d "$bin" ] || find "$bin" -delete
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Telemetry src/Redaction/Aw.Redaction src/Aw.Analyzers src/Sindri/Aw.Sindri.Ui src/Sindri/Aw.Sindri.Ui.Desktop src/UiDriver/Aw.UiDriver.Core src/UiDriver/Aw.UiDriver.Avalonia | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/src/Sindri/Aw.Sindri.Ui.Desktop/Aw.Sindri.Ui.Desktop.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi
[ -x "$bin/sindri-ui" ] || { echo "aot-sindri-ui: no sindri-ui binary in $bin" >&2; exit 1; }
# Copy to <dir> when given, each file with a plain cp so a failure stops the script (find's `-exec cp {} <dir>/ \;`
# ignored cp's status, so a missing or read-only <dir> still printed ok; review run 3, code-reviewer-1).
if [ -n "${1:-}" ]; then
  [ -d "$1" ] || { echo "aot-sindri-ui: no directory $1" >&2; exit 1; }
  for f in "$bin"/*; do
    if [ -f "$f" ]; then
      cp "$f" "$1/" || { echo "aot-sindri-ui: couldn't copy $(basename "$f") to $1" >&2; exit 1; }
    fi
  done
fi
echo "aot-sindri-ui: ok ($(du -h "$bin/sindri-ui" | cut -f1) binary; $(file -b "$bin/sindri-ui" | cut -d, -f1))"
