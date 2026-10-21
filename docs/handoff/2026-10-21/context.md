# Context lane handoff (2026-10-21, ~04:30Z)

Lane: sindri-context. Owner focus: goal 1 is context retrieval wired to the code index, and goal 2 is the index and the map kept in sync. X1 (explain) is paused.
Every builder stopped at the shared usage limit (~04:00Z). Each worktree was committed as a checkpoint and pushed, so nothing lives only on the old box.
Spec: `docs/superpowers/specs/2026-10-19-sindri-code-index-design.md` rev 4 on trunk (§14 phases, §6.4 publish-together). Builder briefs: `~/.agentic-workflow/joi-fairshare-agentic-workflow/plans/` (`code-index/builder-common.md`, `wave1.md`, `f1p.md`; `explain/x1a-brief.md`). These live on the old box: copy them over, or rebuild them from the spec rows.

## The chain

- **Goal 2 (sync):** F1P-a and F1P-b1 (in parallel), then F1P-b2 (needs Ui.Tests in its gate), then F1P-c (api builds its status surfaces). The publish gate turns on with CI-9.
- **Goal 1 (retrieval):** CI-1, then CI-2..CI-5 (stacked on feat/ci-1), then CI-6 (the BuildCodeIndex slice), then CI-9 (job and service, plus CodeIndexOutlooks), then CI-10a reads (api, #547), then **S-9** (the Host adapter over the real facade).
  - S-9 depends on CI-10a: it stacks on #547 and reuses api's `FakeCodeIndex`. S-9a is the context half, built on a fake.
  - CI-7 (graphify) and CI-8 (Ollama embeddings) are not needed for C# callers.

## Branches

| Unit | Branch | PR | Head | Pushed | State | What's left |
|---|---|---|---|---|---|---|
| CI-0b profile keys | feat/ci-0b | #549 | 1514fdf2e | y | ready sent; meta re-gating on ccddefc (aot-smoke serve-10s flake) | merge; don't move the head while it gates |
| CI-1 scaffold and contracts | feat/ci-1 | #542 | c05579b88 | y | review fix (coverage on Callers/Callees rows and answers) coded; CodeIndex unit lane green (54); trunk ccddefc5b merged | **#542 still needs:** solution build `-warnaserror`, `Aw.Architecture.Tests`, scoped format, `check-deps` on c05579b88 (I started them; result unknown at handoff). Then a fresh re-review of the coverage change, `ready #542 <sha>`, and send the sha to the api lane (the #547 builder waits on it to carry `coverage`). The red commit a153a3ca8 carries an extra Claude-Session trailer; it can't be amended, so mention it in the PR |
| CI-2 repo snapshot | feat/ci-2 | none | 474edf366 | y | checkpoint: InputRules, GitRepoSnapshot (Infrastructure/Snapshot), ArrangedSnapshot; was building | build, RepoSnapshotContract over the fake and a temp repo, gates, PR (base feat/ci-1) |
| CI-3 index store | feat/ci-3 | none | 1bf3caf35 | y | red BuildPlanner/StructureStamp stubs only; **the real SqliteIndexStore, fake and contract are in `.handoff-drafts/impl/`** (the CI-2 drafts are mixed in there too) | move the drafts into place, delete `.handoff-drafts/`, red then green, IndexStoreContract (fake and SQLite), gates, PR. Approved choice: `IndexDirectory` added to CodeIndexStateSettings (a CI-1 contract change) |
| CI-4 parser and resolver | feat/ci-4 | none | 8658f6506 | y | CallResolver green (2520048e8, with a Sonnet trailer; can't amend); parser walker split for the analyzer caps, quarantine port, Roslyn VersionOverride in the checkpoint; drafts in `.handoff-drafts/` | build, golden symbols and edges, hostile-parse tests, print precision and recall on the 50 pairs, gates, PR. Spec gap: newest Roslyn = 5.9.0 = the central pin, so VersionOverride pins the same version |
| CI-5 clones and deps | feat/ci-5 | none | 9d8050160 | y | checkpoint: Similarity (MinHash/LSH), IManifestReader, Deps readers | build, known vectors and a near-duplicate pair, gates, PR |
| F1P-a store half | feat/f1p-a | none | b3b485d1b | y | checkpoint: migration 0010, MapHold/MapPublishState, Hold/Release/HeldMap/ShowPublishStatus features, MapPublishStoreContract | PublicAPI from the build output, build, unit and integration, gates, PR (base trunk) |
| F1P-b1 pure half | feat/f1p-b1 | none | 96f278dd2 | y | red e89422817 then a checkpoint of MapPublishDecision, IndexOutlookRules, MappedHeads, NoIndexOutlooks | build, unit lane, gates, PR |
| S-9a context side | feat/s9a (the push to feat/ctx-s9a was rejected) | none | 9c52457ec | y | ICodeSymbolSource, the lens and file map reading it as of the map's commit, docs; drafts in `.handoff-drafts/` | finish the doc section after S-CL, build, gates, PR (base trunk) |
| CI-10a reads (api lane) | feat/sindri-codeidx-reads | #547 draft | api's | y | stacked on feat/ci-1 | waits for the CI-1 coverage sha; retarget to trunk after #542 merges |

## Notes the next units need

- **CI-9:**
  - Owns the repo-section merge of the index keys (union the lists, replace the scalars; documented by #549).
  - Rewords the profile doc's `url` row: a bad URL is the profile's own error, and SND-CODEIDX-005 comes only on the code index's re-check.
  - Must register the real ICodeIndex ahead of api's `UnbuiltCodeIndex` singleton, or replace it.
  - Flips the Sindri→CodeIndex compose edge to current.
  - Owns the stale, building and waiting status overlays (with CI-10b).
  - Replaces F1P-b2's NoIndexOutlooks, which turns the gate on. Re-pins the services goldens and the aot-smoke listing (#496).
- **CI-4:** the 8-candidate cap and "k of N" are tested by Core unit tests on synthetic candidates (the largest fixture group is 4).
- **CI-7:** graphify's venv is Python 3.12 (#525); bind it read-only after the `~/.agentic-workflow` mask.
- **CI-8:** the embedding URL is a base URL (the profile check refuses a path, query or fragment).
- **The owner owes a read of `tests/fixtures/code-index/quality-sample/pairs.json`** (50 caller/callee pairs, line numbers at commit b6f56440). CI-4 prints its precision and recall against them.

## Rules that bit tonight

- Prefix every heavy command with `AW_SESSION_HEAVY_SLOTS=3 MSBUILDDISABLENODEREUSE=1` (meta approved this; it's in builder-common.md).
- Builders run on Sonnet or Haiku only. Commits end with only the Opus Co-Authored-By trailer.
- Builders don't push; the lead merges trunk at push time.
- Merges go only through meta's batches.

## Next step

After the usage reset, on the new box:
1. Finish #542's checks, re-review it and send `ready`.
2. Send the CI-1 sha to the api lane.
3. Start one Sonnet builder per checkpointed unit ("continue from the checkpoint", plus its brief and the row above), up to what memory allows. Priority: F1P-a and F1P-b1 (they unlock F1P-b2), then CI-2, CI-3 and CI-4 (they unlock CI-6), then S-9a, then CI-5.
