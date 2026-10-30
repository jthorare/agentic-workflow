# meta — handoff 2026-10-22 ~04:50Z

You are the meta orchestrator of team `sindri` (lanes api, engine, context, ui, ideas). Start with `/meta`:
`aw-meta role-set --session <your id> --name sindri-meta`, `lanes-refresh`, `lanes --announce` (send to every lane),
`aw-meta watch --for 29m` in the background (re-arm on WATCH EXPIRING), `status`, `health`.

## Main

`6a674c43d` (#595 CI-4). Merged tonight by the owner, mostly ungated:
- context: #587, #580 (F1P-b1), #588 (CI-3), #595 (CI-4)
- ui: #590 (main-red fix)
- api: #583 (workflow rows; #546 merged with it)
- engine: #570 (E1 recipe displayName and versions, the demo critical path), #592, #593, #594 (box-red fixes)
- ideas: #591 (BF-S)
- meta: #589 (meta-codex slice 1)

## Merge queue — continue here

1. **#596 engine `2e7ad0b49`: gate PASS, merge-check READY; owner merges.** It fixes a scrubber false positive from #570:
   SND-SCRUB-002 on `.github/workflows/ci.yml:334` (`x-access-token:${GITHUB_TOKEN}@`), which every main merge trips. Lanes may
   `--no-verify` that one hit until #596 lands; tell them when the allowance ends.
2. **#598 ui `f3dac47a2`** (macOS: CreateItemDialog, ShellWindow, dot-grid): no gate recorded. Run 1 waited about 3 h for heavy
   slots, then Claude Code killed it for low memory. Re-gate.
3. **#597 ui `cedf9e0da`** (context-arch: ARCH-UI-001, ARCH-CYCLE-001, NamedControls ContextPage): ready, gates after #598.
4. **#567 api** (add repo): unblocked by #570; needs a main merge and a fresh `ready`.
5. **#601 ideas `c5718aa10`** (LN-PK lite): ready, after #597. The ideas lane reports 1454 of 1454 scoped tests passing. Recipe
   hashes move (step.lens.round, step.review.round, workflow.phase v2, review-loop v4), so the owner runs approve-all after merging.
6. **#602 context `440c72cfd`** (CI-6, BuildCodeIndex): green, ready for its gate. Not ready yet: #604 S-9a (`325440a29`, needs a main
   merge and a rerun). Context's first next step is the ARCH-INFRA-001 red from #595.
   Do NOT gate #584 (`dd7bd8b13` is a debug WIP). #599 (ui setup) isn't ready: its production client still throws on the five setup
   writes (see ui.md). #603 (ui workflows s2, `1799ce8e8`) isn't ready either; it needs review.

The lane files list what comes next. The owner closes #528 after #567 merges, and #463 after #574 merges.

## Box pressure: read before gating

- About 04:40Z, Claude Code killed meta's gate, watch and timer: the system was critically low on memory (swap about 5.7 of 7 GB,
  about 9.5 GB available). Start heavy work again only when memory recovers.
- **api is paused on new heavy jobs.** Meta asked it at about 04:20Z so context's CI-6 and the gate could get slots. It waits for
  meta's "resume heavy".
- `heavy` (`~/.local/bin/heavy`, cap 3): slot 2 needs 10 GB free and slot 3 needs 14 GB, so in practice it's 1 or 2 slots. Its
  locks are mkdir mutexes with no first-come order, and a job waiting for a box-wide slot keeps its lane's session slot.
  **Open owner questions:** (a) first-come order, and release the session slot while waiting box-wide; (b) lower slot 3's memory
  need. Engine will send its `heavy` `--timeout`/`--hangdump` brief to meta for review.
- 04:15Z incident: an orphaned, unfiltered `Aw.Sindri.Tests` run held a slot for 2.5 h. The rule sent to every lane: builders run
  long tests with `run_in_background` and a lane or class filter, never a whole-project unfiltered run, and kill their runs when
  they stop.

## Known reds on main and this Mac

