# Sindri API surface design — brainstorm digest (2026-10-10)

Task: design Sindri's API + command-layer surface (CLI, Avalonia UI, MCP server, webhooks) → spec
`docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md`, /autoplan it, docs-only draft PR, STOP for approval.
No code. S1 worktree `.claude/worktrees/sindri-s1-app-shell` is READ-ONLY (never cd there to work).

Path: architectural (superpowers:brainstorming). 8 questions, one at a time, each with a recommendation.

## S1 facts (fixed; design around them)
- Routes under MapGroup("/sindri"); GetStatus maps "/sindri/status" inside the group → `/sindri/sindri/status` (S1 bug, its digest notes it).
- HttpResults: ErrorKind → 400/404/409/429/503/504/403/500, ProblemDetails title = SND code.
- LoopbackGuard: non-loopback Host → 400; non-loopback Origin → 403 (browser Origin from loopback allowed!).
- control.json {url,pid,epoch} via IEndpointFile; client 2 s timeout, epoch mismatch = not running (SND-API-001); start spawns `sindri serve` detached, waits ≤10 s (SND-CLI-002).
- S1 plan: "no API token in S1, loopback guard only"; ApproveProfile takes `confirmed: true` set by the CLI after a TTY prompt → any loopback caller could approve. CONFLICT to flag.
- StreamEvents: ILedgerStore.ReadAfter(seq,100) → SSE (TypedResults.ServerSentEvents) with 1 s poll.
- Ledger: LedgerEvent(Sequence, Epoch, EventEnvelope<string>, IdempotencyKey, Item). Errors areas LEASE/PROFILE/LEDGER/API/CLI/SCHED.
- Port default 7410 (ApiSettings), AW_SINDRI_PORT; state AW_SINDRI_HOME default $AW_STATE_DIR/sindri-app.
- S1 plan file: ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-app/s1-plan.md

## Spec anchors
- §17.27.1 one command layer; §17.27.2 action classes table (read / read-only jobs / non-protected decisions / desk actions / protected); §17.27.4 command registry + parity test.
- §17.21 envelope fields id,type,version,tenant,ring,actor,item,causation,correlation,trace,time; API row: approval-tier stays terminal-bound, token alone never decides (D6).
- §17.29.3: MCP/session calls are requests, never decisions. `sindri open`, `handback` (§9.3 takeover).
- §17.12 tailnet + passkey; phone limited to non-protected binary decisions.
- Dashboard spec §6 (amended): per-restart bearer token file 0600, refuse any Origin header, JSON only.
- Memory: threat model non-adversarial (guard honest mistakes).

