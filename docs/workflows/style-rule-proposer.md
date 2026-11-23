# Style-rule proposer

A starter template. It takes the findings of a steering-miner run, the mistakes you corrected more than once. For each of the top 3 that a deterministic check could catch, it drafts a rule record and the CI check that enforces it.

Template name: `style-rule-proposer` (issue 3787, piece 4). This first version ends at a notification that lists the drafts. Saving and staging them as proposals comes with the proposal store.

## What it does

1. `start` (manual) takes the payload below, and `check` (code) stops the run when it has no `repo` or no `findings` list, naming what is missing. It hands on the first 3 findings.
2. `rules` (`rule_catalog`) lists the existing rule records with their checks and sections.
3. `trim` (code) cuts that list down to two short ones, so the steps after it stay under the engine's 64 KB input cap. `known` holds each record's id, section and checks, and `vet` reads it. `brief` holds each record's id, section and the first 160 characters of its text, and `classify` reads it. `trim` keeps the two lists under 60,000 bytes together. When the catalog is bigger, it drops the records after the last that fit, in id order, and counts them in `more`. Those records are then unknown to `vet` and `classify`, so a duplicate of one of them is not caught.
4. `each` loops over those 3. The first ones are the top ranked, so a longer list is fine: the rest get no draft.
5. `classify` (prompt) says whether an analyzer, an architecture test or a banned symbol can enforce the mistake, or none. It answers none when an existing rule covers it.
6. `vet` (code) keeps the answer only when it is well formed and new. Anything else becomes `none`.
7. `is_rule` (condition) ends the item when there is no rule.
8. `draft` (`run_agent`) writes the record, the check and one line in `.agents/rules/csharp.md` on its own branch.
9. `entry` (code) notes the rule, the agent's report, the branch and the commit.
10. `gather` (collect) ends the loop, and `summary` (code) writes one line per draft.
11. `tell` (notify) shows the drafts, how many findings got no draft and how many were past the top 3.

## The payload

The payload needs `repo` and `findings`. A run without them fails at `check` before any model or agent is asked.

- `findings`: the `findings` of a steering-miner run's `rank` step, as `wf run-show` prints them.
- `repo`: the name of the toolkit checkout in your profile. The agent works in it.
- `run` (optional): the steering-miner run's id. The record's `source.ref` cites it as `steering-miner <run>`. Without it, the ref is `steering-miner`. An id with a space or other odd character is dropped.

## What `vet` checks

`vet` keeps an answer only when all of these hold:

- The kind is `analyzer`, `archunit` or `banned-symbol`. Any other kind is `none` with the reason `not_checkable`.
- An analyzer check id looks like `CA1234` (also `IDE`, `MA`, `RCS`). An archunit id looks like `ARCH-CQRS-001`. A banned symbol has no space or newline.
- The slug is lowercase words joined by `-`.
- The section is one the catalog lists, and the text and the reason are not empty. A miss is `none` with the reason `malformed`.
- No record has that slug, lists that check id or is named in `duplicateOf`. A miss is `none` with the reason `duplicate`. A `duplicateOf` that names no record is ignored.

## What the agent may do

- Write the record `rules/engineering/csharp/<slug>.yaml` and its entry in `rules/engineering/csharp.inventory.yaml`.
- Add the check: one `.editorconfig` line, an architecture test with its red fixture, or one `BannedSymbols` line.
- Add one line to `.agents/rules/csharp.md`.
- Run `node scripts/check-rules.mjs`, then try one light build of one project the rule covers.
- Commit on its own branch. It never pushes and never opens a pull request.

If the sandbox cannot run the build, the agent reports the check as `unbuilt` and goes on. That path is unverified: nobody has run this template's agent yet, so expect to build the branch yourself.

If the new rule fires on main, the agent reports how many violations it found. It fixes nothing outside the rule's own files.

No transcript ref goes into a record. `source.ref` cites the miner run, never a session. The agent sees the mistake's label and its short generic phrase, never the turns behind it.

## Run it by hand

1. Run `steering-miner` and read its `rank` step with `sindri wf run-show <run>`.
2. `sindri wf create style-rule-proposer --template style-rule-proposer`
3. `sindri wf activate style-rule-proposer`
4. `sindri wf run style-rule-proposer --payload '{"repo": "<toolkit repo>", "run": "<miner run>", "findings": [...]}'`
5. Each draft's branch is in the notification. Review it there.

## Limits

- Each draft is one agent session, up to 60 minutes, and it builds. That is why only the top 3 findings get one.
- The miner has no `code_style` label yet. `classify` looks at every finding, so most become `none`.
- The engine keeps 64 KB of a step's input and output. A cut output breaks the run: a later step that reads it fails. `trim` keeps its own output under 64 KB by the 60,000-byte rule above. Text full of quotes, backslashes or non-ASCII characters takes up to 6 bytes a character as JSON, so such a catalog fits fewer records. Today's 95 records fit whole (an estimate, not measured).
- A step's input is cut when stored, but its code still gets the whole input. `trim` reads the full catalog, so its stored input is cut past about 70 records. That does not break the run.
- `summary` counts the findings that got no draft, but not why. The reason of each `none` (`not_checkable`, `malformed` or `duplicate`) sits in that item's `vet` step.
- A draft whose agent fails stops the run.
