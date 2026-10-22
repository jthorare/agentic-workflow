# `Aw.Context` — the context engine package (C#)

The context retrieval engine (spec `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`), a tier-1
package with no Sindri imports. Sindri reaches it only through the `IContextEngine` facade in `Aw.Context.Core` and
composes it through the `compose` edge. The user's guide is `docs/sindri/context.md`. Each stage adds its own section.

## S-1A: the package, repo-docs, the query pipeline

### Shape

| Project | Holds |
|---|---|
| `Aw.Context.Core` | `Facade.IContextEngine` (`CheckSettings`, `Query`, `Sources`, `Refresh`) and its records (`Engine.ContextScope`, `ContextQuery`, `ContextCaller`, `ContextSettings`); the source contract (`Sources.IContextSource`, `IChangeFeed`, `SourceRef` with `Canonical`, the typed ids `SourceId`, `ExternalId`, `SourceVersion`, `RecordKey`, `SourceCursor`); the result records (`Results.*`, spec §8.4); ports `Trust.IEgressScrubber`, `Trust.IIngestFilter`, `Trust.IScrubberFactory` (S-2W), `Docs.ISourceCatalog`, `Docs.IDocIndex`, `Models.IModelPort` (README X18: every model call; adapted in Sindri's Host over Judge's provider port by the stage that first calls a model); pure rules: `ScopeRules`, `SettingsReader`, `QueryRules`, `RankFusion`, `Packer`, `TokenEstimate`, `CitationStates`, `UntrustedFence`, `Permalinks` |
| `Aw.Context.Features` | `ContextEngine` (the facade: one line per method, dispatching to a slice) and the slices `QueryContext`, `SourcesStatus`, `RefreshDocs` |
| `Aw.Context.Infrastructure` | `Store.ContextDatabase` (context.db), `Docs.SqliteDocIndex` and `Docs.RepoDocsSource` (FTS5 over scrubbed chunks), `Git.GitCli`, `Scrubbing.IngestSanitizer`, `Scrubbing.RedactionScrubber` (both scrub ports over `Aw.Redaction`) and `Scrubbing.RedactionScrubberFactory` (S-2W) |

`context.repos` must name profile repos, and with a `context:` section every repo in scope needs a name the engine can
use (`ContextRepoName`): otherwise `SND-CONTEXT-001` at validate and approve (`IContextEngine.CheckSettings(section,
repos)`, through `SettingsReader.Check`); an unknown `context.repos` name is also 001 at query time. A profile without
`context:` is never refused for this, and `ScopeRules.Usable` leaves an unusable name out of the scope.

**Placement and limits (README X61).** `SourceDescriptor` carries two init properties so the source contract doesn't
assume a local process: `Placement` (`SourcePlacement.Local` or `Shared`, default `Local`) and `Limits`
(`SourceLimits`: requests per window and max concurrency, each optional; null means none). CTX D6's "no rate limiting"
is a property of local sources, which declare no limits; it is not a global rule. Nothing enforces limits yet. The
contract base checks that the placement is defined, that any declared limits are well formed (at least one, positive,
a request count with its window), and that a `Local` source declares none. repo-docs is `Local` with no limits.

`deps.yaml`: Context → Kernel, KernelData and Redaction (Infrastructure only) are `project` edges. Sindri → Context is a
`project` edge (Sindri's Features and Host use Core) plus a `compose` edge (Sindri's Host calls `AddContextFeatures` and
`AddContextInfrastructure` in `Composition`, nothing else: ARCH-COMPOSE-001).

**Sindri's side.** `SindriProfile.Context` is a `JsonElement?`, left out of the canonical JSON when absent. Validate and
approve call `IContextEngine.CheckSettings`. The slice folder `Aw.Sindri.Features.ContextCalls` holds:
- `ContextScopes`, the one mapping from the approved profile (the read-only port `IApprovedProfile`, so the query
  handlers hold no write port: ARCH-CQRS-002) and `ICallerContextAccessor.Current` to `ContextScope`. The engine's
  client comes from the actor's label: `cli` and `mcp` are themselves, `ui`, `prover` and plain HTTP are http, services
  are service.
- The queries `QueryContextRequest` → `ContextResult` and `GetContextSourcesRequest` → `SourcesView`.
- The scheduled service `ContextDocsRefreshService` (`context-docs-refresh`, event `context.sources.synced`).

Every surface resolves those services, MCP included (S-1M). `ContextCalls` is deliberately one slice folder for every
context use case: `ContextScopes` is shared, it can't move to Sindri Core (only Features and Host may reference another
package's Core, DEPS-PROJ-008), and ARCH-SLICE-001 forbids sharing it across slice folders. Later stages add their
context services here. The routes are `GET /sindri/context/query` and
`GET /sindri/context/sources`. The CLI verbs are `Host/Commands/ContextVerbs`, with text output in `ContextText`.
`Host/Catalog/ContextRows` holds `SyncSources`, `BuildContextMap` and `RunContextBacktest` as ready `job` rows (`JobRows.Job`, request types `SyncContextSourcesRequest`, `BuildContextMapRequest` and `RunContextBacktestRequest`, routes `POST /sindri/context/sources/sync`, `POST /sindri/context/map/build` and `POST /sindri/context/eval/backtest`); the `context-sync` and `context-rebuild` services below run the same work on a cadence.

### Query pipeline (Phase 1, text shape)

1. `SettingsReader.Read`, then `ScopeRules.Resolve`: sessions are gated (020), and a session is scoped to its repo (003).
2. `QueryRules.Check`: one shape. Text becomes at most 16 lower-case terms. The budget is capped. Anchor shapes are 003
   until the graph exists.
3. Every source in `ISourceCatalog.For(settings, repos)` is searched in parallel under the deadline. An error or a
   missed deadline makes that source degraded (010); all of them failing is 011.
4. `RankFusion.Fuse`: hits sharing a canonical key merge, keeping each source's best rank. Each source is re-ranked
   densely, then RRF with k = 60 runs across sources.
5. The top 20 are fetched in turn until the deadline (what isn't fetched in time isn't shown), cleaned whole by
   `IIngestFilter` (sanitize, collapse titles, scrub; line-preserving), then cut to 64 KB. The sanitizer drops inline and
   HTML images and reduces reference-style ones to their text, so no image survives ingest wherever its definition sits, and removes
   remote reference definitions also inside block quotes and list items (one-token destination, optional title).
6. One batched `Versions` call per source, each with only its own references, gives each citation its state.
7. `ResultAssembly`: one `record` item and one citation each. Every field, the citation's key and ids included, is
   scrubbed again on egress (a blocked field is `[REDACTED:blocked]` alone). The title (at most 200 characters) and
   the text are fenced as untrusted, and the items are packed into the budget.

No model is called on the query path.

### Repo-docs

- **Index.** `SqliteDocIndex.Refresh` lists the tree at HEAD (`git ls-tree -r -z`) and matches the globs (`Glob`). It
  reads changed blobs (their size first: over 1 MiB keeps a row without chunks and counts as skipped), cleans each
  document whole and line-preserving (a redacted span keeps its newlines), then chunks the clean text by heading (at
  most 40 lines or 2,000 characters; `DocChunker`), so a secret or a hidden comment that crosses a chunk boundary
  can't slip through. Only then does one short transaction remove stale docs, write new ones and move the cursor
  (`source_cursor`). A document the scrubber blocks keeps its `doc` row with `blocked = 1` and has no chunks; one git
  can't read on this run is skipped and keeps its earlier rows. A path the ids can't carry (`RepoDocs.TryKey` and
  `TryChunkId` refuse it: a control or format character, or near 1 KB) is never indexed and counts as skipped on every
  refresh. A repo git can't read records `SND-CONTEXT-010` as its
  `last_error`, and the refresh reports it in its row.
- **Source.** `RepoDocsSource` searches with each term as a quoted FTS5 phrase (`DocSql.Phrase`), OR-ed and ranked by
  `bm25`, filtered to the repos in scope. A record is a chunk, `<repo>/<path>#L<a>-L<b>`, at the blob sha. The
  canonical key is the document, `repo:<repo>/<path>`. Fetch reads the blob from git at the cited sha, cleans it whole
  (as the index did) and cuts the line span; Versions makes one tree listing per repo.
- **Git.** `GitCli` passes arguments through `ArgumentList` with no shell, removes the caller's `GIT_*` variables,
  accepts shas only as `^[0-9a-f]{7,64}$` (never an option or a ref expression), and kills a process that misses 30 s
  or the caller's token. `cat-file -e` exiting 1 is a missing object (NotFound, `003`); any other failure is `010`.
- **Logs.** No log line carries question or record text. Kernel telemetry keeps ASP.NET's request lines
  (`Microsoft.AspNetCore.Hosting.Diagnostics`, which hold the `?q=` query string) at Warning and above, in every host.

### Store

`$AW_SINDRI_HOME/context/context.db`; Context reads `AW_SINDRI_HOME` (else `$AW_STATE_DIR/sindri-app`) itself, from the
environment Sindri's `Composition` passes. Migrations are embedded, named `NNNN_<stage>_<name>.sql` and numbered on the
branch (spec "Shared files"). This stage adds `0001_s1a_repo_docs.sql`, which creates `source_cursor`, `doc`, `doc_chunk`,
`doc_chunk_fts` (FTS5, external content, kept in step by triggers). The directory is 0700. The file and its `-wal` and
`-shm` are 0600, restored after every write. `Readable` never creates the file: a source with no store answers no hits,
and the status reads "never refreshed".

### Errors

