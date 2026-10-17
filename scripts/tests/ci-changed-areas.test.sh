#!/usr/bin/env bash
# Tests for scripts/ci/changed-areas.sh — the CI path filter. A wrong answer here
# either skips a check that should run or runs everything on a docs change.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../ci/changed-areas.sh"

expect() {
  local name="$1" files="$2" want="$3" got
  # classify_changes reads git's -z output: NUL-separated paths.
  got="$(printf '%s' "$files" | tr '\n' '\0' | classify_changes)"
  if [ "$got" != "$want" ]; then
    printf 'FAIL: %s\n--- want\n%s\n--- got\n%s\n' "$name" "$want" "$got"
    exit 1
  fi
  echo "PASS: $name"
}

ALL='["scorer","judge","sindri","skills/ui-evidence","skills/bugFixOrchestrator"]'

expect "docs-only change runs nothing area-specific" \
  "$(printf 'docs/readme.md\nplanning/TESTING.md\n')" \
  "$(printf 'bash=false\ndotnet=false\npackages=[]')"

expect "one package change runs that package and the bash tests" \
  "$(printf 'scorer/src/index.ts\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=["scorer"]')"

expect "nested skill package is matched by its full path" \
  "$(printf 'skills/ui-evidence/src/run.ts\nskills/review/SKILL.md\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=["skills/ui-evidence"]')"

expect "a judge change also runs scorer, whose tests import judge's source" \
  "$(printf 'judge/src/db.ts\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=["judge","scorer"]')"

expect "a hook change runs the bash tests and dotnet (its hook-contract step runs the hooks against the C# judge)" \
  "$(printf 'config/hooks/send-gate.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the statusline, the hooks' shared libraries or the hook-contract test also run dotnet" \
  "$(printf 'config/statusline.sh\nconfig/lib/failopen.sh\nconfig/lib/locks.sh\nconfig/lib/tests/judge-cs-hook-surface.test.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "another config/lib change runs bash tests only" \
  "$(printf 'config/lib/merge-hook.sh\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=[]')"

expect "a devcontainer or .env.example change runs the bash tests (their checks are bash suites)" \
  "$(printf '.devcontainer/Dockerfile\n.env.example\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=[]')"

expect "an SDK pin change runs dotnet and the bash tests (the devcontainer pin check)" \
  "$(printf 'global.json\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the devcontainer doc runs the bash tests (repo-templates checks its wording)" \
  "$(printf 'docs/devcontainer.md\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=[]')"

expect "a schema or rule record change runs the bash tests (their checks' suites copy them; deps.yaml is below)" \
  "$(printf 'schemas/rule-record.schema.json\nrules/engineering/csharp/method-length.yaml\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=[]')"

expect "the shared lock library runs dotnet too (Evidence's lock interop tests source it)" \
  "$(printf 'config/lib/locks.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the launcher checks' inputs run the bash tests (check-launcher.mjs and check-profile-doc.mjs suites)" \
  "$(printf 'tests/fixtures/launcher/manifest.json\nconfig/launcher/builtins.json\nschemas/launcher.schema.json\ndocs/csharp/sindri-profile.md\ndocs/csharp/sindri-launcher.md\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the fleet fixtures and the sessions tree run the bash tests (check-fleet.mjs reads them) and dotnet (the C# readers will)" \
  "$(printf 'tests/fixtures/fleet/claude-code/stop.json\ntests/fixtures/sessions/proc/211/stat\nschemas/fleet-state.schema.json\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the sessions doc alone runs bash only" \
  "$(printf 'docs/csharp/sindri-sessions.md\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=[]')"

