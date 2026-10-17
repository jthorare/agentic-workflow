#!/usr/bin/env bash
# scripts/ci/changed-areas.sh — the CI path filter.
#
# Usage: changed-areas.sh <event-name> <base-sha> <base-ref> <default-branch>
#
# Prints GitHub step outputs (key=value lines) naming which areas a change
# touches, so each CI job can decide between real work and a no-op success:
#   bash=true|false      the bash tests need to run
#   dotnet=true|false    the .NET solution needs to build (spikes/ never counts)
#   packages=[...]       JSON array of the TS packages that changed
#
# Anything other than a pull_request into the default branch with a known base
# runs everything: a filter that can't tell must not skip a check. A stacked PR
# (based on another branch) runs everything too, because when its parent merges
# GitHub retargets it without a new run, and a narrowed result would carry over.

TS_PACKAGES=(scorer judge sindri skills/ui-evidence skills/bugFixOrchestrator)

# Packages whose tests import another package's source: a change to the key also
# runs the listed dependents (scorer's tests import judge/src/db.ts).
dependents_of() {
  case "$1" in
    judge) echo scorer ;;
  esac
}

json_array() {
  local out="" item
  for item in "$@"; do out+="${out:+,}\"$item\""; done
  printf '[%s]' "$out"
}

print_all() {
  printf 'bash=true\ndotnet=true\npackages=%s' "$(json_array "${TS_PACKAGES[@]}")"
}

# Reads changed paths, NUL-separated (git diff -z), on stdin and prints the outputs.
# NUL separation means no quoting: a non-ASCII or oddly named path arrives verbatim.
classify_changes() {
  local bash=false dotnet=false file pkg
  local -a changed_packages=()
  while IFS= read -r -d '' file || [ -n "$file" ]; do
    [ -n "$file" ] || continue
    case "$file" in
      .github/*|scripts/ci/*|.nvmrc) print_all; return 0 ;;
    esac
    case "$file" in
      spikes/*) continue ;;  # throwaway spike projects: no area, never the dotnet job
    esac
    case "$file" in
      scripts/dotnet-verify-packages.sh) dotnet=true ;;  # the .NET job runs it; its bash test runs below
      # the .NET job fetches the pins (sha256 check) and its Temporal tests and the Sindri host smoke run them
      binaries.lock) dotnet=true; bash=true ;;
      scripts/binaries-lock.sh) dotnet=true ;;  # the .NET job fetches with it; its bash test runs below
      # the Sindri build embeds it (Aw.Sindri.Infrastructure, onyx.lock) and its tests parse it; scripts/tests/onyx-lock.test.sh checks it
      context/onyx.lock) dotnet=true; bash=true ;;
      # global.json also pins the devcontainer's base image, which a bash suite checks (repo-templates).
      global.json|docs/devcontainer.md) bash=true ;;
    esac
    case "$file" in
      config/*|scripts/*|providers/*|setup.sh|mods/*|skills/*|.agents/*|AGENTS.md|CLAUDE.md|.claude/*|.cursor/*|.devcontainer/*|.env.example)
        bash=true ;;
      src/*|tests/*|schemas/packets/*|*.cs|*.csproj|*.props|*.targets|*.slnx|global.json|nuget.config|.editorconfig|BannedSymbols*.txt|packages.lock.json|.config/dotnet-tools.json)
        dotnet=true ;;
    esac
    case "$file" in
      # The dotnet job's hook-contract step runs these hooks against the C# judge
      # (config/lib/tests/judge-cs-hook-surface.test.sh); bash-tests.sh leaves that test to it.
      config/hooks/*|config/statusline.sh|config/lib/failopen.sh|config/lib/locks.sh|config/lib/tests/judge-cs-hook-surface.test.sh)
        dotnet=true ;;
    esac
    case "$file" in
      # scripts/ci/lane-budgets.mjs groups test projects by deps.yaml package; check-deps and check-rules read it too
      deps.yaml) dotnet=true; bash=true ;;
      # lane-budgets.mjs reads deps.yaml through this parser (scripts/* already runs the bash tests)
      scripts/deps/*) dotnet=true ;;
      schemas/packets/*) ;;  # the packet schemas belong to Aw.Packets (its golden test, the dotnet area above)
      # Aw.Evidence's lock interop tests source it: the C# lock adapter and the shell tools share one lock format
      config/lib/locks.sh) dotnet=true ;;
      schemas/*|rules/*) bash=true ;;  # check-deps and check-rules (and their suites) read these
      # scripts/tests/ci-lane-budgets.test.sh checks lane-budgets.mjs against these numbers
      tests/Aw.Kernel.Testing/Lanes.cs) bash=true ;;
      # the launcher checks (check-launcher.mjs, check-profile-doc.mjs) read these; they sit under src/ and tests/ (dotnet) or docs/ (neither)
      src/Sindri/Aw.Sindri.Core/Profile/*) dotnet=true; bash=true ;;
      tests/fixtures/launcher/*|config/launcher/*|schemas/launcher.schema.json) bash=true ;;
      # scripts/tests/scoping-fixture.test.sh and line-fixture.test.sh build their fixture's history and run its self-check (dotnet comes from tests/*)
      tests/fixtures/scoping/*|tests/fixtures/line/*) dotnet=true; bash=true ;;
      docs/csharp/sindri-profile.md|docs/csharp/sindri-launcher.md) bash=true ;;
      # check-fleet.mjs (and its suite) validate the fleet state schema's fixtures and the /proc-shaped sessions tree; both sit under tests/ (dotnet) too
      tests/fixtures/fleet/*|tests/fixtures/sessions/*) bash=true ;;
      docs/csharp/sindri-sessions.md) bash=true ;;
    esac
    for pkg in "${TS_PACKAGES[@]}"; do
      case "$file" in
        "$pkg"/*)
          bash=true
          local p
          for p in "$pkg" $(dependents_of "$pkg"); do
            case " ${changed_packages[*]-} " in
              *" $p "*) ;;
              *) changed_packages+=("$p") ;;
            esac
          done
          ;;
      esac
    done
  done
  printf 'bash=%s\ndotnet=%s\npackages=%s' "$bash" "$dotnet" \
    "$(json_array ${changed_packages[@]+"${changed_packages[@]}"})"
}

main() {
  local event="$1" base="$2" base_ref="${3:-}" default_branch="${4:-}"
  if [ "$event" != "pull_request" ] || [ -z "$base" ] || [ -z "$default_branch" ] \
     || [ "$base_ref" != "$default_branch" ]; then
    print_all
    return 0
  fi
  # --no-renames lists both sides of a move, so a file moved out of a package still
  # selects it; -z turns off path quoting (core.quotePath) for non-ASCII names.
  git diff --no-renames --name-only -z "$base" HEAD | classify_changes
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  set -euo pipefail
  main "${1:-}" "${2:-}" "${3:-}" "${4:-}"
  echo
fi