## Decisions so far
(none yet)
- Q1 (approved): CQRS. ICommand<TResult>/IQuery<TView> markers over IService; commands append ledger events, return CommandAccepted{commandId, sequence}; command decorator chain validate→authorize(class)→fence+dedupe(Idempotency-Key)→audit. Queries read projections only (read-only conn; arch test: no write ports). In-process async projector over ReadAfter(cursor), rebuildable by replay. Read-your-writes: X-Sindri-After: <seq>, wait ≤2 s else 503. GET→IQuery, POST→ICommand (arch test). Resources: status, queue(projection), items(+timeline), instances, decisions, jobs, receipts, metrics, services, profile, lease, commands(registry). Inline projection = recorded later option (needs S1 UoW around Append).
- Q2 (approved): action classes request|job|trigger|decision|desk|protected on every ICommand; caller classes human|session|webhook|service (from token, never body; service in-process only). Session: queries + request only (FileRequest → request.filed in queue; RequestHandback). Jobs human/service only. Refusal 403 SND-AUTH-002 with CLI form. Protected carries confirmHash checked vs content hash. Queries open to all but webhook. Registry lists class + callers.
- Q3 (approved): SSE GET /events?after=&item=&types=; id=ledger sequence, event=type, data=public envelope (scrubbed payload, ids/states only). Clients re-query projections; UI never folds events. job.progress in ledger. Back-pressure: bounded 1000/conn, `event: reset {head}` + close on overflow/retention; ping 15 s; ≤16 streams (429). ILedgerSignal later (poll 1 s fallback). Purpose: UI live refresh/badge, CLI job progress + cancel, optional `status --follow`, later notifier/phone/instances. Not truth, not engine input.
- Q4 (approved, v0 simplified by owner: "overkill for v0"): NO tokens/passkeys/tailnet/guard hook/Origin tightening in v0 → "Later" section only. v0 caller class from header X-Sindri-Caller: session (MCP always; CLI when CLAUDECODE/AW_SINDRI_CHILD/codex/cursor env); absent = human. Q2 authorize table still enforced. Protected keeps S1 TTY hash confirm.
- Q5 (approved): adopt S1 control.json/spawn/epoch. Auto-start: bare sindri/--headless start; commands+queries start (--no-start → SND-API-001); status/stop never (status → not running exit 1; stop no-op 0); doctor local; `sindri mcp` never starts, returns SND-API-001 telling human to run sindri.
- Q6 (approved): `sindri mcp` stdio subcommand, MCP C# SDK, thin client, X-Sindri-Caller: session. Tools: sindri_status, sindri_queue, sindri_item, sindri_item_timeline, sindri_pending_decisions, sindri_job, sindri_request(kind rescope|add-task|note|handback, itemId?, text → POST /requests FileRequest), sindri_context (with S3). New `requests` resource (POST /requests, GET /requests?state=open); replaces RequestHandback. No decision/desk/protected/job tools. PROOF = real Claude Code SUBAGENT (owner): `claude mcp add --scope local` for verification session only; subagent reads queue/item, files rescope (appears needs-you), cannot decide; curl w/ session header → SND-AUTH-002. HTTP endpoints keep WebApplicationFactory tests.
- Q7: OWNER CUT webhooks for now → Later section only (no endpoint, no trigger class phase).
- Q8 (approved): S1 HttpResults unchanged; CLI prints code+msg+catalogue fix, exits 0/1/2. New codes SND-AUTH-002, SND-API-003/004/005, SND-ITEM-404/409, SND-REQ-001, SND-JOB-404. No path version v0; apiVersion in /status + Sindri-Api-Version header; v2 group later. Per-type event version. One SindriJsonContext; --json prints body verbatim (queue --json == curl). CommandRegistry code table in Features, GET /commands, parity unit test.
- NEXT: present design sections + phases, get approval, write spec, self-review, commit on branch docs/sindri-api-surface, /autoplan, fix blocking, draft PR, STOP.
- Spec written + committed on branch docs/sindri-api-surface; parallel-delivery section added (owner asked: agent team via /specToProvenPR). Next: /autoplan.
- autoplan running (5 lenses → $AW_DIR/plans/sindri-api-surface/). product NEEDS_WORK (HIGH: P7/P9 duplicate engine's park/takeover/handback — engine spec docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md, untracked, from sibling session; it wants ledger read endpoint S1-D6). design BLOCKED (needs-you must be own GET /needs-you any-source; 4 dashboard views unscoped; asOf freshness; since not age; per-row actions; cli-only registry status).
- S1 moved: routes now /sindri/status, /ping, /stop, /profile/approve, /events (C1 fixed; no POST /profile/validate — ValidateProfile in CLI). S1 took SND-API-003 (port taken) → shift my API codes to 010+.
- consolidated-review.md written (NEEDS_WORK, C-01..C-28). Next: rewrite spec with fixes + §13 resolutions.
- rev2 committed; pass1 reviews archived in plans/sindri-api-surface/pass1; owner cap: max 3 review passes total (pass 1 done). Sibling engine-spec commit b15708b landed on my branch (shared checkout) — exclude from PR via clean branch.
- pass2 done (product PASS, security PASS, design/devex/arch NEEDS_WORK). writing rev3; then pass 3 = final.
- rev3 committed; pass 3 (FINAL) running.
- Clean PR worktree: /private/tmp/claude-501/-Users-thor-repos-agentic-workflow/464511ec-7f3b-4ce2-a41c-151c3659ba74/scratchpad/api-pr, branch docs/sindri-api-surface-design (origin/HEAD + my 3 commits). After pass 3: final edits → copy spec there, commit, push, draft PR, STOP.
- DONE: draft PR #164 (branch docs/sindri-api-surface-design). Waiting for owner approval. Working branch docs/sindri-api-surface also holds sibling commit b15708b.
- Owner raised cap to 5 passes total. Pass 4 running on final edit (0617aae / PR 8005d3d).
- pass4 all PASS; MEDIUM fixes committed + pushed to PR 164. Pass 5 (last) running.
- pass5 all PASS; fixes pushed to PR 164. STOP for owner approval.