| Code | Kind (HTTP) | CLI exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-001` | Invalid (400) | 2 | The profile's `context:` section is invalid; the message starts with the key path |
| `SND-CONTEXT-002` | Invalid (400) | 2 | A configured source isn't set up, or its secret pointer doesn't resolve (later stages) |
| `SND-CONTEXT-003` | NotFound (404) | 1 | Unknown anchor, or a record or decision linked only to nodes outside a session's repo scope (the same answer). When the graph has near misses, the problem body also has the top-level member `suggestedAnchors` (an array of at most 5 node keys, in scope for the caller; the detail text still says `try …`) |
| `SND-CONTEXT-004` | Unavailable (503) | 2 | The store can't be opened or migrated (the message keeps the AW-DATA code) |
| `SND-CONTEXT-005` | Invalid (400) | 1 | A map file or an output path inside a git worktree (links resolved) |
| `SND-CONTEXT-006` | Invalid (400) | 1 | The question is invalid: no words, two shapes, depth or budget out of range |
| `SND-CONTEXT-010` | Unavailable | — | One source is unavailable: a `degraded` entry, never the answer's error |
| `SND-CONTEXT-011` | Unavailable (503) | 2 | Every source is unavailable |
| `SND-CONTEXT-020` | Forbidden (403) | 1 | Session reads are off (`context.sessions.enabled: false`), on every transport; also a session asking for a human or service job (map import, preview, build, export). Defined by S-1A (spec §3); S-1M maps it to the MCP tool error |

### Tests

`tests/Aw.Context.Tests`:
- Every port's contract suite from `tests/Aw.Context.Testing/Engine`, bound to its fake and its real adapter:
  `ContextSourceContract<T>` (`FakeContextSource` unit; `RepoDocsSource` integration), `DocIndexContract<T>`
  (`FakeDocIndex` and `SqliteDocIndex`, both integration: the fake reads the fixture repo's files),
  `ScrubberContract` (`FakeScrubber` and `RedactionScrubber`, unit), `SourceCatalogContract<T>` (`FixedSourceCatalog`
  unit; `RepoDocsCatalog` integration), `ChangeFeedContract<T>` and `ModelPortContract<T>` (fakes; their adapters come
  with the stages that call them) and `ContextEngineContract<T>` (the real engine over the fakes, and
  `FakeContextEngine`, which Sindri's tests use).
- `ContextDatabaseTests`: FTS5 in the bundled `e_sqlite3`, the file modes, and migrations named for their stage.
- `RepoDocsTests`: a planted secret never reaches `doc_chunk`; a key or hidden comment across a chunk boundary reaches
  neither the index nor a fetch; a fetched span matches the blob's lines; oversized and unreadable docs; git's exit-code
  mapping; an edit reads as a new version.
- The pure rules (unit) and the facade over the fakes (`QueryContextTests`, `SourcesStatusTests`).

In `tests/Aw.Sindri.Tests/ContextCalls`:
- the routes through the real pipeline: human, session gate, scope, foreign Origin, codes, and no question or record
  text in any log line;
- the caller mapping, and validate and approve refusing a bad `context:` section or repo;
- the CLI's argument parsing and text output.

## S-1M: the MCP endpoint, `mcp register`, the doctor rows

### Where the code lives

- `src/Sindri/Aw.Sindri.Host/Mcp/`: the SDK-facing code, like `Host/Endpoints` (Features takes no SDK or ASP.NET
  types). `McpEndpoint` registers the server (`ModelContextProtocol.AspNetCore` 2.2.0, `WithHttpTransport(Stateless =
  true)`), maps it at `/sindri/mcp` after the `LoopbackGuard`, and adds `UseMcpCaller` before the caller middleware:
  any `Origin` is a 403 (`SND-API-008`); the caller, terminal and token headers are dropped and `X-Sindri-Client: mcp`
  set, so the request is `session:mcp` and the engine sees client `mcp`. `McpToolTable` lists the tools explicitly
  (low-level `WithListToolsHandler`/`WithCallToolHandler`, hand-written schemas; API P8 appends its rows).
  `McpToolTable` refuses an unknown tool (023) and a call with no `X-Sindri-Repo` (021; every tool is repo-scoped);
  `ContextTools` then binds arguments (022) and calls the ContextCalls services the HTTP
  routes use (`IService<QueryContextRequest, ContextResult>`, `IService<GetContextSourcesRequest, SourcesView>`), so
  the engine's gate (020) and scope (003) apply. `McpResults` puts the answer in `structuredContent` (the host's AwJson
  options; `context_sources` wraps the view in `McpSourcesAnswer` with the answering `instance`, since Claude Code
  reads `structuredContent` and not the text) beside `McpSummaries`' text; an error is `isError` with `McpToolError {code, message}`.
- `Aw.Sindri.Core/Mcp/`: `McpRegistration` (the claude arguments, the port rule), `McpScope.Judge` (a user-wide
  `sindri` entry in one provider file), and the ports `IClaudeCli` and `IProviderFiles`.
- `Aw.Sindri.Features/RegisterMcp/` (`sindri mcp register`) and `Aw.Sindri.Features/ContextCalls/ContextDoctorChecks`
  with `ContextChecks` (doctor's `context:sources` and `context:mcp-scope`): an `IDoctorChecks` (Core port) that the
  RunDoctor slice's `WithMoreChecks` decorator appends after the core rows, applying the exit rule to the whole list.
- `Aw.Sindri.Infrastructure/Mcp/`: `ProcessClaudeCli` (argument list, no shell, 30 s budget) and
  `ProviderConfigFiles`, both reading `McpSettings` (`AW_SINDRI_CLAUDE_BIN`, `HOME`, `CLAUDE_CONFIG_DIR`, `CODEX_HOME`,
  `PATH`). A bare `claude` is looked up in PATH's absolute entries only, and only an executable file counts, so a
  `claude` in the work repo's checkout never runs.

### Errors (S-1M range 021–029; 020 is S-1A's)

021, 022 and 023 are the endpoint's own refusals, shared with the `sindri_*` tools: API P8 appended those rows to the tool table and moved the three codes to docs/csharp/sindri.md "MCP tools (API P8)".

| Code | Kind | Surface | Meaning |
|---|---|---|---|
| `SND-CONTEXT-020` | Forbidden | MCP tool error | Session reads off (S-1A's code, mapped) |
| `SND-CONTEXT-024` | Invalid | CLI exit 1 | `mcp register`: not a profile repo |
| `SND-CONTEXT-025` | Unavailable | CLI exit 2 | `mcp register --apply`: a provider failed (claude couldn't run or exited non-zero, a project file is tracked or unparseable, a write failed); the others were still registered |
| `SND-CONTEXT-026` | Conflict | doctor `warn` | A user-wide `sindri` MCP entry in a provider's config |

Every facade error passes through to the MCP tool error unchanged (`isError`, text `<code> <message>`).

### Tests

- `tests/Aw.Sindri.Tests/ContextCalls/ContextMcpTests` (integration): the MCP SDK's own client against a started
  host: tools/list, a `context_query` round trip (citations with blob shas; the summary doesn't repeat record text;
  the client sends no `Origin`), a planted secret scrubbed and planted instructions fenced, every refusal's code (020
  twice, the second with human headers that MCP ignores; 003, 021, 022 twice, 006, 023; and `context_sources`' 020, 021, 022), `context_sources`, the guard
  (foreign `Host` 400, foreign and loopback `Origin` 403, also at `/sindri/mcp/` and `/Sindri/MCP`; no `Mcp-Session-Id`), and no question or record text in logs.
- `RegisterMcpTests` (unit), `ClaudeCliContract` (the fake; `ProcessClaudeCli` over a recording stub script, never the
  real claude), `CheckContextTests` (each provider's file shapes, malformed and unreadable files, the sources states),
  `ProviderFilesContract` (the fake; `ProviderConfigFiles` over a scratch home).

## S-2W: extra scrub patterns, per call

Spec Phase 2 and §7 (D14: one scrubber). The profile's `context.scrub.extraPatterns` reach every scrub the engine does once the context lane's scrubber is wired in; until then the engine's ledger scrub uses the built-in rules (`docs/csharp/sindri-workflow.md`, "The ledger mirror").

- **Settings.** `SettingsReader` reads `scrub: { extraPatterns: [{ id, pattern }] }` into
  `ContextSettings.ExtraPatterns` (`Trust.ScrubPattern(Rule, Pattern)`, as written). Unknown keys are refused by the
  section records, a null entry with `context.scrub.extraPatterns[i]`. Core can't check a regex (ARCH-DIR-005 keeps
  `Aw.Redaction` out of Core and Features), so the entries are checked behind a port.
- **`Trust.IScrubberFactory.For(extraPatterns) -> Result<Scrubbers>`.** `Scrubbers(Ingest, Egress, Ruleset)` are one
  call's ports. `RedactionScrubberFactory` (Infrastructure, singleton) builds them with
  `Redactor.Create(extras, Redactor.DefaultMatchTimeout, TimeProvider)` (so the whole-scan deadline runs on the host's
  clock) and maps the first `ExtraPatternError` to `SND-CONTEXT-001` at
  `context.scrub.extraPatterns[<Index>].id|pattern`, with Redaction's message (which never quotes the pattern). It
  keeps the last build, keyed by ruleset, so calls under an unchanged profile don't compile again.
- **Per call.** There is no scrubber singleton any more. `QueryContextService`, `RefreshDocsService` and
  `SourcesStatusService` read the settings, then `For(settings.ExtraPatterns)`; `ISourceCatalog.For(settings, repos,
  scrubbers)` hands them to the sources (`RepoDocsSource` cleans fetched documents with them),
  `IDocIndex.Refresh(repo, globs, scrubbers, ct)` cleans and records with them, and `IDocIndex.Status(repo, ruleset,
  ct)` counts what is outdated. `ContextEngine.CheckSettings` reads the keys, then builds the scrubbers, then checks
  the repos, so `profile validate` and `approve` refuse a pattern Redaction refuses. The approved profile can change
  at run time (the scheduler re-reads approvals), and the next call picks it up.
- **`Trust.ScrubRuleset`** (Vogen, 16–64 lowercase hex): the first 32 hex characters of SHA-256 over
  `IngestSanitizer.Version`, the text of every sanitizer regex (`IngestSanitizer.Rules`) and the built redactor's
  `Redactor.RulesDigest` (every rule it applies, extra patterns included, with ids, patterns, options and the scan
  engine, so a widened rule under the same id counts), length-prefixed. The factory caches the last build by the
  patterns as written. Changing a regex therefore re-cleans by itself; raise `IngestSanitizer.Version` too
  with any change to what the sanitizer removes in code (S-2W made it 7).
- **A ruleset per stored doc.** Migration `0003_s2w_scrub_ruleset.sql` adds `doc.ruleset` (default `''`, so every
  row S-1A wrote is outdated). A refresh treats a document as current only when its blob **and** ruleset match; one
  stored under another ruleset is cleaned again (`RepoRefresh.Recleaned` counts those indexed with text, so they are
  among `Added`; one the new rules block or one over the size cap counts as blocked or skipped instead; Sindri carries
  it on `ContextRepoSynced`). Search filters `d.ruleset = @ruleset`, so a chunk cleaned with other rules is never a hit
  until it is cleaned again, and `DocIndexStatus.Outdated` turns the repo's status row `degraded` with the count.
- **Sanitizer (S-1A review run 5 deferrals).** `HtmlImage` removes `img`, `image` and `style`, and the elements that can fetch
  when they carry an attribute (`meta`, `iframe`, `frame`, `object`, `embed`, `link`, `base`, `script`, SVG `set` and
  `animate…`);
  `HtmlTag` + `RemoteAttribute` remove any other start tag carrying `src`, `srcset`, `imagesrcset`, `srcdoc`, `lowsrc`,
  `dynsrc`, `poster`, `background`, `href`, `data`, `action`, `formaction`, `style` or `ping` anywhere in the tag. Both
  read quoted values (which may hold `>`) first, then the tag raw up to its first `>` (`RawFetchingTag`, `RawTag`), so
  a tag whose quotes don't balance or whose value is too long is removed, not kept; a raw read ends at its first `>`
  or a blank line (the part before it is removed). Markdown image and link text may run across line breaks (not a
  blank line). `Clean` returns null when the text still changes after `MaxPasses` passes and a confirming one (a later
  removal could have joined a tag), and `RedactionScrubber` blocks it, as it does on a regex timeout. `<style>` start
  tags go like `<img>` (the CSS after one is plain text; a whole-block rule deleted the rest of any doc that mentioned
  `<style>`). `RedactionScrubber` turns CRLF into LF before cleaning, so a blank line reads the same everywhere. Image
  rules leave a regex lookaround (`(?<![0-9])`) alone unless a `(` or `]` follows it. The remote
  definition's label takes backslash escapes and one line break (not a blank line), and labels are matched as
  CommonMark does (whitespace runs collapsed, case ignored), so `[x][p q]` meets `[p\nq]: https://…`.
- **The map's text.** Graph answers (show, neighbors, path, and the query's graph, affects, decisions and code
  sections) clean every label, wording and quote through `MapText` (S-3b). Human map text is stored as written in the
  owner-only `context.db` and cleaned on every answer; map status carries no map text. The export is the owner's raw
  round-trip copy (0600, purged after 14 days by `context-purge` when that service is enabled; doctor `context:retention` warns otherwise) and is deliberately not scrubbed (spec §7 "Stored text").
- **Not here: telemetry.** Telemetry's redactor stays `Redactor.Default`: log lines and spans carry codes, counts and hashes,
  never record or query text (§7), and the telemetry redactor is fixed at start while the profile changes at run time.

### Tests (S-2W)

- `ScrubberFactoryContract<T>` (`Aw.Context.Testing`), bound to `FakeScrubberFactory` and `RedactionScrubberFactory`
  (unit): an extra pattern on both ports, the ruleset follows the list, refusals at their key paths.
- `DocIndexContract<T>` gains "a document stored under another ruleset is cleaned again" (fake and SQLite).
- `RedactionScrubberFactoryTests`: Redaction's refusals (Unicode class, empty-matching, built-in or reserved id, nulls)
  at their key paths without the pattern; the last build reused; the fingerprint is length-prefixed.
- `RepoDocsTests`: with a pattern added, nothing stored under the old rules is a hit, the refresh re-cleans both docs,
  `doc_chunk` loses the raw id and gains the placeholder, and a fetch answers the placeholder.
- `RepoDocsTests.Rows_an_S1A_store_wrote_are_outdated_after_the_upgrade_and_cleaned_again`: migration 0003 on a real
  migration-2 store with an S-1A row (outdated, unsearchable, cleaned again).
- `StoredDocsTests`: a blocked or oversized re-clean isn't counted as `Recleaned`. `Aw.Redaction.Tests/RulesDigestTests`:
  the digest follows ids, patterns and options, not the timeout, and the scan engine's behaviour is pinned with
  `EngineVersion`.
