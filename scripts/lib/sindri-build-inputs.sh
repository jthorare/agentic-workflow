# shellcheck shell=bash
# Sourced by src/Sindri/aot-smoke/run.sh and scripts/install-cs.sh: the git pathspecs of every file the Sindri host
# publish reads, so the two
# scripts copy one list and cannot drift apart (a file outside src/ the build embeds, such as
# config/launcher/builtins.json, an AdditionalFiles banned-symbols list, or judge's embedded tests/fixtures/judge/images, is
# added here once).
# Use: git -C "$root" ls-files -z --cached --others --exclude-standard -- "${SINDRI_BUILD_PATHSPECS[@]}"
SINDRI_BUILD_PATHSPECS=(
  global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt config/launcher config/packs config/connections/presets docs/workflows
  tests/fixtures/judge/images
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/CodeIndex src/Connectors src/Context src/Embeddings src/Forge src/Judge src/Packets src/Sindri src/UiDriver
)

# Per-binary inputs (scripts/install-cs.sh, #3041): beside its project's ProjectReference closure, a binary's input set holds the
# root build files and the files outside src/ that the projects of the closure embed. install-cs.sh adds the first list to a binary
# whose closure holds a project under src/Sindri, the second to one that holds a project under src/Judge. Too broad is safe (the
# binary is published once more than needed), too narrow is a bug (a changed embedded file would not reach the build).
SINDRI_EXTRA_INPUTS=(config/launcher config/packs config/connections/presets docs/workflows)
JUDGE_EXTRA_INPUTS=(tests/fixtures/judge/images)
# src/Aw.Analyzers: Directory.Build.targets gives every production project an analyzer reference to it
ROOT_BUILD_INPUTS=(global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt src/Aw.Analyzers)
