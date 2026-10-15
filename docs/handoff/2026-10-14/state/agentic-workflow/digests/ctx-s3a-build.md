# Handoff: stage S-3a (Sindri context lane), step 3 (implement) not yet begun

Owner role: stage owner of S-3a and S-3b, under the lead's brief (context spec "Parallel delivery", items 1–6;
/specToProvenPR steps 1–9 for one stage). Report one line to the lead (`SendMessage to: main`) at each step change,
with tokens. Token budget 3M per stage; ~0.35M used so far (prep).

## Where things are

- Worktree `/Users/thor/repos/agentic-workflow/.claude/worktrees/ctx-s3a`, branch `feat/ctx-s3a`, created from
  5c4a4593 (S-1A's Core contract). No commits of mine yet. Draft PR later with `--base feat/ctx-s1a`. Port 7497.
- **Merge only, never rebase** onto `feat/ctx-s1a`; the lead says when (S-1A's Host wiring). API P1 (#181) merged into
  main as 1027a13f; S-1A is merging main and will base its PR on main.
- Proof dir `~/.agentic-workflow/jthorare-agentic-workflow/proof/ctx-s3a/`: `plan.md` (scope, Core types, codes,
  build order, lead decisions), `verification-plan.md` (created 2026-10-12T15:23:55Z, predates code — keep it),
  `owner-check.md` (draft). `../ctx-s3/` holds the pre-split notes, the seed for S-3b (port 7503, stacked on S-3a;
  plan it while S-3a is in review).
- Spec copy: `git cat-file -p origin/main:docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`
  (§3, §6.1, §6.2, §8.5–§8.7, Phase 3 Files line). API spec §3.7, §4.2, §6 (same command for `…api-surface-design.md`).

## Lead decisions (all final)

1. S-3 split: S-3a = store, keys, overrides, import/export + protected gate, map status, doctor `context:map` and
   `context:retention`, purge service. Codes SND-CONTEXT-030–034 (030 no map; 031 map file invalid w/ key path,
   all-or-nothing; 032 reference to an undefined node; 033 export path outside `context/exports/`; 034 file changed
   since its hash was confirmed). S-3b gets 035–039.
2. Repo scope literal (§3): a session sees only nodes reached by `implements` edges whose file evidence
   (`repo:<repo>/<path>`) is in its `X-Sindri-Repo`; none → SND-CONTEXT-003. Map schema must allow `implements`
   (CodeUnit → Feature|Surface|Action) with file evidence; the fixture map carries some. Session `map` status = scoped
   counts; session export = scoped nodes + edges between them.
3. Import is `protected`, shaped exactly like `profile approve` on this base (`human` + terminal, content hash,
   `confirmed`), with a slot for the challenge. **The shared challenge isn't on the base** (only SND-RECIPE-009 /
   SND-WF-013 codes). Say so in the PR body and the "S-3a" docs section: the import adopts it when the API lane ships it.
4. YAML via YamlDotNet [18.1.0] parser + representation model only (no reflection serializer); Aw.Context.* stay
   IsAotCompatible; no deps.yaml edge unless check-deps / an arch test asks.
5. Order: Core → Infrastructure → Features now; Host routes/verbs/doctor/purge after merging S-1A's Host wiring.

## What the base has (5c4a4593)

- `src/Context/Aw.Context.Core`: `IContextEngine` (CheckSettings, Query, Sources, Refresh), `ContextErrors` (001–011,
  020; `All` list for ErrorCatalogue.Validate), `Engine/ContextCaller(Kind, Repo, Client)`, `ContextCallerKind`
  (Human/Session/Service), `ContextScope(Settings JsonElement?, Repos, Caller)`, `ContextRepo(Name, Path)`,
  `Results/Provenance` (deterministic/model/human), `Results/Trust`, `Sources/RecordKey` (Vogen + StringIdJsonConverter
  pattern to copy), `SourceRef`, `VogenDefaults.cs`. Features/Infrastructure are empty scaffolds;
  Infrastructure `ContextSettings(StateDirectory)` from `AW_STATE_DIR` (S-1A will likely move it to
  `$AW_SINDRI_HOME/context`). No migrations or store yet → my migration is `0001_s3a_graph.sql` (renumber when merging
  S-1A's own context migration).
- Store pattern to copy: `src/Sindri/Aw.Sindri.Infrastructure/Ledger/{LedgerDatabase,SqliteLedger,CountMapper}.cs`
  (MigrationRunner + EmbeddedMigrations, `OwnerOnly.EnsureDirectory/RestrictFiles`, SqliteQueries.List/One,
  SqlStatement binds, Translate errors); csproj `EmbeddedResource … LogicalName="<prefix>%(Filename)%(Extension)"`
  (see Judge Infrastructure csproj). Kernel: `Result<T>`, `ErrorDefinition.With`, `IService<TReq,TRes>`, `ITypedId`.
- Rules: `.agents/rules/csharp.md`, `csharp-testing.md` (red commit first: test + records + parameterless stub that
  throws NotSupportedException; one Lane trait per test; file ≤300 lines, method ≤40, CC ≤10, ≤5 params; PublicAPI
  files from RS0016; `///` on public members; ConfigureAwait(false) in libraries; typed ids via Vogen + user
  JsonConverter; no reflection JSON; fakes + contract base per port; golden files).
- Fixture: `tests/Aw.Context.Testing/Fixtures/split-panes/` (probes.json keys: feature:layout.split-pane(.orientation),
  feature:layout.pane-resize, feature:tabs.tab-manager, action:tab-manager.restore-layout, thing:pane-layout,
  surface:command/split-vertical, surface:keybinding/ctrl+shift+d, surface:setting/paneResizeStep). Resize's action
  only `writes` pane-layout (so S-3b's cascade can't reach it); restore-layout `reads` it.

## Step rules still ahead

Hand tests only with `AW_SINDRI_HOME=<proof dir>/home-s3a`, port 7497, fixture profile approved via
`scripts/sindri-prove.sh human profile approve`; heavy jobs under `~/.agentic-workflow/bin-r1b/heavy`; `df -h /` before
builds (pause <5 GB; 8.1 GB at handoff). `/review` loop ≤5 runs to 0 blocking + 0 issue. Paste local merge-gate output
in the PR. Step 9: `scripts/denylist-check.sh ~/.agentic-workflow/denylist.txt <files>` (never open the denylist). No
`rm -rf`; zsh arrays. After step 9: "ready #N" to the lead and stop.

## Note (2026-10-12, second owner)

A fresh S-3a owner started from this digest and stopped before any work: the 200K-token context guard fired on the
first tool call (~240K), because the session's fixed load (system prompt, agent catalogue, skill list) is already past
200K. No files changed, no commits, nothing run in the worktree. This digest and the proof-dir plans are still current.

## Progress (2026-10-12, second owner) — step 3, library layers done

Pushed feat/ctx-s3a @ 4b717e22 (commits: a821ba9d Core+Infra, 4e1c6fd6 contracts, ef72382b red Features,
3c0eb4f8 Features, 377f16ad arch fixes, 4b717e22 arch lock). Solution builds clean; Context 127, Testing 69, Arch 101 pass.
Lead decisions since: one PR at size L (owner), layered commits; `AW_HEAVY_SLOTS=3` before heavy; facade is
`IContextMap` (Core/Facade) implemented by Features `ContextMap`; on S-1A merge make `IContextEngine : IContextMap`,
add `IContextMap map` ctor param to S-1A's `ContextEngine` with one-line delegations; Sindri uses IContextEngine only.
Orphaned overrides kept (not 032), export lists their targets; MapVersion is int (RS0041), checked > 0.
Key types: Core Graph/ (NodeKey, EdgeRules, EntryKey, KeyMigrations, GraphSnapshot, RepoScope, DecisionRow.ToKey()),
Overrides/ (MapOverride.ToId(), OverrideApplier, RejectionIdentity), Map/ (MapDocument, MapDocumentValidator,
MapAccess gate/scope/export, ports IGraphStore/IMapFileReader/IExportStore, MapRetention). Infra: State/ (settings from
AW_SINDRI_HOME/context, OwnerOnly, Worktrees), Graph/ (migration 0001_s3a_graph.sql, SqliteGraphStore), Map/ (YamlToJson,
MapFileReader, FileExportStore). Features: 7 slices + ContextMap. Tests: tests/Aw.Context.Tests/{Map,ImportMap,ExportMap,
MapStatus,MapHealth,PurgeMap}; Testing/Map (fakes, contracts, SampleMap, FixtureMap with Json/IndentedJson).
Line count (step 7 report): ~895 meaningful C# + 92 SQL at Infra commit; recount at step 7.
Left: wait for lead's "merge S-1A" → merge origin/feat/ctx-s1a (renumber my migration if S-1A has one), Host routes
(GET /context/map, GET /context/map/export, POST /context/map/import protected + catalog rows), verbs
`context map import|export|status`, doctor context:map/context:retention, context-purge IScheduledService, ledger
context.map.imported, docs "S-3a" sections; then steps 4–9 (hand tests per verification-plan.md, evidence, review loop).
Helper script for PublicAPI sync: scratchpad api.sh (RS0016 add / RS0017 remove).

## Progress (2026-10-12 late) — steps 4–8

PR #196 (draft, base feat/ctx-s1a), head c167bf24. Merged S-1A a2d45fce, 1f0fde7c, 8098ea1e, de15b379 (no rebase).
Hand test J1–J3 passed earlier (evidence.md + evidence/j1-3.txt in proof dir; prove script uses port 0 → noted).
Review run 1: 13 issues → fixed (26253691..092e0f77). Run 2: 2 blocking (stale aot expected.txt) + 3 issues → fixed
(run-2 commit, 1b4e439a merge, c167bf24). State + resolutions: ~/.agentic-workflow/jthorare-agentic-workflow/reviews/196.json.
Owner rule: no PR comments; review replies go in evidence.md "review replies" section + PR body.
Left: review run 3 (stop at 0 blocking/0 issue, max 5); re-run hand-test spot checks on the new binary (proof/ctx-s3a/bin,
non-test source changed after the hand test); local gate script scratchpad gate.sh (judge before scorer; ui-evidence
browser test flaky under load → rerun alone); PR body: gate output, review summary, arch-green-at-377f16ad note,
shared-challenge note; evidence.md review-replies; step 9 denylist; mark ready; report "ready #196".
