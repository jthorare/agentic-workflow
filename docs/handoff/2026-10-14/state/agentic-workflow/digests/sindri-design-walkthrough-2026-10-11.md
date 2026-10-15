# Sindri design walkthrough (2026-10-11)

Read: docs/sindri/README.md (epic, X1–X52, wave table), core design (2026-10-07) §1–§16, factory addendum §17.2, §17.3, §17.15, §17.17, §17.20, §17.21, §17.28, §17.29, workflow engine spec rev 7 (all), context retrieval spec (all), API surface spec (all), UI spec §2, §6. Open PRs: #166 #167 #168 #169 (reference only, never merged per X17), #180 (WF P3), #181 (API P1). ~40 feat/sindri-* branches. src/ has Kernel, Redaction, Packets, Readiness, BugFix, Evidence; no Sindri project on main yet.

## Owner's four goals (their words, paraphrased)
1. Control plane: watch agents go scoping → design → impl → test → ship, cheaply; jump into any session/step in Warp.
2. Recipe chaining for complex workflows, cheaper and better than Claude Code Workflows.
3. Context retrieval across multiple lenses so a software factory is viable in a business.
4. A multiplayer factory to build other things.

## Findings (ranked)
H1. No written buy/build decision for the durable-execution substrate (Temporal .NET, Durable Task, Dapr Workflow, MS Agent Framework workflows, Elsa). Context spec has one for Onyx; engine spec has none. Engine spec hand-builds fencing, idempotency, timers+sleep shift, child propagation, nonces, pending parks, drift pins.
H2. Safety boundary (core §8.2 containers, model proxy, egress proxy, spike §13.1) has no phase in API/WF/CTX/UI. Engine P8 launches an unsandboxed `process` target in `<repo>/.claude/worktrees` on the host while every threat-model paragraph assumes the sandbox is the boundary.
H3. Substrate before value: waves 0–8 produce API+UI+engine(desk Steps)+context over docs/Onyx/Linear; factory-line recipes are build step 5 with no spec; agent runtime is P8 (wave 7). No end-to-end automated item until then.
H4. Cost-effectiveness asserted, not measured: usage read only on session.ended (P8/P10); metrics/today "later"; model proxy/quota ledger unphased; no benchmark vs Workflows or /specToProvenPR.
M5. Recipe schema v1 is sequential (no fan-out until v2); §17.7 Step graph, bugfix mode B, stacks need it.
M6. Recipe approval friction (version bump + evals present + TTY challenge; descriptor drift parks every instance).
M7. Attach = focus Warp tab only; no attach for `process` targets; "interact at any step" means open a new seeded session for headless work.
M8. 3 of 6 context dimensions (intent, product, code); behavior/telemetry/design have no adapter; §17.7 lenses have no spec.
M9. Product graph exempt from its own kill criterion ("never gated off").
M10. Resource footprint (Onyx CE compose + Ollama + graphify + Sindri + Avalonia + sessions) unaddressed.
M11. Spec sprawl: ~10k lines, 52 reconciliation rows, specs overriding each other; only the README assembles truth.
L12. Core design §5–§10 still carries superseded tick/tmux/web-dashboard text without inline markers.

## LangChain answer in one line
Not reinventing LangChain (C# stack, agent delegated to Claude Code/Codex/Cursor). Real overlap risk is the workflow engine vs Temporal/DTFx/Dapr/Agent-Framework and the evolve/eval harness vs LangSmith/Promptfoo. Context engine's novel part (product graph + join) has no commodity equivalent; its retrieval plumbing (RRF, packing) is small.

## Decisions and actions after the walkthrough (2026-10-11, same session)
- Owner: Temporal is the engine substrate (Q8); Sindri host need not be AOT (Q9); tmux owns agent sessions, Warp attaches (Q10).
- PR #182 merged to main as 57f28c80: engine spec rev 8 (§0 decision + mapping, §5 rewritten on Temporal, W28–W33, §7/§10/§11 amended, Phases rewritten with P0 = 3-day Temporal spike, 5 numbered pass lines, gates P4; P3 #180 shrinks to pure decisions + evals). Folded in the engine lane's rev 7.2 (PR #180 review decisions). README X53–X59 (X59 = follow-up specs: sandbox phase, standing workflows, collector, interjections→recipes). API §8.1 (one tmux-owned target kind; attach joins live session). UI spec rev 7 note (§6.2, §6.6). Addendum §17.17 Q8–Q10, §17.28.3, §17.29.3. csharp.md AOT scope. AGENTS.md tech stack.
- Messaged the 4 peer sessions (agentic-workflow-26/9a/30/95) with per-lane instructions; engine lane must rebase #180 and drop its spec commit.
- CI on main failing in ~2 s since #175 (predates #182): GitHub annotation says "The job was not started because recent account payments have failed or your spending limit needs to be increased" (Billing & plans). Owner action; nothing in the code.
- Next: engine P0 spike (someone claims it), API P1 (#181) unaffected, UI U0 unaffected.
- #183 (034db775) merged: last two-target launch references removed (API §1 summary + P10a row, README X41/X52, engine §6.5), raised by the API lane.
- Lane status: API lane merged main into #181, P1 unaffected. UI lane: U0 unchanged, will read rev 7 §6.2/§6.6 before U6a/U6b. Engine lane: #180 reworked to rev 8 (units b/e cut, address-based ids, SND-WF-002 retired); P0 Temporal spike started on feat/sindri-wf-p0-temporal-spike with the verification plan first.
