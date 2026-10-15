# Sindri build handoff: machine move, 2026-10-14

Everything a new **meta orchestrator** and the four **lane orchestrators** (API, engine, context, UI) need to pick up
the Sindri build on a new machine. The old machine's memory and `~/.agentic-workflow` state don't travel, so this
folder carries them. `main` was `fe228345` when this was written.

## 1. Restore on the new machine (once, by the owner)

1. Clone the repo and run `./setup.sh` as usual. The toolkit is assumed to be installed already.
2. Copy the carried state into place. It mirrors `~/.agentic-workflow`:
   ```bash
   cp -R docs/handoff/2026-10-14/state/agentic-workflow/. ~/.agentic-workflow/
   chmod +x ~/.agentic-workflow/bin-r1b/heavy ~/.agentic-workflow/orchestrator/*.sh
   ```
   The scripts use `$HOME` and expect the repo at `~/repos/agentic-workflow` (set `AW_REPO` otherwise). They were
   made Linux-portable for this move: `df -Pk`, `wc -c`, and `heavy` finds its Claude session under `node` too.
   Paths in the carried docs read `~/…`; the old machine was macOS with home `/Users/thor`, the new one is Linux with
   home `/home/thor`.
3. Claude memory for this project: copy `state/claude-memory/*.md` into
   `~/.claude/projects/-home-thor-repos-agentic-workflow/memory/` (the folder name is the repo path with `/` → `-`) and add their lines to that folder's `MEMORY.md` (one line
   each, the `description:` field).
4. Add to `~/.zshrc` (owner decisions, 2026-10-14; see §4):
   ```bash
   export AW_HEAVY_SLOTS=2            # box-wide slots for builds/expensive jobs
   export AW_SESSION_HEAVY_SLOTS=1    # heavy jobs per Claude session (a lane and all its subagents)
   export AW_DENYLIST=file:$HOME/.agentic-workflow/denylist.txt
   ```
   On a beefier machine you may raise `AW_HEAVY_SLOTS`, but raise it gradually: see §4 for why it's 2.
5. **The denylist is NOT in this PR.** It's the work-identifier list. Copy `~/.agentic-workflow/denylist.txt` from the
   old machine yourself (off GitHub), then `chmod 600` it. Agents must never open it. They only run
   `scripts/denylist-check.sh`.
6. Toolchain: .NET SDK per `global.json` (install with `dotnet-install.sh` into `~/.dotnet`, which `heavy` puts on
   PATH), the NativeAOT prerequisites (`clang`, `zlib1g-dev`), Node per `.nvmrc`, `jq`, `lsof`, `tmux`, and
   `scripts/binaries-lock.sh fetch temporal` (the lock file has linux-x64 and linux-arm64 rows). Playwright browsers
   for ui-evidence: `npx playwright install --with-deps chromium`.
7. Secrets that lived in the macOS keychain (`agentic-workflow-agent-app`, `gh:github.com` and others) don't move by
   themselves. Read each on the Mac with `security find-generic-password -s "<service>" -w`, then store it on Linux
   with `secret-tool store --label "<service>" service "<service>"` (libsecret) or wherever the tool that reads it
   expects it. Run `gh auth login` again on Linux. The judge's Jev provider reads **`TYPESAFE_API_KEY`**: from the
   environment first, then the keychain service `agentic-workflow-typesafe` (account `TYPESAFE_API_KEY`). On Linux,
   export `TYPESAFE_API_KEY` in your shell profile.

### Linux differences the lanes must know
- **UI real-app proofs were macOS-specific:** AX focus checks through the macOS accessibility API, Reduce motion,
  VoiceOver, and the ⌘ shortcuts. On Linux, prove focus and announcements through AT-SPI (Accerciser or Orca), and
  reduced motion through the desktop setting (GNOME: `gsettings set org.gnome.desktop.interface enable-animations
  false`). The app needs an X11 or Wayland session; a headless box needs `xvfb-run`. The pending Reduce motion and
  VoiceOver owner checks for U0 still want the Mac, or the owner accepts the Linux equivalents.
- **The ⌘⌥←/→ region keys** (UI §8) map to Ctrl+Alt+←/→ on Linux per the spec's platform table. Check that the UI
  spec says so; if it doesn't, that's an owner question.
- **The engine's launch flow and Attach** target Warp plus tmux. Warp runs on Linux; tmux is the session owner either
  way.
- AOT smoke tests publish for the host RID (linux-x64 or arm64), so `expected.txt` path normalizing may need a look
  the first time they run on Linux.

## 2. How the team runs

- **Meta orchestrator** (one session): gates and merges PRs, relays between lanes, takes owner questions, and keeps
  the lanes building. It never builds features.
