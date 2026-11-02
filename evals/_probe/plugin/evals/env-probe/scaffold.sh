#!/usr/bin/env bash
# Pre-creates dirs the agent then tries to write into from Bash: one in the workspace, one under the run's HOME.
# Also records where the scaffold itself ran, and the AW_* values it saw.
set -eu
mkdir -p scaffold_dir "$HOME/.agentic-workflow/scaffold_home_dir"
{
  echo "SCAFFOLD_PWD=$PWD"
  echo "SCAFFOLD_HOME=$HOME"
  for v in AW_TOOLKIT AW_STATE_DIR AW_PROBE_RUN; do printf 'SCAFFOLD_VAR %s=%s\n' "$v" "${!v-<unset>}"; done
} > scaffold_dir/where.txt
