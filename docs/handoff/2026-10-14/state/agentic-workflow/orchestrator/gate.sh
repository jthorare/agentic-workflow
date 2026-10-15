#!/usr/bin/env bash
# gate.sh <pr> <scope...>   scopes: dotnet ts-sindri ts-scorer bash-lib bash-scripts repo
# Merges the PR head onto current origin/main in a scratch worktree and runs the merge-gate parts named.
set -u
PR="$1"; shift
REPO="${AW_REPO:-$HOME/repos/agentic-workflow}"
SP="$HOME/.agentic-workflow/orchestrator"
WT="$SP/gate-$PR"
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
cd "$REPO" && git fetch -q origin
HEAD_SHA=$(gh pr view "$PR" --json headRefOid --jq .headRefOid)
git fetch -q origin "pull/$PR/head"
[ -d "$WT" ] && git worktree remove --force "$WT"
git worktree prune
git worktree add -q --detach "$WT" origin/main || { echo "RESULT pr=$PR ERROR worktree add failed"; exit 2; }
cd "$WT" || { echo "RESULT pr=$PR ERROR cd failed"; exit 2; }
[ "$(cd "$(git rev-parse --show-toplevel)" && pwd -P)" = "$(cd "$WT" && pwd -P)" ] || { echo "RESULT pr=$PR ERROR not in gate worktree"; exit 2; }
if ! git -c user.name=gate -c user.email=gate@local merge -q --no-ff --no-edit "$HEAD_SHA" >/dev/null 2>&1; then
  echo "RESULT pr=$PR CONFLICT with main"; git diff --name-only --diff-filter=U; exit 1
fi
echo "merged $HEAD_SHA onto $(git rev-parse --short origin/main)"
fail=0
HEAVY="$HOME/.agentic-workflow/bin-r1b/heavy"
export AW_HEAVY_SLOTS=2 AW_SESSION_HEAVY_SLOTS=1
step() { local name="$1"; shift; if "$@" > "$SP/gate-$PR-$name.log" 2>&1; then echo "ok   $name"; else echo "FAIL $name (log: gate-$PR-$name.log)"; fail=1; fi; }
for s in "$@"; do
  case "$s" in
    repo)
      step sync-rules scripts/sync-rules.sh --check
      step check-deps node scripts/check-deps.mjs
      step check-rules node scripts/check-rules.mjs
      step env-example scripts/check-env-example.sh
      step no-any bash -c 'npm ci --silent --prefix .github/ci-tools --ignore-scripts && node scripts/ci/no-any.mjs' ;;
    dotnet)
      step build "$HEAVY" dotnet build AgenticWorkflow.slnx -warnaserror -v q
      step unit "$HEAVY" dotnet test --solution AgenticWorkflow.slnx --no-build --filter-trait "Lane=unit" --ignore-exit-code 8
      step integration "$HEAVY" dotnet test --solution AgenticWorkflow.slnx --no-build --filter-trait "Lane=integration" --ignore-exit-code 8
      step format "$HEAVY" --expensive dotnet format AgenticWorkflow.slnx --verify-no-changes -v q
      step verify-packages scripts/dotnet-verify-packages.sh ;;
    ts-sindri) step ts-sindri "$HEAVY" bash -c 'cd sindri && npm ci --silent && npx tsc --noEmit && npx vitest run' ;;
    ts-scorer) step ts-scorer "$HEAVY" bash -c 'npm ci --silent --prefix judge && cd scorer && npm ci --silent && npx tsc --noEmit && npx vitest run' ;;
    ts-ui-evidence) step ts-ui-evidence "$HEAVY" bash -c 'cd skills/ui-evidence && npm ci --silent && npx tsc --noEmit && npx vitest run' ;;
    bash-lib) step bash-lib "$HEAVY" bash -c 'r=0; for t in config/lib/tests/*.test.sh; do bash "$t" >/dev/null 2>&1 || { echo "FAILED $t"; r=1; }; done; exit $r' ;;
    bash-scripts) step bash-scripts "$HEAVY" bash -c 'r=0; for t in scripts/tests/*.test.sh; do bash "$t" >/dev/null 2>&1 || { echo "FAILED $t"; r=1; }; done; exit $r' ;;
    *) echo "FAIL unknown scope '$s' (scopes are space-separated)"; fail=1 ;;
  esac
done
echo "RESULT pr=$PR $([ $fail = 0 ] && echo PASS || echo FAIL)"
