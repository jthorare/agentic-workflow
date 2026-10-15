# Sindri workflow engine design — brainstorm digest (2026-10-10)

Task: design doc only → docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md, then /autoplan, docs-only draft PR, STOP for approval. Never edit/commit/stash in .claude/worktrees/sindri-s1-app-shell.

Settled:
- Q1 block contract (approved): kind interface (IGate/IEvidenceProvider/IStep/IEffectKind) + static data-only BlockDescriptor
  (id, kind, version, inputs/outputs as typed fact keys, capability flags Tools|Effects|AutonomyTier|WriteScope|Network|Budget,
  gate rung 1/2/3 + threshold-applies). Explicit registration into BlockCatalog (descriptors only → pure Core validator);
  separate runtime id→impl map. Wiring: every input produced on EVERY path (or recipe `inputs:`).

Open: Q2 recipe schema (proposed: schema/id/version/inputs/states{kind,step,waitFor,loop,timeout{after,then:park|escalate|retry},budget,next}/
transitions{from,to,on,gates[{use,threshold}],evidence,effects,when}/loops{cap,noProgress[findings,emptyDiff,sameFailure]}/budget/hash;
closed structured `when` grammar; rung from descriptor; first-match in declared order). User asked for comparison with other factories
(loki-mode, addyosmani/factory, planwright, robi42/factory, superplane) — all define phases in code/prompts, not declarative recipes.
Q3 approval/invariant 13, Q4 instance row & transition event, Q5 Core vs S1 ports + deps, Q6 first recipes — still to ask.

S1 gaps found: ILedgerStore.Append owns its own transaction; Kernel IOutbox.Enqueue separate (SqliteOutbox can join a SqliteUnitOfWork);
no single-transaction (event + instance row + outbox) port; no outbox dispatcher, no launcher, no deadline tick (scheduler = cadence services);
IApprovals is profile-only. S1 types: ILedgerStore(Append(entry, grant), ReadAfter, Latest, Schema), LedgerEntry(Envelope, IdempotencyKey, Item),
ILease/LeaseGrant(Holder, Epoch, ExpiresAt), LeaseDecision.Holds, IScheduledService/ScheduledRun(Grant, Slot), SindriErrors SND-*.

Approved since: Q2 schema v1 full reference (fields per state kind, waitFor event/answer/resource/child, timeout then park|escalate|retry,
on: step.completed|step.failed|wait.satisfied|command.<verb>, closed when grammar, evidence {kind,at,expect}, produces, loops grant:n,
terminal outcome resolved|unresolved|alreadySatisfied|failed, next vars, fail-closed, out of v1: forEach fan-out, per-transition timeouts, expr lang).
Q3: approval in IRecipeApprovals store (id,version,hash,approver,time), never in file; hand-written via `sindri recipe approve` TTY;
generated approved by packet freeze + base: id@hash; CAP-001 subset of base caps, CAP-002 cap with: params equal base (no narrowing), CAP-003 unapproved refused;
re-check at start + effect dispatch; revoke → park spec-conflict; evolve recipe changes approval-tier.
Q4: instance row fields; workflow.transition / workflow.refused events; key wf:{instance}:{transition}:{sha}:{fromVersion};
IWorkflowStore.Commit one tx (lease Holds, version CAS, ledger insert, row, deadlines, outbox) → Applied|AlreadyApplied|VersionConflict|StaleEpoch;
workflow.timers scheduled service 15s; sleep gap shifts deadlines (event); takeover pauses (remaining), park stops & fresh timeout;
child→parent via engine inbox table in same tx, separate parent commit.
Packets precedent: JSON Schemas generated from C# records, test fails if stale (schemas/packets/).

Doc written + committed on branch docs/sindri-workflow-engine-design (docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md).
Q5: stacked on S1 branch. Q6: workflow.item + workflow.step approved (+ resource: slot, recipes/sindri/, fail-closed Steps).
Next: /autoplan on the doc, fix blocking, answer rest in §11, draft PR, STOP for approval.

