# Plain words: internal term -> what the UI says

Rule from the owner: **no internal terms on screen** ("facts", "lens", "BlockStep", "SND-*"). Everything below is a
*proposal* for UI copy; the left column is the real term in code/docs. Where a plain word already exists in Sindri's own
UI strings (`Labels*.cs`, the `next` sentences, the chip words) it is marked (real).

## 1. Core vocabulary

| Internal term | On screen | Notes |
|---|---|---|
| recipe | **workflow** (a template) | the page of templates = "Workflows" |
| instance | **run** (of a workflow) / the item's name | "ITM-01JN2/line/1" never shown as the title; show the item title + "Saved search sharing" |
| address (`ITM-…/line/1/scoping.1`) | small grey "reference", copy button | only for paste into a terminal |
| `workflow.line` | **Idea to merged pull request** (the whole pipeline) | proposed name; the real id has no display name |
| station | **stage** | a "stage" is one child workflow of the line |
| state | **step** | in the graph a node = a step |
| active state / Step | a step someone does | |
| wait state | **waiting for ...** | "waiting for the design stage / for you / for CI" |
| terminal state | **finish** (a result) | |
| outcome (`resolved`, `unresolved`, `alreadySatisfied`, `failed`) | Done / Didn't work out / Nothing to do / Failed | real chip has only DONE; `failed` has no red chip yet (gap) |
| transition / route | **what happens next** (arrow label) | label an arrow with the plain outcome, e.g. "gaps found: draft again" |
| loop / round (`rounds 2/3`) | **try 2 of 3** | cap reached = "used all 3 tries" |
| loop cap | "up to 3 tries" | |
| no-progress | "same problem twice" | |
| budget / tokens | **work allowance** ("1.2M of 4M"); say "tokens" only in a tooltip | |
| timeout (`parks in 1d 22h`) | **waits up to** ... "then pauses and asks you" | |
| fact | **result** / "what this step needs" and "what it produced" | NEVER "fact". Types: below |
| fact type `scope-map` | map of what changes | |
| fact type `packet` | **signed-off document** / "package" | see packet |
| fact type `evidence` | proof | |
| fact type `decision` | a choice | |
| fact type `payload` | details | |
| fact type `item`, `link`, `file`, `text` | item, link, file, note | |
| packet | **document set** ("scope document", "design document", "plan") | internal: hashed files from `aw-packets` |
| frozen | **locked in** / "signed off" | "frozen by you at stop 1" -> "locked in when you approved" |
| candidate (packet) | draft, not yet approved | |
| hash (`sha256:1c0e…`) | "fingerprint" (tooltip only) | never the headline |
| owner stop (stop 1/2/3) | **your approval** ("Needs you") | stop 1 "Approve the scope", stop 2 "Approve the design and plan", stop 3 "Approve the merge" |
| desk, desk task | **waiting for you** / "a task for you" | "desk" never on screen (the app itself says DECISION chip) |
| human-only | "only you can do this" | |
| `complete` / CompleteStep | **Send answer** / the option's own label | |
| answer (park) | "your answer" | |
| park / parked (chip PARKED) | **paused, needs you** | reasons below |
| escalate / escalated (chip ESCALATED) | **needs a decision** (sent to a person) | |
| takeover / taken-over (chip WITH YOU) | **you took over** | button "Take over"; the clock pauses |
| handback | **Hand back** (to the workflow) | button; "the workflow carries on from here" |
| takeoverIdle | "you've had this for 2 hours: hand it back?" | the nudge |
| cancel | Cancel run | |
| effect (`effect.notify.desk`) | **action it takes** ("tells you", "claims the branch") | rarely shown |
| gate (`gate.budget.available`) | **check before moving on** | "passes at 0.8 or above" -> "must score at least 80%" |
| threshold | pass mark | |
| evidence (`evidence.check`, proof pack) | **proof** | "proof pack" -> "proof package" |
| lens | **view / angle of review** (e.g. "product view", "code view") | context map; for design "reviewer type" |
| lens round | **review pass** | "review pass 2 of 4" |
| coverage (`1 of 4 lenses limited`) | "checked 3 of 4 areas; 1 couldn't be searched" | |
| context map / graph | **map of the product** (features, screens, actions, code) | |
| node kinds | feature -> **feature**; surface -> **screen / route / setting** (use its `kind`); action -> **action**; thing -> **data**; flow -> **journey**; decision -> **decision**; record -> **ticket / doc**; code -> **file** | |
| edge `part-of` | "belongs to" | |
| edge `on` | "happens on" | |
| edge `reads` / `writes` | "reads" / "changes" | |
| edge `implements` | "built in" (feature built in file) | |
| edge `tests` | "tested by" | |
| edge `decides` / `quotes` / `supersedes` / `mentions` | "decided" / "quoted from" / "replaces" / "mentioned in" | |
| provenance `human/model/deterministic` | "you set it" / "suggested by AI" / "found by the tool" | `confirmed:false` = "suggested, not confirmed" |
| trust `trusted/untrusted` | hide; show "from a ticket" style source instead | |
| anchor | "starting point" | |
| cascade / `affects` | "knock-on effects" | |
| mapped / new / found (scope map) | already in the map / new / found while scoping | |
| scope map | **what will change** | |
| acceptance list (`AC-1 [ears]`) | **what "done" means** (checklist) | `failing` -> "not yet proven" (not red) |
| challenger | **second opinion** (a different AI) | |
| interview | **questions for you** | |
| answer log | record of your answers | |
| holdout / hidden scenario | **hidden test** | "checks the builder never saw" |
| drift (`step.phase.drift`) | "did the code change after proof?" | |
| stack / wave | chain of pull requests / group built at once | |
| `maxStack`, `parallel` | "pull requests in a chain", "built at once" | |
| capability `WriteScope` | "can change files" | `Tools` "can run commands", `Network` "can use the internet", `Effects` "can notify/claim" |
| runtime agent / block / desk | **AI agent** / **automatic** / **you** | see section 3 |
| judge / route (`step.judge.choose`) | **AI picks a path** (and "you pick" when unsure: desk twin) | `uncertain` = "AI wasn't sure, so you choose" |
| profile | **settings** | "profile approve" = "approve settings changes" |
| ledger / event stream | **history** / activity | "events" tab = Activity |
| item | **piece of work** (ticket/idea) | |

