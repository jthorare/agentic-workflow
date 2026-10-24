#!/usr/bin/env bash
# NativeAOT smoke for judge (src/Judge/Aw.Judge.Host): publish it with PublishAot for the host RID, fail on any
# trim/AOT (IL2xxx/IL3xxx) warning or a missing bundled e_sqlite3, then run the native binary offline (a bare PATH,
# AW_JUDGE_PROVIDERS=rules, no Jev key) against a temp AW_STATE_DIR: health, brief save/get, a brief that isn't there,
# the wake-gate and ask-check pre-rules, a question nothing decides, config get, a usage error and a bad allowlist. The
# normalized output (ids and times) must equal expected.txt.
# AW_LOG_LEVEL is unset on purpose: the contract is that stderr stays clean at the default level (a log line carries a
# wall-clock time, so it would show up as a diff).
# A second publish proves YamlDotNet (Judge.Infrastructure's trait-file parser, cutover unit 6.2) loads and parses under NativeAOT: a
# throwaway console app next to the tree, at the version Directory.Packages.props pins, using the same parser and representation
# model the trait reader will (YamlStream; no reflection serializer). Its output must equal yaml-probe-expected.txt.
# Output goes to artifacts/aot-judge-smoke/ (gitignored). The publish runs on a copy of the files it needs (the root
# build files, src/Aw.Kernel, src/Aw.Kernel.Data, src/Aw.Kernel.Telemetry and src/Redaction, src/Aw.Analyzers (every
# production project loads it), src/Judge): PublishAot with a RID makes restore add the host RID's ILCompiler packs to
# the lock files, so it never runs on the checkout, whose lock files stay RID-free.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../../artifacts/aot-judge-smoke"
mkdir -p "$out"

root="$(cd "$here/../../.." && pwd)"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Judge | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi
bin="$out/bin/judge"
if ! ls "$out/bin" | grep -qE '^libe_sqlite3\.(dylib|so)$'; then
  echo "aot-judge-smoke: the bundled e_sqlite3 isn't next to the binary" >&2
  exit 1
fi

work="$(mktemp -d "$out/run.XXXXXX")"
trap 'find "$work" -delete' EXIT
state="$work/state"
mkdir -p "$state"

# Each call gets only these variables: offline, deterministic, the default log level.
# judge_with <providers> args...: the binary with AW_JUDGE_PROVIDERS=<providers>; judge: with rules
judge_with() { local providers="$1"; shift; env -i HOME="$work" PATH=/usr/bin:/bin AW_STATE_DIR="$state" AW_JUDGE_PROVIDERS="$providers" AW_SESSION_ID=smoke "$bin" "$@"; }
judge() { judge_with rules "$@"; }
brief='{"toolUseId":"toolu_1","sessionId":"s1","promptId":"p1","dispatchName":"builder-a","subagentType":"general-purpose","goal":"g","acceptanceCriteria":"the tests pass","proofCommand":"make test"}'
{
  echo "## health"
  judge health || echo "exit $?"
  echo "## brief save"
  printf '%s' "$brief" | judge brief save || echo "exit $?"
  echo "## brief get"
  judge brief get toolu_1 || echo "exit $?"
  echo "## brief get (none)"
  judge brief get toolu_2 || echo "exit $?"
  echo "## brief map-by-name"
  judge brief map-by-name builder-a || echo "exit $?"
  echo "## wake-gate (pre-rule)"
  printf '%s' '{"text":"ok","senderKind":"teammate"}' | judge wake-gate || echo "exit $?"
  echo "## ask-check (pre-rule)"
  printf '%s' '{"transcriptTail":"Tests pass. Next I will merge the PR."}' | judge ask-check || echo "exit $?"
  echo "## wake-gate (nothing decides)"
  printf '%s' '{"text":"progress on the parser, half done","senderKind":"teammate"}' | judge wake-gate || echo "exit $?"
  echo "## config get"
  judge config get || echo "exit $?"
  echo "## usage error"
  judge why || echo "exit $?"
  echo "## bad allowlist"
  printf '%s' '{"text":"ok","senderKind":"teammate"}' | judge --providers bogus wake-gate || echo "exit $?"
  echo "## bad AW_JUDGE_PROVIDERS under health"
  judge_with bogus health || echo "exit $?"
  echo "## health after"
  judge health || echo "exit $?"
  echo "## state files"
  (cd "$state" && find . -type f ! -name '*-wal' ! -name '*-shm' | sort)
} >"$out/raw.txt" 2>&1
sed -E -e "s#$work#<tmp>#g" -e 's/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/<id>/g' \
  -e 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:.]+(Z|[+-][0-9]{2}:[0-9]{2})/<time>/g' "$out/raw.txt" >"$out/actual.txt"
if ! cmp -s "$out/actual.txt" "$here/expected.txt"; then
  diff "$here/expected.txt" "$out/actual.txt" | head -40 >&2
  exit 1
fi

# ---- YamlDotNet under NativeAOT ----
yaml_version="$(sed -n 's/.*Include="YamlDotNet" Version="\[\([0-9.]*\)\]".*/\1/p' "$root/Directory.Packages.props" | head -1)"
[ -n "$yaml_version" ] || { echo "aot-judge-smoke: no pinned YamlDotNet version in Directory.Packages.props" >&2; exit 1; }
probe="$out/yaml-probe"
[ ! -d "$probe" ] || find "$probe" -delete
mkdir -p "$probe"
cp "$root/nuget.config" "$root/global.json" "$probe/"
# The probe sits under the repo, so empty Directory.Build files stop MSBuild picking the repo's up (its analyzers, lock-file guard
# and central package management); the one pin it needs is written here from the repo's.
printf '<Project />\n' >"$probe/Directory.Build.props"
printf '<Project />\n' >"$probe/Directory.Build.targets"
printf 'root = true\n' >"$probe/.editorconfig"
cat >"$probe/Directory.Packages.props" <<PINS
<Project>
  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
  <ItemGroup><PackageVersion Include="YamlDotNet" Version="[$yaml_version]" /></ItemGroup>
</Project>
PINS
cat >"$probe/yaml-probe.csproj" <<PROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" />
  </ItemGroup>
</Project>
PROJ
cat >"$probe/Program.cs" <<'CS'
using YamlDotNet.RepresentationModel;

const string trait = """
    version: 1
    name: no-todo
    subject: a source file
    requirement: leaves no TODO comment behind
    paths:
      - "src/**/*.ts"
      - "lib/{a,b}/*.ts"
    severity: block
    """;
var stream = new YamlStream();
stream.Load(new StringReader(trait));
var root = (YamlMappingNode)stream.Documents[0].RootNode;
foreach (var (key, value) in root.Children)
{
    Console.WriteLine(value is YamlSequenceNode items
        ? $"{key}: [{string.Join(", ", items.Children.Select(c => ((YamlScalarNode)c).Value))}]"
        : $"{key}: {((YamlScalarNode)value).Value}");
}
CS
dotnet publish "$probe/yaml-probe.csproj" -c Release -r "$rid" -p:PublishAot=true -o "$probe/bin" </dev/null >"$probe/publish.log" 2>&1 || { tail -20 "$probe/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$probe/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$probe/publish.log" >&2
  exit 1
fi
"$probe/bin/yaml-probe" >"$probe/actual.txt"
if ! cmp -s "$probe/actual.txt" "$here/yaml-probe-expected.txt"; then
  diff "$here/yaml-probe-expected.txt" "$probe/actual.txt" | head -20 >&2
  exit 1
fi

echo "aot-judge-smoke: native binary passed ($(du -h "$bin" | cut -f1) judge)"
