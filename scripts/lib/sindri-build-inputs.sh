# shellcheck shell=bash
# Sourced by src/Sindri/aot-smoke/run.sh and scripts/install-cs.sh: the git pathspecs of every file the Sindri host
# publish reads (recipes/sindri is the shipped recipes; scripts/install-cs.sh embeds HEAD's copy of them), so the two
# scripts copy one list and cannot drift apart (a file outside src/ the build embeds, such as
# config/launcher/builtins.json, or an AdditionalFiles banned-symbols list, is added here once).
# Use: git -C "$root" ls-files -z --cached --others --exclude-standard -- "${SINDRI_BUILD_PATHSPECS[@]}"
SINDRI_BUILD_PATHSPECS=(
  global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt config/launcher config/packs schemas/scoping
  context/onyx.lock   # embedded by Aw.Sindri.Infrastructure until #1598 step 8 deletes the Onyx code
  recipes/sindri src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/CodeIndex src/Connectors src/Context src/Embeddings src/Forge src/Judge src/Packets src/Sindri src/UiDriver
)
