# Context lane: handoff 2026-10-22

The context lane owns the code index (CI-*), index ↔ context map sync (F1P-*), and the lens reads (S-9*). The spec is
`docs/superpowers/specs/2026-10-19-sindri-code-index-design.md`, and its phase table is at §14, around line 790.

Main at handoff: 6a674c43d or later. Push to `jfork` (joithor/agentic-workflow-jthorare), and open PRs with
`gh pr create -R jthorare/agentic-workflow --head joithor:<branch>`.

## Open PRs and branches

| PR | Branch | Head | State | What's left |
|----|--------|------|-------|-------------|
| [#602](https://github.com/jthorare/agentic-workflow/pull/602) | `feat/ci-6` | `440c72cfd` | **open, ready for gate** | Meta gates it. It includes main through 6a674c43d. |
| [#604](https://github.com/jthorare/agentic-workflow/pull/604) | `feat/s9a` | `325440a29` | **open, needs a main merge** | Main only through a9682529f. The trial merge with main is textually clean. Merge main, rebuild `tests/Aw.Context.Tests` and the Sindri API tests, and rerun the PR body's classes, then ready it. |
| none | `feat/f1p-b2` | `3f5b7c947` | **WIP, no PR** | See "F1P-b2" below. |
| none | `feat/ci-5` | `c0eeba524` | **WIP (`wip:` commit), no PR** | See "CI-5" below. |

Merged today: #556 (CI-0b), #559, #562, #563 (CI-2), #572, #575 (F1P-a), #580 (F1P-b1), #582, #585, #587, #588 (CI-3) and
#595 (CI-4). #549 is merged too; it was superseded by #556.

### CI-6, #602 (green)
- **Scope:** the `BuildCodeIndex` slice (incremental plan, `StructureWriter`, syntax edges, publish, cancel) and the facade reads `Status`, `Heads`, `FindSymbols`, `FileSymbols`, `Callers` and `Callees`. `Similar` and `Neighbors` answer `SND-CODEIDX-013` until the clones and embeddings layers exist; the spec has no "layer not built" code.
- **Checks:** the build has 0 warnings. 85 unit and integration tests pass. The heavy `BuildThisRepoHeavyTests` passes: 10,298 files, 50,165 symbols, 752,576 edges, 102 s, and `Callers(BuildPlanner.Plan)` has 64 edges.
- **Left for later units:** the 2 h build ceiling isn't enforced (there's no error code for it); bodies, MinHash and band keys aren't stored; `ProbeLayers` reports nothing buildable.

### F1P-b2 (WIP, `feat/f1p-b2` 3f5b7c947, on main 8bbee9420)
- **Done:**
  - The map events `context.map.held`, `published` and `hold.dropped`, with `refreshed` gaining `Held`.
  - The refresh gate through `MapPublishDecision`, and the `PublishContextMap` job (190 means succeeded and drop the hold; 046 means failed and keep it).
  - The tick: the hold read, publish backoff, `MappedHeads` move check, and one alarm.
  - `NoIndexOutlooks` registered, PublicAPI and `docs/sindri/context.md`.
  - Goldens regenerated (events catalog, `commands/catalog.json`, `jobs/bad-kind.json`) and the Ui catalog pin moved from 67 to 68.
- **Green (builder's final report, on 3f5b7c947):**
  - `Aw.Sindri.Tests` and `Aw.Sindri.Ui.Tests` build with -warnaserror: 0 warnings.
  - The 3 service classes pass 54/54.
  - CoreApiGolden, JobsApiGolden, HostCommandCatalogContract, CatalogRules and EngineRow pass 80/80.
  - The Ui contract classes (FakeSindriApiContract, SindriApiClientContract, GoldenContract) pass 339/339.
  - Format is clean, and check-deps is ok.
- **Golden changes:** a `PublishContextMap` row in `commands/catalog.json`, the public `context.map.published` row (`held` and `hold.dropped` aren't public), the job kinds in `jobs/bad-kind.json`, and the Ui pin moving from 67 to 68. No golden carries the `refreshed` payload.
- **Main:** merged only through 8bbee9420, so merge main before the PR.
- **Missing (the spec row's proof):** the §11 **integration-lane** cases, Host tests with fake outlooks and a fake clock. Only unit cases exist.
- The first commit d989a069d mixes tests and implementation; that's accepted, so don't rewrite it.
- Its gate must run `Aw.Sindri.Ui.Tests`.
- Builder digest (this box only): `~/.agentic-workflow/digests/f1p-b2-builder.md`.

### CI-5 (WIP, `feat/ci-5` c0eeba524, on main 6a674c43d)
- **Done:** `Fnv1a`, `CloneTokenizer` and `Clones` (MinHash/LSH) in Core; the `package.json`, `.csproj` and `Directory.Packages.props` readers in Infrastructure, registered in `AddCodeIndexInfrastructure`.
- **Green:** the build has 0 warnings, and 49 of 50 CI-5 tests pass.
- **The 50th test:** `Signature_is_a_known_vector` is now pinned to a vector cross-checked in Python, but not rerun.
- **Not run:** that rerun of the five CI-5 classes, `dotnet format`, `check-deps` and the lock-file check.
- The one `--no-verify` commit is a main merge, for the ci.yml:334 false positive only.
- Builder digest: `~/.agentic-workflow/digests/ci-5-builder.md`.

## Worktrees (`~/repos/aw-wt/`, this box only)

Every one is committed and pushed: `git status` shows nothing, and the local head equals the `jfork` branch.

| Worktree | Branch | Head | Note |
|----------|--------|------|------|
| feat-ci-6 | feat/ci-6 | 440c72cfd | #602 |
| feat-s9a | feat/s9a | 325440a29 | #604 |
| feat-f1p-b2 | feat/f1p-b2 | 3f5b7c947 | WIP |
| feat-ci-5 | feat/ci-5 | c0eeba524 | WIP |
| feat-ci-4 | feat/ci-4 | dd9c66045 | #595, merged; can be swept |
| fix-ctx-red | fix/context-gate-reds | 5dcffb9fc | #585, merged; holds #587's commit, already on main; can be swept |
| fix-scrub-2 | fix/scrub-credentialed-url-tests-2 | 57fa6af00 | #572, merged; same, can be swept |

## Builders

All are stopped. None has a heavy job running or queued.

| Builder | Work | Where it stopped |
|---------|------|------------------|
| ctx-ci-6 | CI-6 | finished green; #602 |
| ctx-s9a | S-9a | finished green on a9682529f; #604 |
| ctx-f1p-b2 | F1P-b2 | WIP at 3f5b7c947 (above) |
| ctx-ci-5 | CI-5 | WIP at c0eeba524 (above) |
| ctx-ci-4b, ctx-ci-4 | CI-4 | finished; #595 merged |
| ctx-ci-3, ctx-ci-2, ctx-f1p-a, ctx-f1p-b1 | CI-3, CI-2, F1P-a, F1P-b1 | finished; merged |

## Blocked, and on whom
- **The owner:** #602 and the readied #604 need gates and merges. The owner may merge them directly, as tonight.
- **Precision and recall (owner decision, spec Q3):** CI-4's resolver is at 70% precision (26/37) and 87% recall (26/30) on the signed-off 50 pairs. This decides whether callers and callees count in the scoping recall bars.
- **A spec edit (owner):** the CI-4 row says Roslyn `VersionOverride`, but it was dropped because it pinned the same 5.9.0 as the central pin.
- **The heavy lock (meta, then the owner):** a job waiting for a box-wide slot keeps holding its session slot, and the locks have no FIFO. One CI-6 build held this lane's session slot for about 2.5 h while api retook both box-wide slots. On memory under about 14 GB free, slot 3 never opens. Meta has put it to the owner.
- **A scrubber false positive on main:** `.github/workflows/ci.yml:334` (`${GITHUB_TOKEN}` from #570). Engine is fixing it. Until then, a main merge may use `--no-verify` for that hit only, with the reason given in the commit message.

## Next steps, in order
0. **Fix the failure on main from #595 (CI-4), first.** `Aw.Architecture.Tests` has an ARCH-INFRA-001 failure on two public CodeIndex types; ui reported it, and the gate missed it. That rule keeps Infrastructure types from being public. Make the two types internal, or move them to Core if other projects need them, fixing PublicAPI to match. Run `Aw.Architecture.Tests` with `--filter-class`, open a small fix PR, and ask for a direct merge. Check whether #602 (CI-6) also needs the same fix.
1. **Gate and merge #602 (CI-6).** It unblocks CI-7, CI-8 and CI-9.
2. **#604 (S-9a):** merge main, rebuild, rerun the scoped classes, and ready it.
3. **F1P-b2:** add the §11 integration cases (red first) and merge main, then open the PR (its gate runs Ui.Tests). It unblocks F1P-c and CI-9.
4. **CI-5:** rerun the five classes, run format, check-deps and the lock check, then open the PR. Clones and deps plug into CI-6 once merged; nothing waits on CI-5.
5. **CI-7** (graphify layer) and **CI-8** (Ollama embeddings) can start once CI-6 merges. They're independent of each other.
6. **CI-9** (the BuildCodeIndex job, `code-index-main-move`, `CodeIndexOutlooks`, which replaces `NoIndexOutlooks`): needs CI-6, F1P-b1 (merged) and F1P-b2 merged.
7. **CI-10a** (HTTP/CLI reads) needs CI-9. **CI-10b** (Health and recovery) and **F1P-c** (words; needs F1P-b2) follow, then **CI-11** (owner-check, nightly heavy suite).

**Critical path to S-9:** CI-6 → (F1P-b2) → CI-9 → CI-10a → CI-11 → **S-9**. S-9 is the real `CodeIndexSymbolSource` behind S-9a's port; the lens fills callers and callees, and the `code-index` source row goes live. S-9a has to be merged first.

**The web-app index build:** the first time a person can build an index is CI-6 (the heavy test builds a repo through the facade, with no host). Building web-app's index in the running app needs **CI-9**: register web-app as a profile repo, and a commit to its main starts `index:<repo>` within 2 minutes (the CI-9 row's in-app proof). For an earlier look, point CI-6's heavy test or the facade at a web-app checkout. It's TypeScript-heavy, so callers and callees there come from graphify (**CI-7**). Without CI-7, only structure (files and symbols) is indexed for TS.

## Box notes
- **Builders:** Sonnet, briefed with `~/.agentic-workflow/joi-fairshare-agentic-workflow/plans/code-index/builder-common.md`.
  - Builders don't push.
  - Long tests run in the background with a lane or class filter.
  - Builders kill their own runs when they stop.
  - The 200k context guard is advisory; they stop near 600k.
- **Hooks:**
  - `block-push-main` refuses a command containing both `push` and `main`, including a branch name or `--base main`, so push separately.
  - `block-destructive` refuses recursive force deletes and forced worktree removal, even inside text.
- **Post-merge checks:** run them on `origin/main` itself, never on the branch worktree. #572 merged without its second commit, which was fixed by #587.
