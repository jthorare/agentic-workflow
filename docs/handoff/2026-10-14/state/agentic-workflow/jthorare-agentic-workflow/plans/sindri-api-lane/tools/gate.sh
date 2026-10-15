#!/bin/bash
# usage: gate.sh <worktree> <logprefix> — the AGENTS.md C# merge gate: whole-solution build (0 warnings), format, then
# the whole solution's unit and integration lanes with --no-build (every test project, including Aw.Sindri.Ui.Tests,
# whose fakes write the API goldens). Run it through tools/heavy.sh, which takes the box-wide slot for it.
cd "$1" || exit 2
P="$2"
dotnet build AgenticWorkflow.slnx -warnaserror -v q -clp:ErrorsOnly > "$P-build.log" 2>&1; echo "build rc=$?"
dotnet format AgenticWorkflow.slnx --verify-no-changes > "$P-fmt.log" 2>&1; echo "format rc=$?"
dotnet test --solution AgenticWorkflow.slnx --no-build --filter-trait "Lane=unit" --ignore-exit-code 8 > "$P-test.log" 2>&1; echo "unit rc=$?"
dotnet test --solution AgenticWorkflow.slnx --no-build --filter-trait "Lane=integration" --ignore-exit-code 8 > "$P-arch.log" 2>&1; echo "integration rc=$?"
