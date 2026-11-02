#!/usr/bin/env bash
# Helpers a skill's fixture.sh sources (spec 2026-10-25-skill-revamp-evals-design.md §4): build a small made-up git repo
# in the current directory. Source it, then call aw_fixture_repo. A fixture writes only under $PWD and sets no remote, so
# a stray `git push` has nowhere to go. scripts/skill-evals.sh --selftest runs fixture.sh with the real git on a scrubbed
# environment and lints it for deletes and writes outside $PWD. A real run calls the scaffold with the eval's stub git first
# on PATH and AW_GIT_MODE=readonly, so the setup runs in a subshell in full mode: its commit lands, the agent's mode doesn't change.

aw_fixture_repo() ( # [initial commit message]
  export AW_GIT_MODE=full
  git init -q -b main . || exit 1
  git config user.email "eval@example.invalid"
  git config user.name "eval"
  git config commit.gpgsign false
  git add -A
  git commit -q --allow-empty -m "${1:-fixture}"
)
