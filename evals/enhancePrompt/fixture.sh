#!/usr/bin/env bash
# Workspace for enhancePrompt-natural-1: a small recipe app whose planning docs exist on disk, so "the planning docs we've
# created" has something for the skill to read (the 2026-11-02 run had an empty repo and the model hand-wrote the prompt).
set -eu
. "$(dirname "${BASH_SOURCE[0]}")/../_lib/fixture-lib.sh"
mkdir -p docs/plans app tests
cat > AGENTS.md <<'Q'
# Recipe app

- Python 3.12, standard library only. Tests: `python -m unittest discover tests`.
- Work in a git worktree per feature (`git worktree add ../recipes-<feature> -b feat/<feature> main`); never commit to main.
- One logical change per commit, `type: short description`.
- Plans live in docs/plans/; a plan's task list is the checklist for its PR.
Q
cat > docs/plans/2026-11-01-shopping-list-design.md <<'Q'
# Shopping list from a week's recipes

## Goal
Build one shopping list from the recipes picked for a week, with the same ingredient from two recipes added up.

## Decisions
- Units: grams and millilitres only; "2 cups" is converted on import (app/units.py), never at list time.
- An ingredient with no quantity ("salt to taste") appears once, without a number.
- The list is sorted by store aisle (app/aisles.py), then by name.

## Out of scope
Prices, pantry stock, sharing the list.

## Done when
`python -m unittest discover tests` passes, including tests/test_shopping_list.py for: two recipes with the same ingredient,
a no-quantity ingredient, and aisle order.
Q
cat > docs/plans/2026-11-01-shopping-list-tasks.md <<'Q'
# Tasks: shopping list

1. [ ] app/shopping_list.py: build_list(recipes) -> list of (aisle, name, grams_or_ml_or_None).
2. [ ] Add up the same ingredient across recipes; keep a no-quantity ingredient once.
3. [ ] Sort by aisle (app/aisles.py), then name.
4. [ ] tests/test_shopping_list.py covering the three "Done when" cases.
Q
: > app/__init__.py
cat > app/aisles.py <<'Q'
AISLES = {"flour": 1, "sugar": 1, "milk": 2, "butter": 2, "salt": 3}


def aisle_of(name: str) -> int:
    return AISLES.get(name, 99)
Q
: > tests/__init__.py
aw_fixture_repo "recipe app with the shopping-list plan"
