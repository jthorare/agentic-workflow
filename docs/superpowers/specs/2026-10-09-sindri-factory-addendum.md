# Sindri — factory addendum (§17)

Status: draft for review, revision 6 · 2026-10-11 (applies /autoplan round 1 C1–C17 in §17.19 and round 2 D1–D12 in §17.22)
Revision 6 adds one workflow state machine (§17.28): one engine and a declarative recipe format for every Sindri machine and, later, the worker skills; and Sindri as an application (§17.29): a control plane, the workflow engine and a context retrieval engine, built new in C#, with agents staying in Claude Code.
Revision 5 adds the factory field review (§17.23): review back-pressure, self-proving tests, read-only existing tests,
consequence tiers, diff guardrails, closed outcomes with a Not proven section, the merge-confidence card, gate
removal criteria, escaped-defect tracing, comprehension drift, a PR lease, signed run receipts, no-progress
detection, the implementer's plan review, feedback-loop classes and factory/human cohorts; invariants 17–19.
It also adds a second field pass (§17.24: failure ownership, migration oracles, the critic lens, context assertions,
executor escalation, `spec-conflict`, DORA keys and postmortems, sealed artifacts, one live variant per artifact) and
field harvest (§17.25), which makes finding and building other factories' practices part of Sindri grows Sindri,
and scoping v2 (§17.26): tracker intake by assignee, a challenger that retrieves every round, candidate files per
surface, and a packet the studio edits with the same pipeline (add, remove, prune, ask), with edits as labels.
§17.27 makes every action manageable from the UI: one command layer with the CLI and UI as clients, authority by
action class and surface, a scope button, and a parity test so UI actions marked pending can't slip.
Revision 2 adds:
- development lenses and workflow generation (§17.7)
- lean session bootstrap (§17.8)
- standing workflows (§17.9)
- the context graph as the part Sindri builds, with pluggable components (§17.3)
- a sandbox policy compiled per task (§17.5)
- the human's answers to Q1, Q3 and Q5, with options for Q2 and Q4 (§17.17)

Amends: `2026-10-07-sindri-design.md` (revision 7) and the Plan 6 dashboard spec. Where they disagree, this
addendum wins for the sections it names. Everything else in the main spec stands.

## Core tenet: Sindri grows Sindri

Everything this addendum defines is a **starting point**, not a finished design. That covers the lenses,
evidence kinds, standing workflows, context-graph components, rules, budgets and surfaces. Sindri improves,
grows, enhances and evolves Sindri: it measures its own parts (§7.4, §7.7), proposes changes to them, **finds new
capabilities worth adding** (§17.18), **harvests what other software factories already do and builds the practices
it lacks** (§17.25), and builds the approved ones through its own factory (§7.6). The human
approves direction. Sindri does the finding, the building and the proving.

### Architectural principles that follow from the tenet

Sindri can only evolve parts it can isolate, measure and swap, and it can only afford to run them if most decisions
cost no model turn. Three principles follow, and every section below applies them.

1. **Modularity.** Every part is a module with a typed contract, its own eval suite and telemetry, and a
   registry entry. Parts include lenses, evidence providers, standing workflows, context-graph components, rule
   packs, decision questions, prompts and surfaces. A module can be proposed, A/B tested, adopted or reverted
   without touching its neighbours. A part that can't be swapped alone is a design defect.
2. **Context management.** No session gathers its own context. Context is assembled by code into a budgeted,
   cited injection plan (§17.5, §17.8), and measured for size and relevance (§17.2). Modules declare what context
   they need. They never load "everything that might help".
3. **The decision ladder.** Each decision takes the cheapest rung that can make it with enough confidence:

   | Rung | Example | Cost |
   |---|---|---|
   | 1. Deterministic code | Selector matching, schema checks, path guards, routing tables, lens triggers | ~0 |
   | 2. A decision model | A judge question on Jev, with calibrated thresholds (the existing `judge` chain) | A fraction of a cent, milliseconds |
   | 3. A small model, one bounded call | Classification or extraction with no tools | Low |
   | 4. An agent turn or session | Design, implementation, open-ended investigation | High |

   - A decision escalates a rung only when the lower rung is below its threshold (invariant 4).
   - Every decision records which rung made it, the cost, and the later outcome.
   - Self-evolution pushes decisions **down** the ladder. A rung-3 or rung-4 decision that keeps getting the same
     answer becomes a proposed judge question. A judge question that is reliably confident becomes a proposed
     rule or code path.
   - "Agent turns that a cheaper rung could have made" is a tracked metric (§17.2).

## 17.0 Why this addendum

Plans 1–6 built the control room: measurement, the ledger, the code index, scoping, offline self-evolution and
the dashboard. Nothing yet takes a designed project to merged code without the human dispatching it, and every
agent session still starts heavy: memory bootstrap on the first turn, then every skill and subagent definition
loaded whether or not the task needs it. This addendum defines the production line and the context discipline
it depends on.

**The target pipeline:**

```
 scope ──► design studio ──► handoff packet ──► workflow generation ──► factory build ──► review anywhere ──► human merges
(sindri    (human SME + agent,  (frozen, schema-    (deliverable → lenses,   (Sindri supervises;    (phone or desk;      (epic → default
 scope)     at the desk)         validated)          steps, injection plan)   narrow sessions)       signed decisions)    branch)
```

Next to the pipeline, **standing workflows** (§17.9) run on events: PR babysitting, triage, incidents,
documentation, cleanup, digests and performance watching.

**Changed assumptions:**

| Main spec | Changed to | Section |
|---|---|---|
| Fewer human turns per merged PR is the headline metric (§2) | Build-side turns go toward 0. Product and design turns are the human's job and are expected to rise | §17.2 |
| `full` mode is deferred; scoping output goes to the human for approval (§4, §7.1) | Scoping feeds a design studio with the human. The build starts only from a frozen handoff packet, then runs without the human until review | §17.4 |
| The v1 worker wraps `/bugFixOrchestrator` (§11.4) | A workflow generator turns each deliverable into Steps run by **lenses** (QA, UI, UX, architecture, data, language, security, performance and more), handed off automatically | §17.7 |
| Sessions inherit the host's full skill set and memory bootstrap | Sindri sessions load only what the Step's injection plan names | §17.8 |
| The human always merges (§2 non-goals) | Task PRs merge into a stacked epic branch under the deterministic gate; the epic → default-branch PR is always the human's | §17.10 |
| Approvals come only from host CLI verbs (§8.2, §10) | CLI verbs stay authoritative. A tailnet-only surface adds signed phone decisions for non-protected kinds | §17.12 |
| macOS first, Linux built-ins later (§12) | The factory host is swappable between a laptop and an always-on Linux box | §17.13 |

**Rings:** ring 0 is this toolkit; **ring 1** is workplace repos, where the factory is tested and improved
first; **ring 2** is a private product repo, started after ring 1 holds. Ring specifics stay in their private
profiles and repos. This document is generic.

## 17.1 New invariants

12. **The human is the product SME.** Sindri batches, contextualizes and cites product questions. It never
    answers them, and it doesn't count product or design turns as cost.
13. **Agents don't decide their own scope, and models never grant capabilities.** A session's write scope,
    sandbox policy, context, rules, lenses and next handoff come from the frozen packet and its frozen workflow,
    computed by Sindri code. A model may *propose* the Step order, review-only lenses and injection plans at
    planning time (§17.7). It may never propose a **capability field**: tool, command, MCP, network, write-scope
    or budget allowlists. The session policy is always the profile's approved allowlist (the ceiling) intersected
    with the lens and task allowlists, never more. Freezing and re-freezing are desk actions. Widening scope is a
    scope-expansion request (§6.1), never a silent edit.
14. **No build without a frozen packet.** A build Step starts only from a handoff packet that passes
    `sindri packet validate` and is frozen by hash (§17.4).
15. **Context is budgeted.** Every session starts from an injection plan with a token budget per section.
    Anything loaded that the plan didn't name is a defect with a metric.
16. **Nothing here is final.** Every catalog, schema, threshold and surface in §17 is a registered artifact
    that Sindri may propose to change, extend or replace through §7.4, §17.14 and §17.18. A part with no eval
    suite or telemetry is itself a gap that Sindri proposes to fill.
17. **Sessions don't edit the tests that judge them** (§17.23.3).
18. **Production stops when review is full** (§17.23.1).
19. **Every verdict says what it didn't prove** (§17.23.6).

## 17.2 Metrics, revised

These replace the "human turns per merged PR" row of §2 and its use in the auto-start kill criterion.

| Measure | Target | Why |
|---|---|---|
| Shipping direction, continuity, QA direction, recipes, dispatch turns | → 0 per merged PR (step-0 baseline, then −75% after 4 weeks of factory builds) | The factory's job |
| Mid-build product questions (asked after the packet froze) | Tracked. Each one is a **packet miss** fed to scoping backtests and evolve | The question belonged at the desk |
| Product and design turns at the desk | Tracked, no target. Expected to rise | The human's job |
| Tokens in context at a session's first model call | ≤ the injection plan's budget; tracked per role and lens | Invariant 15 |
| Context relevance (share of injected sections the session cited or used) | ≥ 0.6 per lens after calibration | Invariant 15 |
| Out-of-scope attempts per task (denied by the sandbox policy) | Tracked; a rising rate is a workflow-generator defect | Invariant 13 |
| Phone decisions per merged PR, and median decision latency | Tracked | Measures the review surface |
| Decisions by ladder rung, cost per decision, and agent turns a lower rung could have made (judged after the fact) | The share made at rungs 1–2 rises month over month | The decision ladder |

Auto-start's kill criterion keeps the rework comparison and drops the −20% total-turns condition. §17.23.17 adds
rows for back-pressure, test proof, triage accuracy, escaped defects, no-progress parks, the merge card and cohorts.

## 17.3 Knowledge layer: the context graph

A human engineer brings six kinds of context to a requirement. The factory needs all six, joined:

| Dimension | Examples |
|---|---|
| **Product surfaces and behavior** | Screens and routes, actions, features, entities, flows, the rules attached to them |
| **Codebase** | Symbols, modules, call graph, clones, dependencies, ownership, history |
| **Requirements and conversations** | Tickets, specs, ADRs, chat threads, meeting notes, decisions |
| **User behavior** | Session replays, funnels, journeys, feature usage |
| **Telemetry and operations** | Traces, metrics, logs, errors, incidents, deploys, performance baselines |
| **Design** | Design files, tokens, design-system components, mockups |

**What exists elsewhere, and what doesn't.** Each component is being improved quickly by people focused on it:
connectors, memory, retrieval, storage, and offline consolidation ("dreaming"). Org-knowledge engines join
conversations and docs. Code engines index code. Analytics tools hold behavior and telemetry. **No product joins
all six into one graph keyed by product surface and code symbol.** That join is the part Sindri builds.

**Decision:** Sindri builds the **context graph**: the entity and link model, fusion, the query planner and the
evals. Every other component is a pluggable, pinned, swappable adapter. Each adapter pin is a registered artifact
(§7.4), so evolve proposes upgrades and swaps, and the backtest decides them (see Proof below).

```
 connectors ──► ingest + scrub ──► component stores ──► entity extraction ──► CONTEXT GRAPH (Sindri)
 (adapters)     (§8.3, §8.4)       (memory, vector,     (deterministic first,   entities · typed edges ·
                                    FTS, code index,     model-suggested         provenance · freshness
                                    analytics APIs)      flagged)                       │
                                                                                         ▼
                              query planner: seeds → graph expansion → per-component search → fusion (RRF)
                                                                                         │
                                                                                         ▼
                                                     injection plan sections (§17.8), cited, budgeted
```

**Component interfaces** (all return cited, scrubbed, trust-tagged records with stable refs):

| Component | Interface | Notes |
|---|---|---|
| `Connector` | `sync(cursor)`, `fetch(ref)` | One per source system. Read-only. MCP servers count, through a read-only MCP client with a server and tool allowlist |
| `MemoryStore` | `search(q)`, `write(rec)` (sindri-owned facts only), `consolidate()` | `consolidate` is the "dreaming" pass: dedupe, merge, decay, propose edges. It runs as a heavy job |
| `Retriever` | `search(q, k)` over FTS and vectors | Local by default: embeddings on loopback (§6.2) |
| `CodeGraph` | the existing code index and graphify | Unchanged |
| `ProductGraph` | `nodes(filter)`, `edges(node)` | Sindri-owned. Built from packets, routes, UI code and docs |
| `BehaviorSource` | `journeys(surface)`, `usage(surface)` | Summaries only: structure and counts, never values typed by users |
| `TelemetrySource` | `query(surface, window)` | Errors, latency, traces linked to routes and symbols |

**The context graph itself:**
- **Entities:** product nodes, code symbols and modules, records (tickets, threads, docs), journeys, telemetry
  series, incidents, decisions, rules.
- **Edges:** `implements`, `mentions`, `decides`, `exercises` (a journey through a surface), `observes`
  (telemetry on a route or symbol), `violates`, `supersedes`.
- **Deterministic links first:** explicit ids (ticket keys, PR numbers), paths, symbol names, route patterns,
  flag keys, component names, span names. Model-suggested links carry `provenance: model` and a confidence. They
  are used for recall, never as the only evidence for a surface.
- **Freshness:** every edge stores the source refs and the commit sha it was computed at, and is rebuilt with the
  index (§6.2 freshness rules).

**Query planner:** seeds come from the task or brief text and the packet's product nodes. They are expanded
through the product and code graphs (bounded depth), then sent to each component with the expanded entity set.
Results are fused with reciprocal rank fusion under per-dimension budgets that the injection plan sets.

**Proof:** `sindri scope --backtest` gains one arm per dimension plus `all`. A component or dimension is enabled
for a repo only when its arm improves recall without dropping precision below the §7.5 bar. Swapping an adapter
(a new memory store, say) is an evolve proposal judged on the same arms.

**Separation:** work and personal sources never mix. A profile names its connectors. Ring-1 profiles can't name
personal stores (for example Prism).

**Reuse:** the context graph is a separable package with no Sindri imports, so a ring-2 product with the same
problem can lift it after an ownership check.

## 17.4 Design studio and the handoff packet

**Design studio:** desk sessions where the human and an agent turn a scope map into a design, using the existing
skills (`/officeHours`, brainstorming, `/autoplan`, `/archReview`). The studio adds:
- a running **decision log**: each answered question is a record with its asker, answer and the product nodes it
  affects;
- `/rule` capture: "I always want X" said at the desk becomes a proposed rule (§17.6);
- a local **visual walkthrough** (HTML) generated from the packet. It is never published unless the human
  publishes it.

