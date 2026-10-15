# Sindri merge orchestrator — handoff digest (2026-10-11)

Role: the oracle and merge orchestrator for 4 lane sessions that build the Sindri specs. Lanes send "ready #N". I
gate the PR (its head merged onto origin/main, locally, because CI is blocked by GitHub billing), check that neither
the head nor main moved, `gh pr merge --merge`, then tell the lanes.

- Lanes (SendMessage names): API = agentic-workflow-9a, Engine = -30, Context = -95, UI = -26.
- Scripts in this session's scratchpad: `gate.sh <pr> <scopes>` (scopes repo, dotnet, ts-*, bash-lib, bash-scripts),
  `watch.sh` (the Monitor source: PR, branch and low-disk changes every 120 s).
- Tracker: ~/.agentic-workflow/jthorare-agentic-workflow/plans/2026-10-11-sindri-app-epic/tracker.md
- Known flakes under load: `config/lib/tests/aw-state-dir-isolation.test.sh`, the Evidence
  `StackProbeTests.A_port_is_open_only_while_something_listens` test, and the Redaction unit lane budget. Re-run in
  isolation before calling a failure real.
- Never merge the drafts #166–#169 (references only, X17).
- State at 2026-10-11: main = 33e30e23. Merged: #174 (API P0), #177 (S-1C), #178, #176 (S-FX). Open: #175 (S-2R,
  draft, in its review loop), #179 (engine P1, draft). API P1 is not pushed yet; when it merges, tell Context (S-1A),
  Engine (P2) and UI (U1). UI U0 is not pushed yet.
- Open question for the owner: should context spec §4 put the fan-out exception on the compose edge or the project
  edge? #177 implemented the project edge.

## Update (later 2026-10-12)
- main = 739ca33e. Also merged: #179 (engine P1), #180 (engine P3, trimmed to rev 8). By the owner: #182 (engine rev 8, Temporal + tmux),
  #183 (retired the old two-target launch model). 7/47 phases (~15%).
- Open: #181 API P1 (draft; early check passed), #184 UI U0 (draft, supersedes #168; early check passed; in its /review loop).
  Engine P0, the Temporal spike, is a branch (feat/sindri-wf-p0-temporal-spike) with no PR yet.
- Owner questions pending:
  - the README wave table lacks engine P0;
  - context §4 fan-out edge (compose or project);
  - the sindri→sindri-ui cli cycle (record an exception, recommended, or invert the launch);
  - two copy additions for UI §12.4.
- Host bug routed to the API lane: ServeCommand.RunUntilStopped releases the lease before removing control.json, so ping answers epoch 0 for ~2 s.

## Update (2026-10-12, owner goal)
- OWNER GOAL: the core 4 (API, engine, context, UI specs) fully built THIS WEEK, with the lanes running all day in parallel.
  Lanes were told to stack the next phase on unmerged PRs (contract-first) and retarget to main when the needs merge,
  to send real spec questions to the orchestrator, and never to idle. I subscribed notify_when_idle to all 4 lanes;
  re-subscribe after each notice and nudge an idle lane.
- main = 996f3c1e. Merged since the last update:
  - #185 (stop-order fix);
  - #186 (engine rev 8.1: no spike, P4a Temporal foundation in wave 1);
  - #187 (DEPS-FANOUT-001 package cap removed, X60).
- Owner answers given:
  - sindri→sindri-ui: record a DEPS-CYCLE-001 exception (UI lane, in #184);
  - UI §12.4: add both strings;
  - no spike;
  - remove the fan-out cap.
- Open: #181 API P1 (in review, critical path); #184 UI U0 (owner answers on side branch feat/sindri-ui-u0-owner);
  engine P4a not opened yet.
- Pending owner question: re-run providers/claude/install-hooks.sh. 8 installed hooks differ from the repo, and
  ~/.claude/hooks/lib/failopen.sh is missing.
- Progress: 8 of 47 phases built.

## Update (2026-10-12, owner approvals)
- Merged: #181 (API P1) as 1027a13f. 9 of 47 phases built.
- The owner approved:
  - context stages ship as one large PR each with layered commits (S-1A too);
  - Onyx option A (/api/admin/search) plus the 3 context spec edits;
  - 3-deep stacking;
  - S-4/S-5B/S-6 split ahead of P6/S-5B.
- Hooks reinstalled: all 8 now match the repo, and lib/failopen.sh is present.
- AW_HEAVY_SLOTS=3 is in ~/.zshrc. Running sessions must export it themselves.
- Engine P4 rev 8.2 wording (desk timeout parks; Repo from fact:item) rides in P4's PR.
- UI spec §7/§18.1 fixes ride in #184 (74dd8de2).

## Update (2026-10-12 afternoon)
- Merged: #188 (engine P4a Temporal foundation, e4452c63), #189 (context stage table), #198 (bash tests hermetic re AW_HEAVY_SLOTS; prompt-sort timing). 9/47 phases.
- Open drafts: #184 U0, #190 API P2, #191 engine P2 (all units in; finalizing), #192 engine P4, #193 U1, #194 S-1A, #195 S-5A, #196 S-3a, #197 U2.
- Owner approved:
  - API spec: views=ViewsStatus, `/status views.text`, SND-AUTH-001 text, next-free migration number;
  - UI F2: palette keeps result;
  - merge engine P2 with Codex/Cursor probes pending;
  - review replies go in evidence/commits, not PR comments, this week (the external-write-guard blocks comments on auto turns).
- Rate limits (429) stall lanes; on an idle notice, nudge the lane.
- Gate tip: run `npm ci --prefix judge` before scorer tests.
- Disk: about 10 GB free. Lanes cap at 3 build worktrees; the context lane at 3 stages.
- I lost uncommitted edits in two 5750effa scratch worktrees (find -delete ignores -prune). Commits are kept under backup/5750effa-*.

## Update (2026-10-13, after the machine restart)
- Orchestrator session is now agentic-workflow-98. Lanes renamed: Context = -8d, UI = -c2, Engine = -b0, API = -40 (unconfirmed).
- Scratchpad was wiped by the restart; gate.sh / watch.sh / wt-clean.sh were recovered from the transcript into the same path.
  gate.sh scopes are SPACE-separated (`gate.sh 194 repo dotnet`); an unknown scope now FAILs. It now aborts if the gate
  worktree can't be created (a stale registration once made it run in the main checkout and commit a merge to local main;
  reset with `git reset --keep`, nothing pushed).