## 2. Status words

| Real value | UI |
|---|---|
| glyph `done` / `active` / `pending` | filled circle "Done" / half "In progress" / empty "Not started" |
| chip RUNNING / DONE / QUEUED | Running / Finished / Waiting to start |
| chip DECISION (attn) | **Needs you** |
| chip PARKED (attn) | **Paused** (+ reason below) |
| chip ESCALATED (attn) | **Needs a decision** |
| chip WITH YOU | **You have this** |
| park `rounds` | "Used all tries (3 of 3). Allow more or stop?" |
| park `no-progress` | "Same problem again. Give a hint, allow more tries, or stop." |
| park `budget` | "Work allowance used up. Add more?" (grant-tokens needs a terminal: say "approve in the terminal") |
| park `needs-answer` | "A check failed. Fix the cause, then resume." |
| park `timeout` | "Took too long." |
| park `spec-conflict` | "The workflow was changed or withdrawn while this ran." |
| park `SND-WF-010` | "This step isn't built into this version yet; do it by hand and report." (never show the code) |
| timeout kind `parks` / `escalates` | "pauses and asks you in 1d 22h" / "asks for a decision in ..." |
| urgency `incident / triage / work` | Urgent / To sort / Planned |

Codes (`SND-*`, `RCP-*`, `SCP-*`): never headline. Map to one sentence with the code in a "details" disclosure.
Scoping checklist ids (`SCP-CITE-001`) -> "Every claim has a source", `SCP-COV-*` -> "Every area was checked", `SCP-ACC-*` -> "Done-criteria are testable", `SCP-CHAL-*` -> "Second opinion found nothing new", `SCP-ANS-001` -> "Your answers are recorded", `SCP-TRUST-001` -> "Nothing unverified slipped in", `SCP-MAP-001` -> "Map is complete" (sample wording; the real texts are code strings).

## 3. Step kinds and the sentence "why this matters"

Three kinds the user sees, from `BlockRuntime` + `humanOnly`:

| Real kind | UI badge | Colour idea |
|---|---|---|
| `AgentStep` (`runtime: agent`) | **AI agent** (sparkle) | |
| `BlockStep` (`runtime: block`) | **Automatic check/tool** (cog) | no AI session; may call the judge |
| `DeskStep` (`runtime: desk`) | **You** (person) | |
| `HumanDeskStep` (`desk`, `humanOnly`) | **Only you** (lock/person) -> these are the **approvals** | |
| wait on a child | **Sub-workflow** ("Stage: Design") | |
| wait on event / answer / resource | **Waiting for ...** | |

### Where "why this matters" can come from

**Today nothing in the served data says why a step exists.** What exists:

| Source | What it holds | Served at run time? |
|---|---|---|
| recipe `description` | one sentence per recipe ("Station 1 of the line: scope a change ... and park at the owner's after-scoping stop") | yes (recipe view; check `RecipeView` carries it: the list/show golden fields are `id, hash, version, name, inputs, outputs, urgency, orderedBy, states, loops`, so **description is in the YAML but not in the served view**) |
| state `next` | what to do/what is happening, with the item filled in | yes (`status.next`) |
| Step descriptor XML doc (`'step.scope.review': the owner's stop ...`) | what the Step does, one line | **no** (C# comment only; I scraped it into `steps-catalog.json[].doc`) |
| Step `outcomes`, `reads`, `writes` | machine facts | in the descriptor / `recipe show --json` states |
| recipe `outputs[].label` | "Scope packet", "Plan graph" | yes |

So "why this matters" **needs a new field**: `why` on the state (recipe YAML, hashed like `next`) or on the Step descriptor.
Cheapest honest option: add `why: "<one sentence>"` to each recipe state and surface it in `RecipeStateView` and `StepDeclaration`;
until then the mockups can use the proposed sentences in section 4 (marked as mockup-authored).

### Sentence patterns per kind (fill: *what it does* + *what goes wrong without it*)

| Kind | Pattern | Example |
|---|---|---|
| AI agent step | "An AI does X so that Y. Without it, Z." | "An AI writes a failing test for each point of 'done'. Without it, nothing proves the code later actually fixes the problem." |
| Automatic step | "Checks X automatically. If it's skipped, Z slips through." | "Checks every claim has a source. If skipped, a made-up detail could reach the design." |
| You (desk) | "You decide X because only you know Y. Skipped, the workflow guesses." | "You pick the size. If skipped, the AI's guess sets how the work is split." |
| Only you (approval) | "Nothing moves on until you approve X. This is your last chance to change Y before it's locked in." | "Once you approve, the scope is locked in; later stages can't add things you didn't see." |
| Sub-workflow / stage | "This stage turns A into B. Next stage depends on B." | "Design turns the locked scope into a written spec the plan is built from." |
| Wait for event/answer | "Waiting for X; nothing is stuck." | "Waiting for CI to finish on the pull request." |
| Loop edge | "If the check finds gaps it goes back, up to N tries, then asks you." | |

## 4. Proposed "why" lines for the shipped steps (mockup-authored; none exist in data)

(For the full 91 see `steps-catalog.json`; these are the ones in `workflow.line` and its stages.)

| State (recipe) | Plain name | Kind | Why it matters (proposed) |
|---|---|---|---|
| scoping (line) | Scope | stage | Decides exactly what will change before any design or code, so nobody builds the wrong thing. |
| intake (scoping) | Take in the request | automatic | Sorts a brief, ticket, idea or change into the right reader; without it the stage would guess the input. |
| gather (scoping) | Look at product, code, related work, history | automatic (4 views) | Pulls what already exists; skipped, the scope would miss surfaces. |
| draft (scoping) | Draft what changes and what "done" means | AI agent | Gives the owner something concrete to approve. |
| check (scoping) | Check the draft | automatic | Catches missing sources and untestable criteria before you read it. |
| questions / interview | Ask you | AI agent / only you | Closes behaviour gaps only you can answer; unanswered, the AI assumes. |
| challenge | Second opinion | AI agent (other model) | A different AI hunts for what the first missed. |
| size | Estimate size | automatic (AI picks, you if unsure) | Sets whether this is one pull request or must be split. |
| evidence | Assemble the proof | automatic | Packs every check into the document you approve. |
| stop (scoping) | **Approve the scope** | only you | Last chance to change scope before it is locked in. |
| freeze | Lock in the scope | automatic | Makes the approved scope tamper-proof for later stages. |
| design (line) | Design | stage | Turns the locked scope into a spec reviewed from several angles. |
| draft / revise (design) | Write / fix the spec | AI agent | |
| lens / round (design) | Review pass | AI agent | Independent reviewers find CRITICAL/HIGH problems before a plan is built on them. |
| lens-desk (design) | Accept the risk? | only you | Review ran out of tries with problems left; you decide. |
| plan (line) | Plan | stage | Splits the design into buildable pull-request-sized phases. |
| exec-review (plan) | Dry-run each phase | AI agent | Would a builder, given only this, succeed? Finds vague phases early. |
| holdout (plan) | Write hidden tests | AI agent | Tests the builder never sees, so passing them can't be gamed. |
| decide / stop (plan) | **Approve the design and plan** | only you | Locks in design, plan and all phases together. |
| phase (line) | Build one phase | stage | |
| build (phase) | Build | sub-workflow | Red tests first, then code inside allowed files. |
| scope-desk (build) | Allow a file outside the plan? | only you | A change outside the plan needs your OK. |
| prove (phase) | Prove it works | sub-workflow | Shows it works in the running app, not only in tests. |
| review (phase) | Code review | sub-workflow | Review loops until no blocking issues. |
| ship (phase) | Open the pull request | sub-workflow | Final check that proof, review and code are the same commit. |
| babysit (phase) | Watch the pull request | wait | CI green and docs synced before you merge. |
| ready-to-merge | **Approve the merge** | only you | One click merges the PR (then the docs PR). |

## 5. Internal words that must never reach the screen

fact, lens, BlockStep/AgentStep/HumanDeskStep, desk, packet, frozen, escalate, park, station, effect, gate, route,
outcome, trigger, ledger, projector, views rebuild, nonce, run token (`--run 3.1`: hidden behind the button), hash12,
JCS, `SND-*`, `RCP-*`, `SCP-*`, `ITM-`/`WFI-`/`DEC-` ids (small "reference" only), `fact:` keys, `step.x.y` ids (tooltip
"technical name"), `workflow.x` ids, `aw-packets`, Temporal.