**Handoff packet:** a directory with a manifest, validated by a typed C# schema (System.Text.Json source-generated
records, with a generated JSON Schema for editors; see the readiness design §4b and §4c). Each schema file carries
`schemaVersion`. A minimal version ships in readiness step R1b (#142) so the C# ports themselves run as packets.

| Part | Contents |
|---|---|
| `packet.yaml` (`packet.json` in the R1b build, #142) | id, repo, base sha, status, hash, links to everything below |
| Design | Design docs, ADRs, product-graph delta, data-model delta |
| Decision log | Answered questions, plus deferred ones with an owner and a "build may assume" default |
| Out of scope | An explicit list. The scope-expansion check reads it |
| Workstreams → tasks | Per task: id, deps, size, **write scope** (globs or "new: path"), **read seeds** (symbols, product nodes, refs), acceptance examples (a share held out), evidence plan (§17.11), rule selectors (§17.6), budget |
| Workflow | The frozen output of workflow generation (§17.7) |
| Plans | Generated plan files, so the plan-file tracker and `observe` pick them up |

**Deterministic checks** (`sindri packet validate`):
- the task graph is acyclic, and every task is in a workstream;
- every write scope exists at the base sha or is declared new;
- concurrent tasks don't have overlapping write scopes unless a dependency orders them;
- every acceptance example maps to at least one evidence kind;
- every open question is answered, or deferred with a default;
- every task fits its size class's budget, or is split;
- the holdout share exists and is stored where the implementer can't read it;
- the workflow passes its coverage checks (§17.7).

**Editing before freeze** uses the scoping pipeline itself (§17.26.4): adds, removals and "too much" re-run
retrieval or pruning scoped to the edit.

**Freeze and amend:** `sindri packet freeze` records the hash in the ledger, and builds run against that hash.
Any later change is an **amendment**: a scope-expansion or packet-miss item goes back to the desk queue, and only
the affected tasks are re-planned and re-frozen.

**What R1b builds (`Aw.Packets`, #142).** Before the switch the checks and the freeze run as the standalone NativeAOT
CLI `aw-packets validate|freeze <packet-dir> [--repo <dir>] [--json]` (readiness design §4c). After it,
`sindri packet validate|freeze` runs this CLI through a planned `cli` edge, as `sindri readiness check` does
(readiness design §8): the use cases and adapters live in `Aw.Packets.App`, and a `project` edge could reach only
`Aw.Packets.Core`; #142's `deps.yaml` declares that `cli` edge.
`--repo` is a directory inside the repository the packet builds (default: the packet directory); the base commit's
file list comes from git.
- **The manifest is `packet.json` today, not `packet.yaml`:** the build reads typed, source-generated JSON under
  NativeAOT and carries no YAML library. Keeping JSON or adding a YAML reader is open (readiness design §9 R14).
- **Rule ids,** in the order the checks report them:

  | Rule | Check |
  |---|---|
  | `PKT-ID-001` | Task, workstream, question and example ids are unique |
  | `PKT-WS-001` | Every task is in a declared workstream |
  | `PKT-GRAPH-001` | Every dependency names a task in the packet |
  | `PKT-GRAPH-002` | The task graph is acyclic; each strongly connected group is reported once, with one cycle through it |
  | `PKT-BASE-001` | `baseSha` is a full commit sha that exists in the repository |
  | `PKT-SCOPE-001` | Every write scope is a relative glob matching a file at the base commit, or `new:` a path that can be created there |
  | `PKT-SCOPE-002` | Two tasks no dependency chain orders don't write the same path |
  | `PKT-ACC-001` | Every task has a visible acceptance example, and every visible one has text |
  | `PKT-EVID-001` | Every acceptance example names at least one kebab-case evidence kind (§17.11) |
  | `PKT-DEC-001` | Every question is answered, or deferred with an owner and a `buildMayAssume` default |
  | `PKT-HOLD-001` | Every task holds at least one acceptance example out |
  | `PKT-HOLD-002` | No held-out example keeps its text in the packet; the packet keeps its id and evidence kinds (§17.19) |
  | `PKT-SIZE-001` | **Unchecked:** the size-class budgets aren't defined (readiness design §9 R15) |
  | `PKT-FLOW-001` | **Unchecked** until workflow generation (§17.7) exists |

  This is the R1b set, not the whole `packet validate` set. Three checks the later sections add get `PKT-*` ids at
  F0: measurable acceptance examples (§17.23.14), a named oracle before a migration (§17.24.2), and `freeze`
  refusing a packet with `proposed` items not yet confirmed or rejected (§17.26.4). The §17.26.4 editing (`packet
  edit`, `packet undo`, `source:` provenance on every item, change sets and draft revisions) is F0 too, not R1b.
  Because #142 refuses an unknown member, F0 extends the packet schema with the provenance and status fields.
- **Malformed packets** are refused before any rule runs (`AW-PACKETS-003`, or `-004` for an unknown
  `schemaVersion`): a byte order mark, a duplicate key, an unknown member (an optional top-level `$schema` editor hint
  is allowed), a null in a list, more than 2,000 tasks, or a link that leaves the packet directory (`..`, a root, a
  `:`, or a symbolic link on the way).
- **Write-scope globs** are repo-relative: `*` and `?` stay inside one path segment, and `**` as a whole segment
  crosses segments. An entry is at most 1,024 characters, and matching is a two-pointer walk with no regex.
- **Output:** the text form lists every rule as `pass`, `FAIL` (subject, what was found, the fix) or `unchecked` (the
  reason). A rule reports at most 50 failures, then one `omitted` record whose `omitted` field counts the rest.
  `--json` writes one document, `schemaVersion` 1. For `validate` it holds `packet`, `passed` and `checks[]`: one
  record per failure and one per passing or unchecked rule, each with `rule`, `state`, `subject`, `message` and `fix`.
  For `freeze` it holds `packet`, `frozen`, `hash` (null when a check failed), `digestVersion`, `location` (the
  record's path under `packets/`, null when nothing was frozen), `alreadyFrozen` and the same `checks[]`; it has no
  `passed`.
- **Exit codes:** 0 when every implemented check passes (for `freeze`, the hash is recorded); 1 when a check fails or
  the packet is malformed; 2 for a usage error or when the tool can't do its job (no packet, an unreadable repository
  or packet file, an unwritable freeze log).
- **Freeze:** a packet with a failed check isn't frozen and nothing is written. Otherwise the record (`FreezeRecord`:
  `schemaVersion` 1, repo, packet, hash, `digestVersion`, `baseSha`, `frozenAt`, the files the hash covers) goes to
  `packets/<owner>/<name>/<id>/frozen/<hex>.json` under the state directory, per repository because a packet id is
  unique only within one. The repository is a `RepoSlug` (`owner/name`): each side is letters, digits, `.`, `_` and
  `-`, never ends in `.`, and isn't a Windows device name (`con`, `prn`, `aux`, `nul`, `com1`–`com9`, `lpt1`–`lpt9`,
  with or without an extension), because the two sides are also directory names under `packets/` on every OS. Freezing
  the same hash again writes nothing. The packet directory is never changed. Until sindri's C# ledger exists this
  record stands in for "records the hash in the ledger"; the ledger takes over once the C# Sindri application has one (built new, §17.29).
- **The hash** is digest v1 (readiness design §4c, Digests) over the packet's files.

## 17.5 Narrow scope and sandbox per session

**Task compiler:** for each Step in the frozen workflow, Sindri code (no model) computes a **session spec**:

| Field | Computed from |
|---|---|
| `write` | The task's write scope, plus **new** test files by the repo's test-path convention. Existing tests only when the packet names them (§17.23.3) |
| `read` | The context-graph query for the task's seeds (§17.3), within the injection plan's budget |
| `rules` | Rule selection (§17.6) for the repo, paths, task kind, lens and stage |
| `skills`, `agents`, `mcp` | The lens definition's allowlist (§17.7) |
| `sandbox` | The sandbox policy below |
| `evidence` | The evidence plan's providers (§17.11) |
| `budget` | Tokens, turns and wall time by size class and lens |
| `next` | The workflow's next Step and its input schema |

**Sandbox policy, compiled from the scope:** every session runs with a policy generated from its session spec.
Out-of-scope actions are refused by the gate (§8.1) and the container, never left to the agent.

| Control | Compiled from | Enforced by |
|---|---|---|
| File writes | `write` globs | `PreToolUse` gate on edit tools and on Bash commands with write targets; the `PostToolUse` working-tree check (§5.3) for scripts and codegen; the sindri-side verifier refuses a bundle with out-of-scope paths (§8.5) |
| File reads | Repo plus `read` refs; secrets and deny paths from the profile | Mounts (§8.2) and the gate |
| Commands | The lens's command allowlist (for example only the data lens may run migrations, only the performance lens may run load tests) | The gate (`SND-GATE-1xx`) |
| Network | Model proxy; package registry only when the task may change dependencies; the environment endpoints the evidence plan needs | The egress proxy (§8.2) |
| MCP servers and tools | The lens's allowlist, read-only by default | The image's managed settings and the gate |
| Skills and subagents | The lens's allowlist | Session image settings (§17.8) |
| Budgets | `budget` | The model proxy and the tick |

A denied action returns `SND-SANDBOX-1xx` (a new error area; `SND-SCOPE` stays with `sindri scope`) with the
rule, the attempted action and the fix, and turns into a scope-expansion request. Three denials
of the same kind in one Step park the Step with a `scope-mismatch` reason, which counts against the workflow
generator (§17.2).

**Context discipline:**
- The pack is assembled in budgeted sections: task and acceptance, rules, write-scope code, read context, refs.
  A section over budget is cut by rank, never by position.
- `session.maxContextTokens` (§7.2) is set per lens. Crossing it checkpoints and relaunches from pack plus
  checkpoint instead of compacting into a mixed context.
- A task whose pack can't fit with at least half the window free is split by the compiler (by write scope) or
  sent back to the desk. A session never finds out mid-task that it needs more context.

## 17.6 Rules and taste: a growing, selectively injected library

**Rule record** (a registered artifact, §7.4):

```yaml
id: taste.ts.no-barrel-files
kind: taste            # taste | engineering | process | product-precedent | security | lens
text: "Import from the module file, not an index barrel."   # one or two imperative sentences
why: "Barrels hide cycles and slow type-checking."
select: { repos: ["*"], languages: [ts], paths: ["**/*.ts"], taskKinds: [feature, refactor], lenses: [implementer, architecture], stages: [build, review] }
check: { type: eslint, rule: "no-restricted-imports" }       # deterministic | judge | none
source: { type: desk, ref: "decision-log#D-12" }             # desk | correction | adr | review | import
status: active          # proposed | active | retired
```

**Recorded shape in this repo (readiness R1b, #137):** the C# engineering rules are records in
`rules/engineering/csharp/<slug>.yaml` (schema `schemas/rule-record.schema.json`, check `node scripts/check-rules.mjs`).
They differ from the sketch above in one open way: `checks` is a **list** (a rule can be enforced by several checks,
each with its own `type`, id, `status` and, while its PR is open, `pendingPr`), and `check.type` takes this repo's
check kinds (`analyzer`, `compiler`, `banned-symbols`, `archunit`, `check-deps`, `lane-cap`, `check-rules`,
`build-setting`, `test`, `script`, `ci`, `review`) instead of `eslint`.
They add `section` (the readiness spec section), `gap` (`closed`, `partial`, `pending`, `open`), `gapNote` and
`relatedIds`. Whether to keep the list form or split to one record per check is an open owner question (readiness
spec §9 R12).

**Layers** (a later layer wins on the same id): core defaults → `rules/` in this repo (the human's personal
taste, safe to be public) → profile overlay (workplace and repo specifics, private) → packet (project-specific).

**Selection is deterministic:** match selectors against the session spec, rank by specificity and recent
violations, then fill the lens's rules budget. A session never carries the whole library.

**Enforcement ladder:** injected text → a judge `rule-check` question in the verifier → a deterministic check.
A rule with a `check` is enforced at commit and by the sindri-side verifier. A rule that keeps being judged
becomes a proposal to write its check.

**Growth** (self-adopt tier, §7.4):
- `correct` gains a `taste` label class next to design and process; each one drafts a rule proposal citing the
  turn.
- Review findings the human accepts or rejects become rule proposals or retirements.
- `/rule` at the desk lands as `proposed`, and the phone inbox approves or rejects proposed rules.
- A rule whose fires are overturned more than half the time over 20 fires is proposed for retirement.

**Product precedents** (`kind: product-precedent`) record how the human answered a recurring product question.
Agents cite them when they ask; they never answer for the human (invariant 12).

## 17.7 Development lenses and workflow generation

**Lenses:** a lens is a development discipline with its own questions, rules, tools and evidence. Each lens is a
registered artifact:

```yaml
id: lens.data
title: Database and data
triggers: { changeClasses: [schema, migration, query], paths: ["**/migrations/**", "**/*.prisma", "**/*.sql"], productTags: [reporting] }
questions: ["Is the migration reversible?", "Do new queries have indexes?", "Is tenancy enforced?"]
rules: { select: { lenses: [data] } }
skills: [archReview]          # only these are loaded
agents: [database-administrator]
mcp: []                       # read-only by default
commands: ["<repo migrate cmd> --dry-run", "<repo test:db cmd>"]
evidence: [migration, test]
model: { tier: standard, challenger: different }
budget: { tokens: 120k }
```

**Initial lens catalog** (an open list; lens definitions can be imported from community subagent catalogs as a
trust-tiered pack under §8.3, then evaluated like any other artifact):

| Lens | Typical triggers | Evidence |
|---|---|---|
| Product / PM (acceptance examples, holdouts) | Every feature task | Acceptance examples |
| System architecture | New modules, cross-package changes, new dependencies | Shape signals, `/archReview` notes |
| Implementer (per language: TS, Swift, Python, SQL, …) | Every code task; language picked from the paths | `test` |
| QA and evaluator | Every task | Held-out examples, the evidence plan |
| UI design | Components, styles, tokens | `design-parity`, `a11y` |
| UX design | Flows, navigation, forms, empty and error states | `ui-browser`, `behavior-sim` |
| Database and data | Schema, migrations, queries | `migration`, `test` |
| Security | Auth, input handling, secrets, dependencies, permissions | `security` |
| Performance | Hot paths, queries, bundles, build time | `perf`, `trace` |
| Infrastructure and deploy | CI, containers, IaC, environments | `cli-transcript`, deploy dry-run |
| Integrations | Third-party APIs, webhooks, connectors | `api-transcript` against mocks |
| Accessibility | UI | `a11y` |
| Documentation | Public APIs, user-facing behavior | `docs` |

**Workflow generation:** a meta-orchestrator Step that runs **once per deliverable** at packet time, not once
per agent turn.
1. **Inputs:** the packet, the context-graph query for its surfaces, and the lens catalog.
2. **Deterministic first pass:** lens triggers are matched against each task's write scope, change classes and
   product nodes. This yields the required lenses per task.
3. **Model pass** (bounded job, read-only): proposes the Step graph. That covers the order of lenses per task,
   which lenses review rather than build, where parallel work is safe, and the injection plan per Step
   (§17.8). It may add **review-only** lenses (they read and comment, and get no extra capabilities), and it never
   removes a required one. It can't write capability fields (invariant 13).
4. **Coverage checks** (deterministic): every required lens appears; every acceptance example has an evaluator
   Step; every Step's injection plan fits its budget; write scopes of parallel Steps don't overlap; every Step
   has a valid next handoff.
5. **Direction check** (§6.1) on a different model: "is this the right workflow for this deliverable?"
6. **Freeze** with the packet. From then on, injection and handoff are deterministic.

**Handoff is an artifact plus a ledger transition.** Each Step ends by writing an output that matches the next
Step's input schema. Sindri validates it, records the transition and starts the next Step. No human paste, and no
agent picks the next step. Verdicts are computed by a script from the evaluator's checks (invariant 3).
Information barriers stay: the implementer can't read held-out examples or evaluator notes.

**Existing skills become Step implementations in sindri mode:** `/specToProvenPR` (feature tasks),
`/bugFixOrchestrator` (bug tasks), `/review` and `/addressReview` (review loop), and the evidence providers. In
sindri mode there is no "ask the user" (`--permission-prompts none`; the preamble's asks become parks), evidence
and review state are keyed by worktree, and outward writes go through the write shim (§11.4). The interactive
mode stays for desk use.

## 17.8 Lean session bootstrap

Today every session runs the memory bootstrap on its first turn, then loads every installed skill's and
subagent's description into context. Sindri sessions start from an **injection plan** instead.

| Today (host sessions) | Sindri sessions |
|---|---|
| Memory bootstrap on the first turn (Prism `session_bootstrap`) | No memory bootstrap. Memory arrives through the context graph as cited pack sections |
| All skills and subagents listed | Only the lens's `skills` and `agents`, set in the session image's managed settings |
| All MCP servers connected | Only the lens's `mcp` allowlist |
| Global rules and CLAUDE.md in full | Selected rules (§17.6) plus the repo's agent file trimmed to the sections the injection plan names |
| Context grows by tool output | Per-section budgets, relaunch on overflow (§17.5) |

**Mechanism:** the session image is built per (repo, toolchain). At launch, Sindri writes a per-session settings
overlay that enables only the planned skills, agents, MCP servers and hooks. The `SessionStart` hook injects the
pack. `doctor` measures first-call context tokens per lens against the plan's budget.

**Host sessions (optional):** a `lean` profile for the human's own sessions applies the same idea at the desk.
Its contents are a minimal index of skills, memory loaded on demand, and lenses invoked by name. It is opt-in
and measured the same way.

## 17.9 Standing workflows

Some work isn't a packet. Standing workflows are workflow templates fired by events, using the same lenses,
sandbox policies, budgets and handoffs. Each one has an **autonomy tier**: `report` (writes a digest or comment
only), `propose` (opens a draft PR or an item for the human), or `act` (performs allowlisted actions).

| Workflow | Fires on | Lenses | Default tier |
|---|---|---|---|
| PR babysitter | A sindri PR's CI, review threads or conflicts change | Implementer, QA | `act` (fix rounds within the ship contract) |
| Triager | A new tracker item, alert or failed run | Product, the matching lens | `propose` (size, route, missing info; never answers product questions) |
| Inspector | A merge to the default branch; nightly | QA, security, performance | `report` → `propose` |
| Incident investigator | An alert or error spike from telemetry | Telemetry, the matching lens | `report` (a cited timeline and hypotheses) |
| Incident resolver | The human accepts an investigation's hypothesis | Implementer, QA | `propose` (fix PR; never deploys) |
| Incident comms manager | Incident state changes | Documentation | `propose` (drafts updates; the human sends) |
| Documentation maintainer | Merges that change public behavior | Documentation | `propose` |
| Codebase cleanup | Weekly; shape-signal trends (§6.2) | Architecture, implementer | `propose` |
| Daily digest | Daily | — | `report` |
| Performance watcher | Deploys; nightly benchmarks | Performance | `report` → `propose` |
| Skill maintainer | Skill-change items (§17.14) | Implementer, QA | `propose` |

A workflow moves up a tier only through an approval-tier profile change, after its `report` output has a measured
precision bar like direction checks (§6.1).

## 17.10 Stacked epics

- Each packet gets an epic branch. Task PRs are opened as a stack against it in dependency order (§7.3's stack
  machine).
- With `epicMerge: auto`, a task PR merges into the epic once the deterministic ship gate passes:
  - CI green on the **verified head SHA**, merged with `--match-head-commit <sha>`;
  - review threads resolved **by a human or a trusted bot** (`trustedBots`, §8.3); a thread resolved by a Sindri
    session or the PR babysitter doesn't count;
  - the evidence plan satisfied, and the review-round cap not exceeded.
- Undrafting a task PR in an epic is an explicit allowlist entry (§8.1), not a general undraft permission.
- Ring-1 profiles default to `epicMerge: off`. It turns on only where the workplace policy allows agent merges into
  a shared branch (§17.19 ring-1 table).
- The epic → default-branch PR is always the human's. It carries the packet's walkthrough and the evidence
  roll-up.
- Protected modules (§7.7) always wait for the human, even inside an epic.

## 17.11 Ship contract and evidence providers

**Ship contract** (`skills/_shared/ship-contract.md`, read by every worker and by Sindri's ship Step):
- `maxReviewRounds` defaults to 3 (repo override in the profile). When it's exhausted, the item parks with the
  open findings.
- The evidence plan must be satisfied: every acceptance example has a passing artifact from a provider.
- Publish rules (PR body sections, attachments, tracker comment) are shared, not per skill.

**Evidence provider contract** (`sindri/evidence/`, also callable from skills through a CLI):

```ts
interface EvidenceProvider {
  kind: string;
  applies(task: SessionSpec, changed: ChangedFiles): boolean;
  plan(task: SessionSpec): Promise<Result<EvidencePlanItem[]>>;
  run(item: EvidencePlanItem, at: "base" | "head"): Promise<Result<EvidenceArtifact>>;
  verify(base: EvidenceArtifact | null, head: EvidenceArtifact): Promise<Result<Verdict>>;  // script first, judge only to explain
  publish(a: EvidenceArtifact): Promise<Result<PublishedRef>>;  // ask-first rules stay per provider
}
```

**Initial kinds** (an open list; a new kind is a registered artifact with its own eval suite):

| Kind | Typical change |
|---|---|
| `ui-browser` (ui-evidence, Playwright) | Web UI flows |
| `behavior-sim` (seeded, human-paced sessions checked against invariants) | Interaction-heavy UI |
| `ios-sim` (XcodeBuildMCP) | iOS |
| `test` (unit, integration, contract) | Logic and APIs |
| `api-transcript` (recorded request and response against a mock or preview) | Endpoints, webhooks, integrations |
| `cli-transcript` | CLIs, scripts, installers, infrastructure |
| `migration` (dry-run, rollback, schema diff) | Database changes |
| `perf` (benchmark, bundle size, type-check time) | Hot paths, build budgets |
| `a11y` (axe scan) | UI |
| `design-parity` (pixel diff against a mockup or design file) | UI with a design source |
| `security` (`/cso` diff mode, dependency audit) | Auth, input handling, dependencies |
| `trace` (log or telemetry query on a preview) | Jobs, async flows |
| `docs` (render and link check) | Docs |
| `recording` (screen or simulator video of the flow) | Any UI, as proof |
| `narrative` (explainer video from a Scene Plan JSON, rendered by a pinned renderer) | Optional and non-gating |

Selection = the packet's evidence plan ∪ providers whose `applies` matches the changed file classes. The
`narrative` kind takes JSON only: agents never write renderer code that runs on the host (§8.2).

## 17.12 Review from anywhere

**Network:** the hosts sit on a Tailscale tailnet. Nothing listens on a public interface. The dashboard is served
with `tailscale serve` (tailnet only), never `funnel`.

**Surfaces, matched to the decision:**

| Surface | Decisions | Where |
|---|---|---|
| **Swipe inbox** (mobile view of the Plan 6 dashboard; the Avalonia app, §17.17 Q4) | Binary, low-context, **non-protected** only: undraft a PR, accept evidence, approve or reject a **prompt-text** rule or idea card, keep or drop a scope surface, resolve `shape-unresolved`. Never tier raises, capability changes, evolve adoptions or publishes, or packet freezes | Phone |
| **Walkthrough pages** (HTML from the packet, the scope map, or a PR with its evidence) | Read and comment: designs, scope maps, PR walkthroughs, incident timelines | Phone or desk |
| **Desk queue** | Open questions, design revisions, amendments, packet freezes, protected-surface approvals | Desk |
| **Live session** (Warp remote control, or `sindri attach` over Tailscale SSH) | Watching or taking over a running session | Either |

**Authentication:**
- Host CLI verbs (`approve`, `answer`, `decide`, `handback`) stay the authority.
- A phone decision needs the tailnet identity **and** a passkey (WebAuthn) bound to an enrolled device. Each
  decision is a signed ledger record (kind, item, choice, device, time). It goes through the same code path as
  the CLI verb.
- Phone decisions are limited to the non-protected kinds listed above. Protected surfaces (§7.4 approval tier)
  and packet freezes stay at the desk.
- Free-text feedback from the phone is an `answer` record from an authenticated human.
- Typing into a session through Warp remote control is in-pane input: a **takeover** (§8.2 W6), never an
  authenticated decision.

This replaces Plan 6's "no actions from the browser" for the listed kinds only. §17.27 later extends UI actions to
every command, by action class. Plan 6's rule that the browser
can't bypass a CLI gate still holds: the phone path is the same gate with a second authenticator.

## 17.13 Swappable factory host

- The factory host can be the always-on Linux box or the laptop. Neither is the default. The profile lists both,
  and `sindri host handover <to>` moves the active role in either direction.
- **Handover:** running Steps checkpoint at their next safe point (WIP bundle, §8.2). The ledger, mirrors and
  index are synced to the target with a fenced epoch bump (§9.1), and the target resumes. A host that wakes up
  with a stale epoch stays passive (invariant 1).
- Linux built-ins from §12 move from "later" to a factory prerequisite: the systemd scheduler, the Docker sandbox,
  and a notifier the phone can reach (ntfy on the tailnet, or the dashboard's push).
- The desk reads the active host's dashboard over the tailnet, wherever it is.

## 17.14 Factory-mode skill maintenance

A **skill change** (including a lens or workflow template) is a work-item kind. Sources: a one-line human
request (desk or phone), evolve proposals, hook false-positive telemetry, and Steps that park on the same skill
twice.
- **Worker:** skill TDD. Write pressure scenarios that fail on the current text, edit the skill and show they
  pass. Then run the skill's eval suite (replayed sessions where one exists) and the skill lint: preamble,
  frontmatter, size budget, and no interactive asks on sindri-mode paths.
- **Modularity first:** shared contracts (ship contract, evidence providers, autonomy brief, rule selection, lens
  schema) live in `skills/_shared/` and are referenced, not copied.
- **Ship:** a PR to this repo with a human merge (§7.7). The overlay version applies until the merge (§7.4).

## 17.15 Subscription capacity and handoff

- The model proxy (§8.2) keeps a **quota ledger per account** (subscription or API key) and per provider.
- **Routing for a new Step:** first the lens's allowed providers (profile), then the account with the most window
  left that still keeps `quota.reserveForHuman`.
- **On a rate limit,** a running Step checkpoints. It relaunches from pack plus checkpoint on the next allowed
  account, or waits for its window. Today it can only pause.
- **Accounts are tagged `work` or `personal`.** A ring-1 item never runs on a personal account, and the reverse
  also holds.
- Unattended subscription use needs confirmed plan terms (§13 step 4). API-key mode is the fallback.

## 17.16 Rollout changes

**Prerequisite: agent readiness.** Before F0, this repo reaches agent-readiness Level 4 under
`2026-10-09-agent-readiness-design.md`, including the refactoring that design defines. Level 5 is reached
together with the F-steps, because its criteria (self-improving systems, requirements decomposed into
parallel execution) are what the factory delivers.

These steps come after Plan 6 and replace the order of §13 steps 3a–6 for factory work. Every step ends with a
"Turn it on" task (§13.3). Ring 1 (workplace repos) gets each step after ring 0 holds. Ring 2 starts after F3
holds on ring 1.

| Step | Contents | First value |
|---|---|---|
| F0 | Metrics revision (§17.2); packet schema with `validate`, `freeze`, `init --from-scope` and `amend`; rule, lens and workflow schemas and loaders | Packets, rules and lenses are checkable |
| F0.5 | **Thin slice:** one micro-packet → one task → one PR through `/specToProvenPR` in sindri mode, with 2 lenses (implementer, QA) and 2 evidence kinds (`test`, `cli-transcript`) | The whole pipeline works end to end once, before it grows |
| F1 | Lean session bootstrap (§17.8) with first-call token measurement; ship contract; evidence providers `test`, `ui-browser`, `api-transcript` and `cli-transcript`; sindri mode in `/specToProvenPR` and `/bugFixOrchestrator` | Sessions start small; any task kind can be proven |
| F2 | Task compiler and compiled sandbox policy (§17.5); roles and automatic handoff; stacked epics (main spec step 3a, expanded) | A frozen packet builds task by task in scope, with no pasted prompts |
| F3 | Workflow generation and the starting lens catalog: 4 lenses (implementer, QA, architecture, security), 4 evidence kinds, 3 standing workflows (PR babysitter, triager, daily digest). Scouting grows the rest (§17.18) | Any deliverable gets the right lenses, decided once at freeze time |
| F4 | Review anywhere (§17.12) and standing workflows at the `report` tier (§17.9) | Approvals from the phone; digests, triage and inspection running |
| F5 | Context graph with first adapters, the query planner, and backtest arms per dimension (§17.3) | Measured scoping and pack precision per repo |
| F6 | Skill-change items, rule growth into self-adopt, standing workflows promoted by tier | The factory maintains its own skills, taste and lenses |
| F7 | Swappable host handover on a shared lease, and subscription capacity routing | Builds move between hosts safely, and keep running when a window runs out |

F5 can run in parallel with F1–F4, because it doesn't depend on them.

## 17.18 Capability scouting: Sindri finds what to add next

Self-evolution (§7.4) improves what exists. Scouting finds what **doesn't exist yet** and brings it to the human as
ideas to approve. Examples: generating explainer videos from code and evidence, adding narration with a voice
clone, a new evidence kind, a better memory store, a new lens, a standing workflow nobody asked for yet.

**Sources** (read-only; scouting never installs or runs anything it finds):

| Source | Finds |
|---|---|
| **Internal gaps** | Packet misses, parks by reason, repeated scope-expansion requests, lenses with no evidence kind, evidence kinds with low precision, slow decisions, standing-workflow misses, steering turns the audit still sees |
| **Upstream changes** | New releases of pinned dependencies and vendored packs (§16), provider CLI changelogs (new hooks, flags or SDK features), new MCP servers |
| **The software-factory field** | What other agent factories and agentic dev platforms ship and publish: their tenets and frameworks (for example agent-readiness levels and criteria), orchestration and handoff models, review surfaces, evals, case studies and benchmarks. For example Factory (Droids, agent readiness), Warp (agents and remote control), coding-agent vendors' changelogs and engineering blogs. Each card says which of Sindri's principles or metrics the advance bears on, and whether Sindri already does it, does it worse, or lacks it. Factory repos and topics run through the dedicated field-harvest loop (§17.25) |
| **Ecosystem** | Skill and subagent catalogs, agent-tooling repos, papers and posts on agent harnesses, memory, retrieval and evals |
| **The human's notes** | The notes dir and the desk decision log ("would be nice if…", links saved for later) |

**Scout job:** a weekly bounded job, plus one on demand (`sindri scout [--topic <text>]`). It runs read-only with
network limited to an allowlist of sources, and it writes **idea cards**. Fetched text is untrusted data
(invariant 7): it's summarized and cited, and it never becomes instructions.

**Idea card:**

| Field | Contents |
|---|---|
| What | One sentence, plus where it would plug in (lens, evidence kind, standing workflow, context-graph component, surface, skill) |
| Why | The internal gap or metric it targets (§17.2), with citations |
| Evidence | Links, licence, maturity, maintenance signals, prior art in this repo |
| Cost and risk | Effort size, recurring cost, new egress or data flows, trust tier (§8.3), and whether it touches a protected surface |
| Eval plan | How Sindri would prove it helps: a backtest arm, an A/B, an evidence-precision check, or decision latency |
| Dedupe | Links to earlier cards on the same idea and how they were decided |

**Human decision (swipe inbox, §17.12):** `build` (it becomes a work item that the factory scopes, builds and
proves like any other, with protected surfaces still approval-tier), `later` (resurfaces when its cited gap
grows), or `no` (recorded with an optional reason, so the same idea isn't proposed again unless its evidence
changes materially).

**Ranking:** expected effect on the §17.2 metrics and on the gaps cited, divided by cost. Ideas tied to a
measured gap rank above ideas that are only novel. The inbox shows at most `scout.maxCardsPerWeek` (default 5),
and digests list the rest.

**Guardrails:**
- Scouting proposes. It never adopts. Every `build` passes through the normal Steps, evals and merge rules.
- New external services (for example a voice-cloning API) are new egress and new data flows. Their card must
  say what data leaves the machine. Building one is approval-tier, and the integration runs only on artifacts
  that are safe to send: never workplace code or ring-1 data unless the workplace profile allows that provider.
- Likeness: a voice clone may only be of a person who gave consent, by default only the human's own voice. The
  consent record is a profile entry.
- The scout's own precision is tracked: the share of cards approved, and the share of built ideas that kept
  their eval gain. It's tuned like any other artifact. If fewer than one card in ten is approved over two months,
  the weekly cadence drops to monthly.

**Starting idea backlog** (seed cards, to be re-ranked by the scout once it runs):
- explainer videos generated from code, the packet and evidence (Scene Plan JSON rendered by a pinned renderer;
  the `narrative` evidence kind, §17.11);
- narration for those videos with a consented voice clone of the human;
- the iOS and Android heads of the Avalonia app, if the desktop app plus the desk queue slow decisions (§17.17 Q4);
- marketplace memory or consolidation stores as context-graph arms (§17.3);
- importing community lens and subagent definitions as trust-tiered packs (§17.7).

## 17.19 Review resolutions (revision 3)

These resolve the /autoplan findings C1–C17. Where a rule here conflicts with an earlier section, this section wins.

### Capabilities, protection and the core tenet (C1, C2, tension 2)
- **Every registered artifact is split in two:** *prompt text* (instructions, questions, rubrics, rule wording) and
  *capability fields* (tool, command, MCP, network, write-scope and budget allowlists; trust tiers; workflow
  templates; security rules).
- **Prompt text** may self-adopt under §7.4. **Capability fields** are always approval-tier, a packet can't
  override them downward, and no model output may set them (invariant 13). Sindri still *proposes* capability
  changes (invariant 16); the human approves them at the desk.
- **Ceiling:** the compiled session policy = the profile's approved allowlist ∩ the lens allowlist ∩ the task spec.
  A lens can only narrow, never widen.
- **Imported lens and subagent packs** enter as prompt text at the lowest trust tier. Their capability fields are
  dropped on import and must be re-declared through approval. Upstream upgrades of imported packs never self-adopt.

### What the sandbox really enforces (C3, C4, C5)
| Control | Pattern (cooperative agent; hooks) | Boundary (holds against a hostile agent) |
|---|---|---|
| Writes | `PreToolUse` path guard; `PostToolUse` tree check | The sindri-side verifier refuses any bundle with out-of-scope paths (§8.5) |
| Commands | Gate on Bash command strings (easily evaded) | Credentials and network: a command that needs a secret or an endpoint the session lacks can't succeed |
| Network | — | The egress proxy allowlist per task (§8.2) |
| Reads | Gate on read tools | Mounts: only the repo, the pack and allowed refs are present |
| Budgets | — | The model proxy and the tick |
| Holdouts | — | Stored in sindri-private state, mounted **only** into evaluator Steps, never committed; a canary example in each set detects leaks |
- The per-session settings overlay lives in the read-only `/sindri` mount. The gate, the Stop gate and the safety
  hooks are in root-owned managed settings that the overlay can't disable.
- Rule checks that run repo-controlled config (for example a repo's eslint or analyzer config) run in the
  **phase-2** verifier, never in the phase-1 verifier, which runs no repo code.
- **The holdout store's path and format are open** (readiness design §9 R16). The R1b packet build (#142) checks only
  the packet side: each held-out acceptance example is named by its id in the packet, with its evidence kinds and no
  text (`PKT-HOLD-002`). The store side and the canary are checked once the store exists.

### Hosts and the lease (C6, C17, tension 3)
- One **shared lease** decides the active factory host. It lives on the always-on box (or another shared store),
  and its epoch is checked before every outward action: push, merge, tracker write, notification. A host that
  can't reach the lease is passive and can't act outward. A laptop working offline runs in a degraded local mode
  that can't push or merge.
- Hosts are tagged `work` or `personal`, like accounts. Ring-1 data and sessions never run on a `personal` host.
- `sindri host handover <to> [--dry-run]` signs the transferred ledger snapshot and verifies it on arrival.
  `--back` is a new handover in the other direction, never a restore of an old snapshot (D2).
- The dashboard is reached at one stable tailnet name that follows the lease, so phone passkeys stay valid across
  handovers.

### Context graph boundaries (C7)
- The context graph package defines ports (`Scrubber`, `Sanitizer`, `CodeIndex`, `Store`) that Sindri injects; it
  has no Sindri imports. The existing `Source` adapters are wrapped by a `Source → Connector` shim, not duplicated.
- One store per profile, written only by Sindri. Consolidation ("dreaming") output keeps the **lowest trust** of its
  inputs, so untrusted chat or telemetry text never becomes a trusted fact.
- The graph's evals (backtest arms) stay in Sindri, so the graph can't grade itself.

### Phone decisions (C8, C11, tension 1)
- **Enrollment** happens at the desk (`sindri device enroll`) and creates a passkey for one device and the stable
  tailnet name. `sindri device revoke` takes effect immediately.
- **Each decision signs** `(item id, item version, nonce, choice)`. A decision for a changed item, or a reused
  nonce, is refused with a code, and the item shows its new state.
- **Walkthrough pages** are served from a **separate tailnet hostname** with no cookies and no decision endpoints, so
  they share no passkey RP ID or cookies with the dashboard (D3). Comments link to the dashboard origin.
- **Plan 6 amendment:** the dashboard binds to loopback and is exposed only through `tailscale serve`. The Host and
  Origin checks accept the stable tailnet name, and the login token is replaced by the passkey for tailnet clients.
- **States per surface** (spec'd in Plan 6's amendment): pending, deciding, decided here, decided elsewhere (CLI or
  another device), stale (item changed), offline (queued locally, sent on reconnect, refused if stale), passkey
  failed, revoked device.
- **Not only swipe:** every swipe has visible buttons (WCAG 2.5.1). Swipe right = approve, left = reject, up =
  "queue for desk". Undo works because every outward command a decision causes waits **10 seconds in the outbox**
  before dispatch; undo cancels the queued command, never an action already sent (D3). Recordings carry captions or a transcript.
- **Queues:** the swipe inbox holds non-protected binary items; the desk queue holds open questions, amendments,
  freezes and every approval-tier item; the dashboard's "Needs you" lists both with their queue.

### Ring 1 (C10, tension 4)
| Feature | Ring 1 default | Turns on when |
|---|---|---|
| Assist mode, packets, task compiler, evidence, lenses | On | — |
| `epicMerge: auto` | Off | The workplace allows agent merges into a shared branch |
| Phone decisions | Off | The workplace allows decisions from a personal device on a personal tailnet |
| Personal Linux box as host | Never | Ring 1 runs only on `work` hosts |
| Standing workflows above `report` | Off | The workplace allows agents to comment or open PRs on their own |
| Personal accounts | Never | — |

"F3 holds on ring 1" is judged only over the rows that are on there.

### Developer experience (C12, C14, C16)
- `sindri packet init --from-scope <map>`, `packet amend`, and `packet validate --json` (one record per failed
  check with its rule id and fix).
- `sindri session spec <step>` prints the compiled session spec. `sindri why-denied <id>` shows the denial record:
  rule, attempted action, policy source, and how to request expansion.
- Skills detect sindri mode through `SINDRI_MODE=1` and a state-path shim that keeps today's state files
  (`reviews/<pr>.json`, bugfix state) readable from the same paths.
- A skill lint spec: frontmatter, preamble, size budget, no interactive asks on sindri-mode paths, and pressure
  scenarios as `scenarios/*.md` with an expected-behavior block.
- **Micro-packets (D1):** an S-or-smaller tracker item that passes the trust gate (§8.3) gets a generated packet.
  Its write scope is **computed by code**: paths linked in the item, plus code-index neighbours of symbols the item
  names. If no scope can be computed, there is no micro-packet and the item goes to the desk. A micro-packet is frozen
  by a **one-tap human decision** (phone allowed in ring 0, desk only in ring 1). It still goes through validation.
- Rule, lens and workflow files carry `schemaVersion` and have generated JSON Schemas. Lens `commands` name repo
  command aliases from `repos/<repo>.yaml`, not raw strings. `sindri rules explain <id>` shows why a rule was
  selected. A glossary in `docs/sindri/glossary.md` fixes "lens", "factory", "profile", "ring" and "packet".

### Human effort (C15)
- A weekly **human-minutes** metric (desk sessions, phone decisions, takeovers) with a ceiling in the profile.
  Crossing it two weeks in a row pauses new auto-started work and raises the top time sinks in the digest.
- A notification budget **per surface**: phone pushes per day, digest items, desk-queue additions. Standing
  workflows and scouting draw from it.

## 17.20 Parallelism and distribution

**Today:** one active host (invariant 1), a session cap, and one heavy job at a time per host (invariant 5). Inside
a packet, tasks with non-overlapping write scopes can run in parallel (§17.4, §17.7). Nothing yet schedules
**several packets and standing workflows at once** fairly, and nothing runs Sindri across machines. The target is a
ladder, and each level reuses the one below:

| Level | Shape | What it adds |
|---|---|---|
| **P1 · many workstreams, one host** | One Sindri, many packets, epics and standing workflows | A **scheduler** with a WIP limit per packet and per ring, priorities, and fair sharing. **Conflict-aware dispatch:** tasks whose write scopes or `deps.yaml` neighbours overlap are serialized, and others run together. **Resource classes** replace the single heavy-job lock: CPU, memory, browser, container builds, index builds and model quota each have their own slots. A capacity model sets the session cap from the smallest of quota windows, host resources and the human-minutes budget (§17.19) |
| **P2 · one coordinator, many workers** | One coordinator holds the shared lease (§17.19), the ledger, and every decision and outward action. Worker hosts only run containers | **Worker hosts** (laptop, Linux box, cloud boxes) register over the tailnet, advertise resource classes, and pull Steps under leases with heartbeats. They hold no tokens beyond their session proxies, and their spool ships back to the coordinator. A dead worker's Step returns to the queue from its last checkpoint |
| **P3 · federation** | Several independent Sindri instances (for example one per ring) | Instances never share state or credentials. They share **prompt text only** (lens wording, rules, rubrics) and idea cards, at the lowest trust tier. Evidence providers (code) and workflow templates or any capability field never cross; each instance re-implements or re-approves them (D5). Direction defaults to **home → work** |
| **P4 · team mode** | One Sindri serving a team at work | A shared database store instead of per-host SQLite, a user per human with roles, per-user and per-team budgets, tenant-keyed ledger rows, audit per user, and the second-user rules of main spec §15. Every decision still has a named human approver |

**Limits that don't go away with more machines:**
- **Model quota** is usually the first ceiling. The capacity router (§17.15) spreads Steps across allowed accounts, but
  never across the work/personal line.
- **Human review bandwidth** is the real ceiling. The human-minutes budget caps how much work Sindri starts, however
  many workers exist.
- **Merge contention:** parallel tasks on one epic rebase onto each other. Conflict-aware dispatch and small write
  scopes keep it low, and a task that conflicts twice is serialized.

**Seams built now, so the ladder needs no rewrite** (in the C# port, readiness §4b):
- The ledger behind a `LedgerStore` port, with SQLite (today) and a server database (P4) as implementations,
  sharing one contract-test suite.
- Every ledger event is idempotent, keyed by `(kind, item, attempt, sha)`, so retries and a second worker can't
  apply anything twice.
- `Lease`, `Launcher` and `Scheduler` are ports. `Launcher` gains a remote implementation in P2.
- Every row carries a tenant and ring key from day one, set to a single value until P4.

**Rollout:** P1 joins F2 (the task compiler needs it to run a packet's tasks in parallel). P2 follows F7 (it reuses
the shared lease). P3 starts when ring 1 begins, as the safe way to share improvements between home and work. P4
starts only when a team adopts it, and needs the workplace's approval.

## 17.21 Multiplayer foundations

§17.20 scales Sindri across machines. Serving several **people** (team mode, P4) needs more. Most of it is
missing today, because the design so far assumes one human, one SQLite writer, file logs and polling. The table
lists what exists and what is needed, in the order it's built.

| Piece | Today | Needed for multiplayer |
|---|---|---|
| **Event log and bus** | The ledger is the record. Hooks append to spool files. The dashboard polls | Every state change is an **event** with one envelope: `id`, `type`, `version`, `tenant`, `ring`, `actor`, `item`, `causation`, `correlation`, `trace`, `time`. Events go into an append-only log in the ledger's transaction (**transactional outbox**). Consumers (dashboard, phone, notifier, workers, digests, other instances) read with durable cursors. Transport is a `Bus` port: in-process → database polling or `LISTEN/NOTIFY` → a broker (for example NATS) at P2+. |
| **Exactly-once outward actions** | Writes run directly from the ship Step, with idempotency keys on some adapters | Every push, merge, comment, label and notification is an outbox **command** with an idempotency key, run by one dispatcher under the lease. Retries and a second worker can't repeat it |
| **Identity** | One OS user; host CLI verbs are the authority | People, devices and **service identities** (coordinator, each worker, each agent session, each standing workflow). Every record and event carries an `actor`. Agents never act as a person |
| **Authorization** | Implicit: the host user can do everything | Roles per tenant: *owner* (protected surfaces, capability approvals), *approver* (decisions in a ring), *contributor* (desk sessions, packets), *viewer*. Every decision kind declares the role it needs. Two-person approval is available per kind (for example capability changes at work) |
| **Tenancy** | A tenant and ring key on rows (§17.20 seam), single-valued | Isolation per tenant: data, secrets, budgets and quota, rule and lens overlays, notification routing and retention. A query without a tenant fails. No cross-tenant reads, except published packs (P3). Per-tenant fair scheduling, so one team can't starve another |
| **Concurrency between humans** | One human, so no conflicts | Optimistic concurrency on every item (the item version from §17.19). Claims and assignment ("who owns this decision"), visible presence, and a defined rule for two people deciding at once: the first signed decision wins and the other sees `decided elsewhere` |
| **Collaboration surfaces** | Decisions only | Comments on items, packets and walkthroughs; @-mentions; handing an item to another person; a shared desk queue with filters per person; notification preferences per user |
| **Observability** | JSONL tick and hook logs; scorer reports; OTel planned for sindri and the C# hosts (readiness §5) | **One trace per work item:** tick → job → session → tool calls → outward command, with W3C trace context passed into containers and model-proxy calls. Metrics: queue depth, lease age, Step latency by lens, decision latency by person, quota by account, cost by item, person and team, fail-open counts. Logs are structured and correlated by trace id, scrubbed by the logging processor. Every metric and log carries the tenant |
| **SLOs and alerting** | `doctor` and the silent-failure floor (§10.4) | SLOs for the factory: time from freeze to first PR, decision latency, stall rate, and the coordinator's availability. Alerts route to the on-call person of the tenant, with the main spec's notification budget per person |
| **Audit** | Ledger rows and the profile's audit log (§8.7) | An append-only audit stream per tenant with a **hash chain** (each entry covers the previous one), exportable. It records every decision, approval, capability change, override and outward action, with actor and device |
| **Secrets** | Keychain or 0600 files, per host | A secrets port with per-tenant vault backends. Short-lived credentials are issued to workers per Step, and rotation doesn't need a restart |
| **Config and policy as code** | Profile files with `profile approve` | Versioned profiles per tenant, changed by pull request with the required approvers, and a history of who changed which policy and when |
| **Data lifecycle** | Retention in §8.4 | Retention, export and deletion per tenant. Backups and restore drills for the coordinator store |
| **Operations** | One process on one host | Rolling upgrades with version skew rules: workers are at most one minor version behind the coordinator, and events are versioned and upcast. Zero-downtime migrations for the shared store. A health endpoint per component |
| **API** | CLI verbs; the dashboard's local HTTP | One versioned API (HTTP + event stream) for the CLI, dashboard, phone and other instances, plus webhooks. **Approval-tier and protected decisions stay terminal-bound** (a typed confirmation on an interactive TTY, or an enrolled-device passkey); an API token alone can never make them (D6) |

**Order of work:**
- **During the C# port (cheap seams):** the event envelope and transactional outbox, outward actions as outbox
  commands, `actor`/`tenant`/`ring`/trace fields on every record, the `Bus` and secrets ports, and OpenTelemetry with
  trace propagation. They make P1 simpler as well.
- **With P2:** the bus transport between hosts, service identities for workers, and trace propagation across
  machines.
- **With P4:** people and roles, tenant isolation, collaboration surfaces, hash-chained audit, the shared store,
  SLOs, and per-tenant secrets and policy.

**Not a goal:** Sindri doesn't become a hosted SaaS. Team mode runs on infrastructure the team controls, under
its own policy.

## 17.22 Round-2 resolutions (D1–D12)

Where this conflicts with earlier text, this section wins. §17.5 and §17.13 are read through it.

**The lease, from F0.5 (D2).**
- Store: one row in a small SQLite file on the always-on host, served to other hosts over the tailnet by the
  coordinator process (later the P4 shared store). Fields: holder host id, fencing token (monotonic), expiry.
  TTL 60 s, renewed every 20 s.
- **Required from F0.5**, when the first push happens; a single host holds it trivially. Today's per-host ledger
  epoch (`ledger/db.ts`) and the profile's `hosts.active` (`observe.ts`) are replaced by the lease's fencing token.
- Every outward action (outbox dispatch) and every **desk command** (`approve`, `answer`, `decide`, `freeze`) checks
  the token. A host without the lease forwards the command to the holder, or refuses with `SND-LEASE-0xx`. It never
  writes to its local ledger.

**Phone authority (D3).**
- Nonces are **issued by the server** per item version. An offline decision can only use a nonce received while
  online for that version, and is refused if the item changed.
- Undo is the 10-second outbox delay above. The dashboard origin carries comments; walkthroughs link to it.

**Workers (D4).**
- Workers are enrolled at the desk (`sindri worker enroll`), each with its own key and a class (`work` or
  `personal`, resources). Steps are matched to worker class, and ring-1 Steps only go to `work` workers.
- The model proxy and the real tokens stay on the coordinator. Workers get per-Step proxy credentials.
- **Verification never runs on the worker that produced the work.** Phase 1 and phase 2 run on the coordinator or a
  different worker.

**Outbox correctness (D9).**
- Before retrying a command, the dispatcher checks whether its effect already exists (the PR, comment, label or
  merge). Idempotency keys include the event's sequence number. Requeued Steps carry fenced attempt tokens.
- Rings are a dimension inside a tenant. Per-ring WIP limits (P1) apply within each tenant.

**Multiplayer security (D10).**
- The gate re-checks every outbox command at dispatch time.
- Tenancy is enforced in the store with database-level filters, and every cache key includes the tenant.
- The audit chain's head is anchored outside its store once a day (a signed commit to a private audit repo).
- Policy may require a second approver when an owner approves their own request.
- Instances authenticate each other with mutual TLS or signed requests.
- ntfy publishing is authenticated. Tailnet identity headers are trusted only from the local `tailscale serve`
  socket. Adapter pins are approval-tier. The graph store follows §8.4 retention.

**Desk and status surfaces (D11, D12).**
- A standing-workflow panel shows each workflow's tier, last run, precision and a **pause** control.
- A "blocked on you" alert fires when the factory has no runnable work without a human, within the notification
  budget.
- Phone accessibility: every swipe action has a screen-reader action, and focus moves to the next item after a
  decision.
- Generated walkthroughs follow a semantics checklist (landmarks, headings, alt text, captions).
- `sindri queue why <step>` explains why a Step is waiting. The error catalogue gains the `SANDBOX`, `LEASE`,
  `WORKER`, `OUTBOX` and `PHONE` areas.
- Docs deliverables per F-step are listed in the plan that builds it. Evidence publish asks first unless the repo
  profile says otherwise.

**Sizes, exit bars and ring-1 rows (D8).**

| Step | Size | Exit bar |
|---|---|---|
| F0 | S | Schemas, `validate`, `freeze`, `init --from-scope`, `amend` with golden tests |
| F0.5 | S | One micro-packet → one merged PR with evidence. **It runs on one of the C# port packets** (readiness R2), so the first factory-built PR lands during the port |
| F1 | M | 10 packets built with lean bootstrap; first-call tokens within budget |
| F2 | L | 3 packets with parallel tasks merged into epics, rework ≤ the step-0 baseline |
| F3 | M | Workflow generation used on 5 packets; zero `scope-mismatch` parks from missing lenses |
| F4 | M | 2 weeks of phone decisions; median decision latency below the desk's |
| F5 | L | One context-graph arm adopted per repo by backtest |
| F6 | M | 3 self-adopted prompt changes held for 20 items |
| F7 | M | One handover each way and one rate-limit failover in production |

"F3 holds" means 5 packets merged through workflow generation with rework at or below the step-0 baseline. Human
minutes are compared with the step-0 baseline in the weekly digest.

Ring-1 rows added to §17.19's table: containers on the work host (on, if the work host can run them), auto-start
(off until ring-1 policy allows), P3 federation (on, home → work only), workplace connectors (only those the
workplace approves).

## 17.23 Factory field review (revision 5)

A read of other software factories (2026-10-10) found sixteen practices this design didn't have or only had in
part. This section adds them. Where it conflicts with earlier text, this section wins. §17.5, §17.10, §17.11 and
§17.12 are read through it.

**Sources.** Only ideas are taken; no code is copied, so licences don't constrain this section. Each source is
also a seed for the scout's software-factory field (§17.18).

| Source | Licence | Ideas taken |
|---|---|---|
| addyosmani/factory | MIT | Review back-pressure, a test that proves itself, read-only existing tests, consequence tiers, the tune loop, comprehension drift, "never loosen" |
| superplanehq/superplane (factory PRD and design) | Apache-2.0 | The merge-confidence card, one PR writer per head SHA, factory and human cohorts |
| asklokesh/loki-mode, chainloop-dev/chainloop | not stated; Apache-2.0 | Closed outcomes with a NOT PROVEN list, signed run receipts |
| robi42/factory | MIT | Diff guardrails, the implementer reviewing the plan it will execute |
| inkatze/planwright | MIT | No-progress detection |
| actual-software/software-factory-intensive | none stated | Removal criteria on human gates, the feedback-loop taxonomy, measurable acceptance criteria |
| mmaripanguec/dev-sdd-kit | MIT | Untouchable tests, derived artifacts that say "no data" before they guess |

**New invariants** (added to §17.1):

17. **Sessions don't edit the tests that judge them.** Existing tests are read-only to a session unless the frozen
    packet names the change. A test change always gets a human read (§17.23.3).
18. **Production stops when review is full.** No new Step whose result needs a human decision starts while the
    pending-decision queue is at its limit (§17.23.1).
19. **Every verdict says what it didn't prove.** An item's outcome lists every planned check that didn't run or
    couldn't decide. Silence is never read as a pass (§17.23.6).

### 17.23.1 Review back-pressure

The human-minutes ceiling (§17.19 C15) is a lagging, weekly brake. This adds an instant one, read on every tick.

- **Pending decisions** are counted per ring: PRs waiting for a human read or merge, desk-queue questions and
  amendments, needs-approval items, and open swipe cards other than `fyi`.
- `review.maxPending` (profile, approval tier) defaults to 3 per ring.
- At or above the limit, the router starts no new Step whose result needs a human decision. That covers auto-start,
  a packet's next task, and standing workflows at the `propose` tier. Each such item waits with the reason
  `held: review-backpressure`, which `status` and the dashboard show.
- Running Steps finish. Fix rounds on PRs already in the queue still run, because they shrink it. `report`-tier
  standing workflows still run.
- A queue that stays full for 24 hours raises one `blocks-factory` digest entry, not a notification per item.
- Metrics (§17.2): queue depth over time, and hours items were held.

### 17.23.2 Every new test proves itself

The fail-on-base, pass-on-head check (§6) covers bug reproduce checks only. This extends it to every task whose
evidence plan includes `test`.

- In the verifier container (§8.5), a script splits the bundle's diff into test paths (by the repo's test-path
  convention) and non-test paths. It reverse-applies the non-test hunks and runs the task's new tests. Each test
  that backs an acceptance example must fail. Then it restores the tree. No model is involved.
- When test and implementation share a file (inline tests, for example) and can't be split, the verdict is
  `could-not-determine`. It goes into the Not proven section (§17.23.6) and is never counted as a pass.
- **New evidence kind `mutation`:** mutation testing scoped to the changed lines, through the repo's command alias
  (for example Stryker or mutmut). The share of surviving mutants on changed lines must be at or below the
  profile's bar. It's required at the `production` consequence tier (§17.23.4) and on protected paths. Elsewhere it
  runs on the inspector's nightly cadence (§17.9), not per task, to keep verification inside budget.

### 17.23.3 Existing tests are read-only to sessions

This amends §17.5's `write` field ("plus its tests by the repo's test-path convention").

- The compiled write scope includes **new** test files by convention. It includes an **existing** test file only
  when the packet task names it with the reason `test-change`, decided at the desk.
- Snapshot and golden files count as tests. Deleting a test, skipping it, or loosening an assertion counts as
  changing it.
- The `PreToolUse` gate denies the edit (`SND-SANDBOX-110`), and the sindri-side verifier refuses a bundle that
  changes an existing test outside scope.
- A PR with any existing-test change carries the `test-change` label and waits for a human read. `epicMerge: auto`
  doesn't apply to it (§17.10).
- Metric: existing-test changes per merged PR, and the share of them that a desk decision approved.

### 17.23.4 Consequence tiers

Auto-start was gated on size and ambiguity only (§7.1). Autonomy should follow who gets hurt when the work is
wrong, not how hard it is.

- The profile gives every repo a `consequence` tier. `consequence.paths` (for example auth, payments, migrations, CI
  and agent config) raise the tier for any task that touches them.
- A task's tier is the highest tier among its repo and the paths in its write scope. Ring-1 repos default to
  `production`. Changing a tier is approval tier.

| Tier | Meaning | Auto-start | `epicMerge: auto` | Evidence depth | Human read |
|---|---|---|---|---|---|
| `sandbox` | Unlaunched; nobody depends on it | ≤ S | allowed | fast | samples |
| `personal` | The human's own tools; nobody else depends on them yet | ≤ XS | allowed | full | samples, plus every protected path |
| `shared` | Other people depend on it | off by default | off | full | every PR |
| `production` | Someone's business depends on it | off | off | deep (including `mutation`) | every diff |

Evidence depth sets which evidence kinds are required: `fast` is types and lint, `full` adds `test`, and `deep` adds
`security`, `mutation` and the architecture checks. Protected modules (§7.7) wait for the human at every tier.

### 17.23.5 Diff guardrails

These deterministic checks are added to the shape checks (§6.2). They run at commit and in the verifier, on added
lines only, and a hit is refused like any other shape check.

- **New suppressions:** `eslint-disable`, `@ts-ignore`, `@ts-expect-error` with no reason, `# type: ignore`,
  `#[allow(…)]`, `//nolint`, `#pragma warning disable`, `[SuppressMessage]`.
- **New skipped or focused tests:** `.skip`, `.only`, `xit`, `@Ignore`, `[Fact(Skip=…)]`, `pytest.mark.skip`.
- **Swallowed errors:** an empty `catch`, or `except: pass`.
- **Build output and generated files** outside the repo's allowed paths, and lockfile changes in a task that may
  not change dependencies.
- **A source change with no test change,** unless the task kind is `docs` or `config`, or the packet says
  `test: none` with a reason.

An exception needs an inline `sindri-allow: <check-id> <reason>` marker. The verifier records each one and lists it
under Not proven (§17.23.6). The pattern list is a rule pack (§17.6), so evolve can grow it per language.

### 17.23.6 Closed outcomes and the Not proven section

Every item ends with exactly one outcome:

| Outcome | Means | What happens |
|---|---|---|
| `verified` | Every required evidence item passed | The PR proceeds under the ship contract |
| `already-satisfied` | The reproduce or acceptance check passes on the base | The item closes with that evidence; no PR |
| `blocked` | It needs an answer | It parks as needs-answer (§10.2) |
| `unverified` | It was built, but a required evidence item couldn't run or couldn't decide | The PR stays draft and the gap is named |
| `failed` | Gates are red after the round cap, or no progress (§17.23.13) | It parks with the findings |

**Not proven** is a PR-body section that a script generates from the evidence plan (ship contract, §17.11). It lists:
- every planned item without a passing artifact;
- every `could-not-determine` (§17.23.2);
- every `sindri-allow` exception (§17.23.5);
- every merge-card dimension marked "not assessed" (§17.23.7).

When nothing was skipped, the section says so in one line instead of being left out. Swipe cards show its count.

### 17.23.7 Merge-confidence card

Every task PR and epic PR gets a card with five dimensions, each scored 1 to 5:

| Dimension | Better | Source |
|---|---|---|
| Blast radius | lower | Deterministic: fan-in of changed symbols (code index), product nodes touched, the consequence tier of the paths |
| Reversibility | higher | Deterministic first: a migration with no down step, data deletion, a published API or package contract, or an external side effect lowers it. A judge explains |
| Drift from the packet | lower | Judge: the diff against the task's acceptance examples and the packet's out-of-scope list |
| Security | higher | The security lens's verdict when it ran; otherwise "not assessed" |
| Performance | higher | The performance lens and `perf` evidence when they ran; otherwise "not assessed" |

- Dimension names, directions and the caution and critical thresholds live in Sindri code as a registered artifact.
  Changing them is approval tier. For judged dimensions, a model supplies only a score and a one-line summary
  (invariant 13).
- A critical dimension turns the card red and moves the decision from the phone to the desk queue.
- Each card is stored with the PR's later outcome (merged clean, reworked, reverted, escaped defect), so evolve
  calibrates the thresholds the same way as direction checks.

### 17.23.8 Every human gate has removal criteria

Every human gate and approval-tier rule gets a **gate record** (a registered artifact):

| Field | Contents |
|---|---|
| Why | The incident or risk it exists for |
| Worst case if skipped | What would ship, and what recovery would cost |
| Removal criteria | Measurable, for example "20 consecutive items of class X with no escaped defect and no human fix" |
| Evidence so far | Kept up to date from the ledger |

- Evolve may propose loosening a gate only once its removal criteria hold, and the proposal cites the run of
  evidence. Loosening is always approval tier, never self-adopt.
- Tightening is immediate when an escaped defect traces to that gate (§17.23.9).
- **Never loosened** (structural; no removal criteria):
  - the human merges to the default branch;
  - existing tests are read-only to sessions (invariant 17);
  - the producer never verifies its own work (invariant 3, §17.22 D4);
  - protected modules need approval;
  - models never grant capabilities (invariant 13).

### 17.23.9 Escaped defects, triage accuracy and the tune report

- **Escaped defect:** within 30 days of merge, a fix, revert or incident on lines a Sindri item merged, linked by
  blame and PR refs. Each one starts a bounded trace job that lists the gates, evidence kinds, lenses and checks that
  ran on the original item, and names the one that should have caught it. The output is either a tighten proposal
  for that gate or, when no gate covered the case, a new deterministic check proposal.
- **Repeated catches become checks:** when a verifier or judge rejects the same class of problem 5 times in 30 days,
  Sindri proposes a deterministic check. This extends the enforcement ladder in §17.6 from rules to verifier
  rejections.
- **Triage accuracy** has two parts, both judged after the fact:
  - the share of items routed to auto-start or a micro-packet that later needed the desk (an amendment, a scope
    expansion, or a product question after freeze);
  - the share of items sent to the desk that a micro-packet could have handled.
- **Monthly tune report** (in the digest):
  - merged items;
  - human-minutes;
  - rework;
  - escaped defects and which gates they traced to;
  - triage accuracy;
  - queue depth;
  - proposed tightenings and loosenings.

  It ends with one question, answered with those numbers: *is the factory producing work worth the review attention
  it consumes?*

### 17.23.10 Comprehension drift

The inspector (§17.9) gains one signal: files that Sindri items changed 5 or more times in 30 days with no update to
any doc, ADR, rule or decision-log entry linked to them in the context graph (§17.3). These are the places where the
code has moved and the written understanding hasn't. Each finding becomes a `propose` item for the documentation
maintainer. Like every standing workflow, the inspector reports what it checked and found clean (§10.4).

### 17.23.11 One writer per PR, bound to a head SHA

Claims (§9.4) are per item. A PR also needs a writer lock, because the PR babysitter, a fix-round Step, a rebase and
the human can all push to it.

- Every actor that changes a PR takes an exclusive **PR lease** in the ledger, keyed by the PR and bound to the
  head SHA it started from. It is fenced by the host lease's token (§17.22 D2).
- Waiting for CI or review bots holds no lease.
- A push is made with `--force-with-lease=<bound sha>`. If the head moved, the push is refused, the activity's
  output is discarded, and the work is re-planned on the new head.
- A human push to a Sindri PR is a takeover of that PR (§9.3) until `handback`.
- A check handler starts at most one fix Step per failed head SHA.

### 17.23.12 Signed run receipts

When an item ends, Sindri writes a **receipt** containing:
- the item id, packet hash and workflow hash;
- the base and head SHAs and the bundle hash;
- each evidence artifact's hash and verdict;
- each judge decision id and score;
- the outcome and the Not proven list;
- token and compute totals;
- the hash chain of the item's ledger events.

The receipt holds hashes and ids only, never code or ring-1 text, so it is safe to share.

- It's signed with the host's Ed25519 key, which lives in the host's secret store. The public key is in the profile.
- `sindri verify <item | receipt>` re-hashes the receipt and checks the signature and the event chain. It prints one
  of `verified`, `tampered`, `unsigned` or `unchecked`.
- The receipt hash goes in the PR body. `epicMerge: auto` requires a verified receipt for the exact head SHA. The
  epic → default-branch PR carries the roll-up of its tasks' receipts.
- The daily audit-chain anchor (§17.22 D10) includes the day's receipt hashes.

### 17.23.13 No-progress detection

A fix loop (review rounds, CI fix rounds, verifier rejections) parks early, with the reason `no-progress` and the
repeated findings, when round *n* makes no progress over round *n−1*. That means any of:
- its open findings equal round *n−1*'s or are a superset of them, keyed by file, check and normalized message;
- the round produced an empty diff;
- the same check failed twice with the same failure signature.

It doesn't spend its remaining rounds. Metric: `no-progress` parks per lens.

### 17.23.14 The implementer reviews the plan; acceptance examples are measurable

- **Executability review.** In workflow generation (§17.7), between the model pass and the direction check, a
  review-only implementer-lens Step reads each task: its write scope, read seeds, acceptance examples and evidence
  plan. For each task it answers four questions:
  - Is it executable as written?
  - Is any context missing?
  - Is the write scope enough?
  - Can every acceptance example be checked by a command or an artifact?

  Objections revise the workflow within a round cap, or go to the desk. The evaluator Step later receives the
  packet's design rationale, so the build is reviewed against intent as well as the diff.
- **Measurable acceptance.** `sindri packet validate` (§17.4) rejects an acceptance example that uses an
  unmeasurable term ("fast", "clean", "nice", "intuitive", "simple", "good", "obvious") without a number or a
  reference artifact. Example: "returns in under 200 ms on the 10k fixture" passes; "fast" fails. The term list is
  a rule pack.

### 17.23.15 Feedback loops, including external ones

Every feedback loop is a record (a registered artifact) with: signal, trigger, target, action, verification and
history. There are three classes:

| Class | Fires on | Produces |
|---|---|---|
| Aggregate | A repeated pattern: the §7.4 steering sources, repeated verifier catches (§17.23.9) | A rule, prompt or check proposal |
| Reactive | One incident: an escaped defect, a revert, an incident | An immediate tighten proposal (§17.23.8) |
| External | A signal from outside the factory: a support ticket, a customer report, an app-store review, an error-tracker spike, through a `Connector` (§17.3) | A tracker item labelled `external`, with a triage SLA from the profile. Never a direct prompt edit |

- When the root cause of an external item turns out to be an agent artifact, a reactive loop is opened and linked
  to it.
- The digest lists external items past their SLA.
- External text is untrusted data (invariant 7). It is scrubbed, and values typed by users aren't kept beyond the
  item (§17.3).

### 17.23.16 Factory and human cohorts

The dashboard (Plan 6) gains a **Velocity** view: the last 14 days by default, filtered by repo.

- It shows the same indicators for three cohorts: all work, human-authored PRs, and factory-authored PRs (PRs
  opened for a Sindri item).
- Indicators:
  - merged PRs;
  - cycle time (item start to merge);
  - rework and revert rate within 30 days;
  - escaped defects;
  - tracked cost (model tokens and compute).
- Human time is shown as human-minutes, never as a dollar figure. The view has no per-person breakdown, and it
  presents the cohorts side by side, not as a contest.
- Per workflow and per standing workflow, it also shows the success rate (items closed `verified` and merged ÷ items
  closed) and the cost per merged item.

### 17.23.17 Metrics added to §17.2

| Measure | Target | Why |
|---|---|---|
| Pending-decision queue depth, and hours held by back-pressure | Depth ≤ `review.maxPending`; hours held tracked | Invariant 18 |
| Share of acceptance-example tests proven to fail without the change | ≥ 0.9 of tasks with `test` evidence | §17.23.2 |
| Existing-test changes per merged PR | Tracked; each one desk-approved | Invariant 17 |
| Triage accuracy (both directions) | Rises month over month | §17.23.9 |
| Escaped defects per merged PR, and the share traced to a gate | Falling; traced share ≥ 0.8 | §17.23.9 |
| `no-progress` parks per lens | Tracked | §17.23.13 |
| Merge-card calibration (red cards later reworked vs green cards later reworked) | Red rework rate clearly above green | §17.23.7 |
| Factory vs human cycle time and rework, same size class | Factory rework ≤ human rework | §17.23.16; matches the auto-start kill criterion |

### 17.23.18 Rollout

| Step | Adds |
|---|---|
| F0 | Consequence tiers in the profile schema; gate records; the outcome enum and the Not proven section in the ship contract schema; the measurable-acceptance check in `packet validate` |
| F0.5 | The thin slice runs with read-only existing tests, the prove-the-test script and the diff guardrails from its first PR |
| F1 | Not proven in PR bodies; no-progress detection; the `mutation` evidence kind |
| F2 | Review back-pressure; the PR lease; signed receipts, before `epicMerge: auto` is allowed anywhere |
| F3 | The implementer executability review; the merge-confidence card |
| F4 | The card on the swipe inbox; comprehension drift in the inspector; the Velocity view |
| F6 | Escaped-defect tracing, triage accuracy and the tune report; evolve proposals against gate records; feedback-loop records, with external loops once F5's connectors exist |

## 17.24 Factory field review, second pass

A second, deeper read of the same sources (2026-10-10) found nine more practices. Where this section conflicts
with earlier text, it wins. §17.23.6 and §17.23.9 are read through it.

### 17.24.1 Failure ownership (loki-mode)

Every failed check, Step and item records an **owner**:

| Owner | Examples |
|---|---|
| `code` | A test fails on the head, or a lint, type or shape check fails on code the session wrote |
| `harness` | A Sindri bug, a hook crash (`SND-HOOK-9xx`), a bad handoff |
| `environment` | A CI runner or preview environment fails, a dependency registry is down, a known flake |
| `provider` | A model error, a rate limit, a quota limit, a timeout |

- Ownership comes from code, not a model. Exit sources, error codes, the flake register and a rerun on a clean
  runner decide it. Ownership that can't be decided is recorded as `unknown`, and it never counts as `code`.
- **Only `code`-owned failures count** toward:
  - rework and revert rates;
  - escalation (§17.24.5);
  - the kill criteria (§2);
  - consequence-tier and gate-record evidence (§17.23.4, §17.23.8);
  - evolve's outcome labels (invariant 9).
- Every other failure goes into Not proven (§17.23.6) and into `doctor`'s health report. Repeats of the same
  non-code cause become a harness or environment work item, not a prompt proposal.
- Metric: failures by owner per week. A rising `harness` share is a Sindri defect.

### 17.24.2 Name the oracle before a migration (addyosmani)

A task with change class `migration`, `port`, `upgrade` or `large-refactor` must name its **oracle**: what proves
behaviour didn't change.

- `sindri packet validate` refuses such a task when it has no oracle.
- When existing coverage is thin, workflow generation (§17.7) puts the oracle-building tasks first:
  1. build on the current stack;
  2. typecheck;
  3. pin current behaviour with characterization or golden tests.

  Only then come the migration tasks, checked against those tests.
- The pinning tests are frozen with the packet. Under invariant 17, the migration tasks can't edit them.
- This repo's own C# port (readiness §4a step 1, §4c) is the exception: it is a clean cutover and a reimplementation
  in idiomatic C#, with the TS as a guideline for what each package does, never a behaviour to pin. Workflow
  generation must not add characterization or golden tasks that record TS output for a port packet. Its oracle is
  the C# package's own contract tests plus the **hook contract** only (what the bash hooks and statusline read: the
  commands and arguments they call, the fields they `jq`, the exit codes and stderr prefixes they branch on, the
  files they write), checked by running the real hook scripts against the C# binaries.

### 17.24.3 Critic lens (addyosmani)

A review-only lens that argues **against** the change. Every other Step leans toward shipping, so this one doesn't.

| Looks for | Example |
|---|---|
| Assumptions that spread unstated | A default chosen silently that three callers now depend on |
| Abstraction with one caller | An interface, factory or config option used once |
| Behaviour change behind a green suite | A case the tests never covered now behaves differently |
| Dead code and orphans | Exports, branches or config keys left behind by an earlier approach |
| Six-month comprehensibility | Would someone who wasn't in the session understand why it's shaped this way? |
| Unconsidered blast radius | Other readers of this data, callers of this function |

- It's required at the `production` consequence tier and on protected paths. Elsewhere it's optional.
- It runs on a different model from the implementer. Its findings go into the review round like any reviewer's.
- A critic finding that repeats becomes a shape-check or rule proposal (§17.23.9).

### 17.24.4 Executable assertions on context (dev-sdd-kit)

Context-graph facts and pack sections may carry a cheap **assertion** that is checked before injection. Examples:
- a symbol exists;
- a path or route exists;
- a grep matches;
- a read-only query returns rows;
- a config key has a value.

Rules:
- Assertions run in the task compiler (§17.5) with read-only tools and a time budget.
- A failing assertion drops the section, or marks it `stale` when the injection plan requires it, and records a
  freshness miss against the component that produced it (§17.3).
- A section or fact without an assertion falls back to commit-SHA freshness.
- Metric: freshness misses per component. It feeds that component's backtest arm.

### 17.24.5 Executor escalation on evidence (loki-mode)

The decision ladder governs decisions. This extends it to the model that runs a build Step.

- Each lens names a starting executor tier (§17.7 `model.tier`). A Step moves up a tier only after a
  **`code`-owned** failure (§17.24.1): first a retry at the same tier, then the next tier up. Escalation never
  moves down within an item.
- Sindri remembers outcomes per **repo shape** (workspace kind plus test runners, for example
  `multi-root:dotnet+vitest`). A shape with 2 `code`-owned losses in its last 3 runs at a tier starts at the next tier.
- Non-code failures cause no transition. Each escalation is recorded on the receipt (§17.23.12).
- Evolve may propose changing a lens's starting tier from the shape history. That's a budget field, so it's
  approval tier.

### 17.24.6 `spec-conflict` outcome (loki-mode)

This adds a sixth outcome to §17.23.6:

| Outcome | Means | What happens |
|---|---|---|
| `spec-conflict` | The task contradicts the packet, the code at the base SHA, or another frozen task | Parks to the desk as a packet miss (§17.2), citing both sides. Not counted as a failure |

### 17.24.7 Delivery metrics and blameless postmortems (dev-sdd-kit)

- The Velocity view (§17.23.16) adds DORA's four keys per repo and cohort: deployment frequency, lead time for
  changes, change failure rate and time to restore. They're computed from deploys, merges and incidents through
  `TelemetrySource` (§17.3). A repo with no deploy signal shows "no data" rather than a guess.
- The incident resolver's standing workflow (§17.9) ends with a **blameless postmortem** draft from a template:
  timeline, contributing factors, what detected it, what would have caught it earlier, and actions. The human
  edits and publishes it. Its "what would have caught it" line opens a reactive feedback loop (§17.23.15).

### 17.24.8 Sealed derived artifacts (dev-sdd-kit)

Anything Sindri generates for people to read carries a **seal**: the generator id and version plus hashes of its
sources. That covers walkthroughs, metric reports, the digest, generated docs, the Velocity numbers and scope maps.

- `sindri seal check` (and `doctor`) fails when a sealed artifact's sources changed or it was hand-edited. The fix
  is to regenerate it, the same pattern as `sync-rules.sh --check`.
- A generator writes "no data" for any value it can't derive. It never interpolates or estimates silently.
- Hand-written docs aren't sealed. Comprehension drift (§17.23.10) covers them.

### 17.24.9 One live variant per artifact (Actual AI workshop)

Evolve runs at most **one** variant per artifact at a time: in offline comparison, shadow, or canary. A second
proposal for the same artifact waits until the first is adopted, reverted or rejected. Changes to different
artifacts that share an eval suite are also serialized. This keeps a change in outcome attributable to one cause.

### 17.24.10 Rollout

| Step | Adds |
|---|---|
| F0 | Failure ownership in the ledger schema; the oracle field and its check in `packet validate`; the `spec-conflict` outcome |
| F1 | Ownership on every check and park; sealed digests and reports |
| F3 | The critic lens; executor escalation and the repo-shape history |
| F4 | DORA keys in the Velocity view; postmortem drafts in the incident resolver |
| F5 | Context assertions in the task compiler and the context graph |
| F6 | One live variant per artifact in evolve's scheduler |

## 17.25 Field harvest: Sindri finds what other factories do and builds it in

Sections 17.23 and 17.24 came from a person and an agent reading other software factories by hand. Under the core
tenet, this becomes **Sindri's own job**: it watches the field, finds practices it lacks, proves which ones are
worth having, and builds them through its own factory. §17.18's scout finds ideas. Field harvest is the
specialised, continuous loop for one source class: other software factories, agent harnesses and coding-agent
platforms.

```
 field registry ──► harvest job ──► practice cards ──► gap check vs Sindri ──► rank ──► human: build/later/no
 (repos, topics,    (read-only,      (mechanism, cited    (have / partial /                     │
  blogs, changelogs) sandboxed)       file + line, licence) missing, cited §)                   ▼
                                                                    spec section + guide + work items
                                                                    built and proven by the factory
```

**Field registry** (a registered artifact; the human adds or removes entries from the desk or phone):
- named repos, starting with every source in §17.23 and §17.24;
- GitHub topic queries (for example `topic:software-factory`, `topic:agent-harness`, `topic:coding-agent`), sorted
  by stars and recent activity, with a minimum-signal filter;
- coding-agent vendor changelogs and engineering blogs (§17.18's field row);
- for each entry, the paths that usually hold the design: README, `docs/`, specs, PRDs, design docs, agent
  definitions, skills, hooks, gate scripts and the CI workflows.

**Harvest job** (weekly, plus `sindri harvest [--repo <url>] [--topic <q>]`):
- A bounded, heavy job under the scout's guardrails (§17.18): network limited to the registry's hosts.
- It shallow-clones each source into a scratch directory **outside any worktree**. It never runs, builds or
  installs anything it fetched, and all fetched text is untrusted data (invariant 7).
- It reads only what changed since the last harvest (a commit-SHA cursor per source), so the weekly cost tracks the
  field's activity, not its size.
- It stores no code from a source. It keeps summaries with citations: repo, commit SHA, path and line range.

**Practice card** (extends the idea card, §17.18):

| Field | Contents |
|---|---|
| Practice | One sentence, plus the mechanism: what runs, when, and what it decides |
| Citation | Source repo, commit SHA, file and line range for every claim |
| Licence | The source's licence. MIT, Apache-2.0 or BSD allow porting code with attribution after an ownership check. Anything else, or none, allows ideas only |
| Gap verdict | `have`, `partial` or `missing`. Each verdict cites the Sindri spec sections and code it was checked against, and quotes the line that covers it or says what is absent |
| Bears on | The §17.2 metrics and invariants it affects |
| Fit | Where it plugs in: lens, evidence kind, check, outcome, metric, surface, rule pack, standing workflow, context-graph component |
| Cost and risk | Size, recurring cost, new egress, protected surfaces touched |
| Eval plan | How Sindri would prove it helps (§17.18) |

**Gap check, not trust.** The gap verdict is made by a model **with read access only to Sindri's spec, guide and
code index**. A second model, which doesn't see the first one's verdict, must agree before a `missing` or `partial`
card is shown to the human (invariant 3). A `have` verdict that a later item contradicts is an eval miss for the
gap checker. This is how the "already covered" list in §17.23 was made, now done by Sindri.

**Human decision.** Cards appear in the swipe inbox under `scout.maxCardsPerWeek`, ranked by their effect on cited
gaps ÷ cost. Cards that only add novelty rank below cards tied to a measured gap. Decisions are `build`, `later` or
`no`, as in §17.18.

**What `build` does.** It opens one work item that the factory runs like any other:
1. drafts the spec section (an addendum entry in this document's format, with sources, mechanism, rollout row and
   metric) for the human to approve at the desk;
2. updates the HTML guide (`docs/sindri.html`) and its catalog rows;
3. scopes, builds and proves the mechanism through the normal Steps, evals and merge rules (§17.10). Protected
   surfaces stay approval tier.

The spec edit and the build are separate PRs, so the design can land before the code.

**Guardrails:**
- Harvest proposes and never adopts (§17.18). It can't edit the registry, its own gap-check prompts or eval suite
  (invariant 11).
- A card citing code it can't port (licence) describes the mechanism in Sindri's own words. The build never copies
  that source.
- Sources from a workplace (ring 1) are never added to the public registry. A ring-1 profile may have its own
  private registry.
- If fewer than one harvested card in five reaches `build` over two months, harvest drops from weekly to monthly.

**Metrics** (added to §17.2):
- practices harvested;
- the share judged `missing` that the human agreed with;
- cards built;
- the share of built practices still holding their eval gain after 20 items;
- median time from a practice appearing upstream to it being live in Sindri.

**Seed run.** §17.23 and §17.24 are the first harvest, done by hand on 2026-10-10. Their source list is the
registry's starting content, and their "already covered" findings are the gap checker's first labelled examples.
F6 adds the harvest job; until then, `sindri scout --topic software-factory` (§17.18) runs it on demand.

## 17.26 Scoping v2: tracker intake, a retrieving loop, and a packet you edit with the same machinery

This amends §7.1 (intake), §7.5 (the scoping harness) and §17.4 (design studio, packet, amend). Where it conflicts
with them, this section wins.

The v1 harness (Plan 4) has three limits:
- it starts only by hand;
- its challenger re-reads the drafter's evidence pack, so it can find only what was already gathered (§7.5
  amendment 14);
- the map leaves preprocessing as a one-shot result: the studio can edit it, but nothing re-runs the search
  around an edit.

Scoping v2 fixes all three. The principle: **the same pipeline runs at intake, in the loop, and at the desk.** A
human edit is just another seed for it.

### 17.26.1 Two triggers

| Trigger | How |
|---|---|
| Human | `sindri scope <brief | spec section | tracker project | tracker item>`, as today |
| Tracker intake | The tick (§7.1) scans the tracker with the profile's `intake` filter. Items that triage routes to the scoping queue get a scoping job without a human starting it |

```yaml
intake:
  tracker: linear
  filter:
    assignee: me            # me | unassigned | any
    kinds: [project, issue] # projects always scope; issues scope only when triage sizes them above autoStartMaxSize or marks them ambiguous
    states: [backlog, todo]
    teams: [<team key>]
  scopeOnArrival: true      # false: list in `status` as scopable; the human runs `sindri scope`
```

- `assignee: me` is matched against the tracker identity in the profile. Assigning an item to yourself in the
  tracker is the way to say "Sindri, scope this".
- Intake scoping runs host-side and read-only, exactly as the manual command does. It writes the map to the notes
  dir and adds a **desk-queue entry**: "scope map ready". It never creates tracker issues, writes to the tracker or
  starts a build.
- A finished map is a pending decision, so it counts toward `review.maxPending` (§17.23.1). When the queue is full,
  intake keeps the item `held: review-backpressure` and doesn't scope it yet. That keeps the desk from filling up
  with maps nobody reads.
- An item whose text changes after its map was written gets a `map stale` marker. It's re-scoped only on request
  or when the item changes state.
- Ring-1 profiles default to `scopeOnArrival: false`, and intake follows the workplace's agent policy (§17.17 Q5).

### 17.26.2 The loop retrieves every round

```
 brief + intake record
        │
        ▼
 seed queries ──► retrieve (all sources) ──► draft / revise map ──► deterministic checks
        ▲                                                                   │
        │                                                                   ▼
 expansion queries ◄── challenger: gaps as queries ◄── prune pass ◄── challenger: what's missing?
 (recorded in the map)                                 (what's not supported?)
```

**Each round:**
1. **Challenge for recall.** The challenger (a different model from the drafter) doesn't just name missing
   surfaces. It writes **expansion queries** that would find evidence for them, each typed:
   - `symbol` (a name or pattern);
   - `neighbours` (callers, callees or module neighbours of a symbol already in the map);
   - `similar` (embedding search from a surface's text or a cited symbol);
   - `tracker` (a search over issues, comments and linked docs);
   - `notes`;
   - `route` or `flag` (a pattern over routes, feature-flag keys and config).
2. **Retrieve.** Sindri code, not the model, runs each query against the sources. Today those are the Plan 3 index
   (symbols, the call and module graph, per-symbol embeddings, the deps layer), the tracker, notes and transcripts
   (opt-in). After F5 the context graph's components are added (§17.3). Results are scrubbed, fenced as untrusted,
   deduplicated against what the run already holds, and cut to the round's budget by rank.
3. **Revise.** The drafter revises the map with only the new evidence plus the current map. It doesn't re-read
   the whole pack. A surface it adds must cite one of the retrieved records.
4. **Prune for precision.** A pruning pass on the challenger model marks surfaces as `weak` when:
   - their only evidence is a keyword match;
   - their citations don't support the claim;
   - they come only from `provenance: model` links (§17.3).

   Code checks the citations first. The model only reads the ones code can't decide. A weak surface is moved to a
   **Considered and dropped** list with its reason, not deleted, so the human can see it and bring it back.
5. **Deterministic checks**, as in §7.5.

**Stopping:**
- The loop stops when a round adds no new surface and drops none, or at `scope.maxRounds` (default 5 for v2), or
  at the token budget.
- **Retrieval convergence** also stops it: the expansion queries return no record the run hasn't already seen.
  That is the honest signal of "exhaustive given these sources". The map states it as `converged` or
  `capped (rounds | budget)`.
- A capped map lists its open expansion queries, so the studio can run them later.

**Recorded in the map:**
- every query, its round, its type and how many records it returned;
- which surfaces it led to.

The backtest (§7.5) reads this to show which query types earned their recall. Evolve tunes the challenger's query
prompt against recall and precision, never one without the other (the pass bar is unchanged).

**Code tools.** The loop still gives models **no tools**. "Agent turns that explore the code" are replaced by typed
queries that Sindri runs. That keeps scoping read-only, budgeted and replayable, and keeps exploration out of the
model's hands (invariant 13).

### 17.26.3 The map carries the file level, as candidates

Each surface in a v2 map also lists **candidate files and symbols**: the code its citations and retrievals point
to, ranked, with the query that found each one. These are candidates, not write scopes. They seed the design
studio, so the packet's write scopes and read seeds start from evidence instead of a blank page. `packet init
--from-scope` (§17.4) copies them into each task as **proposed** write scope and read seeds, marked `proposed`
until the human or the studio confirms them.

### 17.26.4 The packet is edited with the same machinery

Preprocessing can miss things or find too much. In the design studio, the human and the agent edit the map and the
packet, and **every edit re-runs the relevant part of the same pipeline, scoped to the edit**:

| Edit | What re-runs | What you get back |
|---|---|---|
| **Add** a surface, file, symbol, route, ticket or product node | A retrieval round seeded by the addition: its neighbours, similar symbols, tests by convention, tickets that mention it, and the challenger's recall pass on just that area | Proposed related surfaces, files and read seeds, each cited. Accept them one by one, or all |
| **Remove** a surface, file or task | A dependency check: what cites it, which workstreams, tasks, acceptance examples or evidence items lose coverage, and which remaining items only existed because of it | A list of knock-on removals and coverage gaps to accept or reject |
| **"Too much"** on a surface, workstream or the whole map | The prune pass at a stricter bar | A shorter list with each dropped item's reason |
| **"Something's missing around X"** (free text) | The challenger turns the text into expansion queries, and one retrieval round runs | New candidates, cited |
| **Narrow or widen** a write scope | `packet validate`'s write-scope rules (exists or declared new, no overlap with concurrent tasks) plus a neighbours query on the new paths | Validation result and any newly reachable tests or callers |
| **Run the open queries** of a capped map | One round per query | As for Add |
| **Several at once** ("add X but remove Y") | All the edits are planned as **one change set** and re-run together: retrieval seeded by X skips Y and anything removed with it, and the dependency check for Y counts X as present | One combined preview of everything the change set adds, removes and leaves uncovered |
| **Restore** ("remove A — actually A was right") | Brings A back from its tombstone with its citations and provenance. If A's removal took knock-on items with it, they are offered back too, one by one | A, plus the knock-on items to accept or leave out, and any coverage A's return closes |
| **Undo** a change set, or go back to a revision | Reverts every edit in that change set (or every change set after the revision), then re-runs the checks | The packet as it was, with the reverted change sets kept in history |

Commands: `sindri packet edit <id> add|remove|prune|ask|narrow|widen|restore …` and `sindri packet undo <id>
<change-set | --to <revision>>` from a terminal, and the same verbs in the studio view (§17.27). They all go through
one code path. The studio agent turns what you say ("add the export job but drop the admin screen") into one change
set using the same verbs. Nothing changes until you accept it, and you can accept part of a change set.

**Change sets and revisions.** Every accepted change set makes a new **draft revision** of the map and packet. The
revisions form an append-only history, so nothing you remove is lost and any earlier state can be restored. Each
change set records:
- what you said or typed;
- the edits it made;
- what it re-ran;
- the knock-on items it took or added.

**Rules for edits:**
- **Provenance on everything.** Every item in the map and packet carries `source: retrieval | model | human` and,
  for retrieval, the query that found it.
- **Tombstones.** An item the human removes is kept with its reason, and the loop never re-proposes it unless new
  evidence appears (a record the run hadn't seen when it was removed). Without this, every re-run would argue with
  the human. A tombstone is never final. `restore` clears it, and the item comes back as human-pinned.
- **Changing your mind is normal.** Restore, undo and re-remove are ordinary edits. Each one goes in the decision log
  with your reason ("A was right once we chose the export design"), so the packet records why the design moved.
- **Human adds are pinned.** The prune pass never drops a human-added item. It may flag one as weak, with the
  reason.
- **Decision log.** Each accepted or rejected proposal is a decision-log record (§17.4), with the asker
  (`retrieval`, `challenger`, `studio agent` or `human`).
- **Same checks before freeze.** After any edit, the deterministic scoping checks and `packet validate` run again.
  `freeze` refuses a packet with `proposed` items not yet confirmed or rejected.
- **After freeze**, an amendment (§17.4) uses the same verbs on a new draft revision. Only the tasks the edit
  touches are re-planned.

### 17.26.5 Edits are labels

The human's edits are the best signal of what preprocessing got wrong. They are outcome labels, so invariant 9
holds.

Labels are computed **at freeze, from the net difference** between the preprocessed map and the frozen packet, not
from each edit. An item removed and then restored produces no label, and neither does a change set that was undone,
so changing your mind while designing never teaches the loop the wrong lesson. Each freeze or amendment labels only
its own net difference. The net difference gives:
- an **add** that wasn't in the map is a **recall miss**, tagged with the query types that could have found it;
- a **remove** of a retrieved or model item is a **precision miss**, with its reason;
- a proposal **accepted** after an Add or Ask counts as a recall hit for that query type;
- mid-build packet misses (§17.2) are recall misses found late, and they weigh more.

These labels join the backtest corpus. Evolve proposes changes to the challenger's query prompt, the prune bar and
per-query-type budgets, judged on recall and precision together (§7.4). Metrics, added to §17.2:
- recall and precision misses per packet, at the desk and after freeze;
- the share of maps that converged rather than capped;
- the share of `proposed` candidates the human confirmed.

### 17.26.6 Rollout

| Step | Adds |
|---|---|
| Scoping v2a (any time after Plan 3; it needs only the code index and the tracker) | The retrieving loop with typed queries over the index, tracker and notes; the prune pass; convergence; the queries recorded in the map; candidate files per surface |
| F0 | `packet init --from-scope` carries the candidates as `proposed`; `packet edit` verbs (including restore) with provenance, tombstones and pinned human adds; change sets, draft revisions and `packet undo`; `freeze` refuses unconfirmed `proposed` items; labels from the net difference at freeze |
| F2 | Tracker intake with the `intake` filter and `scopeOnArrival`, behind review back-pressure |
| F4 | The same edit verbs on the studio's walkthrough page and the phone (keep or drop a surface is already a phone decision, §17.12) |
| F5 | Context-graph components join the retrieval round as query targets, each kept only if its backtest arm helps |
| F6 | Edit labels feed evolve's scoping proposals |

## 17.27 Every step is manageable from the UI

Every Sindri action is manageable from the UI, not only from a terminal. That covers starting a scope, editing a
packet, approving, adopting and promoting. The UI can lag the CLI while it is built, but no action is terminal-only
by design.

This amends:
- the Plan 6 dashboard spec, which is "read-mostly" with actions limited to attach;
- §17.12, where "host CLI verbs stay the authority".

Safety doesn't change, because UI actions go through the same gates as CLI verbs (below).

### 17.27.1 One command layer, two clients

- Every action is a **command**: a typed request handled by one handler. The handler does the validation, writes
  the ledger record and the audit entry, and returns `SND-*` errors. Readiness §4b.1 already shapes each use case
  as a slice with "an endpoint or CLI mapping". This makes both mappings required.
- The CLI and the UI are two clients of the same handlers. The UI holds **no logic of its own**: no extra checks,
  shortcuts or UI-only state changes. A rule that is true in the terminal is true in the browser.
- Long-running commands (scope, backtest, harvest, index build, evolve check) run as ledger jobs. Both clients
  follow the same progress events, for example `gathering…`, `round 2: 14 queries, 9 new records`,
  `pruned 3`. Both can cancel the job.
- **The authority is the command layer.** "CLI verbs are the authority" now means the handlers they call, which
  the UI calls too.

### 17.27.2 Which surface may run which action

Every UI action carries the same identity and gate as its CLI verb, plus the authenticator its class requires:

| Action class | Examples | Terminal | Dashboard on the host (loopback) | Dashboard over the tailnet (desk view) | Phone (swipe inbox) |
|---|---|---|---|---|---|
| Read and watch | Status, timelines, maps, receipts, metrics | yes | yes | yes | yes |
| Read-only jobs | Scope, backtest, harvest, index build, `doctor`, `verify` | yes | yes | yes, passkey | From a card only (for example "run the open queries") |
| Non-protected decisions | The §17.12 list: undraft, accept evidence, keep or drop a surface, prompt-text rules, idea and practice cards | yes | yes | yes, passkey | yes, passkey |
| Desk actions | Answer questions; edit, freeze and amend packets; start an item; hand back a takeover | yes | yes, passkey | yes, passkey | no |
| Protected (approval tier) | Allowlists, egress, trust lists, tier raises, consequence tiers, `review.maxPending`, gate loosening, evolve adoption, `channel promote`, `profile approve` | yes | yes: passkey plus a **typed confirmation bound to the hash of the full diff**, which is shown in full on the page | no | no |

- Every UI action writes a **signed decision record** (kind, item, choice, device, time), as phone decisions
  already do (§17.12).
- Protected actions keep the rule that the confirmation is typed against the exact content. For example, evolve
  adoption types the variant's sha256 (§7.4). A button alone never adopts, promotes or widens anything.
- The phone stays limited to binary, non-protected decisions (§17.12, §17.19 C8).
- Live session input is still a takeover, never a decision (§8.2).

### 17.27.3 The scope button (and its neighbours)

| Where | Button | What it does |
|---|---|---|
| A tracker item or project in the backlog view, a spec section, or an uploaded brief | **Scope** | Shows a dry-run first (which sources would be read, with counts, and the token budget; no model is called). On confirm it starts the scoping job, shows progress per round with its queries, and opens the map when done |
| A scope map | **Edit** verbs from §17.26.4: add, remove, too much, missing around X, run open queries | The same commands as `sindri packet edit` |
| A scope map | **Create packet** | `packet init --from-scope`, then opens the studio view |
| A packet | **Validate**, **Freeze** (desk), **Amend** | The same commands; freeze shows `validate`'s result and the unconfirmed proposals first |
| A past tracker project | **Backtest** | `sindri scope --backtest`, then the recall and precision report |
| An item in `status` | **Start** (desk), **Hold**, **Re-scope** | The same router commands, behind review back-pressure |

The same pattern applies everywhere else: every view that shows a thing Sindri can act on offers that action.
Examples:
- an evolve proposal offers compare, adopt (protected) and revert;
- a channel build offers promote (protected) and rollback;
- a gate record offers propose loosening (approval tier);
- a receipt offers verify;
- the field registry offers add and remove a source;
- a repo offers onboard and index build.

### 17.27.4 Parity is tracked, so "later" doesn't become "never"

- Each command declares its CLI form and its UI action in a **command registry** (a registered artifact). Until
  the UI action exists, the declaration is `ui: pending` with the rollout step it is due in.
- A test fails when a command has no CLI form, or when a `pending` UI action is past its due step.
- `doctor` lists the pending UI actions. The guide's catalog shows each command's UI status.
- Adding a command without a UI declaration fails the same test. A new verb must at least say when its button
  arrives.

### 17.27.5 Rollout

| Step | Adds |
|---|---|
| Plan 6 | The dashboard reads the command registry and shows each action's status. Its server is built on the command layer, so actions can be added without a second implementation |
| F0 | The command registry and the parity test. Every existing verb is declared, mostly as `ui: pending` |
| F2 | Passkey enrolment on the host dashboard. Desk actions (start, hand back, answer) and read-only jobs |
| F4 | The scope button, map editing and packet validate, freeze and amend in the studio view; the tailnet desk view; protected actions with typed hash confirmation |
| Every F-step | Its "Turn it on" task (§13.3) ships its commands' UI actions, or records `ui: pending` with a due step the parity test enforces |

## 17.28 One workflow state machine

Sindri runs many state machines, and so far each section describes its own:
- the item lifecycle (§7.1, §9.2–9.4): triaged, startable, claimed, running, parked, taken over, done;
- the Step contract (§6): produce, deterministic checks, verifier, revise, with round and budget caps that park;
- the direction check (§6.1), with leg timeouts, a bounded exchange count and a known-wait state;
- the ship and stack machine (§7.3, §17.10), the PR lease (§17.23.11) and the review back-pressure loop
  (§17.23.1);
- standing workflows (§17.9) and the Step graph that workflow generation freezes per deliverable (§17.7);
- the workers' own phases, which today live inside the skills: `/bugFixOrchestrator`'s
  intake → investigate → reproduce → fix → evaluate → report (the C# `bugfix-state`, `docs/csharp/bugfix.md`),
  and `/specToProvenPR`'s plan → verification plan → implement → hand test → prove → draft PR → review loop → ready.

Written separately, each of these would repeat the same machinery: durable state, fencing, idempotent transitions,
timers, parks and escalation, round counters with no-progress detection, evidence that counts only when it is
registered, and the receipt trail. This section defines **one engine and one recipe format**, and every
machine above becomes a recipe the engine runs. The engine is built in Sindri. The skills plug into it later (§17.28.4).

### 17.28.1 The model

A **workflow definition is a recipe**: declarative config (YAML, validated by a JSON Schema in `schemas/`), not
code. It names **building blocks** from the registry: gates, evidence kinds, Step implementations, effects and
`next` templates. Each block is a typed C# module with its own contract, eval suite and telemetry (principle 1).
The engine interprets recipes. So:
- a new workflow, or a change to one, is a config change;
- new code is only needed for a new block, and a block is reused by every recipe that names it;
- recipes can be **generated**: workflow generation (§17.7) emits the recipe for a deliverable, so each
  deliverable's workflow is built at packet time from the blocks its lenses need;
- recipes can be **evolved**: they are registered artifacts, so the evolve loop (§7.4) proposes, A/B tests and adopts
  them like a prompt or a lens.

This is the recipe-and-canvas shape other factory implementations use (for example Superplane's configurable
workflows, §16), applied to Sindri's gates and evidence. A recipe is versioned and hashed. A recipe used by a
deliverable is frozen with the packet (§17.7 step 6) and named in the receipt (§17.23.12). Model-written recipes
can't set capability fields (invariant 13): blocks that grant tools, outward effects or autonomy tiers are named
only by approved recipes, and the validator refuses the rest.

```yaml
id: workflow.bugfix
version: 1
states:
  investigate: { kind: active, step: skill.bugFixOrchestrator.investigate, timeout: 4h, onTimeout: park }
  reproduce:   { kind: active, step: skill.bugFixOrchestrator.reproduce,   timeout: 2h, onTimeout: park }
  fix:         { kind: active, step: skill.bugFixOrchestrator.fix,         loop: attempts, timeout: 6h }
  evaluate:    { kind: active, step: skill.bugFixOrchestrator.evaluate }
  report:      { kind: terminal }
transitions:
  - { from: investigate, to: reproduce, gates: [gate.handoff.diagnosed] }
  - { from: reproduce,   to: fix,       gates: [gate.baseline.fails, gate.check.committed], evidence: [test@base] }
  - { from: fix,         to: evaluate,  gates: [gate.candidate.onBaseline, gate.paths.unprotected], evidence: [test@head] }
  - { from: evaluate,    to: report,    gates: [gate.judge.resolved], effects: [effect.pr.draft] }
  - { from: evaluate,    to: fix,       when: notResolved }
loops: { attempts: { cap: 3 } }
budget: { tokens: 400k }
```

A recipe declares:

| Part | What it says |
|---|---|
| States | Each state's kind: `active` (a Step or session is working), `wait` (a known wait: a human answer, a reviewer, CI, a child workflow, the heavy-job lock; never a stall, §9.2) or `terminal`. Each `wait` and `active` state has a timeout and what the timeout does (escalate, park, retry once) |
| Transitions | `from → to`, the command or event that asks for it, its **gates**, the **evidence** it needs, and its **effects** |
| Gates | Checks that must pass before the transition is applied, each placed on the decision ladder: deterministic code first, then a judge question, then a bounded model call. The rung, score and threshold are recorded with the transition |
| Evidence | The artifact kinds a transition needs (§17.11). Only artifacts registered for this instance, bound to its head SHA and unchanged since, count. Verdicts are computed by script (invariant 3) |
| Effects | Outward actions, emitted as outbox commands in the same transaction as the transition (§17.21). The engine never pushes, comments or merges directly |
| Loops | A named counter per loop (review rounds, fix rounds, revise rounds, direction exchanges) with its cap, and §17.23.13's no-progress rule built in |
| Budget | Tokens and turns per state and per instance (§6). Exhaustion is a transition to `parked`, not an error |
| Next | The text `status`, `resume` and the session inbox show for each state: the one allowed next step, which skills read |

**Every recipe gets the universal states and transitions for free:**
- `any → cancelled` (the human, or the item closed in the tracker) and `any → escalated` (a gate below threshold
  on the top rung, invariant 4);
- `active|wait → parked(reason)`, where the reason is one of `rounds`, `budget`, `no-progress`, `timeout`,
  `needs-answer` or `spec-conflict` (§17.24.6), and `parked → previous state` on the human's answer;
- `any non-terminal → taken-over → previous state` on a human takeover and `handback` (§9.3), with timers paused.

**An instance** is a row: the recipe id and hash, the item, the attempt, the current state, the loop counters,
the budget spent, the registered evidence, and a **version** for optimistic concurrency (§17.19). It also carries
the §17.20 seams: tenant and ring keys.

**A transition is one ledger event** with the §17.21 envelope (`actor`, `causation`, `correlation`, trace). It is
applied only if all of these hold:
- the caller holds the host lease and its epoch is current (fencing, §9.1);
- the instance version matches;
- the gates pass.

Its idempotency key is `(recipe, item, attempt, transition, sha)` (§17.20), so a retried or replayed event is a
no-op. The event, the new instance version and the outbox commands commit in one transaction.

**Timers** are deadlines on the instance row. The tick evaluates due deadlines and applies their transitions like
any other event, so a timeout is replayable and never a hidden side effect. Sleep and clock jumps follow §9.2.

**Composition:** a Step graph (§17.7) or a stack (§17.10) is a parent workflow whose states start child
instances and `wait` on their terminal events. A child's `parked` or `escalated` propagates to the parent as an
event, never as shared state.

### 17.28.2 Recipes and blocks

The engine owns persistence, fencing, idempotency, timers, parks, takeover, loops, budgets, receipts and the
event trail. A **recipe** supplies:
1. its states and transitions;
2. which gates, evidence kinds, Steps and effects each one uses, by block id;
3. its loop caps, timeouts and budget;
4. its `next` texts;
5. its eval suite: replayed event logs with expected states, plus the failure cases (gate refusal, timeout,
   no-progress, takeover) it must handle.

A **block** supplies the code, once, for every recipe that names it:
- gates, as deterministic checks or judge questions;
- evidence providers (§17.11);
- Step implementations: the jobs, sessions and skills that do the work in `active` states;
- effect command kinds.

Each block declares its inputs and outputs, so the validator can check that a recipe wires them correctly.

**Tools versus workflows.** The ported packages are mostly **tools**. In a recipe they are blocks: deterministic
code that a step calls or a gate relies on, which does one job and returns a result. The **workflow** is the
sequence around them: the steps, the gates between them, and what happens on failure. A skill today usually holds
both, and only the second half becomes recipe material:

| Package or skill | Becomes blocks (tools) | Becomes a recipe (workflow) |
|---|---|---|
| `ui-evidence` (`src/Evidence`, R2) | `run` (the `ui-browser` evidence provider), `parity` (`design-parity`), `doctor` and `provenance` (checks), `install-browser` (setup) | The skill's sequence: doctor the stack → plan the journey → run headless → lint → check provenance → publish (ask-first). It becomes `workflow.ui-evidence`, a child that a feature workflow starts |
| `bugfix-state` (`src/BugFix`, #155) | `run-test` (the `test` evidence provider), and the gates: the frozen check, the clean tree with registered leftovers, registered evidence, candidate-on-baseline, protected and shadowing paths, judge once per candidate | The phases intake → investigate → reproduce → fix → evaluate → report, attempt caps, mode B, and the `next` hints. This becomes `workflow.bugfix` |
| `judge` | Each decision question is a rung-2 gate block | None: judge answers questions, it doesn't sequence work |
| `scorer` | A reader of events, receipts and transcripts, for the metrics in §17.2 | None |
| `aw-packets` (`src/Packets`) | `validate` (the `PKT-*` checks) and `freeze` as gate blocks | The packet lifecycle: draft → edit → validate → frozen → amended (§17.4, §17.26.4) |

A tool with no branching that needs an agent or a gate stays a block. Wrapping it in states adds nothing.


| Machine | States (abridged) | Gates and evidence it plugs in |
|---|---|---|
| Item lifecycle | triaged → startable → claimed → running → done | Router table (§7.1), the claim ref (§9.4), budget |
| Step contract | produce → checks → verify → (revise)* → done | Per-Step deterministic checks and verifier (§6 table) |
| Direction check | requested → position → challenge(n) → reply(n) → verdict → delivered | Leg timeouts, `maxExchanges`, the verdict judge question |
| Ship and stack | draft → undrafted → fix rounds → green → re-draft → next level | Reviewer and CI state from adapters, PR lease, receipt for `epicMerge` |
| Standing workflow | fired → working → reported / proposed / acted | Autonomy tier (§17.9) as a gate on effects |
| Bug-fix worker | intake → investigate → reproduce → fix → evaluate → report | Frozen check, clean tree, registered runs, judge resolution-check once per candidate (the rules in `docs/csharp/bugfix.md`) |
| Feature worker | plan → evidence plan → implement → prove → review loop → ready | Plan before code, evidence plan satisfied, review rounds (§17.11), ship contract |

### 17.28.3 Placement and ports

- The engine lives in Sindri's C# package. The recipe model, the validator and every pure decision (is this transition allowed,
  which gates apply, what a timeout does) are Core types. *Amended 2026-10-11 (Q8):* instance persistence, replay,
  timers, signals and child workflows are **Temporal's** (engine spec §0, §5), reached through the adapter project
  `Aw.Sindri.Workflows`; every transition is mirrored into the ledger through the `LedgerStore` port, so the §17.21
  event log and the receipts are unchanged. Leases, the launcher and the scheduler stay the existing ports.
- It is not a separate tier-1 package yet. Per the C# rules, a port moves to Kernel or its own package when a
  second package needs it. If a package outside Sindri needs to run recipes, the engine is extracted then,
  with its contract tests.
- **Threat model:** gates guard against an agent's honest mistakes and shortcuts. Containing a deliberately
  adversarial agent is the job of the sandbox and the safety boundary (§8), not of workflow gates. This is the
  same line `docs/csharp/bugfix.md` draws for `bugfix-state`.

### 17.28.4 The existing skills stay as they are until it can host them

- `/specToProvenPR` is **not** ported to any shared machine now. It keeps its own stages and files.
- `/bugFixOrchestrator` keeps the C# `bugfix-state` (merged in #155) as its standalone helper.
- In Sindri mode both run as Step implementations through the write shim (§11.4), and Sindri reconciles their own
  state read-only, as today.
- **Once the engine runs Sindri's own machines** (item lifecycle, Step contract, direction check, ship), the
  bug-fix and feature workflows are written as recipes. The skills then become Step implementations the engine drives:
  they stop owning phase state, and their gates become blocks the recipe names.
- `bugfix-state` is the reference for that port. Its generic parts map one to one onto the engine: the locked
  state file maps to the instance row, registered run records to registered evidence, the frozen check and clean
  tree to gates, and judge-once to a gate whose question is never re-asked.
- Desk (non-Sindri) use of both skills keeps working throughout. The interactive mode is not removed by the port.

### 17.28.5 Testing

- **Recipe validation (deterministic, at registration and before a generated recipe is frozen):**
  - the recipe matches the JSON Schema, and every block id it names is registered;
  - every block's inputs are produced upstream;
  - capability-bearing blocks appear only in approved recipes;
  - every state is reachable;
  - every non-terminal state has a way out;
  - every `wait` and `active` state has a timeout;
  - every loop has a cap;
  - effects only use allowlisted command kinds;
  - the recipe hash matches its content.
- **Engine properties:**
  - replaying an event log reproduces the same instance;
  - applying any event twice is a no-op;
  - a stale lease epoch or instance version is refused;
  - a timer firing during a takeover does nothing;
  - parks and escalations propagate from child to parent.
- **Store contract:** one suite for SQLite and the later server store (§17.20).
- **Per recipe:** its eval suite (§17.28.2, item 5) runs in the evolve loop like any artifact (§7.4), so a
  changed recipe is A/B tested and adopted like a prompt or a lens.
- **Per block:** its own contract tests, run once for every recipe that uses it.

### 17.28.6 Rollout

1. Right after the Sindri app shell (§17.29.4 step 1): build the engine, the recipe schema and validator, and the block
   registry. Move the item
   lifecycle and the Step contract onto it first, as recipes. They are the machines every later one runs inside.
2. Then the direction check, which exercises timers, bounded loops and the known-wait state.
3. Then ship and stack, which exercise child workflows, the PR lease and receipts.
4. Then the worker workflows as recipes, bug fix first (its gates become blocks lifted from `bugfix-state`'s rules)
   and feature second. This is where
   `/bugFixOrchestrator` and `/specToProvenPR` become Step implementations. Until then they are unchanged.
5. Standing workflows join as recipes as they are built (§17.9).
6. Workflow generation (§17.7) switches from emitting a Step graph to emitting a recipe, so each deliverable's
   workflow is generated from blocks and validated before it is frozen.

### 17.28.7 Port order

Superseded by §17.29.4 (owner, 2026-10-11): Sindri isn't ported from TS module by module; it is built as a new C#
application, engine first. The tools stay tools: they become blocks when a recipe needs them (§17.28.2).

## 17.29 Sindri as an application

Sindri is one application the human starts on a machine, not a set of launchd-ticked commands. It provides three
engines: a **control plane**, a **workflow engine** (§17.28) and a **context retrieval engine**. It does **not**
replace the agent tool. Agent work, the human's and the factory's, happens in Claude Code sessions in the human's
terminal (Warp today). Sindri starts those sessions, feeds them context and tracks their work, and the human
interacts with them where they already work.

### 17.29.1 Starting it

```
$ sindri
  <banner>
  profile   ~/…/sindri/profile.yaml (approved, hash 3f1c…)
  index     4 repos in path · watching · nightly full build 02:00
  intake    linear: team ENG (assigned to me) · notes: ~/notes · every 5 min
  services  scorer daily 06:00 · evolve weekly Mon · pr-babysitter on webhook
  api       127.0.0.1:7410 (tailnet: off)
  ui        Sindri.app started
  ready     3 items need you · 2 running · 1 parked
```

- `sindri` starts the control-plane process (one per host, under the host lease, §9.1 and §17.22 D2) and the UI app
  as a client. `sindri --headless` starts it without the UI, for the always-on box. Every other `sindri <verb>`
  is a client call to the running process, and starts it first if it isn't running.
- **Config is the profile** (§11.1): which repos are in the path, which intake sources, cadences, budgets, modes
  and lenses. Changing it goes through `profile approve`, as today.
- **Background services** run on the profile's cadence inside the process:
  - code index: watch the repos in the path, quick builds on change, a nightly full build (§6.2);
  - intake: poll the configured sources (Linear by assignee, PRDs, the notes dir) and create items (§17.26.1);
  - scorer and metrics on their cadence (§17.2);
  - the evolve loop's weekly job (§7.4);
  - webhook listeners that start standing workflows (§17.9).

  These are the old launchd jobs, now scheduled by the process; launchd only keeps the process alive.

### 17.29.2 The three engines

**Control plane.** The ledger, the command layer (§17.27.1), the scheduler and lease, the outbox, notifications,
the API (HTTP plus an event stream, §17.21) and the webhook endpoints. Every surface is a client: the CLI, the UI
app (§17.17 Q4), the phone, and Claude Code sessions (below).

**Workflow engine** (§17.28). Every piece of work is a workflow instance run from a recipe. The factory line is a
chain of recipes, each starting the next:

| Workflow | What it does | Builds on |
|---|---|---|
| Intake | A Linear ticket or project, a PRD or a notes entry becomes an item | §7.1, §17.26.1 |
| Design studio | A desk session with the human, fed by retrieved context, that settles the open questions | §17.4 |
| Scoping | The retrieval engine and blocks map the work: surfaces, workstreams, candidate files | §7.5, §17.26 |
| Design | An exhaustive engineering design: TDD/PRD, product map and codebase scoping, implementation guide, task breakdown; frozen as the packet | §17.4, §17.7 |
| Builder | Implements each task | §6 worker, §17.7 |
| Tester | Proves each task: acceptance examples, evidence providers | §17.11, §17.23.2 |
| Multi-lens validation | The packet's lenses review the change (security, data, UX, …) | §17.7 |
| Verification | The human reviews and decides, from any Sindri surface | §17.12, §17.23.7, §17.27 |
| Shipper | Stacked PRs, receipts, the ship contract | §7.3, §17.10, §17.23.12 |
| PR babysitter | CI and review fix rounds until green | §17.9, §17.23.11 |
| Triage, incident response and comms | Started by webhooks (alerts, errors, new tickets) | §17.9 |

**Context retrieval engine.** The context graph and its query planner (§17.3): business tools and trackers, PRDs and
notes, memory, the code index and the product map, all behind source adapters, scrubbed and cited. Workflows query it
through blocks, and the human's own sessions query it through the CLI and MCP (below).

### 17.29.3 Agents stay in the human's tool

- A workflow state that needs an agent launches a **Claude Code, Codex or Cursor session** (in its container for
  factory work, §8.2) **inside a tmux session Sindri owns** (Q10, 2026-10-11), and records it. The human sees it like
  any other session and attaches through the existing attach resolver (a Warp tab running `tmux attach`). Sindri has no
  chat surface of its own.
- **Interject at any point:**
  - On an item with a live session: attach in Warp and type. That is a takeover (§9.3): the instance pauses,
    with its timers stopped, until `sindri handback`.
  - On an item with no live session (between steps, at a gate, parked): `sindri open <item>`, or "open in Warp" in
    the UI, starts a Claude Code session in the human's terminal, seeded with the item's context: its state, packet,
    evidence, history and retrieved context. The instance is taken over until handback.
  - What the human changes there flows back through the normal paths: packet edits as amendments (§17.4,
    §17.26.4), instructions as recorded inputs the next Step receives, and decisions only through authenticated
    verbs and UI forms. Text typed into a session is never a decision (§8.2 W6).
- **The human's own Claude Code sessions reach Sindri** through the `sindri` CLI and a **Sindri MCP server** (the
  MCP C# SDK, approved in the readiness AOT spike). From any session: an item's state and context, a context
  retrieval query, or a request ("re-scope this", "add a task"). Calls from a session are requests, never
  decisions; approvals stay human actions.
- **The UI app is a window onto the control plane:** the queue, status, decisions, receipts, metrics and "open in
  Warp". It is not a chat surface.

### 17.29.4 Build order

Sindri is built as a new C# application, not ported module by module from the TS `sindri/`. The TS code is a
reference for what each part does, and it keeps running at the desk until the switch.
1. **App shell:** the `sindri` process and CLI, profile loading, the ledger (`LedgerStore`), the host lease, the
   command layer and API, the scheduler for background services, and the UI app shell.
2. **Workflow engine and recipe format** (§17.28.6 step 1).
3. **Context retrieval engine:** source adapters and the query planner over the sources already reachable (§17.17
   Q2's recommendation).
4. **Blocks, each when the first recipe needs it:** judge (#156) and evidence (#158) are wrapped as blocks; the code
   index, scorer, scoping and evolve are rebuilt as blocks or workflows from their TS reference.
5. **Workflows down the factory line,** intake first, then design studio and scoping, then design, builder, tester,
   validation, verification, shipper and PR babysitter; webhook-started workflows as their sources are wired.
6. **The switch** (readiness §4a step 3) once every desk tool has its C# form.

Each step has its own design spec, listed with its stage in `docs/sindri/README.md` ("Design specs for the
application"): the API surface and the UI app design for step 1, the workflow engine for step 2, and the context
retrieval engine for step 3. That README also keeps the cross-spec reconciliation list (X1–X42) and the one build order.

## 17.17 Decisions

**Decided by the human (2026-10-11, after the design walkthrough):**
- **Q8: Temporal is the workflow engine's substrate.** Sindri does not implement durable execution; one Sindri workflow
  type interprets every recipe on a local Temporal server (`temporal server start-dev`, loopback, its own SQLite file
  under `$AW_SINDRI_HOME`) that `sindri serve` starts and launchd keeps alive. Recipes, the validator, approval and
  invariant 13, callers, gates and the ledger as the public log stay Sindri's. Engine P0 is a three-day spike with
  numbered pass lines; a failed spike returns the hand-built §5 of engine rev 7, cut down. Engine spec §0 and §5;
  README X53, X57, X58.
- **Q9: NativeAOT is relaxed for the Sindri host.** The Temporal .NET SDK has no trimming support, so the host (and its
  worker) publishes framework-dependent; the adapter is its own project opting out of `IsAotCompatible`; every other
  library and the Avalonia app stay AOT. README X54; csharp.md "AOT scope".
- **Q10: tmux owns every agent session; Warp attaches.** The human can join the live agent at any Step and the first
  typed turn is a takeover (§9.3). Non-interactive agent runs are never used for Steps. §17.29.3; API §8.1; README X55,
  X56.

**Decided by the human (2026-10-11):**
- **Q7:** Sindri is built as a new C# application, not ported (§17.29). It provides a control plane, the workflow
  engine and a context retrieval engine, and it doesn't replace the agent tool: agent work stays in Claude Code in the
  human's terminal (Warp), with interjection at any point by attach or `sindri open` (§17.29.3). Build order:
  app shell → engine → context retrieval → blocks as recipes need them → workflows (§17.29.4). The finished tool ports
  (`Redaction`, `Packets`, `Readiness`, `BugFix`, and `Judge` #156 and `Evidence` #158 once they land) become blocks;
  no further standalone ports are started.
- **Q6:** one workflow state machine, built in Sindri (§17.28). Every Sindri machine is a declarative recipe (config,
  not code) interpreted by one engine and built from registered blocks. Recipes can be generated per deliverable
  and evolved like other artifacts.
  `/specToProvenPR` is not ported to a shared machine now, and `bugfix-state` stays standalone. The worker skills move
  onto the engine once Sindri runs its own machines on it (§17.28.4, §17.28.6).

**Decided by the human (2026-10-09):**
- **Q1:** stacked PRs into epic branches, with `epicMerge: auto` into the epic only (§17.10).
- **Q3:** the factory host is swappable between the laptop and the Linux box; neither is the default (§17.13).
- **Q5:** ring 1 (workplace repos) comes first, to test and improve the factory. Ring 2 starts after F3 holds on
  ring 1. Ring-1 runs follow the workplace's agent policy: human-gated, `assist` mode, and work accounts only.

**Q2: component options for the context graph.** These are candidates to evaluate as backtest arms, not
decisions. None was re-verified for this revision.

| Component | Candidates |
|---|---|
| Connectors | MCP servers (read-only client); unified-API connector platforms; per-source native adapters (as Linear today); the workplace knowledge engine's connectors (ConnectAI) |
| Memory and consolidation ("dreaming") | Prism (personal only); memory-layer libraries with consolidation passes (for example Mem0, Zep/Graphiti, Letta, Cognee) |
| Retrieval and storage | SQLite FTS5 + sqlite-vec (local, matches the ledger); LanceDB; Postgres + pgvector (for a shared box) |
| Org knowledge (conversations, decisions) | ConnectAI (workplace source of record today); commercial engines with MCP read surfaces (for example Unblocked, Glean, Falconer); notes vault |
| Codebase | The existing index + graphify; Serena (LSP); hosted code engines as an arm only if the profile allows code to leave the machine |
| Product graph | Sindri-owned (no external candidate joins product nodes to code) |
| User behavior | Product analytics APIs (for example PostHog), browser session-replay data, OTel browser spans |
| Telemetry and operations | OTel backends, error trackers, deploy logs, through `TelemetrySource` adapters |
| Design | Design-file APIs, `design-tokens.json`, design-system component inventories |

The recommendation is to build the context graph, query planner and evals first over the sources already
reachable (code index, graphify, Linear, notes, ConnectAI through MCP). Then add one component per arm and keep
it only when the backtest says it helps.

**Q4: the review surface.** Pros and cons:

| Option | Pros | Cons |
|---|---|---|
| **PWA on the tailnet** (the Plan 6 dashboard, made mobile) | One codebase with the dashboard. No app store. Tailnet-only by construction. WebAuthn passkeys work in mobile Safari and Chrome. Ships fastest | Web push on iOS needs the PWA installed to the home screen, and is less reliable than native. No background refresh. Swipe gestures need care to feel native |
| **Native iOS app** | Best swipe feel, reliable push, Face ID, offline queue of decisions | A second codebase, App Store or TestFlight distribution, and the tailnet still has to be on. Slowest to build |
| **Warp remote control only** | Exists today. Full fidelity on live sessions | A terminal on a phone: no structured decisions, and in-pane input is a takeover, not an approval (§8.2). Fine for watching, poor for reviewing |
| **Chat bot** (a DM channel) | Push and replies come free, and it's familiar | Decisions leave the tailnet through a third party. Weak authentication for signed decisions. Rich walkthroughs don't fit |
| **GitHub mobile** | Already installed. Good for reading PR diffs and comments | It sees only GitHub objects: no packets, rules, lenses or evidence roll-ups, and no Sindri decisions |

~~The recommendation is the PWA first, with Warp remote control for live sessions. Build native only if push
reliability or swipe feel measurably slows decisions (§17.2 decision latency).~~

**Decided (owner, 2026-10-10): the Avalonia app** (readiness spec §4a "UI"). The dashboard, the §17.27 action forms
and the swipe inbox are one [Avalonia UI](https://avaloniaui.net/) app in C#, so the UI is cross-platform from the
start and moves to native phone apps by adding iOS and Android heads over the same views, not by rewriting a web
app. This takes the native column's strengths (swipe feel, reliable push, Face ID, an offline decision queue) without
a second codebase, at the cost of app distribution (TestFlight or sideloading) once the mobile heads exist. Desktop
comes first; Warp remote control stays the tool for live sessions. Open for the mobile step:
- passkeys come from each platform's API (ASAuthorization on iOS and macOS, Credential Manager on Android, Windows
  Hello), behind one `IPasskeys` port with an adapter per head, instead of WebAuthn in a browser;
- push to the phone over the tailnet (ntfy, or APNs and FCM through a relay) is picked then.
