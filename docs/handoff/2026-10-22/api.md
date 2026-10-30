# api lane handoff — 2026-10-22

Owns HTTP/CLI/MCP surfaces, the session collector, add repo (SO-A3a), setup MCP (SO-A4) and setup Onyx (SO-A5).
Main when written: 6a674c43d. Every branch below is on `joithor/agentic-workflow-jthorare` (remote `jfork`/`upfork`).
Every worktree below has no uncommitted and no unpushed work (checked: `status --porcelain` empty, `rev-list jfork/<branch>..HEAD` = 0).

## Merged this session

#561 (needs-you setup rows), #571 (CI-10a code-index reads; supersedes #547, owner closes #547), #583 (workflow rows;
supersedes #546, owner closes #546), #586 (`src/CodeIndex` in `scripts/lib/sindri-build-inputs.sh`, which fixed main's
aot-smoke and `scripts/sindri-app.sh --rebuild`).

## Branches and PRs in flight

| Item | PR | Branch | Head | State | What's left |
|------|----|--------|------|-------|-------------|
| Add a repo from the app, default profile allows the shipped recipes (SO-A3a) | #567 | `feat/sindri-add-repo` | `7bbd95bea` | Main `6a674c43d` merged. Two post-merge analyzer fixes committed: `fd743c98e` (MA0051: `AddSindriAdapters` 41 lines; AddRepo's registrations moved onto the `IRepoLocator` line) and `7bbd95bea` (CA1502: `ApiScenarios.Start`; setup hosts through a `SetupHost` helper). **Not built since those two fixes** (the heavy pause, then the handoff). Earlier gate runs: format was the only red in its own diff (fixed in `70c1afeee`). | Build `tests/Aw.Sindri.Tests` and `tests/Aw.Sindri.Ui.Tests`; scoped tests (`--filter-namespace` `*GetNeedsYou*` `*AddRepo*` `*Setup*` `*Profile*` `*Commands*` `*Shipped*` `*CodeIndexReads*`); `--filter-class` `*CoreApiGoldenTests` `*SetupApiGoldenTests`; format on the changed `.cs`; then `ready #567 <sha>` to meta. Merge resolutions to keep: `CommandTable` keeps main's `["agents" or "setup" or "code-index", ..] => ReadVerbs` plus #567's `["repo", ..]` line; host registration keeps both `IShippedEffects` and `IEmbeddedRecipes`. |
| Needs-you `stop {n, of}` and `timeout` joins on desk/park rows | none yet | `feat/sindri-needs-you-stops` | `2de3f529a` | Green on `69be1403b` (main `8bbee9420` merged): builds 0 errors; Sindri `*NeedsYou*` 141/141; Ui `*NeedsYou*` 195/195; Architecture only the known main reds; IDE0001 fixed in `2de3f529a`, format exit 0. | **Open after #567 merges**: merge main; resolve `GetNeedsYouService` against #567's `SetupNotices` (#567 moves setup rows into `SetupNotices`; this branch moved the Onyx and recipe rows into `NeedsYouLiveRows` to stay within AW1007's 7 dependencies); rebuild, `*NeedsYou*` tests, format; push; PR with the body below (add the `NeedsYouLiveRows` line); ready. |
| macOS session collector (owner report: Agents tab says "unsupported-platform" on a Mac; the demo box is a Mac) | none yet | `feat/sindri-sessions-macos` | `dba671ea5` (`wip:`) | **Unverified WIP**: written by builder api-codeidx, not known to compile (its last build, after the test-project analyzer fixes, did not report before the stop). Cut from `2020871f2`; main not merged. | Build and fix analyzer errors; Sessions tests (`--filter-namespace Aw.Sindri.Tests.Sessions`, incl. the real-Mac integration test); `npm ci` in `scripts/` once (Ajv missing), then `bash config/hooks/tests/fleet-state.test.sh` (macOS bash 3.2); Architecture; format; `node scripts/check-deps.mjs`; split the WIP into feat/test commits if wanted; merge main; PR with the body below; ready. |
| SO-A4: register Sindri's MCP from the app | none yet (stacks on #567) | `feat/sindri-setup-mcp` | `2d772ba7a` (`wip:` on top of `48ec41635`) | Commits 1 (`086050b83`: `POST /sindri/setup/mcp`, SND-SETUP-004 before the claim, key `mcp-`+16 hex of sha256(repo name), catalog row `JobRows.Job` with `ServiceGrants [setup]`) and 2 (`48ec41635`: `mcp.registered`, the job fails with the provider's sentence, `setup:mcp` needs-you row) were verified (deps/rules/format 0, builds 0, Architecture 130/0, scoped tests green). The `wip:` commit (automatic runs through `SetupJobQueue`, consent on `ApproveProfile`, off switches `setup.mcp: off` / `AW_SINDRI_APP_NO_MCP=1`, "Turn on for your repos" need) is **not built**. | Build, fix, tests; then the tracked-file, no-consent and off-switch refusal tests; the claude argv hardening test; the aot-smoke catalog check; merge #567's latest and main; PR (base main, after #567 merges) stating the deviation: `RegisterMcp` is class Job like AddRepo, not desk (a session is still refused: golden `setup-mcp/refused-session`; the setup service keeps its grant), and a null `repo` takes the snapshot's only repo. |
| SO-A5: set up Onyx from the app | none yet | `feat/sindri-setup-onyx` | `78de3c86a` (`wip:`) | **Unverified WIP**: code and tests written by builder api-setuponyx, never built (its build was queued behind the box-wide heavy lock). Branched from `8bbee9420`. The commit message lists the content. | Build; PublicAPI lines from RS0016/RS0017; analyzer fixes; tests, especially the password **sentinel** test (one known password down every path, grepped in the ledger, jobs, answers, progress lines and logs); command and event catalog goldens one at a time (`AW_GOLDEN_UPDATE=1`, `--filter-class`); Architecture; format; PR. Deviations to state: routes answer 200, not 202 (as every existing job route); a refused login is SND-ONYX-008 (the existing sign-in code), 007 through the not-admin path; `control.json` "pending until the first repo" is not built; a failed login fails the job (the owner presses Set up Onyx again). |