expect "the scoping fixture runs the bash tests (scoping-fixture.test.sh builds its history)" \
  "$(printf 'tests/fixtures/scoping/gold/expiry.json\ntests/fixtures/scoping/history/build.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the line fixture runs the bash tests (line-fixture.test.sh builds its history)" \
  "$(printf 'tests/fixtures/line/gold/plan/expiry/packet.json\ntests/fixtures/line/history/build.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "SindriProfile.cs runs bash and dotnet" \
  "$(printf 'src/Sindri/Aw.Sindri.Core/Profile/SindriProfile.cs\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "WorkflowSettings.cs (any Profile record) runs bash and dotnet" \
  "$(printf 'src/Sindri/Aw.Sindri.Core/Profile/WorkflowSettings.cs\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "a launcher doc alone runs bash only" \
  "$(printf 'docs/csharp/sindri-launcher.md\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=[]')"

expect "a C# change runs dotnet only" \
  "$(printf 'src/Aw.Kernel/Result.cs\nDirectory.Packages.props\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "an AOT smoke script or its expected output runs dotnet" \
  "$(printf 'src/Aw.Kernel.AotSmoke/run.sh\nsrc/Aw.Kernel.Data.AotSmoke/expected.txt\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "a packet JSON Schema change runs dotnet (its golden test compares it with the records)" \
  "$(printf 'schemas/packets/tasks.schema.json\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "a .NET tool manifest change runs dotnet" \
  "$(printf '.config/dotnet-tools.json\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "deps.yaml or its parser runs dotnet (lane budgets group test projects by package) and the bash tests" \
  "$(printf 'deps.yaml\nscripts/deps/yaml.mjs\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "Lanes.cs also runs the bash tests (ci-lane-budgets checks lane-budgets.mjs against its numbers)" \
  "$(printf 'tests/Aw.Kernel.Testing/Lanes.cs\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the NuGet supply-chain script runs dotnet (where CI runs it) and bash (its test)" \
  "$(printf 'scripts/dotnet-verify-packages.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "a binaries.lock pin runs dotnet (it fetches and sha256-checks the pins, and its Temporal tests and host smoke run them) and bash (its test)" \
  "$(printf 'binaries.lock\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the binaries.lock script runs dotnet (where CI fetches with it) and bash (its test)" \
  "$(printf 'scripts/binaries-lock.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "the Onyx pins run dotnet (embedded in the Sindri build, parsed by its tests) and bash (their check)" \
  "$(printf 'context/onyx.lock\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "spike projects never run dotnet" \
  "$(printf 'spikes/aot/Spike.csproj\nspikes/aot/Program.cs\nspikes/Directory.Build.props\n')" \
  "$(printf 'bash=false\ndotnet=false\npackages=[]')"

expect "a workflow change runs everything" \
  "$(printf '.github/workflows/ci.yml\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a Node version change runs everything" \
  "$(printf '.nvmrc\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a CI script change runs everything" \
  "$(printf 'scripts/ci/bash-tests.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a lookalike prefix is not a package" \
  "$(printf 'judgement/notes.md\n')" \
  "$(printf 'bash=false\ndotnet=false\npackages=[]')"

# A throwaway git repo with git's defaults pinned on (rename detection, quoted
# paths), so the real `main` diff is exercised, not just classify_changes.
scratch_repo() {
  local repo; repo="$(mktemp -d)"
  git -C "$repo" init -q
  git -C "$repo" config user.email ci@example.invalid
  git -C "$repo" config user.name ci
  git -C "$repo" config diff.renames true
  git -C "$repo" config core.quotePath true
  mkdir -p "$repo/scorer/src" "$repo/docs"
  printf 'export const db = 1;\n' > "$repo/scorer/src/db.ts"
  printf '# readme\n' > "$repo/docs/readme.md"
  git -C "$repo" add -A && git -C "$repo" commit -qm base
  echo "$repo"
}

test_rename_out_of_a_package_selects_the_package() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  git -C "$repo" mv scorer/src/db.ts docs/db.ts && git -C "$repo" commit -qm move
  got="$(cd "$repo" && main pull_request "$base" trunk trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=false\npackages=["scorer"]')" ] \
    || { echo "FAIL: a rename out of scorer must select it, got: $got"; exit 1; }
  echo "PASS: test_rename_out_of_a_package_selects_the_package"
}

test_non_ascii_filename_in_a_package_selects_the_package() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  printf 'export const e = 1;\n' > "$repo/scorer/src/$(printf '\303\251').ts"
  git -C "$repo" add -A && git -C "$repo" commit -qm accent
  got="$(cd "$repo" && main pull_request "$base" trunk trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=false\npackages=["scorer"]')" ] \
    || { echo "FAIL: a non-ASCII file in scorer must select it, got: $got"; exit 1; }
  echo "PASS: test_non_ascii_filename_in_a_package_selects_the_package"
}

test_a_stacked_pr_runs_everything() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  printf '# more\n' >> "$repo/docs/readme.md"
  git -C "$repo" add -A && git -C "$repo" commit -qm docs
  # Based on another feature branch, not the default one: after the parent merges,
  # GitHub retargets the PR without a new run, so the filter must not narrow here.
  got="$(cd "$repo" && main pull_request "$base" feat/parent trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: a PR into a non-default branch must run everything, got: $got"; exit 1; }
  echo "PASS: test_a_stacked_pr_runs_everything"
}

test_a_pr_with_an_unknown_default_branch_runs_everything() {
  local got
  got="$(main pull_request deadbeef feat/x "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: an unknown default branch must fail closed, got: $got"; exit 1; }
  echo "PASS: test_a_pr_with_an_unknown_default_branch_runs_everything"
}

test_non_pr_event_runs_everything() {
  local got
  got="$(main push "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: push event should run everything, got: $got"; exit 1; }
  echo "PASS: test_non_pr_event_runs_everything"
}

test_pr_without_base_fails_closed() {
  local got
  got="$(main pull_request "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: a PR with no base sha should run everything, got: $got"; exit 1; }
  echo "PASS: test_pr_without_base_fails_closed"
}

test_non_pr_event_runs_everything
test_pr_without_base_fails_closed
test_rename_out_of_a_package_selects_the_package
test_non_ascii_filename_in_a_package_selects_the_package
test_a_stacked_pr_runs_everything
test_a_pr_with_an_unknown_default_branch_runs_everything
