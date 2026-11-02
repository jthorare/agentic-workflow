Ran shipRelease with --no-deploy on feat/halve-servings against main.

- Working tree clean. `git fetch origin` then `git rebase origin/main`: nothing to replay.
- Tests: runner is `npm test` (node --test). 7 tests, 7 passed, 0 failed.
- Coverage: not available (no coverage tooling in package.json).
- Verification: n/a (the diff touches only src/scale.js and test/, no UI or routes).
- Duplicate check: `gh pr list --head feat/halve-servings --base main` returned none.

Would run:
- `git push origin feat/halve-servings`
- `gh pr create --base main --head feat/halve-servings --title "feat/halve-servings" --body "..."`

Deploy: skipped (--no-deploy). Docs: /syncDocs not run in this session.