Classify these; never send them back as the PR's.
- Fixed tonight: format IDE0001 (#590), LockButtonWalk (#590), the Launch probes (#592), most git-template slow commits (#593).
- Still red: the Architecture pair and NamedControls ContextPage (#597); ARCH-INFRA-001 on two public CodeIndex types (new since #595, context's); Ui CreateItemDialog, ShellWindow, dot-grid (#598); Meta
  SystemCommandRunner x5 and QueueGateSignal; Evidence SqliteReader hot journal; PreviewTmux; ContextApiGolden context-code capped;
  PosixProcessAncestry; bash ci-bash-tests (temp dir), install-headroom, sindri-ui-fixture, line-fixture.
- Seen once, probably flakes: ListFilterAttributes, AgentRuntimeInterpreter.Cancel, LaunchesApiGolden sessions-id-exit.
- Unclassified: aot-smoke exit 1 on #598 run 1.

## Owner decisions tonight

- **Builders per lane are uncapped.** Ignore aw-meta's "builders: 1 per lane (disk)" line; only throttle under 2 GB free memory.
- The lanes cleaned up idle workers on request. The owner's `sindri serve` and Temporal (pids 24776 and 24810) are left alone.
- Standing decisions: the owner merges everything (joithor can't merge or close on jthorare); lanes do cursory checks and meta runs
  full gates; the TS→C# cutover is a GO; Codex teams use one provider; GitHub CI isn't a usable signal.

## How to gate

- Run `meta-gate-reserve <arg from aw-meta status>; aw-meta gate-run N; meta-gate-reserve --clear` as one background command, one
  gate at a time.
- Classify reds: `grep -h '^failed ' gates/N/1/{unit,integration}.log | sed 's/ (.*//' | sort -u` and
  `grep 'not ok\|FAIL' gates/N/1/bash-*.log`. Gate state is in `~/.agentic-workflow/meta/teams/sindri/gates/`.
- When the owner says "merged": TaskStop the gate, `meta-gate-reserve --clear`, check `git log origin/main`, and announce
  "main moved to <sha>" to every lane. The sha must be in the text.
- A lane's ready needs `ready #N <sha>` as its first line; otherwise gate it by number.

## Meta's own work

- meta-codex: a whole team on Codex, meta included (brief `~/.agentic-workflow/jthorare-agentic-workflow/plans/meta-codex/brief.md`;
  owner decision 2026-10-21: a team is one provider, all Claude or all Codex). Six slices, each its own PR, in order:
  1. One provider per team; the meta role set from a reported session id: **merged, #589** (`53937c574`).
  2. Codex meta stop gate and inbox (`stop-check` with no `--transcript`; inbound rows from `lane-reply`): **next; not started, no branch.**
  3. Watch for a Codex meta (no background notifications on Codex; `aw-meta-launch` runs the watch): not started.
  4. `lanes-context` and `lane-recycle` on Codex (context size from Codex session files): not started.
  5. `usage-wait` on Codex (usage-limit stops and their reset time): not started.
  6. Skill and docs ("Running on Codex" in skills/meta/SKILL.md) and an end-to-end proof: not started.
  Dispatch one Sonnet builder per slice, serially, each from main, in its own worktree; follow the brief's "Rules for every slice".
- cutover-scrub: `~/repos/aw-wt/cutover-scrub`, branch `feat/cs-scrub`, no builder running. Brief: `.../plans/cutover/brief.md`, part A.
  Its work is pushed as WIP commit `85c09d687` on joithor `feat/cs-scrub` (unbuilt, untested; one fake-key test literal split for the scrubber).
- Builders open PRs from joithor on the `jfork` remote (joithor/agentic-workflow-jthorare), not `fork`.

## Handoff check (~05:30Z)

Every lane replied HANDOFF DONE (api d555bb1a6, engine 770fb16b9, context 9288ed67b, ui 842fafbd7 / d13deef4a, ideas
6cd54898a) and stopped its builders. `aw-meta worktree-list` finds no unpushed work. The only leftovers: PR-body scratch notes in
three merged engine branches (launch-path-rounds, launch-tests-macos, test-git-template) and a regenerated
`tests/Aw.Architecture.Tests/packages.lock.json` in two old gate trees. They're safe to sweep.

## Other

- The github MCP and serena fail to connect; the `gh` CLI works (api.github.com dropped out twice tonight).
- The demo checklist is stale (about 17:00Z): `~/.agentic-workflow/digests/sindri-demo-checklist-2026-10-21.md`.