Added: 'Parallel execution with an agent team' section under Phases (graph P1→{P2,P3}→P4→{P5,P6}→P7; P6 needs P2; units, hotspots, roles). Committed 211d1d0. Reviews running: arch, devex, security; product + design done (NEEDS_WORK).
Reviews done: product, design, devex, security (all NEEDS_WORK, no CRITICAL); arch pending. Files in ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-workflow-engine/.
Planned fixes: desk Step runtime until ILauncher (fixes product HIGH + design F5); universal-state next/outcomes (cancelled outcome; escalated is a human wait);
answer flow + status layout + approve preview; event descriptors with outcomes (waitFor event outcomes); ValidationContext data;
CLI process model (validate/eval in-process, rest via serve; profile must name workflow.* services); instance address <item>/<recipe>/<attempt>[/child];
block descriptors pinned in approval (descriptor hashes) SND-RECIPE-006; errors in docs/csharp/sindri.md not docs/sindri/errors.md;
engine phases own S1-D2/D3(dispatcher only; outbox_commands exists)/D5/D6; S1 exit scheme 0/1/2; security: RCP-CAP-005 gates/evidence/thresholds superset,
CAP-006 budgets/caps ≤ base, CAP-007 capability params literal/profile only, base picked by code; trigger sources typed, grant needs human TTY;
server-issued challenge for approve; revoke cascades to generated; child by hash + acyclic + depth cap; sleep detection via monotonic-vs-wall;
YAML duplicate keys/anchors/merge refused; Commit checks key before version; registry events in ledger; scrub gate reasons/inputs.
NOTE: shell cwd sometimes lands in S1 worktree — always cd to main repo; never edit there.

Rev 2 written + committed (resolves C1–C32; consolidated-review.md written). User: re-review max 3 total passes (pass 1 done). Next: pass 2 /autoplan --slim → outputs *-r2.md.
Pass 2 results: design (2 HIGH: escalated entries, budget park), product (1 HIGH desk timeouts/noise), security (1 HIGH actor derivation), devex (6 HIGH), arch pending.
Verified S1: Output.Error exit 1 not-running / 2 else, line "CODE message (fix: …)"; migrations gapless, outbox appended as own.Count+1 (adding Sindri migrations shifts it → S1-D9 pin outbox at 3, P2).
Rev-3 plan: checker verbs (validate/eval) 0/1/2 like aw-packets, others S1 Output; no reserved migration ranges (sequential, renumber at rebase); P4 needs P2; eval files in P1; item recipe: child outcomes only resolved/cancelled; item.routed declares fact:stepSpec; profile keys workflow.effects (P6) workflow.route (P7), validate reports skipped; units append to PublicAPI/own errors partial file/group table; P2 challenge new code only, profile-approve flag flagged to S1; child address step.<entry>; start picks latest approved; --nonce/--run options; dispatch 10s delay for revoke signal; notify.desk → notifications.jsonl; desk: deskCapable descriptor flag, desk timeout floor 2d then park, desk notifications non-interrupting digest, needs-me sections; eval --scaffold; escalated rows gate/timeout/child; budget grant --grant-tokens (terminal, capped); answer text → fact:answer; question field; actor derivation table (harness env markers → session; terminal needs TTY + challenge); accept overrides only escalated gate; complete needs --run token & declared outputs, via: desk; CAP-005 by path, loops by state, missing budget = violation; revoke parks running instances immediately via inbox; ledger writer redacts whole payload; arch test all terminal routes; MCP requests = workflow.request event.
Rev3 Write was refused (needs Read first); re-issuing.
Rev 3 committed. Next: pass 3 (FINAL) → *-r3.md, then fix blocking + answer rest, PR draft, STOP.
DONE: draft PR #165 (branch docs/sindri-workflow-engine, clean, worktree in scratchpad wf-engine-pr). Shared branch docs/sindri-workflow-engine-design also has another session's API-surface commits and was pushed. Waiting for owner approval.
Pass4: product PASS, security PASS, design 1 HIGH (J1 reevaluate key), devex 2 HIGH (shared serve/home; notify.delivered early), arch 3 HIGH (H1 transition resume, H2 child row pops mirror, H3 record only decisions). Rev5 edits go to worktree scratchpad/wf-engine-pr (PR #165 branch docs/sindri-workflow-engine). Then pass 5 (final).
Rev6 pushed (485a0ff2), PR #165 body updated. 5 passes done. STOP for approval.