- Merged: #203 (architecture page storyboards, joithor), #194 (context S-1A, 6c4f8720).
- Redaction `The_timeout_grows_with_the_input_so_size_alone_never_blocks` fails on plain main at load 47+ (Blocked vs
  Redacted): a machine-load flake, not PR-caused. LANE-PKG-001 needs a quiet box to measure.
- Owner answers: #184 merges with "owner hand pass pending" for Reduce motion; UI §8 adds only ⌘⌥←/→ (no Ctrl+F6).
- Next: #196 retargets to main and re-sends ready; #191 engine P2 ready soon; #184 conflicts with main (UI lane merging).
- New PRs #205/#206 (joithor, ui-evidence TS): not lane PRs; ask the owner before merging.

## Update (2026-10-14 morning, second restart)
- Scripts now live in ~/.agentic-workflow/orchestrator/ (gate.sh, watch.sh, wt-clean.sh), which survives reboots.
- Lane sessions after the restart: Context = -ca, Engine = -15; API and UI are -5d and -a7 (asked which is which).
- Merged: #196 (context S-3a, d4de828b), #205 (ui-evidence scroll/env-login, 5034f4d8). #206 conflicts with #205 (4 ui-evidence
  files); its author joithor must merge main.
- Owner answers (2026-10-14): UI rev 8 not yet (U3 = shared subset + hidden WorkflowsStripSlot); X key = rejectLike, hidden if
  none; the API serves the recordedOnly and voided sentences; UI §4.3 failure row stands (API §3.6 copy changes); `profile` is a
  needs-you subject kind; leave the main checkout on the owner's docs/sindri-ui-screens branch (owner's rev-8 work).
- Sindri aot-smoke heartbeat `(last TIME)` race: fix rides in engine #191/#192.
- Confirmed names (2026-10-14): API = -a7, Engine = -15, Context = -ca, UI = -5d.
- Owner approved: idle service.ran runs write no row while the last outcome is ok (API §10 C8 edit, done in #190; engine drops its version).
- Context switches ImportContextMap to the X30 challenge in S-3b after #191 merges (engine waiver expires 2026-10-27). Tell -ca when #191 merges.

## Update (2026-10-14 afternoon, third restart)
- Orchestrator is now agentic-workflow-a6. Lane sessions are -21, -c3, -1f and -e0 (identities asked; not yet mapped).
  The UI rev-8 design session (-7f) has ended.
- Merged: #191 (engine P2, cfd8a172), #210 (UI rev 8, 4a2389bd). Before merging #210 I merged main into its branch and
  renumbered its README rows to X62 (rev-8 edits) and X63 (map review), because main already used X60 and X61. I also
  added PROTO_URL and MOCK_URL to .env.example.
- Owner decided X63: reversible map-review overrides are `desk`; promote and import stay `protected`.
- Rev-8 work is assigned per lane in ~/.agentic-workflow/orchestrator/rev8-assignments.md. Engine's urgent item is
  P2b (E-21/E-22 recipe schema fields), because P2 merged before them.
- AW_DENYLIST=file:~/.agentic-workflow/denylist.txt is in ~/.zshrc (#208).
- Open: #192 (P4), #209 (P5), #190 (API P2), #195 (S-5A), #184/#193/#197 (UI), and #206 (joithor's; conflicts after #205).
- Lane names confirmed (2026-10-14 pm): API = -c3, Engine = -e0, Context = -1f, UI = -21.
- MERGE ORDER: #190 (API P2) before #192 (engine P4); #192 carries #190's unreviewed branch. Don't gate #192 until #190 merges.
- Ledger migrations reserved: 0005 for #192 (temporal_event_id), 0006 for P6 (workflow_claims).
- Context renumbers rev-8 codes to SND-CONTEXT-040/041 (S-3a already uses 031/032), as a docs commit in S-3b.
- #211 (context): Redaction unit lane made deterministic.

## Update (2026-10-14 evening, after the crash)
- Lock rules changed (owner): 1 heavy job per session plus 2 box-wide slots for expensive jobs; `heavy` auto-detects them,
  reclaims dead slots, and passes nested calls straight through. Lane brief: ~/.agentic-workflow/orchestrator/resume-2026-10-14.md.
  gate.sh now runs its builds and tests through heavy.
- Orchestrator is -2c. Lanes: Context = -9c, Engine = -9e, UI = -3f, API = -f9 (by elimination; not yet confirmed).
- Context codes for rev 8 are SND-CONTEXT-044/045 (040–043 were taken); X63 goes to S-4.
- Gating #211 (Redaction timing fix).
- 18:20: every build is expensive (2 box-wide); DOTNET_PROCESSOR_COUNT=4; no node reuse. #184 gate killed by memory; re-gating.
