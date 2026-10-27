# PR cards and deep dives

`scripts/build_prs.py` turns `prs/pr-NN.md` (one markdown file per PR) into milestone-grouped cards and one shared `<dialog>` that holds each PR's deep dive. Put this placeholder in the TDD's PR-order section, after the diagram:

```html
<!-- PR-CARDS -->
```

Each `prs/pr-NN.md`:

```markdown
---
milestone: "M1 · Short name"          # cards group under this, in first-seen order
milestone_note: "no visible change"   # optional tag beside the milestone name
title: Short PR title                 # optional; default: the H1 minus "PR N:"
change: One line on what changes      # optional; default: first sentence of ## Summary
rollback: How to undo it              # optional; default: the text after "Rollback:" in ## Summary
---
# PR 4: Short PR title

## Summary
What changes and why it is safe alone. Rollback: how to undo it.

## Code changes
| path:line | change |
|---|---|
| src/Thing/Repo.ts:664 | what changes there |

## Graph context
Sindri neighbors: callers, readers and writers of each touched symbol.

## Tests
What proves it, by name.
```

Screenshots of the pages a PR touches: pass `--shots shots.json`, a list of `{"prs": [4], "file": "shots/orders.png", "caption": "...", "page": "...", "route": "..."}` (`file` relative to the manifest). They render above the deep dive.