## Worktrees (this box only; every one pushed)

| Worktree | Branch | Head |
|----------|--------|------|
| `~/repos/aw-wt/sindri-add-repo` | `feat/sindri-add-repo` | `7bbd95bea` |
| `~/repos/aw-wt/sindri-needs-you-stops` | `feat/sindri-needs-you-stops` | `2de3f529a` |
| `~/repos/aw-wt/sindri-sessions-macos` | `feat/sindri-sessions-macos` | `dba671ea5` |
| `~/repos/aw-wt/sindri-setup-mcp` | `feat/sindri-setup-mcp` | `2d772ba7a` |
| `~/repos/aw-wt/sindri-setup-onyx` | `feat/sindri-setup-onyx` | `78de3c86a` |
| `~/repos/aw-wt/handoff-api` | detached, used to write this file | — |

Already removed: the worktrees for #571, #583 and #586.

## Builders (all stopped for the handoff)

| Builder | Work | Where it stopped |
|---------|------|------------------|
| api-codeidx | #571, then the macOS collector | After the code was written and while a rebuild was queued; never saw a clean build. WIP `dba671ea5`. |
| api-setupmcp | SO-A4 | After commit 2 (`48ec41635`), with the automatic-run work in the tree. WIP `2d772ba7a`. |
| api-setuponyx | SO-A5 | With its first build queued. WIP `78de3c86a`. |
| api-wfrows, api-nystops | #583; needs-you stops | Stopped earlier. Their branches merged or were taken over. |

## Blocked, and on whom

