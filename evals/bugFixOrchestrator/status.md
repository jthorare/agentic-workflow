# Not run yet (#2910). Cases 1 and 2 need no per-repo config reader.
fire_rate: none
delta: none
last_run: none
pr: none

- bugFixOrchestrator-sibling-sweep and bugFixOrchestrator-stop-and-close are in this tree.
- bugFixOrchestrator-data-repair-never waits on #2909 (skills/_shared/repo-config.sh must exist for the dataRepair step to read the fixture repo.json). It lives on branch bugfix/gh-2910-data-repair-eval; add it back to evals/bugFixOrchestrator/behaviour/ once #2909 merges.
