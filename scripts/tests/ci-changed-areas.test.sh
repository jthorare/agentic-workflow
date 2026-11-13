#!/usr/bin/env bash
# Tests for scripts/ci/changed-areas.sh — the CI path filter. A wrong answer here
# either skips a check that should run or runs everything on a docs change.
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; return 0; }
trap __aw_tmp_clean EXIT
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

expect "docs-only change runs nothing area-specific" \
  "$(printf 'docs/readme.md\nplanning/TESTING.md\n')" \
  "$(printf 'bash=false\ndotnet=false\ntla=false')"

expect "a hook change runs the bash tests and dotnet (its hook-contract step runs the hooks against the C# judge)" \
  "$(printf 'config/hooks/send-gate.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the statusline, the hooks' shared libraries or the hook-contract test also run dotnet" \
  "$(printf 'config/statusline.sh\nconfig/lib/failopen.sh\nconfig/lib/locks.sh\nconfig/lib/tests/judge-cs-hook-surface.test.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "another config/lib change runs bash tests only" \
  "$(printf 'config/lib/merge-hook.sh\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=false')"

expect "a devcontainer or .env.example change runs the bash tests (their checks are bash suites)" \
  "$(printf '.devcontainer/Dockerfile\n.env.example\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=false')"

expect "an SDK pin change runs dotnet and the bash tests (the devcontainer pin check)" \
  "$(printf 'global.json\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the devcontainer doc runs the bash tests (repo-templates checks its wording)" \
  "$(printf 'docs/devcontainer.md\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=false')"

expect "a schema or rule record change runs the bash tests (their checks' suites copy them; deps.yaml is below)" \
  "$(printf 'schemas/rule-record.schema.json\nrules/engineering/csharp/method-length.yaml\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=false')"

expect "the shared lock library runs dotnet too (Evidence's lock interop tests source it)" \
  "$(printf 'config/lib/locks.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the launcher checks' inputs run the bash tests (check-launcher.mjs and check-profile-doc.mjs suites)" \
  "$(printf 'tests/fixtures/launcher/manifest.json\nconfig/launcher/builtins.json\nschemas/launcher.schema.json\ndocs/csharp/sindri-profile.md\ndocs/csharp/sindri-launcher.md\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the fleet fixtures and the sessions tree run the bash tests (check-fleet.mjs reads them) and dotnet (the C# readers will)" \
  "$(printf 'tests/fixtures/fleet/claude-code/stop.json\ntests/fixtures/sessions/proc/211/stat\nschemas/fleet-state.schema.json\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the sessions doc alone runs bash only" \
  "$(printf 'docs/csharp/sindri-sessions.md\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=false')"

expect "SindriProfile.cs runs bash and dotnet" \
  "$(printf 'src/Sindri/Aw.Sindri.Core/Profile/SindriProfile.cs\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "WorkflowSettings.cs (any Profile record) runs bash and dotnet" \
  "$(printf 'src/Sindri/Aw.Sindri.Core/Profile/WorkflowSettings.cs\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "a launcher doc alone runs bash only" \
  "$(printf 'docs/csharp/sindri-launcher.md\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=false')"

expect "a C# change runs dotnet only" \
  "$(printf 'src/Aw.Kernel/Result.cs\nDirectory.Packages.props\n')" \
  "$(printf 'bash=false\ndotnet=true\ntla=false')"

expect "an AOT smoke script or its expected output runs dotnet" \
  "$(printf 'src/Aw.Kernel.AotSmoke/run.sh\nsrc/Aw.Kernel.Data.AotSmoke/expected.txt\n')" \
  "$(printf 'bash=false\ndotnet=true\ntla=false')"

expect "a packet JSON Schema change runs dotnet (its golden test compares it with the records)" \
  "$(printf 'schemas/packets/tasks.schema.json\n')" \
  "$(printf 'bash=false\ndotnet=true\ntla=false')"

expect "a .NET tool manifest change runs dotnet" \
  "$(printf '.config/dotnet-tools.json\n')" \
  "$(printf 'bash=false\ndotnet=true\ntla=false')"

expect "deps.yaml or its parser runs dotnet (lane budgets group test projects by package) and the bash tests" \
  "$(printf 'deps.yaml\nscripts/deps/yaml.mjs\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "Lanes.cs also runs the bash tests (ci-lane-budgets checks lane-budgets.mjs against its numbers)" \
  "$(printf 'tests/Aw.Kernel.Testing/Lanes.cs\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the NuGet supply-chain script runs dotnet (where CI runs it) and bash (its test)" \
  "$(printf 'scripts/dotnet-verify-packages.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "a binaries.lock pin runs dotnet (it fetches and sha256-checks the pins, and its Temporal tests and host smoke run them) and bash (its test)" \
  "$(printf 'binaries.lock\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the binaries.lock script runs dotnet (where CI fetches with it) and bash (its test)" \
  "$(printf 'scripts/binaries-lock.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=false')"

expect "the Vale config and style run the bash tests (check-prose.test.sh runs them)" \
  "$(printf '.vale.ini\n.vale/styles/Aw/Slop.yml\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=false')"

expect "spike projects never run dotnet" \
  "$(printf 'spikes/aot/Spike.csproj\nspikes/aot/Program.cs\nspikes/Directory.Build.props\n')" \
  "$(printf 'bash=false\ndotnet=false\ntla=false')"

expect "a workflow change runs everything" \
  "$(printf '.github/workflows/ci.yml\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=true')"

expect "a Node version change runs everything" \
  "$(printf '.nvmrc\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=true')"

expect "a CI script change runs everything" \
  "$(printf 'scripts/ci/bash-tests.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\ntla=true')"

expect "a TLA+ spec, its runner or the formal-verify skill runs the TLC job (the last two also run the bash tests)" \
  "$(printf 'specs/tla/LedgerProjector/LedgerProjector.tla\n')" \
  "$(printf 'bash=false\ndotnet=false\ntla=true')"

expect "the TLC runner and its test run the bash tests; the runner also runs the TLC job" \
  "$(printf 'scripts/tlc-check.sh\nscripts/tests/tlc-check.test.sh\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=true')"

expect "the formal-verify skill runs the TLC job and the bash tests" \
  "$(printf 'skills/formal-verify/scripts/tlc.sh\n')" \
  "$(printf 'bash=true\ndotnet=false\ntla=true')"

expect "a lookalike prefix is not an area" \
  "$(printf 'judgement/notes.md\n')" \
  "$(printf 'bash=false\ndotnet=false\ntla=false')"

# A throwaway git repo with git's defaults pinned on (rename detection, quoted
# paths), so the real `main` diff is exercised, not just classify_changes.
scratch_repo() {
  local repo; repo="$(mktemp -d)"
  git -C "$repo" init -q
  git -C "$repo" config user.email ci@example.invalid
  git -C "$repo" config user.name ci
  git -C "$repo" config diff.renames true
  git -C "$repo" config core.quotePath true
  mkdir -p "$repo/scripts" "$repo/docs"
  printf 'echo db\n' > "$repo/scripts/db.sh"
  printf '# readme\n' > "$repo/docs/readme.md"
  git -C "$repo" add -A && git -C "$repo" commit -qm base
  echo "$repo"
}

test_rename_out_of_an_area_selects_the_area() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  git -C "$repo" mv scripts/db.sh docs/db.sh && git -C "$repo" commit -qm move
  got="$(cd "$repo" && main pull_request "$base" trunk trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=false\ntla=false')" ] \
    || { echo "FAIL: a rename out of scripts/ must select bash, got: $got"; exit 1; }
  echo "PASS: test_rename_out_of_an_area_selects_the_area"
}