- **Lane orchestrators** (four sessions: API, engine, context, UI): each drives its spec's phases with
  `/specToProvenPR`. Builder subagents run on **Sonnet**; the session model only critiques, reviews and orchestrates.
  Each lane runs at most 2 builder subagents at once.
- Sessions get new names every restart, so lanes announce themselves to the meta orchestrator. Messages go through
  SendMessage.

### The merge flow
1. A lane opens a **draft PR early** (owner rule: work is never only local).
2. Each PR goes through the `/review` loop until there are 0 blocking and 0 issue findings, with **at most 5 runs**.
   Suggestions and nits get written replies in the PR body or the evidence notes, not GitHub comments.
3. A real-app hand proof (`/specToProvenPR` evidence), the lane's own gate, and `denylist-check` on the PR title,
   body and commit messages all pass.
4. The lane sends `ready #N` to the meta orchestrator.
5. The meta orchestrator runs `state/agentic-workflow/orchestrator/gate.sh <pr> <scopes...>`. Scopes are
   **space**-separated: `repo dotnet bash-lib bash-scripts ts-sindri ts-scorer ts-ui-evidence`. The script merges the
   PR's head onto `origin/main` in a scratch worktree and runs the CLAUDE.md merge gate locally, because CI is blocked
   by GitHub billing.
6. A red test that the PR doesn't touch is rerun alone. If it passes alone, or fails the same way on plain `main`, it
   counts as a known flake (§6).
7. Before merging, check that neither the PR head nor `origin/main` moved since the gate ran. Then
   `gh pr merge N --merge`, and tell the lanes that main moved.

`watch.sh` is the meta orchestrator's Monitor source. Every 120 s it prints PR, branch and low-disk changes. Re-arm it
every 30 minutes.

## 3. Lane status and what to pick up

Every lane's in-progress work was committed and pushed at the move. Uncommitted work became a `wip(handoff)` commit
(not reviewed, not gated), and local-only branches were pushed to **`handoff/<branch>`** refs. The real PR branches
were not touched. **Resume from the `handoff/` ref where one exists:** reset the lane branch to it, or merge it in,
then continue. Every WIP commit passed `denylist-check` with 0 hits.

