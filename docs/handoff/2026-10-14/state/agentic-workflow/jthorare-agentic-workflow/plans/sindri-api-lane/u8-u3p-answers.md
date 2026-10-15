# API lane → UI lane: answers to U8-API-1…9 and U3'-API-9…12 (2026-10-14)

The full table, with citations, is in `rev8-contract-plan.md` §8. The new fields appear in the contract unit's goldens unless an item says otherwise.

## Taken (new fields)

- **U8-1 / U3'-9. State chip.** `chip: {word, tone}` on `InstanceSummary`, `ChildRow` and `NeedsYouRow`.
  - `tone` is `attn`, `fail`, `ok` or `neutral`, from fixture.md's chip table.
  - `NeedsYouRow.chip` comes with P4.
  - Open question to the lead: is PRJ-exports' started desk step `WITH YOU` or `DECISION`?
- **U8-2 / U3'-10. Open list and counts.** `GET /workflow/instances?root=true&open=true`, plus `InstanceList.counts {open, needsYou, withYou, running, done}`.
  - The counts are over the root set before `open` filters it.
  - Golden: `workflow-instances/open`.
- **U8-3. Answer on the list row.** `InstanceSummary.answer?`, the same `{kind, actionId, class, cli, atTerminal}` record as `status.answer`.
- **U8-4. Command routes.** Not added as addressed routes. Instead the views carry `id` (`WFI-…`) and `current.desk.run`, the run token.
  - Post to the engine's id routes: `/sindri/workflow/instances/{id}/nonce`, `…/commands` and `…/runs/{run}/complete`.
- **U8-5. Instance tabs.**
  - The timeline already exists as `GET /sindri/workflow/instances/{id}/events?after=` (engine #192). It gains `children=true` (engine lane).
  - `GET /decisions?instance=<address>` and `GET /requests?instance=<address>` come in API P11.
  - Until then, keep your fallback.
- **U8-6. Parent.** `parent?: {address, title, task?, workstream?}` on the view. `task` and `workstream` are null until engine P7.
- **U8-7. Open all in Work.** `GET /items?parent=<address>` comes in API P11. Until then, open Work unfiltered.
- **U8-8. Facts route.** `FactView {key, type, body, truncated}`. `body` is an object for `scope-map`, `packet` and `payload`, and a string for `text`.
  - Goldens: `workflow-instance-fact/scope-map`, `/packet`, `/payload`, `/text`, `/truncated`, `/not-found`.
- **U8-9. Cancel confirm.** `commands[].confirm?` holds one sentence on destructive verbs. It is `null` until the owner approves the sentence templates; show "Cancel <address>?" while it's null.
- **U3'-11. Needs-you empty sentence.** It becomes "Nothing needs you. N running, M queued." (P4). The "Last cleared HH:MM" variant waits on the owner.
- **U3'-12. Health sentence.** `health.sentence` on `GET /status`, e.g. "Sindri is degraded: A, B, and C."
  - It comes from new per-problem `clause`s, in API P2 unit E2 (#190).
  - Append "Updated Ns ago" yourself, from `asOf`.
  - The code-index and Onyx clauses arrive with their health sources.

## Engine lane (asked separately)

- `children=true` on `/events`.
- The parent on the start event.
- The open run's runtime and children on the Query, for the confirm sentence.
- `recipes/dev/incident-hold.yaml` (U3'-ENG-1), plus the two dev recipes with a `file` output on the desk state.
- P2b merged before the real-app proof.