- The needs-you stops PR and SO-A4's PR wait on **#567 merging** (owner, after its gate).
- #567's gate waits on a green local run after the two analyzer fixes (api).
- Known main reds, routed to **ui**, so not ours: Architecture ARCH-UI-001 (`ContextMapViewModel` uses `Avalonia.Rect`) and ARCH-CYCLE-001 (a Ui namespace cycle through `ViewModels.Context` and `Views.Context.Map`), from `d1c5f1d31`; Ui Views `CreateItemDialogTests` palette focus (:269) and `ShellWindowTests` option-arrows (:1796). #590 may have fixed some.
- The scrubber falsely flags `.github/workflows/ci.yml:334` (a `${GITHUB_TOKEN}` placeholder from #570; **engine** is fixing it) and `tests/Aw.Sindri.Tests/Profile/IndexCodeIndexKeysTests.cs:74`. A main merge may use `--no-verify` for those hits only, said in the commit message.
- MCP log flake (`SindriRequestToolTests…at_trace`): waiting on failure text from a meta gate.

## Next steps, in order

1. #567: run the checks listed above on `7bbd95bea` (merge main first if it moved), fix anything red, then `ready #567 <sha>`.
2. The macOS collector, which matters for the demo box: build, test, PR, ready.
3. SO-A5: build, sentinel test green, PR, ready.
4. After #567 merges: the needs-you stops PR, then SO-A4 (finish, PR, ready).
5. After #567 merges: a follow-up so `EmbeddedShippedEffects` reads engine's `IEmbeddedRecipes` (#570) instead of its own embedding.
6. Later: context map-status surfaces (`sindri context map status --json`, doctor `context:publish`), which wait on context's F1P-b2. Parked: #543, A3c, A7a part 2.

## How this lane works (box facts)

- The gh/git account `joithor` gets 403 on origin. Push to `jfork`/`upfork`; open PRs with
  `gh pr create --repo jthorare/agentic-workflow --base main --head joithor:<branch>`. Push and `gh pr create` are separate commands (a hook refuses them combined).
- Meta records ready only as a first line `ready #<pr> <sha>`.
- `heavy dotnet …` for every build or test: one slot per session plus a three-slot box lock that is not FIFO. Use `export MSBUILDDISABLENODEREUSE=1` and `-nodeReuse:false -warnaserror`.
- Box rule: long tests run in the background with a lane, namespace or class filter, never a whole test project unfiltered, and are killed when you stop.
- MTP filters: several of one kind OR together; mixing kinds ANDs (exit 8). If `dotnet test --project tests/Aw.Architecture.Tests` says "Zero tests ran", run the dll: `heavy dotnet artifacts/bin/Aw.Architecture.Tests/debug/Aw.Architecture.Tests.dll`.
- `PublicAPI.Unshipped.txt` conflicts: check that both sides add different types, strip the markers, and confirm `sort | uniq -d` is empty.
- Main merges into these branches have twice tripped method-size analyzers (MA0051, CA1502) where both sides added a line; build after every merge.
- `dotnet restore` can rewrite `tests/Aw.Architecture.Tests/packages.lock.json` (adds `Aw.Kernel.Data`). Don't commit that drift.
- Owner decisions: builders may work past the 200k context-guard hook (advisory on a 1M model; put it at the top of every brief); builders per lane are uncapped, throttled only by free memory under 2 GB.
- Builder watches sometimes never fire (one waited five hours on a finished run). If a builder goes quiet, read its logs.

## PR body drafts (were only in /tmp)

### macOS session collector (`feat/sindri-sessions-macos`)

Title: `feat: the session collector reads macOS (ps, lsof and the argument block)`

- The Agents tab on a Mac said "unsupported-platform" because the only process and socket sources read /proc. This adds macOS sources and picks them by platform.
- `MacProcessTable`: `ps -axww -o pid=,ppid=,uid=,stat=,tty=,lstart=,comm=` with `TZ=UTC LC_ALL=C` from `/bin/ps`, current user, zombies dropped; `lsof -a -p PID -d cwd -F n` for the working directory; `lsof -a -p PID -d 0-65535 -F ftdn` for open descriptors (capped at 64). `MacUnixSockets`: `lsof -a -U -u UID -F pftdn`. `MacToolRunner`: absolute paths, bounded, 5 s, 16 MiB cap; no Aw.Meta reference.
- `AddSindriSessions` picks macOS only when `AW_SESSIONS_PROC_ROOT` is unset, there is no /proc, the OS is macOS and `/bin/ps` exists; `unsupported-platform` is now only for a box with neither.
- (a) StartTicks on macOS = ps `lstart` read in UTC, as epoch seconds (`PsStartTime`), in both the C# table and `config/hooks/fleet-state.sh`. Claude Code stores a macOS session record's `procStart` as that same lstart text in UTC. `SessionLiveness.Matches` used `long.TryParse`, so **every Claude record on a Mac was discarded**; it now accepts the digits (Linux) or the lstart text (macOS). A null `agentPid`/`agentStartTicks` from an old hook still validates.
- (b) tmux socket path: the macOS `OpenFiles` returns `socket:[key]` for a unix socket (key = lsof's device column), and `MacUnixSockets` keys its map the same way, so `PassHostSources`' join and Core are unchanged.
- (c) Tty: ps `??` is 0; `ttysNNN` is NNN + 1.
- argv[0] and the environment come from `sysctl KERN_PROCARGS2` (`MacArgumentBlock`), not `ps -E`, so a prompt containing `TMUX=` can't pass for an environment entry; only allowlisted names are kept, and the buffer is cleared.
- The hook walks ancestors with one `ps -p` per step (about 8 ms each); a full listing costs 130 to 500 ms, over the hook's 100 ms budget. bash 3.2-safe.
- No separate red commit: MA0182 and CS9113 reject unreferenced stub types.
- Tests: parsers over scrubbed Mac recordings (`tests/fixtures/sessions/macos`), process-table and socket contracts over recordings and the fake, `MacArgumentBlockTests`, `SessionSourceChoiceTests`, `SessionLivenessTests`, `MacSourcesIntegrationTests` (integration lane, skips off macOS), and a macOS block in `fleet-state.test.sh`.

### Needs-you stops and timeouts (`feat/sindri-needs-you-stops`)

- The needs-you list's `desk:` and `park:` rows carry what the UI needs for "Approval n of of" and the timeout line, so the app computes nothing.
- `stop {n, of}` on a `desk:` row: the parked desk state's 1-based position among all the recipe's desk states, in recipe order.
- `timeout {kind, inSeconds, on}` on `desk:` and `park:` rows: the instance status's `StatusTimeout`, `inSeconds` measured at the response.
- Read-time joins in `GetNeedsYou` (`NeedsYouJoins`): each instance and recipe read at most once per response; a failed join omits its field, and the list never fails for it. `stageTitle` is omitted (no stage display names on main yet).
- `GetNeedsYouService` keeps seven dependencies: the Onyx and recipe rows moved to `NeedsYouLiveRows` (AW1007).
- The Ui client mirrors the fields (`NeedsYouRowView.Stop`, `.Timeout`, `NeedsYouStopView`). Docs: `docs/csharp/sindri.md` needs-you section. Goldens `needs-you/desk-with-stop`, `needs-you/park-with-timeout`.
- Tests: `NeedsYouJoinsTests`, `GetNeedsYouTests`, `ServedWorkflowRoutesTests`, `SindriApiClientNeedsYouTests`, Ui `GoldenContractTests`, Architecture.