- `QueryContextTests`, `SourcesStatusTests`, `SettingsReaderTests`, `TextRulesTests` (the HTML carriers and labels,
  including unbalanced quotes and values too long for the quote-aware read),
  and `ContextEngineContract` (an extra pattern that doesn't parse is 001 at its key path).
## S-3a: the product graph store, map import / export, map status, doctor, purge

### Shape

| Project | Adds |
|---|---|
| `Aw.Context.Core` | `Graph`: `NodeKey` (typed id; the kind is the scheme), `NodeKind`, `NodeKinds` (the surface kinds: screen, pane, dialog, command, keybinding, setting, menu, route, api, job, notification, permission, report, flag), `EdgeType`, `EdgeRules` (the §6.1 edge table), `EvidenceRef`, `GraphNode`, `GraphEdge`, `NodeAlias`, `DecisionRow` (`ToKey()`: `decision:` + SHA-256 of record and quote), `GraphSnapshot` (`Resolve`, `ScopedTo`), `RepoScope`, `TrustPolicy`, `EntryKey` (version 1, golden-tested), `IKeyMigration`, `KeyMigrations`. `Overrides`: `MapOverride` (`ToId()`: a content hash, so a re-import keeps one row), `OverrideId`, `OverrideAction`, `RejectionIdentity`, `OverrideApplier` (pure; orphaned overrides kept). `Map`: `MapDocument` (the file), `MapDocumentValidator` (all or nothing, 031/032 with key paths, key migration), `MapAccess` (the read gate and scope, the export document), `MapRetention`, the result records, and the ports `IGraphStore`, `IMapFileReader`, `IExportStore`. `Facade.IContextMap` (`PreviewMap`, `ImportMap`, `ExportMap`, `MapStatus`, `CheckMap`, `CheckRetention`, `PurgeExpired`); `IContextEngine : IContextMap` |
| `Aw.Context.Features` | `ContextMap` (one line per method) over the slices `PreviewMap`, `ImportMap`, `ExportMap`, `ShowMapStatus`, `CheckMap`, `CheckRetention`, `PurgeMap`; `ContextEngine` delegates the map methods to it |
| `Aw.Context.Infrastructure` | `Graph.SqliteGraphStore` (over `Store.ContextDatabase`), `Map.MapFileReader` (YamlDotNet's parser and representation model to JSON, then AwJson; no reflection serializer), `Map.FileExportStore`, `Map.Worktrees` |

**Sindri's side** (`Aw.Sindri.Features.ContextCalls`): the queries `GetContextMapRequest` → `MapStatusView`
(`GET /sindri/context/map`) and `ExportContextMapRequest` → `MapExported` (`GET /sindri/context/map/export?path=`);
the command `ImportContextMapRequest(Path, Hash?, Answer: ChallengeAnswer?)` → `ImportContextMapResponse(Preview, Imported?, Sequence?, Challenge: MapImportChallenge?)`
(`POST /sindri/context/map/import`; catalog row `ImportContextMap`: `protected`, terminal only,
`cli-only:typed-confirmation`), which appends `context.map.imported` (`ContextMapImported`: version, repo, hash,
counts); the scheduled service `ContextPurgeService` (`context-purge`); doctor rows `context:map` and
`context:retention` in the `RunDoctor` slice's `ContextEngineChecks`, which `ProfileFacts.Context` runs (so
`RunDoctorService` stays within AW1007's dependency limit). CLI: `Host/Commands/ContextMapVerbs` and `ContextMapText`. The map JSON
types are in `SindriJsonContext`; the reader and the exports folder take its `JsonSerializerOptions`.

**Version retention (`MapRetention.ExcessVersions(versions, now)`).** The purge deletes a `Retained` version only when it is
beyond the newest 20 retained **and** its successor (the next Current-or-Retained version by number) was created more than
90 days ago. A version is what an as-of read (`AsOfMapBuilder.Authored`, newest Current-or-Retained created at or before a
sample's date) returns for the dates from its own creation to its successor's, and the backtest's eval window is 90 days (the
eval cache and report expiry), so a version replaced inside the window may still be read. No migration: `map_version.created`
is enough. `CheckRetention` counts the same set.

`GET /sindri/context/map/export` is the one query that writes: a file under `exports/` (owner-only, purged after 14
days), never the store or the ledger. It is a query because the API spec lists it as one, and it changes no state a
command or event describes; a later file-writing route should not take it as a precedent without the same reasons.
Import and preview refuse a session-kind caller in the engine too (SND-CONTEXT-020), whatever surface reaches them.

**The challenge (S-3b):** the import uses the engine's shared challenge (engine spec §4.2, README X30). The preview
(`ImportContextMapRequest` without a hash) issues `import map <hash6>` with the subject `map:<content hash>` through
`IChallenges` and returns it as `MapImportChallenge`; the import's `ChallengeAnswer` must redeem it
(`SindriErrors.AuthRefused`, SND-AUTH-002, when missing or stale), after a hash that isn't the file's now is refused
with SND-CONTEXT-034. `ProtectedCommandTests` has no exemption left for it.

### Store

Migration `0002_s3a_graph.sql` (after S-1A's `0001`): `map_version` (status `building|draft|current|retained`, one
`current` by a partial unique index), `node`, `node_evidence`, `node_alias`, `edge`, `edge_evidence`, `decision`
(wording and quote only: the record it quotes is its `quotes` edge) and `map_override` (outlives versions; ids are
content hashes, `ON CONFLICT DO NOTHING`). Trust is stored as computed from provenance, never read from a file. A save
writes a `building` version in batches of 500 statements, then makes it `current` and the old one `retained` (with
the file's overrides) in one transaction; a failed batch deletes the building version.

### Errors

| Code | Kind (HTTP) | CLI exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-030` | NotFound (404) | 1 | No map yet |
| `SND-CONTEXT-031` | Invalid (400) | 1 | The map file is invalid (schema version, shape, unknown kind, disallowed edge, unreadable); key paths given; nothing written |
| `SND-CONTEXT-032` | Invalid (400) | 1 | The map file refers to a node it doesn't define (edge end, alias target); nothing written |
| `SND-CONTEXT-033` | Invalid (400) | 1 | Export path outside `$AW_SINDRI_HOME/context/exports/` |
| `SND-CONTEXT-034` | Conflict (409) | 1 | The map file changed since its hash was confirmed; nothing written (fix: run `context map import <file>` again) |

### Tests

`tests/Aw.Context.Tests`: `Map/` (keys and the `EntryKey` golden snapshot, the validator, key migration with a
synthetic v2 and "no override orphaned", the override layer, repo scope, the port contracts bound to fakes and real
adapters, the store's file modes and the YAML → store round trip); `ImportMap/`, `ExportMap/` (with the golden export
`ExportMap/Golden/fixture-export.json`), `MapStatus/`, `MapHealth/`, `PurgeMap/` through the facade over the fakes.
`tests/Aw.Context.Testing/Map`: `FakeGraphStore`, `FakeExportStore`, `FakeMapFileReader` and their contract bases,
`SampleMap`, `FixtureMap` (the split-panes product map). `tests/Aw.Sindri.Tests/ContextCalls/ContextMapApiTests`: the
routes through the real pipeline (preview, protected import and its refusals, event, export, session scope, purge).

## S-3b: the cascade, graph reads, the node and text shapes, the probe runner

### Shape

- **Core `Graph/`.** These are pure:
  - `Cascade.Affects` (§6.1): own scope, state, dependency and flow hops, the ascend, BFS with a visited set under the depth cap.
  - `GraphWalk.Neighbors` (depth 0–4) and `GraphWalk.ShortestPath` (at most 8 edges, either way).
  - `AnchorMatch.Resolve` (key, alias, label; 037 or 003) and `AnchorMatch.FromText` (whole-label matches, most specific first).
  - `GraphIndex` (internal adjacency in a fixed order).
  - The answer views `NodeFact`, `EdgeFact`, `NodeView`, `NeighborsView` and `PathView`.
- **Core `Map/`.**
  - `IGraphStore.Snapshot(draft)` reads the version, its graph and the overrides in one read session (S-3a's follow-up).
  - `MapReader`, a domain service shared by every graph read: gate, then snapshot, then overrides applied, then
    `MapAccess.Visible`. A session's walks run on its scoped graph, so they can't leave it.
  - `VisibleMap`.
- **Core `Map/MapText`.** Every label, wording and quote crosses this call's `Scrubbers`: ingest (sanitize, the
  built-in rules, the extra patterns), then egress, one field at a time. Labels are capped at 200 characters and
  untrusted ones are fenced (`<untrusted source="map">`). The text is cleaned on the way out with the current rules and
  nothing cleaned is stored (human map text is stored as written), so the map needs no ruleset fingerprint (S-2W's follow-up, closed).
- **Core `Eval/`.**
  - `ProbeSet`, `ProbeCase`, `ProbeExpect` and `ProbeDecision` (probes.json's shape plus an optional `schemaVersion`; unknown keys refused).
  - `ProbeId`.
  - `ProbeScoring` (keys in answer order, precision@5, citation correctness, absent nodes).
  - `ProbeRunner`. `Run` asks through a delegate and compares with the baseline (038: lower means, or a probe with more absent nodes than the same probe in the baseline); `Check` validates the file (035 with a key path).
  - `ProbeReport` and `ProbeTotals`.
  - The ports `IProbeFileReader` and `IProbeReportStore`.
- **Core settings and errors.** `ContextSettings.CascadeDepth` (`context.cascadeDepth`, 0–4, default 2) and `ContextSettings.CascadeComponentFanOut` (`context.cascadeComponentFanOut`, 1–50, default 5).
  `ContextErrors.Attention`, the CLI's exit-1 set, moved here from the verbs (S-3a's follow-up).
- **Features.** `IContextGraph` (`ShowNode`, `Neighbors`, `Path`; `IContextEngine` extends it) over the slices
  `ShowNode`, `ListNeighbors`, `FindPath` and `EvalProbes` (`IContextEngine.EvalProbes`, `EvalProbesService` over
  `IProbeFileReader` and `IProbeReportStore`: reads the file, finds the baseline, scores, writes the report). In `QueryContext`, `QueryRules` accepts the node shape (item and symbol
  stay 003 until their phases), and `GraphAnswer` builds the `graph`, `affects`, `decisions` and `code` sections, all
  citing the map version as `C1`. An affects item takes the weakest provenance on its path and is confirmed only if
  every node and edge is. `ResultAssembly` puts the graph part first, then the records.
- **Infrastructure.** `ProbeFileReader`: the named file or `context/eval/probes.json`; 036, 005, 035; SHA-256.
  `FileProbeReportStore`: `context/eval/probes/probes-<time>.json`, 0600 in 0700 folders, the baseline by probe file hash: the newest run that was complete and did not regress.
- **Sindri.**
  - Query routes `GET /sindri/context/node`, `/neighbors` and `/path`, with their `ContextCalls` services.
  - CLI `context show | neighbors | path` (`ContextGraphVerbs`) and `context eval probes` (`ContextEvalVerbs`). Probes
    have no route (a lead decision): the CLI asks each probe through `GET /sindri/context/query`; the engine's
    `EvalProbes` slice reads the file, scores and writes the report. A run in which a probe's query failed is
    incomplete, whether some or every probe failed: exit 2, the failed probes listed, nothing saved, never a baseline.
    `ProbeRun` carries `incomplete` and `failed` beside `{report, path}` in `--json`. The report's `baseline` field was
    `previous`; old report files still read.
  - Text renderers in `Text/`. `ContextText` prints an affects item's depth and path.
- **The map import's challenge.** Described in S-3a above. `ProtectedCommandTests` lost its `ImportContextMap`
  exemption, #192's context-lane waiver.

### Errors

| Code | Kind (HTTP) | CLI exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-035` | Invalid (400) | 1 | The probe file is invalid (shape, unknown key, duplicate or non-slug id, empty, wordless or over-long question (6000 characters percent-encoded), schema version); key path given; nothing run |
| `SND-CONTEXT-036` | NotFound (404) | 1 | No probe file: the named one doesn't exist, or none was named and `context/eval/probes.json` doesn't exist |
| `SND-CONTEXT-037` | Conflict (409) | 1 | A node name matches several nodes; the message lists their keys (fix: ask by key); the problem body also has `candidates`, an array of at most 100 keys the caller may see |
| `SND-CONTEXT-038` | Conflict (409) | 1 | A probe run scored lower than the baseline of the same file (its newest complete run that did not regress) in precision@5 or citation correctness, or a probe returned more absent nodes than it did there |

039 is reserved. A depth (neighbors, query) or a budget that is out of range (depth 0–4, budget 1 or more) is `SND-CONTEXT-006` on every transport, and one that is not a whole number is 006 in the CLI (exit 1) and HTTP (MCP's binder answers `SND-CONTEXT-022` for an argument that isn't a 32-bit JSON integer, even one that is only too large), parsed once in `QueryRules.ParseDepth` and `ParseBudget`; the CLI also refuses a question or `--node` over `QueryRules.MaxEncodedText` (6000) characters once percent-encoded with 006 before sending, so Kestrel's 8 KB request line never answers 414; the CLI adds its own `--depth` or `--budget` hint; a graph read with no map reuses 030; a key, alias
or name not in the caller's map reuses 003.

### Tests

- **Unit.**
  - Cascade (the §6.1 fixtures: orientation reaches the tab manager at depth 1 through pane-layout; sibling exclusion;
    ascend not re-entering own scope; dependency and flow hops; depth cap; cycles; unowned actions; thing anchors).
  - GraphWalk, AnchorMatch, ProbeScoring and ProbeRunner (numbers by hand; regression; the file check with key paths;
    the fixture probes.json).
  - The slices over the fakes (aliases, names, overrides, scrubbed and fenced labels, 003/020/030/037/006, a session's
    scope); the query's node and text shapes (the Phase 3 signal, depth 0, `--draft` with no draft, text anchors, no map).
  - Text renderers.
- **Integration.** `IGraphStore.Snapshot` on SQLite (the contract); the probe file reader and reports folder on disk;
  one Sindri host test for the three routes, a node query, a session's scope, a bad depth and a foreign Origin; the
  import challenge (`ContextMapApiTests`).
- **Fixture.** `surface:command/switch-tab` (part-of the tab manager, restore-layout `on` it) lets the ascend reach the
  tab manager; S-3a's golden export follows.

## S-4: the extractor, the draft, map review, lenses and the file map (X63)

User-facing behaviour is in `docs/sindri/context.md` (S-4). This is where the code lives.

### Shape

- **Core `Extract/`.** `MapExtractor` composes the deterministic map from file text through table-driven `Recognizers` (command ids,
  keybindings, settings keys, routes, UI names), each node with a file-level `implements` from `code:<path>`; `context.extract.recognizers` (`[{kind, glob, pattern, keyFrom}]`, default `[]`, at most 50, a pattern at most 512 characters) adds profile rules to that table through `RecognizerSet`: a file whose path matches a `glob` is read (a denied path never is) and each match of the .NET `pattern` proposes `surface:<kind>/<the keyFrom group>` at confidence 0.8, suggested and never confirmed; the match timeout is fixed at one second, and a file that exceeds it is `degraded: recognizers` in the build report, not a failure. A bad entry is `SND-CONTEXT-130` (unknown kind, glob or key group, a pattern that does not compile) and too many entries or a pattern over the cap is `131`, each naming `context.extract.recognizers[i].…`; `132` is reserved. `context.extract.graphify` (`{timeoutMinutes, maxSnapshotMB, maxFileMB, maxGraphMB}`, defaults 60, 2048, 16, 256, `0` is no limit) is read into `ContextSettings.Graphify` (`ClusterLimits`, a tool-neutral name because `IClusterSource` is a Core port; `GraphifyRules` does the checks); a negative, fractional, over-10,000,000 (over 70,000 for `timeoutMinutes`, a timer's ceiling) value is `SND-CONTEXT-133` naming `context.extract.graphify.<key>`. `BuildMapService` and the backtest's `AsOfMapBuilder` hand the limits to `IClusterSource.Clusters(repo, commit, limits, ct)` on every call, so a profile change applies to the next build with no restart; the source reports why clusters are degraded in `ClusterSet.Reason`, and the build's `degraded` line reads `clusters: <reason>` (a truncated snapshot that still clusters is `Available` with a `Reason`). The backtest's as-of map runs the same recognizers and adds the set's hash to its cache key only when the set is not empty. An extraction in which a recognizer timed out is partial and load-dependent, so it is not cached and the backtest report says `recognizers timed out`. `ClusterFeatures` turns
  graphify clusters (`IClusterSource`) into candidate features, optionally named by `ILabeler.Label(clusters, extraPatterns, maxTokens, ct)` (the token cap
  arrives with each pass, from `context.labeling.maxTokens`; the profile's `context.scrub.extraPatterns` scrub every path sent to the model). Ports: `IRepoReader` (files at a sha), `IClusterSource`, `ILabeler`.
- **Core `Overrides/`, `Review/`, `Proposals/`, `Lenses/`.** Pure: `OverrideApplier`, `ReviewBacklog` (the queue), `ReviewPlanner` (one
  decision to override rows; `Repoint` is an undo of the orphan plus the same decision on the new node), `ReviewFileMatcher`, `PromoteReadiness`,
  `ReviewHistory`, `ProposalPlanner`, `GraphStatuses`, `LensesBuilder`, `FileMapBuilder`. `ReviewDrafts.Gate` (the whole-map paths: session callers
  are `SND-CONTEXT-020`) and `ReviewDrafts.GateDecision` (a single decision is left to Sindri's class check; only the bulk accept keeps the gate).
- **Core `Locks/IHeavyJobLock`** is the context engine's lock port; `Sindri.Core/Locks/IHeavySlots` is Sindri's own (one lock for the engine
  and the shell tools today; the workflow engine's `heavy` allocator is meant to use it too and is not wired yet).
- **Infrastructure.** `GitRepoReader` over `GitCli` (048), `GraphifyClusterSource` (pinned, sandboxed, fail-closed; `GraphifySnapshot` unpacks the commit under the file and snapshot caps, counting only copied bytes; `GraphifyProcess` runs graphify under the time limit and keeps at most 64 KB of each output stream; `graph.json` is parsed from a stream). The backtest's as-of cache key gains the limits when they are not the shipped ones, and a map whose cluster pass was partial (`ClusterSet.Reason`) is not cached, `ModelLabeler` over `IModelPort`
  (042), `FileReviewFileStore`, migration `0004_s4_map_review.sql` (`map_override` rebuilt with `propose` and `undo`, `link`, `undoes` and `payload` columns).
- **Features.** Slices `BuildMap`, `ReviewQueue`, `ReviewMap`, `ReviewHistory`, `ExportReview`, `PreviewReviewFile`, `ApplyReviewFile`, `PromoteMap`,
  `ProposeFromScope`, `GetProposals`, `GetLenses`, `GetFileMap`; the facade methods are on `IContextMap` and `IContextLenses`; `Propose` and `Proposals` are on `IScopeProposals`, which `IContextMap` extends.
- **Cleanup pass (`IContextMap.CleanDraft`, owner F1).** `CleanMapService` settles the whole review queue of the latest draft so `PromoteReadiness` passes, with no human step: the deterministic group is accepted (`AcceptDeterministic`), orphaned overrides dropped, model entries at or above `CleanupPolicy.KeepAt` (0.75) accepted, and the weaker ones put to the cleanup agent (`IModelPort`, `ModelPurpose.Cleanup`, batches of 40, at most `CleanupPolicy.MaxTokens` per pass). The prompt is scrubbed, each label and path sits in the untrusted fence, entries get short ids (`n1`…) and only ids that were asked count; `keep` accepts, `remove` rejects. An entry the agent did not judge (model down, over the cap, an answer that does not parse) is rejected. `CleanupReport.Mode` is `none` (nothing was weak), `agent` (all judged), `partial` or `unavailable` (none judged). Decisions are planned by `ReviewPlanner` and written as the caller; a session is refused (020). The queue is re-read after each pass (a rejected node removes its links); a pass that decides nothing while entries remain is an error, never a spin.

- **Promote under a deny glob.** `MapAccess.View` decides the cut; `PromoteMap` replaces the draft with the cut graph and the undo rows for the person's accept, rename, move and merge decisions about what was cut (`MapView.UndoCutDecisions`, `who` = `service:deny-cut`) in one `ReplaceDraft` transaction, then promotes it, so `context.map.promoted` follows every committed promote. `BuildMap` writes the same rows with its `SaveDraft`. `DenyCut.Apply` drops a marked undo row and the row it undoes while their node is cut or gone (every view, history and export included); rejects and deletes are never undone, and a reject or delete whose node is gone is satisfied, not orphaned. The `context:map` doctor row counts over the same view. A store 046 from `ReplaceDraft` is neutral (the draft is no longer the open one); `PromoteMap` words it for a promote.
- **Sindri Core.** `IHeavySlots`/`HeavySlotTaken`; the events `context.map.reviewed`, `context.map.promoted`, `context.map.proposed`
  (the sixth public context event, added by S-4) and `context.map.built`; `IndexSettings` (`index.denyPaths` on the profile and on a repo).
- **Sindri Infrastructure.** `DirectoryHeavySlots` over `locks/heavy-job.lock[.k]` (`mkdir` mutex, owner file `<pid> <utc> <who>`,
  `AW_HEAVY_SLOTS` default 1 (the box cap, as the heavy wrapper reads it), `AW_HEAVY_LOCK_HELD=1` makes it a no-op, a dead owner's slot is reclaimed like the heavy wrapper does);
  it interoperates with `config/lib/locks.sh` (an integration test takes both slots from bash).
- **Sindri Features `ContextCalls/`.**
  - Queries: `GetContextReview`, `GetContextReviewHistory`, `ExportContextReview`, `GetContextProposals`, `GetContextLenses`, `GetContextFileMap`.
  - Commands: `ReviewContextMap` (classified: the variant is the verb), `PromoteContextMap`, `ProposeContextMap`.
  - `ContextRebuildService` (`context-rebuild`), `SindriHeavyJobLock : IHeavyJobLock` over `IHeavySlots` (wait 60 s, then 040), `ContextChallenges` (the shared X30 challenge), `ReviewInput`.
  - `ContextScopes.Entries` maps the profile's repos and `index.denyPaths` to the engine's `ProfileRepoEntry`.
- **Sindri Host.** Routes: `GET /sindri/context/map/review`, `/review/history`, `/review/export`, `/map/proposals`, `/lenses`, `/filemap`; `POST /sindri/context/map/review`,
  `/promote`, `/proposals`. CLI: `ContextReviewVerbs` (review, promote, propose, proposals), `ContextLensVerbs` (lenses, filemap), `ReviewArguments` (the pure parse),
  `ReviewSession` (the review loop as a state machine; the TTY shell only reads lines and sends), `ContextExit` (040 is 75), text in `Text/ContextReviewText` and `ContextLensText`.
  MCP: `context_lenses {anchor? | anchors?, mapVersion?}` in `ContextTools` (a lone anchor stays schema 1, several distinct anchors answer schema 2; `context_query` also takes `mapVersion?`; no new tool, the session reads sets through S-LB2); `context_node {key}` and `context_neighbors {key, depth?}` in `GraphTools` (S-3M; the same services as the HTTP routes, 022 for bad arguments, summaries in `McpSummaries.Node` and `.Neighbors`, tested in `ContextGraphMcpTests`). `Models/JudgeModelPort` adapts Judge's `IProviderCatalog` as `IModelPort` (agent CLIs only, never `jev`), the catalog built in a container of its own (`JudgeCatalog`).

### Classes (README X63)

| Row | Class | Who may run it |
|---|---|---|
| `ReviewContextMap:accept`, `reject`, `rename`, `move`, `accept-deterministic`, `undo`, `drop`, `repoint` | desk | terminal, app |
| `ReviewContextMap:apply-file`, `merge`, `add`, `delete` | protected (`NeedsTerminal`) | terminal, with the typed challenge `review <verb> <hash6>` bound to the request's fields (a file's content for apply-file) |
| `PromoteContextMap` | protected (`NeedsTerminal`) | terminal, challenge `promote map v<N>` bound to the draft |
| `ProposeContextMap` | job | terminal, app, `service:engine` |
| `BuildContextMap` | job (P6 runtime) | terminal, app, `service:engine` (a session is refused by the gate, `SND-AUTH-002`) |

`map build` is a job on API P6's runtime: `AddContextJobs` (Features) registers it with `AddJob` (key `build`), `ContextJobEndpoints` maps it with `MapJob` (any body starts it), and `BuildContextMapService` is the work, run by the job runner under the starting caller: it passes the engine's step lines to the job's progress, appends `context.map.built` and returns a small result (`repo`, `draft`, `proposed`, at most 20 `degraded` lines). `ContextRebuildService` runs the same slice on its cadence outside the gate.

### Errors (S-4 range 040, 042–048)

| Code | Kind (HTTP) | CLI exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-040` | Unavailable (503) | 75 | The heavy-job lock stayed busy, or its directory can't be used; the message names the holder |
| `SND-CONTEXT-042` | Invalid (400) | 1 | Labeling over its token cap (or the host has no model for it) |
| `SND-CONTEXT-043` | Conflict (409) | 1 | No entry of the review file matches the latest draft |
| `SND-CONTEXT-044` | NotFound (404) | 1 | A draft is needed and none exists (`draft` asked, or `map review` listing the queue) |
| `SND-CONTEXT-045` | NotFound (404) | 1 | A repo outside the profile or the session's repo (one answer for both) |
| `SND-CONTEXT-046` | Conflict (409) | 1 | Promote refused: no draft, undecided review items, or the draft changed after you confirmed it (review the new draft, then promote again) |
| `SND-CONTEXT-047` | Conflict (409) | 1 | Stale review target, or an undo of an unknown or undone override |
| `SND-CONTEXT-048` | Unavailable (503) | 2 | Git can't read the repo at the commit |

041 is retired; 049 is reserved for findings in the review loop.

### The app's wire (UI contract delta, D1–D14)

The goldens under `tests/Aw.Sindri.Tests/Golden/Api/context-*/` pin every body below (`ApiScenarios`, `ContextApiScenarios`, the `MapTabsRepo` fixture). JSON is camelCase through AwJson.

| Route | Request | Response |
|---|---|---|
| `GET /sindri/context/lenses` | `anchor` (repeat it for up to 20; one distinct value answers schema 1, several schema 2), `depth` (0–4, default `context.cascadeDepth`; else 006 / 400), `hops` (S-CH: a comma list of `state`, `flow`, `dependency`, `component`; default the first three; else `SND-CONTEXT-140` / 400), `draft`, `mapVersion` (S-LB1a; not with `draft`) | `{schemaVersion, anchor{key,label,kind,trust}, mapVersion, mapCommit?, draft, depth, asOf, bands{intent,product,code}, edges[], conflicts[], coverage, searched, degraded[], cites{}, skippedComponents?}` (spec §8.9; S-CH, only with `hops` and a unit over the cap); several anchors answer schema 2, see "S-LB1a" below |
| `GET /sindri/context/filemap` | `repo`, `path`, `unmapped`, `draft` | `{schemaVersion, repo, sha, mapVersion, draft, path, entries[{name,path,type,status,files,features[{key,provenance,confidence?,confirmed,linkId?,edge}],intent{records,decisions}\|null}], unmapped[], suggested[{linkId,file,feature,confidence,evidence[]}], totals{files,mapped,suggested,unmapped}, symbols}` |
| `GET /sindri/context/map` | `anchor` (optional) | the map status; with an anchor also `coverage`, the lenses' record from the same Core function (`LensCoverageBuilder`) |
| `GET /sindri/context/map/review` | `limit` (per group, default 100, max 1000; else 006) | `{draft, version{number,commit}, entries[…, trust], decided, proposals, counts{deterministic,model,orphaned}, byKind{}, truncated{…}, undecided}`; `decided` counts the proposals of this draft with a standing decision, `proposals` = decided + deterministic + model |
| `GET /sindri/context/map/review/history` | `limit` (default 200, max 1000; else 006) | `{entries[], truncated}` |
| `POST /sindri/context/map/review` | `{verb, node?, link?, override?, overrides?, label?, other?, path?, answer?}` | `{result{written[],skipped[],undecided}, sequence, challenge?, summary?}` |
| `POST /sindri/context/map/proposals` | `{run, repo, surfaces[{name, key? or kind?, parent?, evidence?, quote?}]}` (a session is `SND-AUTH-002`, 403) | `{result{draft, added, existing, rejected, withdrawn, kept}, sequence, surfaces[{surface, node, status, trust}]}`; `draft` is null when nothing was opened (a withdrawal with no draft). An empty `surfaces[]` withdraws the run: one `undo` row for each of its undecided proposals (`withdrawn`), while the ones a person accepted, merged, renamed or moved stay (`kept`); a repeat withdraws nothing and writes no second event, and a retry after a failed event append writes it once. `surfaces` in the answer is every surface of the run in the order submitted, each `mapped`, `new` or `unmapped` (spec §8.7) with its label and node key, the same list `GET` returns, so the counts are not the only answer; `context map propose` prints it under the counts |
| `GET /sindri/context/map/proposals` | `run` | `{run, map, draft, surfaces[{surface, node, status, trust}]}`: every surface the run submitted (kept per run in `proposal_run`, migration 0005; a second submission replaces the list), each `mapped`, `new` or `unmapped` against the current map at call time, read through the same view as every other read (a denied path hides its nodes; a session sees only its repo). `trust` is always `untrusted`; `node` is null when unmapped, and for `new` when the caller is fenced (session, MCP, service). The POST row's list is the same one for a clean caller. |

- **Repoint** (`verb: repoint`, `override` = the orphan, `node` = the new target) writes `[undo of the orphan, the same decision on the new node]`; undoing both rows restores the orphan. A target not in the draft is 047.
- **Undo list** (`overrides`, 1–1000 ids, only with `undo`, not beside `override`): all or none in one transaction; 047 names the first stale id; one `context.map.reviewed` event with `rows` = n. CLI `--undo <id>[,<id>…]`. *Provisional owner default (2026-10-14 night).*
- **Lenses** details. Since S-LB1a schema 1's `mapVersion` is exactly `v<N>` (it was `v<N>@<sha>`), with the commit in the sibling `mapCommit` (7 lower-case hex, omitted when none); decisions gained `label` and `kind`, intent records `kind`, and code files `kind`, `label` and `trust`. `bands.product.counts` also carries `neighbors` and `edges` (the totals before the 100 cap, null when not counted); `own[]` is the anchor's own scope; `neighbors[]` and `affects[]` carry `depth` and `path`. A neighbor's hops read `<edge type> <key>` going with the edge and `<edge type>-by <key>` going against it, over structure edges where they reach (else any edge); an affects path is the cascade's own words. `product.edges` are the structure edges among product items. Intent: `records[]` (`source` from the key: `linear`, `linear:doc`, `slack`, `onyx`, `repo-doc`; `state` `unchecked`, `cites` empty until S-5A/S-6) and `decisions[]` (with `at` and `quote{record,text,trust:untrusted}`); `counts.orphaned` is `null` (follow-up F2). Code: `files[]`, one per file (`suggested` = every link of it unconfirmed, `linkId` = its best link then); `counts.featuresWithNoCode` counts scope features with no confirmed `implements`. Coverage counts a confirmed `implements` only as implemented (not `tests`, not a model suggestion); `code.suggested` counts scope nodes whose only `implements` are unconfirmed. Conflicts: one per decided node, `supersedes` only until S-CF. Provisional: all of the product `edges`, the `-by` paths, `featuresWithNoCode` and the gap notes.
- **File map** details. A file's `features` include its suggested link (with `linkId`) and `tests` links (`edge: tests`; they never make a file mapped); a directory's hold the confirmed ones only. `intent` counts the distinct records and decisions on the entry's features, `null` with none. `unmapped[]` lists the unmapped entries even when `unmapped=true` filters `entries`. `sha` is the seven-character HEAD the tree was listed at.
- **Session scope (owner decision 2026-10-15: B).** `MapAccess.View`'s session step cuts the graph with `GraphSnapshot.ScopedTo`, which keeps `RepoScope.NodesFor` (the nodes the repo implements and their code files) and, through `RepoScope.WithLinkedIntent`, the decision nodes with a `decides` edge to one, the record nodes with a `mentions` edge to one and the records those decisions quote. Edges, aliases and `Decisions` rows follow the kept nodes, so a record's `mentions` list shows only its in-scope ends. The deny cut runs first and judges every profile repo's globs for a session as for a human (`MapAccess.View` gives `DenyCut` two sets: every profile repo, whatever `context.repos` narrows to, judges record keys and evidence refs, which name their repo, and the repos the map is built from (`ScopeRules.Mapped`) judge `code:` keys, which name none; the session's repo is used only for the gate and this scope step; `BuildMap` cuts its draft with the same two sets). A record node's label is untrusted wherever a node is rendered (`TrustPolicy.ForNode`: `MapText.Fact`, `LensScope.Item` for own scope and reach, `LensesBuilder` for the anchor, and `GraphAnswer` through the kind overload for the cascade) (fenced for a session, `trust: untrusted` beside the clean label for a human); a person's decision wording stays trusted and unfenced, and the quote is fenced. A record or decision linked only outside the scope is `003` on a direct key with no title, and a `supersedes` conflict whose superseded decision is linked only outside the scope is not shown to the session. A session's map export is `SND-CONTEXT-020` (spec §3): the file is not scrubbed. `ScopeRules.MayRead` is unchanged: a session still queries repo-docs sources only, not Onyx or Linear search, until `context.repoTeams`.
- **Trust and the fence (provisional owner default, 2026-10-14 night; CTX §7).** Every lens item, record, decision, queue entry and node fact carries `trust`. A human at the CLI or in the app (`FencePolicies.For`: human and client `cli` or `http`) gets the label clean beside `trust`; a session, MCP and in-process callers get untrusted text inside `<untrusted source="map">…</untrusted>` as before. The CLI's text output fences an untrusted label itself when it called as a human (a session's answer is already fenced), `--json` prints the body unchanged. Query `ResultItem`s stay fenced for everyone. The file map carries only repo paths and node keys, so it has no `trust`. If the owner refuses the policy, drop `FencePolicies` to always-fenced and document the fence grammar for the app.
- **Anchor errors** carry their candidates in text: 003 ends `(fix: try <key>, <key>)` (up to five), 037 lists the keys (goldens `unknown-anchor-003`, `ambiguous-037`). Over HTTP, 003 also carries `suggestedAnchors` (only when there are suggestions, at most 5) and 037 `candidates` (at most 100) as problem members; MCP errors stay `{code, message}`.
- **HTTP status per code.** 003 404, 037 409, 030 404, 044 404, 045 404, 046 409, 047 409, 043 409, 005 and 006 400, 042 400, 040 503 (CLI 75), 048 503 (CLI 2), 020 403, `SND-AUTH-002` 403, `SND-CLI-003` 400.
- **Heavy-job slots.** `HeavyLockSettings` reads `AW_HEAVY_SLOTS` as the heavy wrapper does: unset or empty is 1, a whole number of at least 1 is that many, anything else (`two`, `0`, `-1`) is 1 in the wrapper (and `AW_SESSION_HEAVY_SLOTS` likewise), in `config/lib/locks.sh`'s `acquire_any_lock` and here. The callers of `config/lib/locks.sh` (the ui-evidence skill, scaffold `e2e.sh`) pass `${AW_HEAVY_SLOTS:-2}`, so an unset variable is 2 slots there.

### Tests

- **Unit.** The planner (repoint, undo, bulk), `ReviewHistory`, the review-file preview, `ReviewDrafts` gating (a single decision from a session is not refused by the engine;
  the bulk accept, apply, export, promote and build are 020), the catalog classes with `Authorization` for every caller, `ReviewInput`, `ReviewArguments`, `ReviewSession`,
  `ContextExit`, the text renderers (every status a word), the verbs over a scripted API, `DirectoryHeavySlots` and `SindriHeavyJobLock`, `JudgeModelPort` over a fake catalog,
  the profile's `index.denyPaths` (parse, hash stability, validation, the scope).
- **Integration.** `ContextReviewApiTests` (the rebuild service, desk and protected review, promote and its challenge, apply-file, 005, 043, 048, 040 with the bash helper holding both slots),
  `ContextLensesApiTests` (lenses, filemap, 003/044/045, MCP `context_lenses`, proposals), and the lock directory shared with `config/lib/locks.sh`.

## S-5A: the onyx adapter

### Shape

- `Aw.Context.Core.Onyx`: `OnyxSettings` (only `Create` makes one: loopback url, a secret-pointer token, a pinned
  release tag; `CheckVersion` compares a reported version), `OnyxRecordKeys` and `OnyxRecordIdentity` (canonical key
  and alias). `SourceHit.Aliases` carries the aliases a hit adds; `RankFusion` merges every hit's aliases into
  `FusedItem.Aliases` (source by source, best first, each once); the query cleans each at ingest and `ResultAssembly`
  egress-scrubs each on its own into `Citation.Aliases` (a blocked or empty one is dropped). JSON `cites.<n>.aliases`;
  the text form shows the first beside the key.
- `Aw.Context.Core.Engine`: `SourcesReader` reads `context.sources` (`onyx`, `linear.teams`) for `SettingsReader`;
  `ContextSettings.Onyx` and `ContextSettings.LinearTeams` hold the result. `ScopeRules.MayRead` keeps intent sources
  from session callers until `context.repoTeams` exists (the linked intent in the graph is separate: owner decision 2026-10-15, B).
- `Aw.Context.Infrastructure.Onyx` (all internal): `OnyxSource` (`IContextSource`), `OnyxClient`, `OnyxRequests` and
  `OnyxRoute` (the allowlist), `OnyxVersionProbe`, the wire records and their own `OnyxWireJsonContext`, and
  `OnyxSources.Create(OnyxSettings, IReadOnlySet<string> teams)`, the per-call factory registered by
  `AddContextInfrastructure`.
- `Aw.Context.Infrastructure.Sources.ContextSourceCatalog` replaces S-1A's repo-docs catalog: repo-docs, then onyx when
  the profile names it; no repo in scope gives no source (`SourceCatalogContract`).
- Sindri: `Composition` passes `ContextEnvironment(environment)` to `AddContextInfrastructure`, which resolves a name
  that is a secret pointer through `ISecretPointers` (`SecretPointerEnvironment`); every other name reads the real
  environment. Doctor's `context:onyx` is `OnyxDoctor` in the `RunDoctor` slice, the third row of `ContextEngineChecks` (after
  S-3a's `context:map` and `context:retention`): it resolves the key's pointer first, then asks the facade's `Sources`
  for the approved profile as a service caller and judges the `onyx` row.

### Routes and allowlist

The adapter can send only these requests to Onyx v4.9.0; `OnyxRequests` refuses anything else before it reaches the
network (tested), and the client has no proxy and follows no redirects. `Search` drops every document Onyx marks
`hidden` (`OnyxSearchDoc.Hidden`; the admin search returns them), so Sindri sees what a standard Onyx search would.

| Route | Use | Key |
|---|---|---|
| `POST /api/admin/search` | `Search`: document ids, `updated_at`, `semantic_identifier`, links; no model | admin |
| `GET /api/document/document-size-info` | `Fetch`: how many chunks a document has | admin |
| `GET /api/document/chunk-info` | `Fetch`: the chunks' text | admin |
| `GET /api/version` | `Probe` and doctor: `backend_version` against the pin | none |
| `GET /api/me` | `Probe` and doctor: the key's role and account type | the key |

### Keys and aliases

Onyx's Linear connector uses the issue's UUID as its document id, so those hits key directly on
`linear:issue/<uuid>` (lower case). The identifier (`ENG-482`) becomes an alias only when the connector-built title
prefix (`[ENG-482] `) and the `linear.app` link name the same identifier and its team is in
`context.sources.linear.teams`. Slack threads (`<channel>__<thread_ts>`) key as `slack:<channel>/<thread_ts>`; anything
else as `onyx:<source_type>/<document id>`. Body text never contributes. Fusion deduplicates on the canonical key, so
the same issue from Onyx and the native Linear adapter (S-6) is one item.

### Versions

`Versions` is always `SND-CONTEXT-010`: no v4.9.0 route gives `updated_at` by id, so onyx citations are `unchecked`.
`SupportsAsOf` is false.

### Errors

| Code | Kind (HTTP) | Exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-002` | Invalid (400) | 2 | `context.sources.onyx.token` is missing or isn't a secret pointer (`profile validate`; the text isn't repeated); at run time, a pointer that doesn't resolve (a row and doctor code) |
| `SND-CONTEXT-050` | Forbidden | — | Onyx refused the key (403), or the key lacks the admin role `POST /api/admin/search` needs: a row, doctor or `degraded` code |
| `SND-CONTEXT-051` | Unavailable | — | Onyx answered with an unexpected status or shape (not the pinned version's): a `degraded` code |
| `SND-CONTEXT-052` | NotFound | — | Onyx has no such document: the item is dropped, not degraded |
| `SND-CONTEXT-053` | Forbidden | — | The admin token belongs to a person's account, not Sindri's dedicated user (`STANDARD`, email `OnyxSettings.DedicatedAccountEmail`, `sindri@example.com`, compared case-insensitively) or a service account: a `degraded` row and a doctor `warn`; the address of another account is never echoed |
| `SND-CONTEXT-055` | Invalid (400) | 2 | The url isn't loopback or the version isn't a pinned release tag (`profile validate`); at run time, Onyx reports another version (a row and doctor `fail`) |

An onyx failure during a query is always the `degraded` entry `SND-CONTEXT-010` with the cause in its message; the
answer's own error is `011` only when repo-docs is down too. The pointer errors are Sindri's: `SND-SECRET-001`
(malformed), `002` (doesn't resolve) and `003` (unsafe `file:`), in `docs/csharp/sindri.md`; through the context
environment a pointer that fails resolves to nothing, which the onyx client reports as `SND-CONTEXT-002` naming only
the pointer's scheme and pointing to `sindri doctor`. `OnyxClient` resolves the pointer once per client (one source,
one query) as a task started off the calling thread and awaited under the call's linked deadline and cancellation, so
a locked keychain or an `op` sign-in degrades onyx with `SND-CONTEXT-010` within the deadline; a resolution that
failed or timed out lives only as long as that source, and the next query tries again. Doctor's `OnyxDoctor` resolves the pointer through
`ISecretPointers` before the probe and fails with that `SND-SECRET-00x` code and message. A query never checks the
version (§8.8 makes it a doctor check; §5.1's errors are Unavailable, NotFound and Fatal): `055` shows in doctor and
the row, and a reply the pinned shape can't read degrades onyx (`010` with cause `051`).

### Tests

- `tests/Aw.Context.Testing/Fakes/FakeOnyxServer`: a loopback fake that answers like v4.9.0 (auth by key kind, admin
  search, document size and chunk info, version, `/api/me`).
- `tests/Aw.Context.Tests/Onyx`: `ContextSourceContract` against `FakeOnyxServer`, the client's allowlist, failures,
  settings and record keys; `Core/SourcesSettingsTests` (profile keys, pointer forms, session rule);
  `Infrastructure/ContextSourceCatalogContractTests`.
- `tests/Aw.Sindri.Tests/ContextCalls/OnyxContextTests` (integration, the real host with `env:` fixture pointers):
  fused query with `unchecked` onyx citations, the status row, doctor ok / warn / fail, Onyx down degrading only
  itself; `OnyxProfileTests` (validate and doctor without Onyx); `Secrets/*` for the pointers.

## S-5B: the intent layer

### Shape

- `Aw.Context.Core/Linking`: `RulesLinker` (rules `issue-key`, `url`, `path`, `identifier`, `quoted-title`; one `LinkRule` per link; an issue key whose record is not fetched yet links by the key, `record:SPL-104`, and the overlay resolves it through the record's aliases, so the links never depend on fetch order), `IntentOverlay` (a record that is only the target of a link is a node too, and a link to a record not synced yet waits instead of counting as orphaned; merges
  `IntentLayer` into the visible map before the deny cut, so a deny path hides intent records too), `DecisionExtractor` and
  `ConflictChecker` (quotes must be verbatim in the scrubbed text; an answer outside the record's linked nodes is refused, `057`),
  `IIntentStore` (records, links, decisions, edges, conflicts, per-source cursor, last sync and last error).
- `Aw.Context.Infrastructure/Intent/SqliteIntentStore` and migration `0006_s5b_intent_links.sql` (outside the map versions, so a build or
  promote never drops it). `FakeIntentStore` and `IntentStoreContract` in `Aw.Context.Testing`.
- `Aw.Context.Features/SyncSources`: `SyncSourcesService` walks each live intent source against the current map (`IntentSyncRun`: search a node,
  then for each new hit fetch, link, commit the record with its links and run `DecisionPass` on it under `context.labeling.maxTokensPerSync`, before
  the next fetch; the node's cursor commits when the node is through), one run per source at a time (`056`). A 2-minute cap (`059`) is checked before
  each node and before each fetch after the run's first, so every run completes at least one record and a slow source can't starve: a record in
  hand is always extracted (the cap never cuts a model call), so no record waits queued because of the cap. A node the cap cuts short keeps its
  cursor at the node and is walked again, its stored hits skipped as unchanged. A capped run records `059` as the source's last code without
  failing it (status: `ok`/`degraded` with code `059`; the next run continues and clears it). A failure of the decision pass in the same run (the model down, a store write failing) wins over `059`: the recorded code is the failure and the source shows `unavailable`. A finished walk commits the end cursor so trailing
  nodes without search terms don't stall the next run. A map version change re-links every record a node's search still returns, across runs (a record no node finds any more is not reached, and keeps its old links until its upstream version moves): a hit is skipped as unchanged only when its upstream version is the stored one and its links were
  computed against the current map version (`intent_record.linked_map`, set by each record's page; `IIntentStore.LinkedUnder`), so a run that stops early (the cap, the source
  down) leaves nothing with old-map links, and each record is re-fetched once per map version, not on every run. The page that moves a done record to a new map version queues it in the same statement (`MarkLinked`), so only a finished extraction marks it done again: a stop, a cancel or a failed save between the commit and the extraction leaves it queued with its old decisions. A failed `LinkedUnder` read fails the run with the store's code (it is never read as "nothing linked", which would re-fetch and re-extract every record). A record stays queued until its conflict checks are through too (a stopped check, the token cap
  or a model failure leaves the pair to the next time the walk reaches the record; a save with the checks pending queues a done record again, so a map
  change that re-extracts it is retried too; a done record whose re-extraction is held back (the token cap, labeling off, a model failure) is queued again by
  `IIntentStore.Requeue` and keeps its old decisions until then); a re-extraction keeps the verdicts of pairs whose decision keys survive and never replaces a human
  decision (`DecisionBatch.Pending`). A verdict answer that can't be read leaves the pair unanswered (`unread`, `tries` 1-2, the record stays queued,
  `057` counted); the third unreadable answer marks it `refused` and it is not asked again. A record whose extraction prompt alone exceeds `maxTokensPerSync` could never be made in any run: it is skipped (`057` counted in `refused`, no decisions, not queued)
  and the pass goes on with the records after it, instead of holding it back forever and stopping the run's extraction. Raise `maxTokensPerSync` to extract it; it is retried when its version changes. A record whose own extraction, as the run's first model call, leaves no room under the cap for even one conflict check has its pairs refused (`057` counted, verdict `refused`) and is done, so it cannot hold back the records after it every run; a refused pair is permanent and is not retried on a version change unless the decision's quote changes, so raise `maxTokensPerSync` before that happens. A first record that got at least one pair answered stays queued and the next run continues. `IContextEngine.Sync(source?, scope)` returns a `SyncReport` of counts and codes only.
- Reads: `MapReader` merges the layer for every graph read; the `item` shape (`context query --item <KEY>`) anchors on the record a tracker
  key names (`ItemAnchors`) and answers `003` with suggestions when none is linked. The fix line follows the state: with `context-sync`
  named in the profile (`ContextScope.SyncConfigured`) it says the record isn't linked yet or the key is unknown and to check the key, wait for
  the next context-sync slot or watch `sindri context sources status` (or `sindri context sources sync`); without it, to name the service. Sindri resolves a Sindri item id (`ITM-…`) before the engine (`ItemKeys`): a linked item
  asks for its record; one with no record yet answers `003` "has no tracker record linked yet"; an id no item has, or a session may not
  see, goes on unchanged and answers the same `003` as any unknown key.
- Decisions and conflicts cite the record their quote comes from (`C<n>` of that record when the answer fetched it, else the map's `C1`);
  `conflicts[].intent` holds those citation markers (never decision keys) and `behavior` stays empty. A feature the record's text does not
  name is not an anchor (the rules link only what the text names), and the feature lens's intent band lists records with a `mentions` edge
  to its own scope, so a record that only reaches it through an `issue-key` link to another record shows on that record's lens.
- `SourcesStatusService` fills `SourceStatusRow.Sync` (`lastSync`, `state`, `records`, `linked`, `decisions`, `queued`, `code`) for each
  intent source; labeling off or over its token cap (`042`) only queues extraction and stays `ok`. While any record is queued and no other code is
  recorded, `code` stays `042` after every run (derived from the queue, so a run that fetches nothing does not clear it). A run the time cap stopped
  shows `059` (state unchanged, `ok` or `degraded`; the fix is to wait, the next run continues), and `042` only when labeling is off or over its cap. A query whose answer reaches a queued record carries
  `degraded` `042` for source `decisions` (`QueryContextService.NoteQueuedDecisions`); nothing is asked of the model.

### Sindri: the `context-sync` service (slice 6)

`ContextSyncService` (`context-sync`, `services: { context-sync: { every: 15m } }`, runs only when named, outside the command gate like
`context-rebuild`) syncs each intent source and records `context.sources.synced` with an additive optional `Links` payload
(`ContextLinksSynced`: counts and the code, never record text). The event key now names its source:
`context.sources.synced:<epoch>:<source>:<slot>` (the docs refresh's key changed the same way). Doctor row `context:sync` warns when an
intent source never synced or its last run failed. `SyncSources` is a job on API P6's runtime (`AddContextJobs`, key `sync`, one for every sync; a start naming a string that is not a kebab-case source id is refused at once as `058`). `SyncContextSourcesService` is the work: it syncs through the engine under the starting caller, one source at a time like `ContextSyncService` (a sync of every source lists the intent sources and calls the engine for each), appends one `context.sources.synced` per source as it finishes (slot: when the job began; appended even if the job is being cancelled) and returns per-source counts and the code (`SyncContextSourcesResult`, never record text). A second sync start while one runs is `SND-JOB-409` carrying `runningJob`; `056` remains the in-run code when a job meets a sync the `context-sync` service is running: it fails the job for a named source and is that source's row (code `SND-CONTEXT-056`, no event) in a sync of every source. The route accepts an empty body (every source). `sindri context sources sync [<source>] [--detach] [--json]` starts the job and follows it with `JobFollower` (`ContextJobVerbs`).

### Errors (056-059)

| Code | Kind (HTTP) | Exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-056` | Conflict (409) | 1 | A sync of that source is already running |
| `SND-CONTEXT-057` | Invalid | - | A model answer the engine refused, or a record skipped because its extraction prompt alone is over `maxTokensPerSync` (or whose conflict checks can't fit even one pair under the cap as the run's first call: the pairs are refused, for good unless the decision's quote changes); counted in `refused`, never fails the sync. A verdict that can't be read is retried (3 tries per pair), then the pair is `refused` |
| `SND-CONTEXT-058` | NotFound (404) | 1 | `--source X` isn't a configured source with a sync |
| `SND-CONTEXT-059` | Unavailable | 0 | A sync stopped at its time cap; partial, resumes from the cursor; shown in `sources status` without failing the source |

### Tests

`tests/Aw.Context.Tests/{Linking,SyncSources,QueryContext}` (rules, overlay, store contracts, sync, decisions, conflicts, item shape,
sources status); `tests/Aw.Sindri.Tests/ContextCalls/ContextSyncTests` (integration: service named or not, event with counts, event key,
sources status, doctor row, item query), `ContextCliTests` and `ContextReviewRowsTests` (the pending form and the catalog row).

## S-7A: the evaluation harness (corpus, as-of maps, arms, reports)

### Shape

- **Core** (`Aw.Context.Core/Eval/Backtest`, pure): `Arm`/`Arms` (names, order, `--arms` parsing, 082), `CorpusBuilder` (a sample is a merge keyed by its issue: the cut is the last first-parent commit before the merge at or before the issue's `createdAt`, and a merge at or before `createdAt` is no sample; truth is `git diff --name-only <merge>^1 <merge>` minus the deny globs), `BacktestScoring` (node recall, strict node precision, file recall@20, macro means; a metric with nothing to average is `null`, "not measured", never 0), `PassLine`/`PassBars` (the §9.4 bars; below 30 samples the overall line is the owner decision and enables nothing), `AdjudicatedScoring`, `MapQuality/*`. Ports: `IMergeHistory`, `IBriefSource`, `IAsOfDocs`, `IEvalStore`, `IArmQuery`, `IAdjudicator`. `MapExtractor.Version` keys the as-of cache.
- **Infrastructure** (`Eval/`): `GitMergeHistory` (first-parent log over `GitCli`, caller `GIT_*` removed), `OnyxBriefSource` (searches each profile team key, keeps hits whose S-5A alias is `TEAM-n`, reads description chunks through the adapter's own allowlist; the brief is cleaned at ingest; a record edited after its creation is `leaky`, because Onyx keeps no content history), `AsOfRepoDocs` (the repo docs at the cut commit, same globs, size cap, chunker and ingest scrub, held in memory; the live `RepoDocsSource` is unchanged and keeps `SupportsAsOf: false`), `FileEvalStore` (0700/0600 files, refuses a worktree with 005, 90-day TTL).
- **Features**: `QueryPipeline` is the old `QueryContextService` body behind one seam (`IMapProvider` plus a source catalog plus an as-of time); `QueryContextService` calls it with the live map, `ArmQuery` calls it with a sample's as-of map and an arm-filtered catalog. **No `asOf`, arm or graph parameter was added to `GET /sindri/context/query` or MCP**, so a session can never see an as-of or arm view. `RunBacktest/*` (`CorpusLoader`, `SampleScorer`, `AsOfMapBuilder`, `ArmRunner`, `ReportAssembly`, `RunBacktestService`), `Adjudicate/Adjudicator`, `EvalMap/*`, `EvalPurge/*`. The eval slices sit behind one `ContextEval` group in the facade.
- **As-of map** (`AsOfMapBuilder`): `MapExtractor.Extract` over the cut commit's tree, plus `ClusterFeatures.Propose` over that commit's clusters with no labels (no labeling pass, no model call), joined under the newest map version created at or before the issue's creation (authored nodes whose code the cut lacks are dropped, `AsOfPruning`), with the `map_override` rows dated before the issue applied, all through `MapAccess.View` (the deny cut every reader makes; no pending proposal). A cache that can't be written costs time, never the map. Cached by repo, sha12, extractor version and whether clusters answered (`eval/cache/maps/<repo>-<sha12>-x<v>-c<0|1>.json`). Nothing is written to `context.db`.
- **Arms**: `brief` (text anchors only, depth 0, no source), `docs` (as-of docs, empty graph), `graph` (the floor), `graph+reviewed` (today's map with every decision; labeled leaky), `all` (as-of graph and as-of docs). `linear`, `onyx`, `code` print `not available (S-6 / S-7B / S-9)`. `ranker` is 082 naming S-8.
- **Heavy lock**: one slot for the whole run (`IHeavyJobLock`); busy is 040. A session caller is 020 ("a human or service job"). Cancellation writes no report.
- **Adjudicator** (`context.eval.adjudicator`, default off): asks twice (second time the surfaces reversed), keeps agreeing answers, a disagreement is `unstable` and counts as not relevant, a model error stops adjudication (`Adjudication.Error`, `adjudication incomplete` in the report, no further call); sends the scrubbed brief inside the untrusted fence and node labels only; truth nodes are never judged; stops at 1.5M tokens (083, degraded, the rest `unjudged`, adjudicated precision "not measured"). The cap is the §9.4 bar kept as a constant, not a profile key.
- **State is files**: `context/eval/{backtest,map,cache}`; no `context.db` migration, no new ledger event type.

### Sindri

`GET /sindri/context/eval/map` (a query: no catalog row), `sindri context eval map|purge|backtest`. `eval purge` runs in the CLI process (`--all` also clears the probe reports, so the next `eval probes` has no baseline) as the CLI's own caller class: a session gets SND-CONTEXT-020 from the engine. The `context-purge` service applies the 90-day eval TTL and `doctor`'s `context:retention` counts expired eval files. **`RunContextBacktest`** is a ready `class: job` row (`JobRows.Job`, `POST /sindri/context/eval/backtest`, command type `RunContextBacktestRequest {Arms, Since, Until, Limit}`). `AddContextJobs` registers it with `AddJob` (key `backtest`; the precheck refuses an unknown arm as SND-CONTEXT-082, and a limit of zero or less or a window that ends before it starts as SND-CLI-003, before any job exists; the request names no repo, so no `repo:` selector is passed), `ContextJobEndpoints` maps it with `MapJob`, and `RunContextBacktestService` runs `IContextEngine.EvalBacktest` under the starting caller with the job's progress, answering `RunContextBacktestResult` (counts, one `BacktestArmLine` per arm, the pass line, the report path, at most 20 degraded notes). `context eval backtest` is parsed by `BacktestLine` and run by `ContextJobVerbs` through the `JobFollower`. A session start is refused by the gate; a second start is SND-JOB-409. The action id is one constant (`ContextRows.BacktestAction`). Goldens: `jobs-id/context-backtest.json`, the catalog row.

### Errors (S-7A range 080-083)

| Code | Kind (HTTP) | Exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-080` | NotFound (404) | 1 | No eligible sample, or every sample was dropped (the message gives the counts left out and why) |
| `SND-CONTEXT-081` | Unavailable (503) | 1 | No configured source can supply issue briefs |
| `SND-CONTEXT-082` | Invalid (400) | 1 | `--arms` names an unknown arm (the message lists the arms and which are not available yet) |
| `SND-CONTEXT-083` | degraded | 0 | Adjudication stopped at the 1.5M-token cap |
| `SND-CONTEXT-090` | degraded | 0 | The rerank (`context.ranker.rerank`, or the `ranker-rerank` backtest arm) could not order the items: the answer lists `rerank` as degraded and keeps the feature or fusion order |

084-086 are reserved for review findings, 087-089 for S-7B. Reused: 020, 030, 040, 048 (a cut git can't read: that sample is dropped and counted), 005, 004 (a cache or context.db read failing stops the run with its own code), 001, 011.

The Onyx issue listing is one admin search per profile team. Onyx v4.9.0's admin search takes no offset and returns at most 50 hits, so it can't be paged: a team whose search returns 50 is reported as `issue listing capped: <team>` (a degraded note, and in the 080 message when nothing was eligible), and `--since` goes into the search as Onyx's update-time filter (`time_cutoff`; an issue is never updated before it was created, so none created in the window is lost) so the cap is spent inside the window. `--until` has no source filter and applies after the listing.

### Tests

`tests/Aw.Context.Tests/{Eval/Backtest,RunBacktest,Adjudicate,EvalMap}` (pure scoring, merge history on the fixture repo, the briefs against the fake Onyx, the whole backtest on the split-panes fixture, as-of honesty, adjudicator with `FakeModelPort`); `tests/Aw.Sindri.Tests/ContextCalls/{BacktestJobTextTests,ContextBacktestJobApiTests,ContextEvalNotRunningTests,ContextEvalStoreHostTests,ContextEvalVerbsTests,ContextJobVerbsTests,ContextLensesApiTests,ContextReviewRowsTests}` (the job text, the job route and its prechecks, the CLI parse and exit codes, the eval verbs).
## S-D: the diff shape

The fifth query shape (`ContextQuery.Diff = new DiffRange(base, head)`, optional `Repo`): the files changed from the merge base of two commits to the second, mapped to nodes, then the same cascade, packing, sources and citations as the other shapes.

- **Refs only.** `DiffRange` is two lower-case hex shas of 7 to 64 characters. A branch, tag, `HEAD`, `~`, `^`, `...` or upper case is `SND-CONTEXT-006` ("resolve the refs to shas first"); the engine reads no refs and makes no forge calls. Resolving a PR to its shas is the caller's job.
- **Git.** `IRepoReader.Changes` runs `rev-parse --verify --quiet <sha>^{commit}` per side (the full sha it returns must start with the input: an abbreviation that is also a branch or tag name resolves to that ref in git, so it is refused as 110 with "pass the full sha", as is an ambiguous abbreviation), `merge-base`, then `diff --name-status -z --no-renames --no-ext-diff --no-textconv <mergeBase> <head> --` (ArgumentList, `GIT_*` removed, 30 s). Nothing is read from the working tree or from file content. A rename is a deletion plus an addition.
- **Result.** `diff` holds `repo`, `base`, `head`, `mergeBase` (full shas), `files[]` (`path`, `change` of added, modified, deleted or typeChanged, `status` of mapped, suggested or unmapped as `filemap` says, `nodes[]` of `key` and `confirmed`), `unmapped[]`, `counts` (`changed = mapped + suggested + unmapped + denied`), `truncated` (at most 1000 files kept: when set, `files[]` and `unmapped[]` hold the first 1000 visible paths in ordinal order, while `counts`, `anchorsTotal` and the anchors cover every visible file), `anchorsTotal` and `anchorsTruncated`. `anchors` are the first 20 nodes: confirmed before suggested, then by key; `files[].nodes` always lists every node.
- **Deny globs.** A changed path under the repo's deny or secret globs is only counted in `counts.denied`, never named, for every caller. Every caller's paths (`files[].path`, `unmapped[]`) go through the same egress scrub (`MapText`) the cascade's code paths get, so one answer never shows a path two ways (scrubbed, not fenced).
- **One repo's links.** The changed files are read against the addressed repo's links only: a `code:` link whose file evidence (`repo:<repo>/<path>`) names only other repos is ignored, so a path two mapped repos share claims only the addressed repo's nodes. A link with no repo evidence counts for any repo.
- **Repo.** `repo` may be left out when the caller's scope holds one repo (always for a session); otherwise it is required (006). A repo outside the profile or the session's scope is 045 (one answer for every such reason). No map is 030, which a session whose own repo implements no node also gets; `draft` with no draft is 044.
- **Errors.** `SND-CONTEXT-110` (NotFound, exit 1): the clone does not hold a sha. `111` (Invalid, exit 1): no merge base. 112 to 119 are reserved; 120 to 129 belong to the lenses and map versions (S-LB1a). An unreadable checkout is 048.
- **Tests.** `QueryRulesDiffTests`, `DiffAnchorsTests`, `DiffQueryTests`, the `RepoReaderContract` Changes cases (fake and git), `ContextDiffTests` and the `context-query` API goldens (`diff-spl-103`, `diff-unknown-sha-110`).

## S-LB1a: lenses over a set of anchors and as of a map version

- **Anchor sets.** `GET /sindri/context/lenses?anchor=A&anchor=B` (up to 20; more is `SND-CONTEXT-006`) answers schema 2 (`LensesSetView`): `anchors[]`, `missingAnchors[]` (an anchor this caller can't read or that doesn't resolve; none resolving is 003), one union of the bands read from one map snapshot, and `coverage.byAnchor`. Each band item names the anchors that reached it (`anchors`) and its `depth`. Repeats of one value, or one `anchor`, keep schema 1 with its old shape plus the additions below. The request types are `GetContextLensesRequest` (one) and `GetContextLensesSetRequest` (several); the endpoint picks by the distinct count. Top-level `edges` (mentions, decides, quotes, supersedes, implements, tests) and `bands.product.edges` (structure types) are disjoint by type: draw both.
- **`mapVersion=`.** `lenses` and `query` read the graph as of a retained version: `v3`, `3` or `v3@a41b07d`. `draft` with `mapVersion` is 006. A version that isn't held (decided by asking the store, never by the retention count) is `SND-CONTEXT-120` (NotFound, 404, exit 1). The answer's `mapVersion` is exactly `v<N>`; the commit the version was built at is the separate `mapCommit`: 7 lowercase hex characters, omitted when the version names no commit (`LensBasisReader.ShortCommit`, pinned by `MapCommitTextTests`).
- **Schema 1 fields.** `mapVersion` changed from `v<N>@<sha>` to exactly `v<N>`, the commit now in the sibling `mapCommit` (7 lower-case hex, omitted when none); decisions gained `label` and `kind`, intent records `kind`, code files `kind`/`label`/`trust`.
- **`searched`.** Every lenses answer says what was looked at: `anchors`, `mapVersion`, `depth`, `hops`, `sources[{id, state, reason, code, asOf}]` (`map`, `onyx`, `code-index`; `state` is `ok`, `not-configured`, `unavailable` or `degraded`, never `error`) and `bands{intent,product,code}.empty`.
- **`coverage.map`.** `{paths, truncated, files, unmapped, suggested, created, ageDays}`: the repo's tracked files at HEAD (never a denied path) under the anchors' paths. `created` is the version's moment for a version read; `asOf` is when the answer was built.
- **`cites[*].fetchedAt`.** A query citation carries `fetchedAt`: map-authored: the version's `created`; live source hits: when this answer fetched them (the answer's `asOf`). Lens results carry no cites yet.
- **Errors.** `SND-CONTEXT-120` is the first of the range 120 to 129.
- **CLI.** `context lenses <key>... [--depth N] [--draft | --map-version V] [--json]` (no key is a usage error, exit 2, nothing sent; `--draft` with `--map-version` is SND-CONTEXT-006, exit 1, nothing sent) and `context query ... --map-version V`. Text names missing anchors and prints each source state as a word (`onyx degraded (SND-CONTEXT-010)`).
- **Tests.** `LensesSetTests`, `LensesAtVersionTests`, `MapCommitTextTests`, `ContextLensesApiTests`, `ContextReviewVerbsTests`, the `context-lenses` goldens (`set-two-anchors`, `set-missing-anchor`, `set-21-anchors-006`, `map-version-v1`, `map-version-not-retained-120`, `session-set-other-repo`).

## S-CL: the code lens (`GET /sindri/context/code`)

- **What it is.** `GET /sindri/context/code?anchor=A&anchor=B[&draft=true | &mapVersion=v2]` (scoping station C3) answers `CodeView` (schema 1): `files` (files with an `implements` edge into the anchors' own scopes, each with `testedBy[]`), `tests`, `data.things` (the Things the own scope reads or writes, with `access` and `via`), `data.files` (schema files that name a Thing), `callers` and `callees` (`unavailable` with reason `index-not-built` until the code index is wired, S-9; see S-9a below), `counts`, `truncated`, `lexical[]`, `edges`, `coverage`, `searched`, `degraded` and `cites`. The anchor-set rule, `missingAnchors`, `mapVersion=`, `coverage` (with `map` and `byAnchor`) and `searched` are the lenses' (S-LB1a), through the shared `AnchorSet`. In process the same call is `IContextCode.Code`, which the workflow engine's `input.context.code` block will call (its descriptor is still pending).
- **Tests.** `basis` is `edge` (a stored `tests` edge), `imports` (the test's text names an implementing file's last two path segments, slash or dotted form, with word boundaries) or `sibling` (the test's stem without its marker equals the implementing file's stem, never a generic one such as `index`); `edge` outranks `imports`, which outranks `sibling`. A lexical test is `suggested: true` with no `linkId` (it is not a map link). Test files are the repo's tracked files that match `context.code.testGlobs`, matched **case-sensitively** (so `latest.ts` is not a test).
- **Schema files.** For each searchable Thing (at least 4 characters in some form of its key name, label or aliases; at most 30), the repo's tracked files that match `context.code.schemaGlobs` and no deny glob are searched for the name in four forms (`shared-link`, `shared_link`, `sharedlink`, `shared link`, with a trailing `s` or `es` allowed). `[]` turns the scan off (`lexical-schema` is `not-configured`).
- **How it reads.** `git grep` at one commit (never the working tree), then the candidates (100 at most, 512,000 characters each) are read through the repo reader (deny globs enforced) and matched in process. The commit is the map version's when it names one for that repo, otherwise the repo's HEAD; `lexical[].commit` says which. Only a path and the form that matched leave the engine, never a line. Search terms come from map text cleaned by the scrubber, and a name the scrubber redacted gives no term.
- **Failures are rows, never route errors.** `searched.sources` has `map`, `code-index`, `lexical-tests` and `lexical-schema`. A clone without the map's commit makes both lexical rows `unavailable` with reason `commit-not-held` and code `SND-CONTEXT-110` (a recorded limit; the engine never fetches). The profile's `context.deadline` bounds the lexical step: on expiry what was found is returned and the row is `degraded` with `SND-CONTEXT-152` (and a line in `degraded[]`). More than 50 implementing files, or more candidates than the cap, is `ok` with reason `capped`.
- **Caps.** 50 implementing files searched; 30 Things; 100 candidates per search; 20 schema files per Thing; 100 entries per list. They are fixed in `CodeRules`, not profile keys.
- **Errors.** `SND-CONTEXT-150` (a `context.code.schemaGlobs` or `testGlobs` entry is not a repo-relative glob; the message starts with the key path), `151` (more than 50 entries in one list), `152` (the lexical search ran out of `context.code.deadline`; only ever the code of a `searched` row). Reused: `003`, `006` (over 20 anchors, a malformed `mapVersion`, `draft` with `mapVersion`), `037`, `110`, `120`.
- **Session cut.** A session reads through the same scope as `lenses`: an anchor outside its repo is not found, and the lexical step searches only repos the caller may read.
- **Tests.** `GetCodeTests`, `CodeLensBuilderTests`, `CodeLensMergeTests`, `TestPathsTests`, the `CodeSearchContract` cases (fake and git), `ContextCodeApiTests`, `ContextCodeVerbsTests` and the `context-code` API goldens.

## S-9a: the code symbol port (Context's half of wiring the code index, on a fake)

`Aw.Context.Core/Symbols/`: `ICodeSymbolSource` (`Heads`, `FindSymbols`, `FileSymbols`, `Callers`, `Callees`; every call takes `asOf`, the commit the map version recorded for the repo, or null), its rows (`SymbolSite`, `SymbolHit`, `FileSymbolsRow`, `CallEdgeRow`: each carries `commit`, `asOfCommit` and `state`), the answers (`SymbolAnswer<T>`, `CallAnswer` with `unresolved`), `CodeIndexHead` and `CodeIndexState` (the block's words: ok, stale, checkout-not-on-main, layer-unavailable, missing, damaged, building, not-configured). Context never references `Aw.CodeIndex`: Sindri's Host adapts the block's facade to this port in S-9 (a `CodeIndexSymbolSource`, replacing the default `NoCodeSymbolSource` that `AddContextFeatures` registers, so the lens reads `index-not-built` until then).

- **As-of, computed at read time.** `CodeIndexReading.Head` makes a readable index that is not at the map's commit `Stale` with both commits kept (a map published ahead of its index, or an index ahead of the map), `Stamp` gives every row `state: stale` and `asOfCommit`, and nothing is stored, so rows turn fresh when the index for the map's commit lands. An abbreviated commit (7 or more characters) is the same commit as its full sha; no `asOf` never mismatches.
- **The lens row** (`searched.sources`, `code-index`) follows spec §7.3: ok; degraded for `stale`, `checkout-not-on-main` or a layer that is down (reason the layer, code the block's `SND-CODEIDX`); unavailable for `index-not-built` (`SND-CODEIDX-013`), `index-damaged` (`-010`) and `index-building`; not-configured. A read that fails is degraded with that error's code, never an error of the route. `Degraded`'s comment now reads "answered from an older or partial source, or its last run failed".
- **Code lens.** `CodeSymbolSearch` (Features) asks, per repo the listed files belong to, `Heads`, then `FileSymbols` (at most 200 files), then `Callers` and `Callees` at depth 1 for at most 20 symbols (exported first, then file order; the caps are `CodeRules.MaxCallSymbols` and `MaxSymbolFiles`), and lists at most 100 edges, the strongest per symbol first. `callers` and `callees` are `CodeSymbols`: `state` (`unavailable`, `stale`, `ok`), `reason`, `items`, and, when the index answered, `commit`, `asOfCommit`, `asOfMismatch`, `unresolved`; they are the same `unavailable / index-not-built` as before for a repo with no index. When a read of the index fails, `callers` and `callees` are `unavailable` with reason `index-unavailable` and the failing `SND-CODEIDX` code. Names, signatures, kinds, refs and paths are cut to one line (line breaks and runs of white space become one space) and then scrubbed, like every other piece of map text.
- **File map.** The top-level `symbols` object keeps its `state` and `reason` ('index not built', 'index building', 'index damaged', 'not configured', 'stale', or 'index unavailable' with the failing `code` when a read fails) and gains `files[]` (the symbols each listed file defines; a directory row has none), `commit`, `asOfCommit`, `asOfMismatch` and `code`, all left out of the JSON when empty. The design is `mockups/context-filemap.html` in the UI screens spec (the column and its empty states, "from abcdef0, not 1234567" for as-of mismatch).
- **Not in S-9a:** the lenses' code band `symbols` items, symbol-level `implements`, `context query --symbol` and `coverage.code.level` stay S-9's.
- **Tests.** `CodeSymbolSourceContract` (the fake and S-9's adapter pass the same cases, including stale, index-ahead and missing), `CodeIndexReadingTests`, `CodeLensSymbolsTests`, `GetCodeSymbolsTests` and `GetFileMapSymbolsTests` with their golden files.

## S-H: the history lens

`GET /sindri/context/history` (`GetContextHistoryRequest`, `IContextHistory.History`, `HistoryView` schema 1; `sindri context history`): the records and decisions behind the anchors' own scopes, cut by a window, and the landed changes of the files that implement them. A query (ARCH-CQRS-001); no MCP tool.

- **Shape.** `Aw.Context.Core/History/`: `HistoryWindow` (absolute instants only), `MergeSubject`, `RecordTimes`, `HistoryCut` (the pure cut, union, order and caps), `HistoryLens` (resolve the anchors' own scopes over `LensSets`, the files with their repo from the edge evidence, `Edges`, `Coverage`, `Searched`), `IFileHistory` (git adapter `GitFileHistory`, `GitCli.FileLog.cs`; fake `FakeFileHistory`, both held to `FileHistoryContract`). `Aw.Context.Features/GetHistory/`: `GetHistoryService` (map read, settings, scrub), `HistoryPipeline` (intent times, the cut, one `git log` per repo bounded by `context.deadline`), `HistoryInputs` (the map and window; a backtest arm hands it an as-of `VisibleMap` and the sample's creation instant), `HistoryTexts` (scrub, one line, fence).
- **The cut (CTX §9.1).** `before` is inclusive. A record whose `createdAt` is after it is dropped; one whose update is after it, or whose times are unknown, is `leaky`. A `human` decision is cut by `at` (null is kept); a model or deterministic one follows its quoted record. `since` cuts changes and records, never decisions. Ordering: records by update, decisions by `at`, changes by committer date, newest first, then key; 100 each after the cut. `createdAt` is the new column `intent_record.created_at` (migration `0007_s5b_intent_created`, filled from `SourceRecord.CreatedAt`); `fetchedAt` is the stored `fetched_at`.
- **Files.** Those with an `implements` edge into the own scope, confirmed then confidence then key, at most 50, only in repos the caller may read and outside the deny globs; a file whose edge evidence names no repo counts in `counts.unlocated`. The upper bound is the map version's commit when it names one in the version's repo, else HEAD (`git[].upTo`, `head`).
- **`searched`.** Reasons: `capped` (101 changes in a repo, or files cut at 50), `shallow-clone`, `no-files`, `commit-not-held` (110), `history-unlinked`, `not-synced` (161), `not-configured`. A row has a reason, a code, both or neither.
- **Not-synced gaps.** `SearchedSources.Intent` gives a never-synced configured source `unavailable` / `not-synced` / `SND-CONTEXT-161`. The Sindri services (`GetContextHistoryService`, and the two lenses services) add `sync: {state, jobId}` from the `jobs` view (`SyncGaps`, ARCH-CQRS-001: the engine reads no jobs); `ApiViews.ContextLenses` and `ApiViews.ContextHistory` are invalidated by `job.started`, `job.finished` and `job.cancelled`. `ContextSyncGapsService` (built-in, every minute, a quiet run when nothing is missing) starts `SyncContextSourcesRequest` through its command chain as `service:context-sync-gaps` (the authorization table already lets class `service` run action class `job`): `SND-JOB-409` is a quiet run, any other refusal fails the run (needs-you `failure:service:context-sync-gaps`), it never starts a second sync while one runs, and after failed syncs in a row it re-checks on the scheduler tick by reading the jobs view; the scheduler has no event trigger. The decision is the pure `SyncGapDecision` (Core): nothing, job running, backoff not due (5, 10, 20, 40, 60 minutes at most; a success resets), start. A tick with nothing to do is only view reads: no command, no ledger write, no log.
- **Errors.** `SND-CONTEXT-160` (Timeout, the git step ran out of `context.deadline`) and `161` (Unavailable, a configured source never synced) are codes of `searched` rows, never a route failure; 162 to 169 are reserved. Reused: `003`, `006` (more than 20 anchors, a malformed or relative or reversed window, a draft with a version), `037`, `048`, `110` (also the as-of map version's commit missing from the clone), `120`.
- **Tests.** `HistoryWindowTests`, `MergeSubjectTests`, `RecordTimesTests`, `HistoryCutTests`, `FileHistoryContract` (fake and git), `GetHistoryTests`, the intent store contract's `created_at`/`fetched_at` cases, `ContextHistoryApiTests`, `ContextSyncGapsServiceTests`, `ContextSyncGapsHostTests`, `ContextHistoryTextTests`, the `context-history` goldens.

## S-LB1b: the subgraph (scoping spec C14)

`GET /sindri/context/subgraph?nodes=&nodes=&depth=&mapVersion=` and `QUERY /sindri/context/subgraph` (the HTTP QUERY method, the IETF safe method with a body, draft; JSON body `{nodes[], depth?, mapVersion?}`) answer `SubgraphView` (schema 1): the bands, edges, conflicts and coverage of the given node keys and of what they reach within `depth` (0 to 4, default 0) edges in either direction. One service (`GetContextSubgraphService`, over `IContextLenses.Subgraph`) and one session filter serve both transports, so the same keys answer the same bytes; the pair is pinned by `ContextSubgraphApiTests`.

- **Limits.** GET takes at most 20 distinct keys, each at most `QueryRules.MaxEncodedText` characters once percent-encoded; QUERY takes 1 to 1000. Anything else, an empty list or a malformed key is `SND-CONTEXT-006` (400; the message names the key). `depth` outside 0 to 4 is 006. An unretained `mapVersion` is `SND-CONTEXT-120`.
- **Missing keys.** A key the version does not hold, a session cannot see or a deny glob hides goes to `missing` (request order), one answer for every reason. All keys missing is a valid answer (empty bands), not 003. Edges to a hidden node are dropped.
- **Shape.** `nodes[]` echoes the keys found; bands are keyed by kind (records and decisions in `intent`, `code:` nodes in `code`, the rest in `product`), at most 100 per list with `truncated` and the full `counts` (`bands.product.counts` also carries `neighbors` and `edges`, the totals before the cap). Every item carries `label`, `kind`, `trust` and `depth` (distance from the given keys). The top-level `edges` holds the cross-band edges among the returned nodes (a code file implementing or testing a feature, an intent record mentioning a node) and `bands.product.edges` the structure edges among the product nodes; the two never overlap, so draw both (checked against the stored edge rows: their union is every stored edge among the returned nodes). Bands hold only the given keys and what widening reaches (no cascade), so at depth 0 the given product nodes bring no records or decisions: `searched.bands.intent.empty` describes that band, while `coverage.intent` still counts the intent attached to the given product nodes (plan C15: coverage is over the given product nodes), and a conflict on a given node is summary-only (its `decision` and `other` keys may be outside `bands.intent`; give the decision key, or widen, to read them). A session sees only what its repo evidences (CTX §3), so the same widening can reach different nodes: a session gets a code file's `implements-by` neighbor where the human also reaches evidence-less surfaces through `part-of`. `asOf` is when the map was read for the answer; `coverage.map.created` is when that map version was written, so for a retained version they differ.
- **QUERY is a query.** It sits in the queries group (`X-Sindri-After` honoured), takes no idempotency key, writes no ledger event, refuses an `Origin` header with 403 `SND-API-008` ("a body query from a foreign Origin is refused"), and has no catalog row (queries have none, ARCH-CQRS-003; API10 means goldens only). ARCH-CQRS-001 reads "queries are GET or QUERY; a command is never GET or QUERY", with no carve-out list; future body reads use QUERY, never POST. Clients use `new HttpMethod("QUERY")` or `curl -X QUERY`; a POST to the route is 405. Owner decision 2026-10-18, replacing the POST carve-out.
- **CLI.** `sindri context subgraph <key>... [--keys-from <file|->] [--depth 0-4] [--map-version V] [--json]`; the keys file holds one key per line, `#` comments and blank lines skipped. It sends GET up to 20 keys that each fit, otherwise QUERY.
- **Goldens.** `Golden/Api/context-subgraph/`: `get-three-keys`, `query-packet-set` (the same keys as the GET), `depth-2`, `missing-after-v2`, `session-own-repo`, `too-many-006`, `not-retained-120`, `query-refused-origin`. The slug is in the UI lane's `_notRead` until U9.

## S-CH: the opt-in component hop

`hops=` (query string, `--hops` on `sindri context query|lenses`, an optional string `hops` on MCP `context_query` and `context_lenses`) lists
the cascade hops to walk, exactly as written: `hops=component` walks only that hop. Absent, the cascade keeps its three hops and every answer is
byte-identical to before. A name outside `state`, `flow`, `dependency`, `component` is `SND-CONTEXT-140`. `neighbors` takes no `hops`.

The component hop goes from an own-scope node through a confirmed `implements` edge to its code unit and through another confirmed
`implements` edge to another node, ascended to its feature, at cost 1. Its path steps read `component <code key>` then `implements <node key>`.
A suggested `implements` never counts. A code unit implementing more than `context.cascadeComponentFanOut` features (1-50, default 5; counted on
the caller's visible graph) is not walked: when that withheld a node, the answer carries a top-level `skippedComponents: [{unit, features}]`
(ordinal by unit, omitted when empty) on `lenses`, `lenses` sets and `query`. `searched.hops` echoes the hops that ran in cascade order
(`state`, `dependency`, `flow`, `component`), whatever order the request listed them. The same hop runs under `diff=`.

## S-LB2: the context Step inputs

`input.context.lenses`, `input.context.history` and `input.context.code` are the scoping recipe's gather inputs (scoping spec C1, C3, C5). Each calls its facade in process (`IContextEngine`: lenses, history and code) as `service:engine` with a `ContextScope` cut to the instance's repo (`ContextInputScopes`: the profile's repos filtered to the one repo of the instance's item; a missing repo or a repo outside the profile is `SND-CONTEXT-045`, a missing profile `SND-PROFILE-005`). The answer reaches the Step as a text section and as the typed view; a refusal becomes a section that says so. Details and the section names: `docs/csharp/sindri-workflow.md` "The context inputs (S-LB2)". The plain-text renderers (`ContextLensText`, `ContextHistoryText`, `ContextCodeText`) live in `Features/ContextCalls` and are shared by the CLI verbs (the host sees them through `InternalsVisibleTo`).

## F1P: publishing a refreshed map with the code index (the store half)

The map and the code index still build on their own; this is where a refreshed map waits for the index of its commit. The decision, its numbers and
its words are the code index spec's (`2026-10-19-sindri-code-index-design.md` §6.4, CI13); the contract of record for what the store holds and what
each actor may do is the retrieval spec §8.7a. This section is the as-built record of `Aw.Context` (F1P-a); Sindri's pure decision, jobs and tick are F1P-b.

### Shape

- **Tables (migration `0010_s4p_map_publish`).** `map_version_head(version, repo, commit_sha, on_default)` (cascade-deleted with the version; the commit is 7 to 40 lowercase
  hex, checked by the table too), `map_hold(version)` (one draft; cascade-deleted with it) and `map_publish_chain(id = 1, since)` (the start of the wait: tied to no draft, so a
  rebuild keeps it). Reads never migrate: a store from before 0010 holds nothing and has no heads.
- **Types (Core/Map).** `MapHead(Repo, Commit, OnDefault)` (`IsCommit`, `Check`: 191), `MapHold(Version, Since, Heads)` (`NextSince(chain, now)`: the chain's start, never later than now),
  `MapPublishState(Hold, LatestDraft, Current, CurrentHeads)`, `MapPublishView(Repos, Hold, LatestDraft, Current, CurrentHeads)` and `MapBuildReport.Heads`.
  `MapImport.Heads` is what a build names; null keeps a replaced draft's heads, or for an import the one commit its file names (as `OnDefault`).
- **Store (`IGraphStore`).** `Heads(version)`, `Held()`, `Hold(MapHold)` (one transaction: the draft must be the latest, else 190; `since` is `MapHold.NextSince(stored chain, hold.Since)` and the clamped value is stored;
  any other hold is replaced), `Release(version)` (the chain stays) and `AppendOverrides(rows, ct, expectedDraft)` (the latest draft is checked inside the append's transaction: 190, because the overrides layer is shared across versions).
  Writes: a build (`SaveDraft`) drops the old draft, its hold and heads with it and writes the new heads; `ReplaceDraft` and `OpenDraft` copy the source's heads (a replacement that names heads keeps those);
  every `Promote` and every import (`SaveCurrent`) deletes the hold and the chain in its own transaction; retention (`Delete`) removes a version's heads, and a retained version keeps them.
- **Facade (`IContextMap`).** `Held`, `Hold`, `Release` refuse a session (020); `PublishStatus` is the read for everyone else and is cut to the caller's repos (`context.repos` for a human, the one repo for a session, the
  usual 020/003 session rules): no other repo's name, commit or count. `Promote` and `CleanDraft` take `int? expectedVersion = null` (null: the latest draft, the human path). With a value, a latest draft of another number, or none,
  is `SND-CONTEXT-190` and nothing is promoted or written; the cleanup checks it on every pass read and on its append. Without one, no draft is still 046.
- **Build.** `BuildMapService` records, for each mapped repo, the commit its extraction read and whether the default branch holds it (a default ref that can't be read counts as not on it) into `MapBuildReport.Heads` and the draft.
- **Profile.** `context.publish.waitForIndexMinutes` (`ContextSettings.Publish`, 0 to 240, default 15), `SND-CONTEXT-001` with the key path outside the range. A map file whose `commit` is not 7 to 40 lowercase hex is `SND-CONTEXT-031` at `commit`.

### Errors

`SND-CONTEXT-190` (`DraftChanged`, conflict, attention): the expected draft is not the latest. `SND-CONTEXT-191` (`MapHeadInvalid`): a head's commit is not 7 to 40 lowercase hex, or a repo appears twice. `192..194` stay reserved.

### Tests

`MapPublishStoreContract` (`tests/Aw.Context.Testing/Map`, run against `FakeGraphStore` and the SQLite store): heads in order, an import's one commit, invalid commits refused everywhere, the hold and its read, 190 for a wrong draft, the chain surviving a
rebuild and clamped to now, promote and import clearing the hold and the chain, replacement and open copying heads, retention, release, `expectedDraft`. `MapPublishTests`, `MapHoldTests`, `PublishSettingsTests` and `MapPublishMigrationTests`
(an old store opens and gets the tables) in `tests/Aw.Context.Tests`.


## S-8: the ranker stages and the ranker arms

The query's fusion (`RankFusion`, reciprocal rank fusion) is followed by two optional stages, both **off by default** (`context.ranker`, `docs/csharp/sindri-profile.md`); the profile enables a stage only after a backtest shows it helps, or the owner decides below 30 samples.

- **Features** (`Aw.Context.Core.Ranking.FeatureRanker`, pure). Five scores from 0 to 1 per fused record, from how the map links it to the nodes the answer reached (`GraphPart.RecordSignals`, read by `RecordLinks`): cascade distance (`1 / (1 + steps)`), edge type (decides 1, quotes 0.7, mentions 0.4, other 0.2), provenance (human 1, deterministic 0.7, model 0.4), supersession (a record whose decision a later one replaced scores 0, otherwise 1) and recency (halves every 90 days from the link's date). The boost is the weights' mean of the five, and the item's fusion score is multiplied by `1 + boost`: features can at most double a score, so they reorder neighbours and never overturn the fusion's lead wholesale. Weights start equal.
- **Rerank** (`RankerStage`, `RerankOrder`). Only for the top `rerankTop` items (default 30, at most 50) and only when there are two or more: the scrubbed, fenced titles go to the model port with purpose `rerank` (a budgeted call, 20 seconds, 4 tokens per item), and the answer is read as a JSON array of the item numbers. Numbers out of range and repeats are skipped and items the model left out keep their order after the named ones. A model that is unavailable or answers no order leaves the order as it was and the answer lists `rerank` as degraded (SND-CONTEXT-090). A local cross-encoder adapter is not built: the port is the model port, and a cross-encoder would be another `IModelPort` adapter, not a new stage.
- **The ranker arms.** `context eval backtest --arms ranker` runs `ranker-rrf`, `ranker-features` and `ranker-rerank` over the same inputs as `all` (the as-of map and the docs at the cut) with the stage set forced by the arm (`ArmQuestion.Ranker`, never reachable from the query API or MCP); each row has dimension `ranker`. `--arms all` does not run them, because the rerank row asks a model. The rerank row's model calls are not counted against the adjudicator's token cap.