test_non_ascii_filename_in_an_area_selects_the_area() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  printf 'echo e\n' > "$repo/scripts/$(printf '\303\251').sh"
  git -C "$repo" add -A && git -C "$repo" commit -qm accent
  got="$(cd "$repo" && main pull_request "$base" trunk trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=false\ntla=false')" ] \
    || { echo "FAIL: a non-ASCII file in scripts/ must select bash, got: $got"; exit 1; }
  echo "PASS: test_non_ascii_filename_in_an_area_selects_the_area"
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
  [ "$got" = "$(printf 'bash=true\ndotnet=true\ntla=true')" ] \
    || { echo "FAIL: a PR into a non-default branch must run everything, got: $got"; exit 1; }
  echo "PASS: test_a_stacked_pr_runs_everything"
}

test_a_pr_with_an_unknown_default_branch_runs_everything() {
  local got
  got="$(main pull_request deadbeef feat/x "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\ntla=true')" ] \
    || { echo "FAIL: an unknown default branch must fail closed, got: $got"; exit 1; }
  echo "PASS: test_a_pr_with_an_unknown_default_branch_runs_everything"
}

test_non_pr_event_runs_everything() {
  local got
  got="$(main push "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\ntla=true')" ] \
    || { echo "FAIL: push event should run everything, got: $got"; exit 1; }
  echo "PASS: test_non_pr_event_runs_everything"
}

test_pr_without_base_fails_closed() {
  local got
  got="$(main pull_request "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\ntla=true')" ] \
    || { echo "FAIL: a PR with no base sha should run everything, got: $got"; exit 1; }
  echo "PASS: test_pr_without_base_fails_closed"
}

# A run-everything path listed first, with far more paths after it than a pipe
# holds: the filter stops classifying early, and git must still exit cleanly.
test_a_large_diff_that_runs_everything_exits_zero() {
  local repo base got code=0
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  mkdir -p "$repo/.github" "$repo/docs/many"
  printf 'on: push\n' > "$repo/.github/ci.yml"
  for i in $(seq 1 4000); do : > "$repo/docs/many/page-with-a-long-name-$i.md"; done
  git -C "$repo" add -A && git -C "$repo" commit -qm many
  got="$(cd "$repo" && bash "$DIR/../ci/changed-areas.sh" pull_request "$base" trunk trunk)" || code=$?
  rm -rf "$repo"
  [ "$code" = 0 ] && [ "$got" = "$(printf 'bash=true\ndotnet=true\ntla=true')" ] \
    || { echo "FAIL: a large diff touching .github/ must exit 0 and run everything, got exit $code: $got"; exit 1; }
  echo "PASS: test_a_large_diff_that_runs_everything_exits_zero"
}

test_non_pr_event_runs_everything
test_pr_without_base_fails_closed
test_a_large_diff_that_runs_everything_exits_zero
test_rename_out_of_an_area_selects_the_area
test_non_ascii_filename_in_an_area_selects_the_area
test_a_stacked_pr_runs_everything
test_a_pr_with_an_unknown_default_branch_runs_everything