### Merged so far (phases on main)
- S1: complete.
- API: P0, P1.
- Engine: P1, P2, P3, P4a.
- Context: S-1C, S-2R, S-FX, S-1A, S-1M, S-2W, S-3a, plus S-DL (#208) and the Redaction fix (#211).
- UI: U0 (#184).
- UI rev 8 design (#210) is merged and approved: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design.md`.
- By phase count that's about 15 of 55 (API 2/12, engine 4/9, context 7/19, UI 1/14).

### Merge order (hard rule)
- **#190 (API P2) merges before #192 (engine P4)**, because #192 carries #190's branch.
- #209 (P5) and P6 are stacked on #192.
- #193 (U1) is based on main, and #197 (U2) is stacked on #193.

### API lane (spec: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md`)
| Work | Branch / PR | Resume from | Next |
|---|---|---|---|
| P2 projector | #190 `feat/sindri-api-p2-projector` | `handoff/feat/sindri-api-p2-projector` (16ad2783, includes WIP) | Units E1 (owner rule: an idle `service.ran` writes no row while the last outcome is ok; API §10 C8 edited), E2, F. Merge main **and fix the UI fake**: `FakeSindriHandler.Status` must write `views`, `health` and `lastRun`, or `GoldenContractTests` (running-approved) goes red. Then the hand test, review, ready. |
| Rev-8 contract unit (E-20, E-22, E-23) | #213 `feat/sindri-api-rev8-contract` | `handoff/feat/sindri-api-rev8-contract` (9cd7f62b, 30 WIP files) | Gate with the whole solution's unit and integration lanes (UI tests included), then review and ready. Off main, so it can merge before #190. 30 goldens under `tests/Aw.Sindri.Tests/Golden/Api/workflow-*`. The record is `InstanceDetail` (#192 keeps `InstanceView`). Re-record the recipe hashes after engine P2b. |
| P3 items | `feat/sindri-api-p3-items` (no PR) | `handoff/feat/sindri-api-p3-items` (5f45efe8) | Unit B fix written, gate not run. |
| P5 stream | `feat/sindri-api-p5-stream` (no PR) | origin (c865d5e3) | Contract unit with red ReadLedger and GetEventCatalog slices. |
| Later | — | — | E-28 (P10a: agent daily budgets, SND-LAUNCH-010), P12 stats (E-24, E-25), P13 engine views (E-30), E-29 (doctor `consumers`, /status graph). |

Owner-approved wording:
- Cancel confirm: "Cancel <address>? <N> running steps stop; <M> children are cancelled."
- The needs-you empty sentence stays "Nothing needs you. N running, M queued."

### Engine lane (spec: `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`, rev 8.1: Temporal)
| Work | Branch / PR | Resume from | Next |
|---|---|---|---|
| P4 interpreter | #192 `feat/sindri-wf-p4-interpreter` (9dcab704) | origin, plus fix-agent refs `handoff/p4fix-interp`, `-cli`, `-bridge`, `-nonce`, `handoff/p4fix2-wf` | Review run 2 was in progress. Fold the fix branches in. It has E-30 `temporal_event_id` (ledger migration **0005**) and the Quiet/Worked bridge. Merges after #190. |
| P2b recipe fields (E-21, E-22) | `feat/sindri-wf-p2b-recipe-rev8` (no PR) | `handoff/feat/sindri-wf-p2b-recipe-rev8` (1a2a020b) | Part A is done: `urgency`, `outputs`, `inputs[].kind`, and dev recipes `workflow.dev-incident-hold`, `workflow.dev-triage-three-state`, `workflow.dev-desk-round-trip`. Part B: the seven RCP rules, `recipe show --json`, proof, PR, review. Every recipe hash changes; an absent `urgency` hashes as `work`; stored registry rows keep their hash behind a compatibility reader. Also make two flaky P2 tests deterministic: `RecipeApproveCliTests…SND_RECIPE_007` and `RecipeRegistryCliTests`' Temporal wait. |
| P4b status record | `feat/sindri-wf-p4b-status-layout` (on #192) | origin, plus `handoff/feat/sindri-wf-p4b-sa`, `handoff/feat/sindri-wf-p4b-w0` | The structured status-layout record, list-filter search attributes, fact producer and time (needed by the API rev-8 work). |
| P5 timers / takeover | #209 `feat/sindri-wf-p5-timers` (on #192) | origin, plus `handoff/fix/p5-r1-a`, `handoff/fix/p5-r1-c` (and origin `fix/p5-r1-b`) | Review-run-1 fixes in flight. |
| P6 effects | `feat/sindri-wf-p6-effects` (on #192) | `handoff/feat/sindri-wf-p6-effects` (0a6ba6bd), sub-branches `feat/sindri-wf-p6-{a,b,c,d}` on origin | Integrating; ledger migration **0006** (`workflow_claims`). |
| Later | — | — | P7, P8; E-31's engine half (the scoping recipe writes draft map proposals) after P7. |

### Context lane (spec: `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`; stage table §Phases)
| Work | Branch / PR | Resume from | Next |
|---|---|---|---|
| S-5A Onyx source | #195 `feat/ctx-s5a` | `handoff/feat/ctx-s5a` (0003af9e) | Rerun after the main merge, then ready. |
| S-3b cascade | #212 `feat/ctx-s3b` | `handoff/feat/ctx-s3b` (1a5c0830) | Includes the `ImportContextMap` switch to the shared X30 challenge with the waiver removed (**deadline 2026-10-27**) and the rev-8 codes renumbered to **SND-CONTEXT-044/045** (031/032 and 040–043 were taken). |
| S-4 map review | — | — | X63 (owner: reversible map-review overrides are `desk` with Undo; promote and import stay `protected`), E-26 lenses, E-27 file map, E-31's context half, and the fixture `tests/Aw.Context.Tests/Fixtures/map-tabs.json`, all in one PR. |

### UI lane (rev 7 spec `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` + rev 8 screens spec)
| Work | Branch / PR | Resume from | Next |
|---|---|---|---|
| U1 catalog | #193 `feat/sindri-ui-u1` (base main) | `handoff/feat/sindri-ui-u1` (2717ef87: the main merge plus WIP) | Finish merging main fe228345, gate, ready. |
| U2 queue / item | #197 `feat/sindri-ui-u2` (on #193) | origin (04439c72) | Carry U1 up. The item sheet re-bases on the U8 frame later. |
| Rev-8 UI contract unit | `feat/sindri-ui-r8-contract` (local only) | `handoff/feat/sindri-ui-r8-contract` (= API contract head 49eae596; no UI work yet) | Shell navigation, `ISindriApi` and the JSON context, fake routes serving #213's goldens, the shared instance table, Attach, the lock popover. Draft PR early. |
| U3', then U8 (wave 1), then U5', U9–U13 | — | plans in `state/.../plans/sindri-ui/` (u3p-plan.md 21 tasks, u8-plan.md 13 tasks) | Owner answers: Home counts all 6; "open" means not terminal (fixture reads 8, with a collapsed "N done today" line); no protected confirmation in ⌘K for now. Rev-8 readings confirmed: no attention band, no urgency stripes, Decisions folds into Needs you, U3' builds the standalone item view model, U8 builds the desk Complete form. |
| Owner items pending | — | — | The Reduce motion hand pass (`proof/sindri-ui-u0/manual-test.md`) and a VoiceOver run. |

### Not lane work
- #206 (joithor, ui-evidence role/text targets) conflicts with main after #205. Its author merges main, then gate it.
- #166, #167 and #169 are reference drafts: never merge them. #168 shows "merged" only because #184 contained its
  commits.

## 4. Rules and owner decisions to keep

- **Heavy-job locks** (`state/agentic-workflow/bin-r1b/heavy`). Every build and test goes through `heavy <cmd>`.
  - One heavy job per Claude session; a lane and its subagents share one slot.
  - Every **build** also takes one of `AW_HEAVY_SLOTS` box-wide slots (2). That means `dotnet build`, publish,
    format, restore and run; `dotnet test` without `--no-build`; npm install, test and vitest; aot-smoke; and
    Playwright, ui-evidence and browser runs.
  - Only `dotnet test --no-build` and plain scripts are "light".
  - Each .NET job gets `DOTNET_PROCESSOR_COUNT=4` and no MSBuild node reuse.
  - A slot whose owner process died is reclaimed. Nested `heavy` calls pass straight through.
  - Lane pattern: `heavy dotnet build tests/Aw.X.Tests`, then
    `heavy dotnet test --project tests/Aw.X.Tests --no-build --filter-trait Lane=unit`.
  - Edit `heavy` only by writing a new file and `mv`-ing it over the old one: running jobs read the script as they go.
  - Why it's 2: the old 16 GB machine crashed at 3 box-wide slots with no per-session cap, then again had swap at
    6.4/7.2 GB and load 128 with four concurrent builds.
- **Review loop:** stop at 0 blocking and 0 issue findings, with at most 5 `/review` runs per PR. If run 5 isn't
  clean, the owner may approve a narrow re-check of only the fixes (done for #184).
- **Owner decisions this week:**
  - Temporal is the workflow substrate; no spike.
  - DEPS-FANOUT-001 is removed (X60).
  - Multiplayer later, not foreclosed (X61).
  - The sindri→sindri-ui DEPS-CYCLE-001 exception is recorded.
  - Onyx option A; **ConnectAI is not to be used**.
  - One PR per context stage; stacking up to 3 deep.
  - Merge engine P2 with the Codex/Cursor probes owner-pending.
  - Ledger migrations take the next free number at merge time.
  - UI rev 8 is approved, and its README rows are X62 and X63.
  - `AW_DENYLIST` is an injected pointer (#208).
- **Known load flakes** (rerun alone before calling a failure real):
  - `config/lib/tests/aw-state-dir-isolation.test.sh` (other sessions write under `~/.agentic-workflow`);
  - Evidence `StackProbeTests` and the HUP/INT signal tests;
  - Sindri `SpikeEvalTests` (10 s budget), `HttpControlPlaneProbeContractTests`, `ApiProblemTests` and
    `CliStatusJsonTests`;
  - `RecipeRegistryCliTests` and `RecipeApproveCliTests` (being fixed in P2b);
  - Redaction's timing test was fixed in #211.
- **Gate tips:**
  - `gate.sh` installs `.github/ci-tools` for `no-any` and `judge` for scorer tests.
  - For a PR that touches the Sindri host, also run `bash src/Sindri/aot-smoke/run.sh` in the gate worktree. Its
    heartbeat `(last TIME)` race is fixed on main.

## 5. Files in this folder

- `state/agentic-workflow/digests/`: per-lane and per-PR handoff digests (`sindri-merge-orchestrator.md` is the meta
  orchestrator's running log; `sindri-*-lane.md`, `ctx-*.md` and `sindri-api-*.md` are the lanes').
- `state/agentic-workflow/jthorare-agentic-workflow/plans/`: the Sindri epic and lane plans, including the tracker
  in `2026-10-11-sindri-app-epic/`, the lane plans in `sindri-api-lane/` and `sindri-ui/`, and the verification
  plans.
- `state/agentic-workflow/jthorare-agentic-workflow/reviews/`: review records for the open PRs #192, #195 and #209.
- `state/agentic-workflow/orchestrator/`: `gate.sh`, `watch.sh`, `wt-clean.sh`, `handoff-push.sh`, and the briefs
  `rev8-assignments.md` and `resume-2026-10-14.md`.
- `state/agentic-workflow/bin-r1b/heavy`: the lock wrapper.
- `state/claude-memory/`: the project memory notes that apply to this build.

## 6. First moves for the new meta orchestrator

1. Restore (§1). Open the five sessions, start `watch.sh` as a Monitor, and have each lane announce itself.
2. Give each lane its §3 row. Lanes resume from their `handoff/` refs and delete those refs once folded in.
3. Critical path:
   - #190 → #192 → #209 and P6;
   - #213 → the UI contract unit → U3' and U8;
   - engine P2b, which unblocks the recipe hashes in the API goldens;
   - #195 and #212 for context.
