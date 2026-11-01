# Pin hand check (concept pins Q6, Part of #1372)

The hand check for turning `context.ranker.pinLeg` on (spec `docs/superpowers/specs/2026-10-29-sindri-concept-pins-query-design.md`,
Q6 and "Bar for turning the pin leg on"). This is the worked example, on this repo's own map with the six pins below. For
Q6 on a work repo, the owner swaps in that repo's pins and rows on their own box (work names never go in this repo); the
table, the columns and the bar stay the same.

Every question is one this repo's sessions really ask about an area, where the area's word is shared with other areas, so an
unpinned query has a reason to rank the wrong doc first. Question 4 uses an alias word from ANOTHER pin on purpose ("repro" is
bugfix's, the question is about the sandbox): the pin leg must not push its right answer out of the top 5.

**Owner:** tick keep or drop in the Keep column only. A dropped row is replaced from the spares at the end. Everything after
that is filled in by the run.

## Pins used

Paste into the profile (`index.concepts`), then `sindri profile approve`. Paths are this repo's folders, so no feature keys
are needed and the pins hold the same files on every rebuild.

```yaml
index:
  concepts:
    - id: meta
      name: Meta harness
      summary: The team of lane sessions, their gates, merges and worktrees
      paths: ["src/Meta/**", "skills/meta/**", "scripts/meta-launch.sh", "scripts/install-meta.sh"]
      aliases: ["lanes", "merge gate"]
    - id: bugfix
      name: Bug fix orchestrator
      summary: A bug is fixed only when the check that failed before the fix passes after it
      paths: ["src/BugFix/**", "skills/bugFixOrchestrator/**"]
      aliases: ["red check", "repro"]
    - id: evidence
      name: UI evidence
      summary: Screenshot runs, pixel parity and publishing them to a PR
      paths: ["src/Evidence/**", "skills/ui-evidence/**"]
      aliases: ["ui-evidence", "parity", "screenshots"]
    - id: sandbox
      name: Sandbox
      summary: What an agent Step may read, write and reach on the network
      paths: ["src/Sindri/*/Sandbox/**", "src/Sindri/Aw.Sindri.Ui/*/Sandbox/**"]
      aliases: ["learn mode", "agent home"]
    - id: code-index
      name: Code index
      summary: Symbols, callers and callees per repo
      paths: ["src/CodeIndex/**", "context/code-index*"]
      aliases: ["graphify", "callers", "callees"]
    - id: scoping
      name: Scoping station
      summary: Turning an item into a frozen scoping packet
      paths: ["src/Sindri/*/Scoping/**", "src/Sindri/Aw.Sindri.Host/Commands/Scoping/**", "src/Packets/Aw.Packets.Core/Kinds/Scoping/**"]
      aliases: ["intake", "scoping desk"]
```

## Questions

Columns after Keep, per question, three runs (runbook step 7): **A** `sindri context query "<question>"` with the leg off,
**B** the same with `--concept <pin>`, **C** A again with `context.ranker.pinLeg: on`. Tick when the expected answer is in the
top 5. **Alias** is ticked when the answer's notes say `anchored on concept:<pin> (alias "<word>")`.

| # | Keep | Question | Pin | Alias word | Expected top answer | Why an unpinned query can miss | A | B | C | Alias |
|---|------|----------|-----|------------|---------------------|-------------------------------|---|---|---|-------|
| 1 | [ ] keep [ ] drop | how do the lanes get their worktrees cleaned up after a merge? | meta | lanes | `docs/csharp/meta.md` "Worktree cleanup" (`worktree-sweep`) | the scoping and bugfix docs also talk about worktrees | [ ] | [ ] | [ ] | [ ] |
| 2 | [ ] keep [ ] drop | what does the merge gate run before a ready PR is merged? | meta | merge gate | `docs/csharp/meta.md` `gate-run` and `merge-check` | the Merge Gate list in `AGENTS.md` and the readiness doc match "gate" too | [ ] | [ ] | [ ] | [ ] |
| 3 | [ ] keep [ ] drop | why must the red check fail at the baseline commit before a fix counts? | bugfix | red check | `docs/csharp/bugfix.md` "The check reproduces the bug" | "baseline" is also the readiness baseline (`docs/readiness/baseline-2026-10.md`) | [ ] | [ ] | [ ] | [ ] |
| 4 | [ ] keep [ ] drop | why can't a repro command inside the sandbox reach the network? | sandbox | repro (bugfix's) | `docs/csharp/sindri-sandbox.md` network rules and `sandbox.denied` | the alias anchors on bugfix, the wrong pin: the leg must not lift bugfix docs over the sandbox doc | [ ] | [ ] | [ ] | [ ] |
| 5 | [ ] keep [ ] drop | what does parity-stale drop from a PR's attachments? | evidence | parity | `docs/csharp/evidence.md` `parity-stale` | "stale" hits the map's stale-map notes and stale locks in the context docs | [ ] | [ ] | [ ] | [ ] |
| 6 | [ ] keep [ ] drop | where do a PR's screenshots get published, and does it ask first? | evidence | screenshots | `docs/csharp/evidence.md` `publish-plan` / `gh-publish`, `skills/ui-evidence/SKILL.md` | "publish" is mostly `install-cs.sh` publishing binaries and the map publish | [ ] | [ ] | [ ] | [ ] |
| 7 | [ ] keep [ ] drop | how do I end learn mode early for one repo? | sandbox | learn mode | `docs/csharp/sindri-sandbox.md` `sindri sandbox learn off` | the profile doc and the UI doc mention learn mode in passing | [ ] | [ ] | [ ] | [ ] |
| 8 | [ ] keep [ ] drop | where does the agent home live and how does it get signed in? | sandbox | agent home | `docs/csharp/sindri-sandbox.md` `sindri sandbox login` (`agent-homes/<target>`) | the doctor's harness-login warning (`docs/sindri/context.md`) reads the same login files | [ ] | [ ] | [ ] | [ ] |
| 9 | [ ] keep [ ] drop | how is the graphify version pinned and checked? | code-index | graphify | `context/code-index.lock` and `scripts/code-index-lock.sh check` | `context/graphify.pin`, `install-deps.sh` and the setup docs all name graphify | [ ] | [ ] | [ ] | [ ] |
| 10 | [ ] keep [ ] drop | how do I list the callers of a method, and how sure is each row? | code-index | callers | `docs/csharp/code-index.md` `sindri code-index callers` and `coverage` | the map's lens and `context_neighbors` docs also walk edges | [ ] | [ ] | [ ] | [ ] |
| 11 | [ ] keep [ ] drop | what does the intake block write when I run it by hand? | scoping | intake | `docs/csharp/sindri-scoping.md` `desk` (`<out>/<key>.json`) | the workflow doc's `step.intake.ticket` and `step.design.intake` steps match "intake" too | [ ] | [ ] | [ ] | [ ] |
| 12 | [ ] keep [ ] drop | why is a scoping draft refused inside a worktree? | scoping | none (name match) | `docs/csharp/sindri-scoping.md` `SND-SCOPING-002` | the meta doc's worktree rules rank on "worktree" | [ ] | [ ] | [ ] | [ ] |

## Spares

Take these in order for a dropped row.

| # | Question | Pin | Alias word | Expected top answer |
|---|----------|-----|------------|---------------------|
| S1 | which exit code does ui-evidence give when its lock stays busy? | evidence | ui-evidence | `docs/csharp/evidence.md` exit 75 and "Stale locks" |
| S2 | what freezes the check when the repro moves to the reproduce state? | bugfix | repro | `docs/csharp/bugfix.md` `advance reproduce` |

## Reading the result

The pin leg fails the bar when any kept row has A ticked and C not ticked: turning the leg on pushed a right answer out of the
top 5. Row 4 is the one most likely to do it.
