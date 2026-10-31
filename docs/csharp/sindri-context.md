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
2. `QueryRules.Check`: one shape. Text becomes at most 64 lower-case terms. There is no token budget: `ContextQuery.Budget` and the profile key `context.maxBudget` are retired (read and ignored, so an old client or profile still works). Anchor shapes are 003
   until the graph exists.
3. Every source in `ISourceCatalog.For(settings, repos)` is searched in parallel under the deadline. An error or a
   missed deadline makes that source degraded (010); all of them failing is 011.
4. `RankFusion.Fuse`: hits sharing a canonical key merge, keeping each source's best rank. Each source is re-ranked
   densely, then RRF with k = 60 runs across sources.
5. The top 20 are fetched in turn until the deadline (what isn't fetched in time isn't shown), cleaned whole by
   `IIngestFilter` (sanitize, collapse titles, scrub; line-preserving), kept whole. The sanitizer drops inline and
   HTML images and reduces reference-style ones to their text, so no image survives ingest wherever its definition sits, and removes
   remote reference definitions also inside block quotes and list items (one-token destination, optional title).
6. One batched `Versions` call per source, each with only its own references, gives each citation its state.
7. `ResultAssembly`: one `record` item and one citation each. Every field, the citation's key and ids included, is
   scrubbed again on egress (a blocked field is `[REDACTED:blocked]` alone). The title (at most 200 characters) and
   the text are fenced as untrusted, and the items are grouped into sections with none dropped (`Packer.Pack` has no budget; the retired `truncated`, `budget` and per-section `more` fields are gone from the wire). A caller pages the result set (MCP `limit` and `offset`, `McpPages`: both optional, offset 0 and limit 100 by default, limit under 1 or offset under 0 is 006; only top-level lists page, each by its own index, never inside an item; `page.more` and `page.lists` are computed on the whole set).

No model is called on the query path.

### Repo-docs

- **Index.** `SqliteDocIndex.Refresh` lists the tree at HEAD (`git ls-tree -r -z`) and matches the globs (`Glob`). It
  reads changed blobs (no size limit: a document over 1 MiB is chunked like any other, removed per owner q93, 2026-10-23), cleans each
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
| `SND-CONTEXT-006` | Invalid (400) | 1 | The question is invalid: no words, two shapes, depth out of range |
| `SND-CONTEXT-010` | Unavailable | — | One source is unavailable: a `degraded` entry, never the answer's error |
| `SND-CONTEXT-011` | Unavailable (503) | 2 | Every source is unavailable |
| `SND-CONTEXT-020` | Forbidden (403) | 1 | Session reads are off (`context.sessions.enabled: false`, on by default since owner q93, 2026-10-23), on every transport; also a session asking for a human or service job (map import, export, hold, release, eval purge). Defined by S-1A (spec §3); S-1M maps it to the MCP tool error |
| `SND-CONTEXT-021` | Invalid (400) | 1 | The call names no repo (no `X-Sindri-Repo` header): every context call says which repo it is for, with no guess from the folder and no fallback (owner q94, 2026-10-23). The message names the header and gives an example |

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
beyond the newest 100 retained **and** its successor (the next Current-or-Retained version by number) was created more than
90 days ago. A version is what an as-of read (`AsOfMapBuilder.Authored`, newest Current-or-Retained created at or before a
sample's date) returns for the dates from its own creation to its successor's, and the backtest's eval window is 90 days (the
eval cache and report expiry), so a version replaced inside the window may still be read. No migration: `map_version.created`
is enough. `CheckRetention` counts the same set.

`GET /sindri/context/map/export` is the one query that writes: a file under `exports/` (owner-only, purged after 14
days), never the store or the ledger. It is a query because the API spec lists it as one, and it changes no state a
command or event describes; a later file-writing route should not take it as a precedent without the same reasons.
Import refuses a session-kind caller in the engine too (SND-CONTEXT-020), whatever surface reaches them; preview is open to a session (owner q93, 2026-10-23).

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
  - `GraphWalk.Neighbors` (depth 0–8; deeper is clamped) and `GraphWalk.ShortestPath` (any length, either way; the 8-edge limit was removed per owner q93, 2026-10-23).
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
  - The `/node` answer (also `context_node` and `context show --json`): `mapVersion, node, aliases, out[], in[]`. `node` is `key, kind, label, provenance, confirmed, trust, confidence` plus `source` and `description`, both additive and null when the map stores nothing. `source` is `{kind, ref, title?, url?}`: a `record:` node names its record (`linear:issue/…` is a `ticket`, `linear:doc/…` and `repo:…` a `doc`), a `code:` node its path (`file`), a decision the record it quotes (title from that record's node when the map holds it), any other node its first evidence record; `title` is untrusted text and is fenced like a label. `url` is null today: the map stores no permalink and no forge URL, and `Permalinks.Allowed` admits only linear.app. `description` is a decision's quote, scrubbed, one line, cut on a word to 280 characters with `…`, and always fenced as untrusted for a caller that gets fences (the quote rule); a record, code or feature node has none because the map keeps no record text. `provenance` is who produced the node (`human`, `deterministic`, `model`), not a reference. Each `out`/`in` edge (`from, type, to, provenance, confirmed, trust, order`) adds `otherKind` (the kind of its `to` for `out`, its `from` for `in`) and `otherLabel` (that node's label, fenced like any label; null when it is not in the caller's map), read from the one graph the answer already holds. Core types `NodeDetail`, `EdgeDetail`, `NodeSource`, built by `NodeDetails`; `schemaVersion` does not apply to this answer, so nothing is bumped.
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

039 is reserved. A depth (neighbors, query) that is out of range (depth negative; a depth past 8 is read as 8) is `SND-CONTEXT-006` on every transport, and one that is not a whole number is 006 in the CLI (exit 1) and HTTP (MCP's binder answers `SND-CONTEXT-022` for an argument that isn't a 32-bit JSON integer, even one that is only too large), parsed once in `QueryRules.ParseDepth`; the CLI also refuses a question or `--node` over `QueryRules.MaxEncodedText` (6000) characters once percent-encoded with 006 before sending, so Kestrel's 8 KB request line never answers 414; the CLI adds its own `--depth` hint; a graph read with no map reuses 030; a key, alias
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
  keybindings, settings keys, routes, UI names), each node with a file-level `implements` from `code:<path>`; `context.extract.recognizers` (`[{kind, glob, pattern, keyFrom}]`, default `[]`, at most 500, a pattern at most 2000 characters; raised per owner q93, 2026-10-23) adds profile rules to that table through `RecognizerSet`: a file whose path matches a `glob` is read (a denied path never is) and each match of the .NET `pattern` proposes `surface:<kind>/<the keyFrom group>` at confidence 0.8, suggested and never confirmed; a pattern runs NonBacktracking (linear, no time limit) unless it uses a back-reference, look-around or atomic group, and then it keeps the backtracking engine with a one-second limit, and a file that exceeds it is `degraded: recognizers` in the build report, not a failure. Profile globs and `regex:` role entries are checked the same way; a `regex:` entry that times out during a cleanup fails it with `SND-CONTEXT-134` naming the pattern and the input length. A bad entry is `SND-CONTEXT-130` (unknown kind, glob or key group, a pattern that does not compile) and too many entries or a pattern over the cap is `131`, each naming `context.extract.recognizers[i].…`; `132` is reserved. `context.extract.graphify` (`{timeoutMinutes, maxSnapshotMB, maxFileMB, maxGraphMB}`, defaults 60, 2048, 16, 256, `0` is no limit) is read into `ContextSettings.Graphify` (`ClusterLimits`, a tool-neutral name because `IClusterSource` is a Core port; `GraphifyRules` does the checks); a negative, fractional, over-10,000,000 (over 70,000 for `timeoutMinutes`, a timer's ceiling) value is `SND-CONTEXT-133` naming `context.extract.graphify.<key>`. `BuildMapService` and the backtest's `AsOfMapBuilder` hand the limits to `IClusterSource.Clusters(repo, commit, limits, ct)` on every call, so a profile change applies to the next build with no restart; the source reports why clusters are degraded in `ClusterSet.Reason`, and the build's `degraded` line reads `clusters: <reason>` (a truncated snapshot that still clusters is `Available` with a `Reason`). The backtest's as-of map runs the same recognizers and adds the set's hash to its cache key only when the set is not empty. An extraction in which a recognizer timed out is partial and load-dependent, so it is not cached and the backtest report says `recognizers timed out`. `ClusterFeatures` turns
  graphify clusters (`IClusterSource`) into candidate features, optionally named by `ILabeler.Label(clusters, extraPatterns, maxTokens, ct)` (the token cap
  arrives with each pass, from `context.labeling.maxTokens`; the profile's `context.scrub.extraPatterns` scrub every path sent to the model). Ports: `IRepoReader` (files at a sha), `IClusterSource`, `ILabeler`.
- **Core `Overrides/`, `Review/`, `Proposals/`, `Lenses/`.** Pure: `OverrideApplier`, `ReviewBacklog` (the queue), `ReviewPlanner` (one
  decision to override rows; `Repoint` is an undo of the orphan plus the same decision on the new node), `ReviewFileMatcher`, `PromoteReadiness`,
  `ReviewHistory`, `ProposalPlanner`, `GraphStatuses`, `LensesBuilder`, `FileMapBuilder`. the review paths (queue, history, bulk accept, review file, promote, clean) check only `MapAccess.Gate`: `ReviewDrafts.Gate` and `GateDecision`, which refused a session, were removed per owner q93 (2026-10-23).
- **Core `Locks/IHeavyJobLock`** is the context engine's lock port (the backtest takes it; the map build does not: `BuildMapService` waits on the in-process `MapBuildGate`, one build at a time per host, owner 2026-10-24); `Sindri.Core/Locks/IHeavySlots` is Sindri's own (one lock for the engine
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
  - `ContextRebuildService` (`context-rebuild`), `SindriHeavyJobLock : IHeavyJobLock` over `IHeavySlots` (waits up to 12 h with progress, then 040), `ContextChallenges` (the shared X30 challenge), `ReviewInput`.
  - `ContextScopes.Entries` maps the profile's repos and `index.denyPaths` to the engine's `ProfileRepoEntry`.
- **Sindri Host.** Routes: `GET /sindri/context/map/review`, `/review/history`, `/review/export`, `/map/proposals`, `/lenses`, `/filemap`; `POST /sindri/context/map/review`,
  `/promote`, `/proposals`. CLI: `ContextReviewVerbs` (review, promote, propose, proposals), `ContextLensVerbs` (lenses, filemap), `ReviewArguments` (the pure parse),
  `ReviewSession` (the review loop as a state machine; the TTY shell only reads lines and sends), `ContextExit` (040 is 75), text in `Text/ContextReviewText` and `ContextLensText`.
  MCP: `context_lenses {anchor? | anchors?, mapVersion?}` in `ContextTools` (a lone anchor stays schema 1, several distinct anchors answer schema 2; `context_query` also takes `mapVersion?`; no new tool, the session reads sets through S-LB2); `context_node {key}` and `context_neighbors {key, depth?}` in `GraphTools` (S-3M; the same services as the HTTP routes, 022 for bad arguments, summaries in `McpSummaries.Node` and `.Neighbors`, tested in `ContextGraphMcpTests`). `context_lenses` is paged by `LensPages` for every transport: `limit` (default 100) and `offset` cut each band list and `edges` on their own, `page.totals` holds each list's full count, and nothing else is cut (no payload cap). `ClusterFeatures` leaves tests, fixtures, demos, icons, deprecated trees, shells, routers and crons out of a feature's implementers and proposes nothing for a cluster made only of them (`SharedFileClusterTests`); `Packer` splits the budget across the sections that have candidates and gives unused budget back to earlier overflowed sections (`PackerTests`). `Models/RoutedModelPort` adapts Judge's `IProviderCatalog` as `IModelPort` (agent CLIs only, never `jev`), the catalog built in a container of its own (`JudgeCatalog`). It routes each call by `context.models` of the approved profile (`ApprovedModels`, read once per approved hash), remembers a harness's usage limit per job, parks the job on `model-limit` when every harness is limited (`sindri jobs resume <id>` wakes it), and writes one `context.model.called` event per pass through `LedgerModelCallLog` (model routing spec 2026-10-29).

### Classes (README X63)

| Row | Class | Who may run it |
|---|---|---|
| `ReviewContextMap:accept`, `reject`, `rename`, `move`, `accept-deterministic`, `undo`, `drop`, `repoint`, `apply-file`, `merge`, `add`, `delete` | desk, `SessionOk` | terminal, app, agent session; no typed challenge (removed per owner q93, 2026-10-23: each is a reversible override row) |
| `PromoteContextMap` | desk, `SessionOk` | terminal, app, agent session; no typed challenge (removed per owner q93, 2026-10-23; the previous version is retained) |
| `ProposeContextMap` | job | terminal, app, `service:engine` |
| `BuildContextMap`, `RefreshContextMap`, `RunContextBacktest` | job (P6 runtime), `SessionOk` | terminal, app, `service:engine`, agent session (owner q93, 2026-10-23; the single-flight bounds them; the backtest still takes a heavy slot, a map build none) |

`map build` is a job on API P6's runtime: `AddContextJobs` (Features) registers it with `AddJob` (key `build`), `ContextJobEndpoints` maps it with `MapJob` (any body starts it), and `BuildContextMapService` is the work, run by the job runner under the starting caller: it passes the engine's step lines to the job's progress, appends `context.map.built` and returns a small result (`repo`, `draft`, `proposed`, at most 20 `degraded` lines). `ContextRebuildService` runs the same slice on its cadence outside the gate.

### Errors (S-4 range 040, 042–048)

| Code | Kind (HTTP) | CLI exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-040` | Unavailable (503) | 75 | The heavy-job lock stayed busy, or its directory can't be used; the message names the holder (the backtest only; the map build takes no slot) |
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
| `GET /sindri/context/lenses` | `anchor` (repeat it for as many as you need, any number; one distinct value answers schema 1, several schema 2), `depth` (0–4, default `context.cascadeDepth`; else 006 / 400), `hops` (S-CH: a comma list of `state`, `flow`, `dependency`, `component`; default the first three; else `SND-CONTEXT-140` / 400), `draft`, `mapVersion` (S-LB1a; not with `draft`) | `{schemaVersion, anchor{key,label,kind,trust}, mapVersion, mapCommit?, draft, depth, asOf, bands{intent,product,code}, edges[], conflicts[], coverage, searched, degraded[], cites{}, skippedComponents?}` (spec §8.9; S-CH, only with `hops` and a unit over the cap); several anchors answer schema 2, see "S-LB1a" below |
| `GET /sindri/context/filemap` | `repo`, `path`, `unmapped`, `draft`, `cursor`, `limit` (entries per page, clamped to 1–1000; default 200 once a `cursor` or `limit` is given; with neither, every child is returned) | `{schemaVersion, repo, sha, mapVersion, draft, path, entries[{name,path,type,status,files,features[{key,provenance,confidence?,confirmed,linkId?,edge}],intent{records,decisions}\|null}], unmapped[], suggested[{linkId,file,feature,confidence,evidence[]}], totals{files,mapped,suggested,unmapped}, symbols, nextCursor, total}` |
| `GET /sindri/context/map` | `anchor` (optional) | the map status; with an anchor also `coverage`, the lenses' record from the same Core function (`LensCoverageBuilder`) |
| `GET /sindri/context/map/review` | `limit` (per group, default 100, max 1000; else 006) | `{draft, version{number,commit}, entries[…, trust], decided, proposals, counts{deterministic,model,orphaned}, byKind{}, truncated{…}, undecided}`; `decided` counts the proposals of this draft with a standing decision, `proposals` = decided + deterministic + model |
| `GET /sindri/context/map/review/history` | `limit` (default 200, max 1000; else 006) | `{entries[], truncated}` |
| `POST /sindri/context/map/review` | `{verb, node?, link?, override?, overrides?, label?, other?, path?, answer?}` | `{result{written[],skipped[],undecided}, sequence, challenge?, summary?}` |
| `POST /sindri/context/map/proposals` | `{run, repo, surfaces[{name, key? or kind?, parent?, evidence?, quote?}]}` (a session is `SND-AUTH-002`, 403) | `{result{draft, added, existing, rejected, withdrawn, kept}, sequence, surfaces[{surface, node, status, trust}]}`; `draft` is null when nothing was opened (a withdrawal with no draft). An empty `surfaces[]` withdraws the run: one `undo` row for each of its undecided proposals (`withdrawn`), while the ones a person accepted, merged, renamed or moved stay (`kept`); a repeat withdraws nothing and writes no second event, and a retry after a failed event append writes it once. `surfaces` in the answer is every surface of the run in the order submitted, each `mapped`, `new` or `unmapped` (spec §8.7) with its label and node key, the same list `GET` returns, so the counts are not the only answer; `context map propose` prints it under the counts |
| `GET /sindri/context/map/proposals` | `run` | `{run, map, draft, surfaces[{surface, node, status, trust}]}`: every surface the run submitted (kept per run in `proposal_run`, migration 0005; a second submission replaces the list), each `mapped`, `new` or `unmapped` against the current map at call time, read through the same view as every other read (a denied path hides its nodes; a session sees only its repo). `trust` is always `untrusted`; `node` is null when unmapped, and for `new` when the caller is fenced (session, MCP, service). The POST row's list is the same one for a clean caller. |

- **Repoint** (`verb: repoint`, `override` = the orphan, `node` = the new target) writes `[undo of the orphan, the same decision on the new node]`; undoing both rows restores the orphan. A target not in the draft is 047.
- **Undo list** (`overrides`, 1–1000 ids, only with `undo`, not beside `override`): all or none in one transaction; 047 names the first stale id; one `context.map.reviewed` event with `rows` = n. CLI `--undo <id>[,<id>…]`. *Provisional owner default (2026-10-14 night).*
- **Lenses** details. Since S-LB1a schema 1's `mapVersion` is exactly `v<N>` (it was `v<N>@<sha>`), with the commit in the sibling `mapCommit` (7 lower-case hex, omitted when none); decisions gained `label` and `kind`, intent records `kind`, and code files `kind`, `label` and `trust`. `bands.product.counts` also carries `neighbors` and `edges` (the list lengths, null when not counted; every list holds every item); `own[]` is the anchor's own scope; `neighbors[]` and `affects[]` carry `depth` and `path`. A neighbor's hops read `<edge type> <key>` going with the edge and `<edge type>-by <key>` going against it, over structure edges where they reach (else any edge); an affects path is the cascade's own words. `product.edges` are the structure edges among product items. Intent: `records[]` (`source` from the key: `linear`, `linear:doc`, `slack`, `github`, `repo-doc`; `state` `unchecked`, `cites` empty until S-5A/S-6) and `decisions[]` (with `at` and `quote{record,text,trust:untrusted}`); `counts.orphaned` is `null` (follow-up F2). Code: `files[]`, one per file (`suggested` = every link of it unconfirmed, `linkId` = its best link then); `counts.featuresWithNoCode` counts scope features with no confirmed `implements`. Coverage counts a confirmed `implements` only as implemented (not `tests`, not a model suggestion); `code.suggested` counts scope nodes whose only `implements` are unconfirmed. Conflicts: one per decided node, `supersedes` only until S-CF. Provisional: all of the product `edges`, the `-by` paths, `featuresWithNoCode` and the gap notes.
- **File map** details. A file's `features` include its suggested link (with `linkId`) and `tests` links (`edge: tests`; they never make a file mapped); a directory's hold the confirmed ones only. `intent` counts the distinct records and decisions on the entry's features, `null` with none. `unmapped[]` lists the unmapped entries even when `unmapped=true` filters `entries`. `sha` is the seven-character HEAD the tree was listed at.
- **Session scope (owner decision 2026-10-15: B).** `MapAccess.View`'s session step cuts the graph with `GraphSnapshot.ScopedTo`, which keeps `RepoScope.NodesFor` (the nodes the repo implements and their code files) and, through `RepoScope.WithLinkedIntent`, the decision nodes with a `decides` edge to one, the record nodes with a `mentions` edge to one and the records those decisions quote. Edges, aliases and `Decisions` rows follow the kept nodes, so a record's `mentions` list shows only its in-scope ends. The deny cut runs first and judges every profile repo's globs for a session as for a human (`MapAccess.View` gives `DenyCut` two sets: every profile repo, whatever `context.repos` narrows to, judges record keys and evidence refs, which name their repo, and the repos the map is built from (`ScopeRules.Mapped`) judge `code:` keys, which name none; the session's repo is used only for the gate and this scope step; `BuildMap` cuts its draft with the same two sets). A record node's label is untrusted wherever a node is rendered (`TrustPolicy.ForNode`: `MapText.Fact`, `LensScope.Item` for own scope and reach, `LensesBuilder` for the anchor, and `GraphAnswer` through the kind overload for the cascade) (fenced for a session, `trust: untrusted` beside the clean label for a human); a person's decision wording stays trusted and unfenced, and the quote is fenced. A record or decision linked only outside the scope is `003` on a direct key with no title, and a `supersedes` conflict whose superseded decision is linked only outside the scope is not shown to the session. A session's map export is `SND-CONTEXT-020` (spec §3): the file is not scrubbed. A session searches the record index (GitHub, Slack, Linear) like a human (owner q93 2026-10-23, confirmed 2026-10-24; there is no `context.repoTeams`), its untrusted text fenced.
- **Trust and the fence (provisional owner default, 2026-10-14 night; CTX §7).** Every lens item, record, decision, queue entry and node fact carries `trust`. A human at the CLI or in the app (`FencePolicies.For`: human and client `cli` or `http`) gets the label clean beside `trust`; a session, MCP and in-process callers get untrusted text inside `<untrusted source="map">…</untrusted>` as before. The CLI's text output fences an untrusted label itself when it called as a human (a session's answer is already fenced), `--json` prints the body unchanged. Query `ResultItem`s stay fenced for everyone. The file map carries only repo paths and node keys, so it has no `trust`. If the owner refuses the policy, drop `FencePolicies` to always-fenced and document the fence grammar for the app.
- **Anchor errors** carry their candidates in text: 003 ends `(fix: try <key>, <key>)` (up to five), 037 lists the keys (goldens `unknown-anchor-003`, `ambiguous-037`). Over HTTP, 003 also carries `suggestedAnchors` (only when there are suggestions, at most 5) and 037 `candidates` (at most 100) as problem members; MCP errors stay `{code, message}`.
- **HTTP status per code.** 003 404, 037 409, 030 404, 044 404, 045 404, 046 409, 047 409, 043 409, 005 and 006 400, 042 400, 040 503 (CLI 75), 048 503 (CLI 2), 020 403, `SND-AUTH-002` 403, `SND-CLI-003` 400.
- **Heavy-job slots.** `HeavyLockSettings` reads `AW_HEAVY_SLOTS` as the heavy wrapper does: unset or empty is 1, a whole number of at least 1 is that many, anything else (`two`, `0`, `-1`) is 1 in the wrapper (and `AW_SESSION_HEAVY_SLOTS` likewise), in `config/lib/locks.sh`'s `acquire_any_lock` and here. The callers of `config/lib/locks.sh` (the ui-evidence skill, scaffold `e2e.sh`) pass `${AW_HEAVY_SLOTS:-2}`, so an unset variable is 2 slots there.

### Tests

- **Unit.** The planner (repoint, undo, bulk), `ReviewHistory`, the review-file preview, `ReviewDrafts` gating (a single decision from a session is not refused by the engine;
  the bulk accept, apply, export, promote and build are 020), the catalog classes with `Authorization` for every caller, `ReviewInput`, `ReviewArguments`, `ReviewSession`,
  `ContextExit`, the text renderers (every status a word), the verbs over a scripted API, `DirectoryHeavySlots` and `SindriHeavyJobLock`, `RoutedModelPort` over a fake catalog,
  the profile's `index.denyPaths` (parse, hash stability, validation, the scope).
- **Integration.** `ContextReviewApiTests` (the rebuild service, desk and protected review, promote and its challenge, apply-file, 005, 043, 048, 040 with the bash helper holding both slots),
  `ContextLensesApiTests` (lenses, filemap, 003/044/045, MCP `context_lenses`, proposals), and the lock directory shared with `config/lib/locks.sh`.

## S-5A: the record index

Sindri used to read Slack and Linear text through Onyx; it now keeps its own record index and reads those sources itself. Onyx's Confluence and Drive connectors have no replacement. The full design is `docs/superpowers/specs/2026-10-27-sindri-owned-index-design.md`; how the owner connects the sources is `docs/sindri/sources.md`.

### Shape

- `Aw.Context.Core.Sync.IIndexSync`: the record feeds as one port for the `index-sync` service. `Tick` runs one pass of every feed of the profile, `Decide` runs the decision pass over records already stored, `Progress` reads each feed's phase, last tick and milestones, and `Counts` counts what one source holds (the purge preview).
- `Aw.Context.Infrastructure.Sources.ContextRecordFeedCatalog`: the feeds in a stable order: repo docs over the repos in scope, then Linear issues and projects when the profile names `context.sources.linear`, then the feeds the Host adds (GitHub issues and PRs, Slack threads). No repo in scope gives none.
- `Aw.Context.Core.Engine.SourcesReader` reads `context.sources` (`linear.teams`, `github.repos` and `host`, `slack.channels` and `workspace`) for `SettingsReader`. A `token` key is a secret pointer; `sindri index connect github|slack` writes it. `context.index.windowDays` (default 183) bounds what a first sync reads.
- Records land in the one SQLite store under `$AW_SINDRI_HOME`, keyed `github:...`, `slack:<channel>/<thread_ts>` and `linear:issue/<uuid>`; fusion deduplicates on the canonical key, and an issue key such as `ENG-482` is an alias of its record. A session searches the index like a human, for the repos of the approved profile, and its untrusted text is fenced.
- Sindri: `Composition` passes `ContextEnvironment(environment)` to `AddContextInfrastructure`, which resolves a name that is a secret pointer through `ISecretPointers` (`SecretPointerEnvironment`); every other name reads the real environment.

### Pulls are bounded chunks

Every `RecordFeed.Pull` is a bounded chunk and its cursor moves only with the records it returns (#1679; `RecordFeedResumeContract` in `Aw.Context.Testing` states it and every feed runs it: `SlackFeedResumeTests` and `GitHubFeedResumeTests` in `Aw.Sindri.Tests/IndexSources` through `ConnectorFeed`, `LinearFeedResumeTests` and `LinearProjectFeedResumeTests` in `Aw.Context.Tests/Linear`). `RecordSyncRun` commits each page with its cursor and reads an empty page as the end of the phase, so a feed answers a rate limit with an error (or a page of what it did read), never an empty page. Slack's `PullSlackService` reads a channel newest to oldest: one chunk is the history pages down to an older day (one request per page, paged by the exclusive `latest` bound), a day record from history alone, then the chunk's thread roots newest first, at most 20 `conversations.replies` per pull; `SlackCursor` holds the per-channel watermark and, while a pass is under way, the pass's top, the chunk's bound and the thread it stopped at. GitHub resumes on its `(updatedAt, seen ids)` cursor, Linear on its end cursor plus the run's watermark.

`SyncIndexSourceService` (the named `SyncIndexSource` job) and `IndexSyncService` (the scheduled run) return a source's `SND-CONTEXT-059` or `AW-CONNECTORS-009` row as a successful job (a down source does not fail the run), and say it in the summary: `SourceStops` in `ContextCalls` turns the row into `partial: slack stopped at its time cap (SND-CONTEXT-059), 0 fetched` (named job) or a `partial:` run summary in place of `ok:` (scheduled). The job's own `code` stays for failed jobs, and no job state was added.

### Operating it

`sindri index sync [<source>]` and the built-in `index-sync` service (every 5 minutes while serve runs) run the feeds; `sindri index status` shows one line per source; embedding is a separate resumable job (`sindri index embed pause|resume`); `sindri index purge <source>` deletes one source's records. Doctor rows: `context:index-store`, `context:index-github`, `context:index-linear`, `context:index-slack`, `context:index-ollama`, `context:index-sync`. Errors are `SND-INDEX-*` (`docs/sindri/errors.md`). The `SND-CONTEXT-050` to `055` codes are retired.

A source that is down during a query is the `degraded` entry `SND-CONTEXT-010` with the cause in its message; the answer's own error is `011` only when every source is down. The pointer errors are Sindri's: `SND-SECRET-001` (malformed), `002` (doesn't resolve) and `003` (unsafe `file:`), in `docs/csharp/sindri.md`.

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
- **Infrastructure** (`Eval/`): `GitMergeHistory` (first-parent log over `GitCli`, caller `GIT_*` removed), `StoreBriefSource` (pages the record store's stored Linear issues, keeps those whose title key `[TEAM-n]` names a profile team, never capped; the brief is the stored title and chunks, cleaned again at ingest; a record edited after its creation is `leaky`, because the store keeps no content history), `AsOfRepoDocs` (the repo docs at the cut commit, same globs, size cap, chunker and ingest scrub, held in memory; the live `RepoDocsSource` is unchanged and keeps `SupportsAsOf: false`), `FileEvalStore` (0700/0600 files, refuses a worktree with 005, 90-day TTL).
- **Features**: `QueryPipeline` is the old `QueryContextService` body behind one seam (`IMapProvider` plus a source catalog plus an as-of time); `QueryContextService` calls it with the live map, `ArmQuery` calls it with a sample's as-of map and an arm-filtered catalog. **No `asOf`, arm or graph parameter was added to `GET /sindri/context/query` or MCP**, so a session can never see an as-of or arm view. `RunBacktest/*` (`CorpusLoader`, `SampleScorer`, `AsOfMapBuilder`, `ArmRunner`, `ReportAssembly`, `RunBacktestService`), `Adjudicate/Adjudicator`, `EvalMap/*`, `EvalPurge/*`. The eval slices sit behind one `ContextEval` group in the facade.
- **As-of map** (`AsOfMapBuilder`): `MapExtractor.Extract` over the cut commit's tree, plus `ClusterFeatures.Propose` over that commit's clusters with no labels (no labeling pass, no model call), joined under the newest map version created at or before the issue's creation (authored nodes whose code the cut lacks are dropped, `AsOfPruning`), with the `map_override` rows dated before the issue applied, all through `MapAccess.View` (the deny cut every reader makes; no pending proposal). A cache that can't be written costs time, never the map. Cached by repo, sha12, extractor version and whether clusters answered (`eval/cache/maps/<repo>-<sha12>-x<v>-c<0|1>.json`). Nothing is written to `context.db`.
- **Arms**: `brief` (text anchors only, depth 0, no source), `docs` (as-of docs, empty graph), `graph` (the floor), `graph+reviewed` (today's map with every decision; labeled leaky), `all` (as-of graph and as-of docs). `linear`, `code` print `not available (S-6 / S-7B / S-9)`. `ranker` is 082 naming S-8.
- **Heavy lock**: one slot for the whole run (`IHeavyJobLock`); busy is 040. A session caller is 020 ("a human or service job"). Cancellation writes no report.
- **Adjudicator** (`context.eval.adjudicator`, default off): asks twice (second time the surfaces reversed), keeps agreeing answers, a disagreement is `unstable` and counts as not relevant, a model error stops adjudication (`Adjudication.Error`, `adjudication incomplete` in the report, no further call); sends the scrubbed brief inside the untrusted fence and node labels only; truth nodes are never judged; stops at 1.5M tokens (083, degraded, the rest `unjudged`, adjudicated precision "not measured"). The cap is the §9.4 bar kept as a constant, not a profile key.
- **State is files**: `context/eval/{backtest,map,cache}`; no `context.db` migration, no new ledger event type.

### Sindri

`GET /sindri/context/eval/map` (a query: no catalog row), `sindri context eval map|purge|backtest`. `eval purge` runs in the CLI process (`--all` also clears the probe reports, so the next `eval probes` has no baseline) for whoever runs the CLI (a session may purge, as a human does). The `context-purge` service applies the 90-day eval TTL and `doctor`'s `context:retention` counts expired eval files. **`RunContextBacktest`** is a ready `class: job` row (`JobRows.Job`, `POST /sindri/context/eval/backtest`, command type `RunContextBacktestRequest {Arms, Since, Until, Limit}`). `AddContextJobs` registers it with `AddJob` (key `backtest`; the precheck refuses an unknown arm as SND-CONTEXT-082, and a limit of zero or less or a window that ends before it starts as SND-CLI-003, before any job exists; the request names no repo, so no `repo:` selector is passed), `ContextJobEndpoints` maps it with `MapJob`, and `RunContextBacktestService` runs `IContextEngine.EvalBacktest` under the starting caller with the job's progress, answering `RunContextBacktestResult` (counts, one `BacktestArmLine` per arm, the pass line, the report path, at most 20 degraded notes). `context eval backtest` is parsed by `BacktestLine` and run by `ContextJobVerbs` through the `JobFollower`. A session start is refused by the gate; a second start is SND-JOB-409. The action id is one constant (`ContextRows.BacktestAction`). Goldens: `jobs-id/context-backtest.json`, the catalog row.

### Errors (S-7A range 080-083)

| Code | Kind (HTTP) | Exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-080` | NotFound (404) | 1 | No eligible sample, or every sample was dropped (the message gives the counts left out and why) |
| `SND-CONTEXT-081` | Unavailable (503) | 1 | No configured source can supply issue briefs |
| `SND-CONTEXT-082` | Invalid (400) | 1 | `--arms` names an unknown arm (the message lists the arms and which are not available yet) |
| `SND-CONTEXT-083` | degraded | 0 | Adjudication stopped at the 1.5M-token cap |
| `SND-CONTEXT-090` | degraded | 0 | The rerank (`context.ranker.rerank`, or the `ranker-rerank` backtest arm) could not order the items: the answer lists `rerank` as degraded and keeps the feature or fusion order |
| `SND-CONTEXT-240` | Unavailable (503) | 2 | Every model harness a call may try is at its usage limit or failing (model routing, `context.models`; raised by the router, not by the settings) |
| `SND-CONTEXT-241` | Unavailable (503) | 2 | No model harness a call may try is installed |
| `SND-CONTEXT-242` | Unavailable (503) | 2 | Jev refused a pack of choice questions (the label pick; input too big, an unreadable answer, a timeout). The labeler splits the pack in halves, twice at most, and never shows it: what still fails takes the model's first candidate. The message is a reason code |
| `SND-CONTEXT-243` | Unavailable (503) | 2 | Jev can't be asked for the rest of this pass (no key, offline, excluded, a refused key, rate limited, a server error, or the pick is off in the approved profile). The labeler stops asking and every undecided cluster takes its first candidate; only the reasons other than `off` and `no-key` reach the build as `labels: jev unavailable (<reason>)`. The message is a reason code |
| `SND-CONTEXT-250` | Invalid (400) | 2 | A page action in `index.pages` names a concept that is neither a pin nor a derived concept of the current map (the message offers the nearest id) |
| `SND-CONTEXT-251` | Invalid (400) | 2 | A page's `surface` or an action's `action` key is not in the current map |
| `SND-CONTEXT-252` | Invalid (400) | 2 | A page's repo can't be told: its surface is evidenced in several repos and `repo` is absent |
| `SND-CONTEXT-253` | Invalid (400) | 2 | Over a cap: 200 pages, 40 actions on a page, 20 concepts on an action |
| `SND-CONTEXT-254` | Invalid (400) | 2 | A page id repeats, or an action id repeats within its page |
| `SND-CONTEXT-255` | Invalid (400) | 2 | A page or action name is empty, over 80 characters, or starts or ends with a space (control and format characters are stripped first) |
| `SND-CONTEXT-256` | Invalid (400) | 2 | A `--action` spec is malformed (no name, no key after `=`, no concept after `->`), or a SetPages request has nothing to set or remove |
| `SND-CONTEXT-257` | Not found (404) | 1 | No page has that id (remove, `context_pages`) |
| `SND-CONTEXT-258` | Not found (404) | 1 | `context_pages` names no such concept or repo |
| `SND-CONTEXT-259` | Invalid (400) | 2 | A pasted page list (`QUERY /sindri/context/pages/preview`) is empty or over 65,536 UTF-8 bytes |

084-086 are reserved for review findings, 087-089 for S-7B. Reused: 020, 030, 040, 048 (a cut git can't read: that sample is dropped and counted), 005, 004 (a cache or context.db read failing stops the run with its own code), 001, 011.

The issue listing reads the index store, so it is as complete as the last `sindri index sync`.

### Tests

`tests/Aw.Context.Tests/{Eval/Backtest,RunBacktest,Adjudicate,EvalMap}` (pure scoring, merge history on the fixture repo, the briefs against a seeded index store, the whole backtest on the split-panes fixture, as-of honesty, adjudicator with `FakeModelPort`); `tests/Aw.Sindri.Tests/ContextCalls/{BacktestJobTextTests,ContextBacktestJobApiTests,ContextEvalNotRunningTests,ContextEvalStoreHostTests,ContextEvalVerbsTests,ContextJobVerbsTests,ContextLensesApiTests,ContextReviewRowsTests}` (the job text, the job route and its prechecks, the CLI parse and exit codes, the eval verbs).
## S-D: the diff shape

The fifth query shape (`ContextQuery.Diff = new DiffRange(base, head)`, optional `Repo`): the files changed from the merge base of two commits to the second, mapped to nodes, then the same cascade, packing, sources and citations as the other shapes.

- **Refs only.** `DiffRange` is two lower-case hex shas of 7 to 64 characters. A branch, tag, `HEAD`, `~`, `^`, `...` or upper case is `SND-CONTEXT-006` ("resolve the refs to shas first"); the engine reads no refs and makes no forge calls. Resolving a PR to its shas is the caller's job.
- **Git.** `IRepoReader.Changes` runs `rev-parse --verify --quiet <sha>^{commit}` per side (the full sha it returns must start with the input: an abbreviation that is also a branch or tag name resolves to that ref in git, so it is refused as 110 with "pass the full sha", as is an ambiguous abbreviation), `merge-base`, then `diff --name-status -z --no-renames --no-ext-diff --no-textconv <mergeBase> <head> --` (ArgumentList, `GIT_*` removed, 30 s). Nothing is read from the working tree or from file content. A rename is a deletion plus an addition.
- **Result.** `diff` holds `repo`, `base`, `head`, `mergeBase` (full shas), `files[]` (`path`, `change` of added, modified, deleted or typeChanged, `status` of mapped, suggested or unmapped as `filemap` says, `nodes[]` of `key` and `confirmed`), `unmapped[]`, `counts` (`changed = mapped + suggested + unmapped + denied`), `truncated` (at most 1000 files kept: when set, `files[]` and `unmapped[]` hold the first 1000 visible paths in ordinal order, while `counts`, `anchorsTotal` and the anchors cover every visible file), and `anchorsTotal`. `anchors` are every claimed node: confirmed before suggested, then by key; `files[].nodes` always lists every node.
- **Deny globs.** A changed path under the repo's deny or secret globs is only counted in `counts.denied`, never named, for every caller. Every caller's paths (`files[].path`, `unmapped[]`) go through the same egress scrub (`MapText`) the cascade's code paths get, so one answer never shows a path two ways (scrubbed, not fenced).
- **One repo's links.** The changed files are read against the addressed repo's links only: a `code:` link whose file evidence (`repo:<repo>/<path>`) names only other repos is ignored, so a path two mapped repos share claims only the addressed repo's nodes. A link with no repo evidence counts for any repo.
- **Repo.** `repo` may be left out when the caller's scope holds one repo (always for a session); otherwise it is required (006). A repo outside the profile or the session's scope is 045 (one answer for every such reason). No map is 030, which a session whose own repo implements no node also gets; `draft` with no draft is 044.
- **Errors.** `SND-CONTEXT-110` (NotFound, exit 1): the clone does not hold a sha. `111` (Invalid, exit 1): no merge base. 112 to 119 are reserved; 120 to 129 belong to the lenses and map versions (S-LB1a). An unreadable checkout is 048.
- **Tests.** `QueryRulesDiffTests`, `DiffAnchorsTests`, `DiffQueryTests`, the `RepoReaderContract` Changes cases (fake and git), `ContextDiffTests` and the `context-query` API goldens (`diff-spl-103`, `diff-unknown-sha-110`).

## S-LB1a: lenses over a set of anchors and as of a map version

- **Anchor sets.** `GET /sindri/context/lenses?anchor=A&anchor=B` (any number; none is `SND-CONTEXT-006`) answers schema 2 (`LensesSetView`): `anchors[]`, `missingAnchors[]` (an anchor this caller can't read or that doesn't resolve; none resolving is 003), one union of the bands read from one map snapshot, and `coverage.byAnchor`. Each band item names the anchors that reached it (`anchors`) and its `depth`. Repeats of one value, or one `anchor`, keep schema 1 with its old shape plus the additions below. The request types are `GetContextLensesRequest` (one) and `GetContextLensesSetRequest` (several); the endpoint picks by the distinct count. Top-level `edges` (mentions, decides, quotes, supersedes, implements, tests) and `bands.product.edges` (structure types) are disjoint by type: draw both.
- **`mapVersion=`.** `lenses` and `query` read the graph as of a retained version: `v3`, `3` or `v3@a41b07d`. `draft` with `mapVersion` is 006. A version that isn't held (decided by asking the store, never by the retention count) is `SND-CONTEXT-120` (NotFound, 404, exit 1). The answer's `mapVersion` is exactly `v<N>`; the commit the version was built at is the separate `mapCommit`: 7 lowercase hex characters, omitted when the version names no commit (`LensBasisReader.ShortCommit`, pinned by `MapCommitTextTests`).
- **Schema 1 fields.** `mapVersion` changed from `v<N>@<sha>` to exactly `v<N>`, the commit now in the sibling `mapCommit` (7 lower-case hex, omitted when none); decisions gained `label` and `kind`, intent records `kind`, code files `kind`/`label`/`trust`.
- **`searched`.** Every lenses answer says what was looked at: `anchors`, `mapVersion`, `depth`, `hops`, `sources[{id, state, reason, code, asOf}]` (`map`, one id per record source, `code-index`; `state` is `ok`, `not-configured`, `unavailable` or `degraded`, never `error`) and `bands{intent,product,code}.empty`.
- **`coverage.map`.** `{paths, truncated, files, unmapped, suggested, created, ageDays}`: the repo's tracked files at HEAD (never a denied path) under the anchors' paths. `created` is the version's moment for a version read; `asOf` is when the answer was built.
- **`cites[*].fetchedAt`.** A query citation carries `fetchedAt`: map-authored: the version's `created`; live source hits: when this answer fetched them (the answer's `asOf`). Lens results carry no cites yet.
- **Errors.** `SND-CONTEXT-120` is the first of the range 120 to 129.
- **CLI.** `context lenses <key>... [--depth N] [--draft | --map-version V] [--json]` (no key is a usage error, exit 2, nothing sent; `--draft` with `--map-version` is SND-CONTEXT-006, exit 1, nothing sent) and `context query ... --map-version V`. Text names missing anchors and prints each source state as a word (`linear degraded (SND-CONTEXT-010)`).
- **Tests.** `LensesSetTests`, `LensesAtVersionTests`, `MapCommitTextTests`, `ContextLensesApiTests`, `ContextReviewVerbsTests`, the `context-lenses` goldens (`set-two-anchors`, `set-missing-anchor`, `bad-depth-006`, `map-version-v1`, `map-version-not-retained-120`, `session-set-other-repo`).

## S-CL: the code lens (`GET /sindri/context/code`)

- **What it is.** `GET /sindri/context/code?anchor=A&anchor=B[&draft=true | &mapVersion=v2]` (scoping station C3) answers `CodeView` (schema 1): `files` (files with an `implements` edge into the anchors' own scopes, each with `testedBy[]`), `tests`, `data.things` (the Things the own scope reads or writes, with `access` and `via`), `data.files` (schema files that name a Thing), `callers` and `callees` (`unavailable` with reason `index-not-built` until the code index is wired, S-9; see S-9a below), `counts`, `truncated`, `lexical[]`, `edges`, `coverage`, `searched`, `degraded` and `cites`. The anchor-set rule, `missingAnchors`, `mapVersion=`, `coverage` (with `map` and `byAnchor`) and `searched` are the lenses' (S-LB1a), through the shared `AnchorSet`. In process the same call is `IContextCode.Code`, which the workflow engine's `input.context.code` block will call (its descriptor is still pending).
- **Tests.** `basis` is `edge` (a stored `tests` edge), `imports` (the test's text names an implementing file's last two path segments, slash or dotted form, with word boundaries) or `sibling` (the test's stem without its marker equals the implementing file's stem, never a generic one such as `index`); `edge` outranks `imports`, which outranks `sibling`. A lexical test is `suggested: true` with no `linkId` (it is not a map link). Test files are the repo's tracked files that match `context.code.testGlobs`, matched **case-sensitively** (so `latest.ts` is not a test).
- **Schema files.** For each searchable Thing (at least 4 characters in some form of its key name, label or aliases; at most 30), the repo's tracked files that match `context.code.schemaGlobs` and no deny glob are searched for the name in four forms (`shared-link`, `shared_link`, `sharedlink`, `shared link`, with a trailing `s` or `es` allowed). `[]` turns the scan off (`lexical-schema` is `not-configured`).
- **How it reads.** `git grep` at one commit (never the working tree), then the candidates (500 at most, 512,000 characters each) are read through the repo reader (deny globs enforced) and matched in process. The commit is the map version's when it names one for that repo, otherwise the repo's HEAD; `lexical[].commit` says which. Only a path and the form that matched leave the engine, never a line. Search terms come from map text cleaned by the scrubber, and a name the scrubber redacted gives no term.
- **Failures are rows, never route errors.** `searched.sources` has `map`, `code-index`, `lexical-tests` and `lexical-schema`. A clone without the map's commit makes both lexical rows `unavailable` with reason `commit-not-held` and code `SND-CONTEXT-110` (a recorded limit; the engine never fetches). The profile's `context.deadline` bounds the lexical step: on expiry what was found is returned and the row is `degraded` with `SND-CONTEXT-152` (and a line in `degraded[]`). More than 50 implementing files, or more candidates than the cap, is `ok` with reason `capped`.
- **Caps.** 200 implementing files searched; 100 Things; 500 candidates per search; 100 schema files per Thing; no cut on the lists (raised and removed per owner q93, 2026-10-23). They are fixed in `CodeRules`, not profile keys.
- **Errors.** `SND-CONTEXT-150` (a `context.code.schemaGlobs` or `testGlobs` entry is not a repo-relative glob; the message starts with the key path), `151` (more than 500 entries in one list), `152` (the lexical search ran out of `context.code.deadline`; only ever the code of a `searched` row). Reused: `003`, `006` (no anchor, a malformed `mapVersion`, `draft` with `mapVersion`), `037`, `110`, `120`.
- **Scoping graph and scoped reads (issue 1005).** `ScopeGraph` builds the ranked, capped graph the scoping Steps work from (the lenses over the anchors, product first, edges with both ends in the set, a `cap` limit row when nodes or bytes run out). `RememberScopeGraph(instance, graph)` keeps the newest graph per workflow instance in `context.db` (`scope_graph`, `scope_graph_node`); `ReadInScopeGraph(instance, run, read)` checks one `context_node`, `context_neighbors` or `context_query` (with `node`) read against it and logs it (`scope_read`), and `GraphReads(instance, run)` returns that run's log oldest first. A node outside the graph is `SND-CONTEXT-154` and is not logged, unless the call passes `outside: true` and a `reason`; an instance with no remembered graph is `153` and the Host reads unscoped. A session names its run with the `X-Sindri-Instance` and `X-Sindri-Run` headers, set at launch; MCP keeps those two and `X-Sindri-Repo` and drops every other `X-Sindri-*` header.
- **Session cut.** A session reads through the same scope as `lenses`: an anchor outside its repo is not found, and the lexical step searches only repos the caller may read.
- **Tests.** `GetCodeTests`, `CodeLensBuilderTests`, `CodeLensMergeTests`, `TestPathsTests`, the `CodeSearchContract` cases (fake and git), `ContextCodeApiTests`, `ContextCodeVerbsTests` and the `context-code` API goldens.

## S-9a: the code symbol port (Context's half of wiring the code index, on a fake)

`Aw.Context.Core/Symbols/`: `ICodeSymbolSource` (`Heads`, `FindSymbols`, `FileSymbols`, `Callers`, `Callees`; every call takes `asOf`, the commit the map version recorded for the repo, or null), its rows (`SymbolSite`, `SymbolHit`, `FileSymbolsRow`, `CallEdgeRow`: each carries `commit`, `asOfCommit` and `state`), the answers (`SymbolAnswer<T>`, `CallAnswer` with `unresolved`), `CodeIndexHead` and `CodeIndexState` (the block's words: ok, stale, checkout-not-on-main, layer-unavailable, missing, damaged, building, not-configured). Context never references `Aw.CodeIndex`: Sindri's Host adapts the block's facade to this port in S-9 (a `CodeIndexSymbolSource`, replacing the default `NoCodeSymbolSource` that `AddContextFeatures` registers, so the lens reads `index-not-built` until then).

- **As-of, computed at read time.** `CodeIndexReading.Head` makes a readable index that is not at the map's commit `Stale` with both commits kept (a map published ahead of its index, or an index ahead of the map), `Stamp` gives every row `state: stale` and `asOfCommit`, and nothing is stored, so rows turn fresh when the index for the map's commit lands. When the index trails the map along main (its commit is an ancestor of the map's, found with the repo reader's merge base in Sindri's `CodeIndexSymbolSource`), `CodeIndexReading.CatchingUp` makes the head `CatchingUp` with the files changed between the two commits as `Pending`: `Stamp(head, path)` gives rows in those files `stale` (with the map's commit) and every other row `ok`. An index that is not an ancestor (a checkout off main, a diverged history), ancestry git cannot tell, or a graph layer that is down stays `Stale`, degraded, as before (#990). An abbreviated commit (7 or more characters) is the same commit as its full sha; no `asOf` never mismatches.
- **The lens row** (`searched.sources`, `code-index`) follows spec §7.3: ok; ok with reason `catching-up` and `catchingUp {indexCommit, mapCommit, pending[]}` while the index trails the map along main (a recorded limit of the code lens, scoping spec §8.2; the callers' and callees' `reason` is `catching-up` and their `state` is `stale` only when a row in a pending file is listed); degraded for `stale`, `checkout-not-on-main` or a layer that is down (reason the layer, code the block's `SND-CODEIDX`); unavailable for `index-not-built` (`SND-CODEIDX-013`), `index-damaged` (`-010`) and `index-building`; not-configured. A read that fails is degraded with that error's code, never an error of the route. `Degraded`'s comment now reads "answered from an older or partial source, or its last run failed".
- **Code lens.** `CodeSymbolSearch` (Features) asks, per repo the listed files belong to, `Heads`, then `FileSymbols` (at most 200 files), then `Callers` and `Callees` at depth 1 for at most 20 symbols (exported first, then file order; the caps are `CodeRules.MaxCallSymbols` and `MaxSymbolFiles`), and lists at most 100 edges, the strongest per symbol first. `callers` and `callees` are `CodeSymbols`: `state` (`unavailable`, `stale`, `ok`), `reason`, `items`, and, when the index answered, `commit`, `asOfCommit`, `asOfMismatch`, `unresolved`; they are the same `unavailable / index-not-built` as before for a repo with no index. When a read of the index fails, `callers` and `callees` are `unavailable` with reason `index-unavailable` and the failing `SND-CODEIDX` code. Names, signatures, kinds, refs and paths are cut to one line (line breaks and runs of white space become one space) and then scrubbed, like every other piece of map text.
- **File map.** The top-level `symbols` object keeps its `state` and `reason` ('index not built', 'index building', 'index damaged', 'not configured', 'stale', or 'index unavailable' with the failing `code` when a read fails) and gains `files[]` (the symbols each listed file defines; a directory row has none), `commit`, `asOfCommit`, `asOfMismatch` and `code`, all left out of the JSON when empty. The design is `mockups/context-filemap.html` in the UI screens spec (the column and its empty states, "from abcdef0, not 1234567" for as-of mismatch).
- **Not in S-9a:** the lenses' code band `symbols` items, symbol-level `implements`, `context query --symbol` and `coverage.code.level` stay S-9's.
- **File map paging.** A request with neither `cursor` nor `limit` returns every child (`nextCursor` null), as before paging; either one pages. `entries` is one page of the directory's direct children (directories first, then by name). `nextCursor` is the `path` of the page's last entry, or null on the last page; send it back as `cursor` (a cursor that is not a child of `path` is SND-CONTEXT-006; one whose entry has since gone resumes where it sorted). `total` is the number of direct children matching the query (after `unmapped`). `unmapped[]` and `suggested[]` cover the page's entries; `totals` still counts every file under the path. A call costs the children, not the tree: the repo's sorted listing is kept per commit (`RepoTreeCache`: one `git ls-files` per commit and deny list, not per call), a directory's children are found by binary search over it, the file-link index is built once per applied graph, and status, features and intent are read only for the entries on the page (all children when `unmapped` has to judge each one). `totals` is one pass of dictionary lookups over the files under the path. Tests: `FileMapPagingTests`.
- **Tests.** `CodeSymbolSourceContract` (the fake and S-9's adapter pass the same cases, including stale, index-ahead and missing), `CodeIndexReadingTests`, `CodeLensSymbolsTests`, `GetCodeSymbolsTests` and `GetFileMapSymbolsTests` with their golden files.

## S-H: the history lens

`GET /sindri/context/history` (`GetContextHistoryRequest`, `IContextHistory.History`, `HistoryView` schema 1; `sindri context history`): the records and decisions behind the anchors' own scopes, cut by a window, and the landed changes of the files that implement them. A query (ARCH-CQRS-001); no MCP tool.

- **Shape.** `Aw.Context.Core/History/`: `HistoryWindow` (absolute instants only), `MergeSubject`, `RecordTimes`, `HistoryCut` (the pure cut, union, order and caps), `HistoryLens` (resolve the anchors' own scopes over `LensSets`, the files with their repo from the edge evidence, `Edges`, `Coverage`, `Searched`), `IFileHistory` (git adapter `GitFileHistory`, `GitCli.FileLog.cs`; fake `FakeFileHistory`, both held to `FileHistoryContract`). `Aw.Context.Features/GetHistory/`: `GetHistoryService` (map read, settings, scrub), `HistoryPipeline` (intent times, the cut, one `git log` per repo bounded by `context.deadline`), `HistoryInputs` (the map and window; a backtest arm hands it an as-of `VisibleMap` and the sample's creation instant), `HistoryTexts` (scrub, one line, fence).
- **The cut (CTX §9.1).** `before` is inclusive. A record whose `createdAt` is after it is dropped; one whose update is after it, or whose times are unknown, is `leaky`. A `human` decision is cut by `at` (null is kept); a model or deterministic one follows its quoted record. `since` cuts changes and records, never decisions. Ordering: records by update, decisions by `at`, changes by committer date, newest first, then key; 100 each after the cut. `createdAt` is the new column `intent_record.created_at` (migration `0007_s5b_intent_created`, filled from `SourceRecord.CreatedAt`); `fetchedAt` is the stored `fetched_at`.
- **Files.** Those with an `implements` edge into the own scope, confirmed then confidence then key, at most 50, only in repos the caller may read and outside the deny globs; a file whose edge evidence names no repo counts in `counts.unlocated`. The upper bound is the map version's commit when it names one in the version's repo, else HEAD (`git[].upTo`, `head`).
- **`searched`.** Reasons: `capped` (101 changes in a repo, or files cut at 50), `shallow-clone`, `no-files`, `commit-not-held` (110), `history-unlinked`, `not-synced` (161), `not-configured`. A row has a reason, a code, both or neither.
- **Not-synced gaps.** `SearchedSources.Intent` gives a never-synced configured source `unavailable` / `not-synced` / `SND-CONTEXT-161`. The Sindri services (`GetContextHistoryService`, and the two lenses services) add `sync: {state, jobId}` from the `jobs` view (`SyncGaps`, ARCH-CQRS-001: the engine reads no jobs); `ApiViews.ContextLenses` and `ApiViews.ContextHistory` are invalidated by `job.started`, `job.finished` and `job.cancelled`. `ContextSyncGapsService` (built-in, every minute, a quiet run when nothing is missing) starts `SyncContextSourcesRequest` through its command chain as `service:context-sync-gaps` (the authorization table already lets class `service` run action class `job`): `SND-JOB-409` is a quiet run, any other refusal fails the run (needs-you `failure:service:context-sync-gaps`), it never starts a second sync while one runs, and after failed syncs in a row it re-checks on the scheduler tick by reading the jobs view; the scheduler has no event trigger. The decision is the pure `SyncGapDecision` (Core): nothing, job running, backoff not due (5, 10, 20, 40, 60 minutes at most; a success resets), start. A tick with nothing to do is only view reads: no command, no ledger write, no log.
- **Errors.** `SND-CONTEXT-160` (Timeout; no longer emitted by the history lens since #1221: a git read past an opted-in `context.history.deadline` is an `ok` row with reason `deadline`, not an error) and `161` (Unavailable, a configured source never synced) are codes of `searched` rows, never a route failure; 162 to 169 are reserved. Reused: `003`, `006` (no anchor, a malformed or relative or reversed window, a draft with a version), `037`, `048`, `110` (also the as-of map version's commit missing from the clone), `120`.
- **Tests.** `HistoryWindowTests`, `MergeSubjectTests`, `RecordTimesTests`, `HistoryCutTests`, `FileHistoryContract` (fake and git), `GetHistoryTests`, the intent store contract's `created_at`/`fetched_at` cases, `ContextHistoryApiTests`, `ContextSyncGapsServiceTests`, `ContextSyncGapsHostTests`, `ContextHistoryTextTests`, the `context-history` goldens.

## Map search (owner q103)

`GET /sindri/context/search?q=&limit=&area=&repo=&draft=&mapVersion=` and `QUERY /sindri/context/search` (JSON body `{q, limit?, area?, repo?, draft?, mapVersion?}`) answer a ranked match list over everything in the map the caller may read, best first. The whole-map view uses it to zoom to, highlight and dim matches; it is not anchor resolution.

```json
{ "mapVersion": "v3", "total": 12, "hits": [
  { "key": "feature:tabs.tab-manager", "kind": "feature", "label": "Tab manager", "repo": "aw-split-panes",
    "area": "feature:tabs.tab-manager", "score": 4.2113, "matched": "title", "snippet": "Tab manager", "trust": "trusted" } ] }
```

- **What is searched.** Per node, three fields: the title (the label), the path (a code node's file path and the evidence keys, with the `repo:` scheme dropped, so a file path or a ticket id finds the node that rests on it) and the text (a decision's verbatim quote and the string values of the node's attributes). A record node's title is its label.
- **Ranking.** SQLite FTS5, `bm25` with column weights title 10, path 5, text 1. Every word must match; the last word also matches as a prefix (`tab manag` finds "Tab manager"). When no whole word matches, a trigram index over title and path answers with substring matches (`anager`). `score` is the negated bm25 rank rounded to 4 places (higher is better); the rank is rounded to 4 places before ordering, and ties go to the shorter title, then key order. `matched` is the first of `title`, `path`, `text` that holds a query word; `snippet` is that field around the first hit (160 characters, whitespace collapsed).
- **Limits.** `limit` is 1 to 200 (default 50); `total` is the count of every matching node, so a display page can say "50 of 1,204". An empty question, one with no letters or digits, more than 512 characters, a limit outside 1 to 200, or `draft` together with `mapVersion` is SND-CONTEXT-006. `area` keeps one area's nodes (an unknown area matches nothing).
- **Index.** An in-memory FTS5 database per map graph, built on the first search of that graph and kept (the last four graphs) in `MapSearchCache`, keyed by the graph object the map reader hands back. A new map version (build, import, promote), an override, a synced record or a changed deny glob hands back another graph, so the next search rebuilds; nothing stale is served and nothing is written to disk. Draft and promoted maps both work. The index is per caller scope (a session's repo cut, the profile's scrub patterns), because what a caller may see differs. Cost on a map of about 27,000 nodes: the build is document extraction (cleaning ~27k labels like the whole-map answer does) plus two FTS inserts per node, an estimate of 1 to 2 seconds once per graph, tens of MB; a search against it is a few milliseconds.
- **Scope and text.** The same read as every graph answer: a session sees only its repo's nodes and the records and decisions linked to them, a repo it does not implement gives no hits; deny globs hide paths. Labels and snippets are cleaned with the call's ingest and egress scrubbers; the index holds cleaned text, and the fence is added on the way out: untrusted hits (record titles, decision quotes from models) are wrapped in the fence for MCP and session callers and left plain for a human at the CLI or over plain HTTP, the same convention as the other context answers. The CLI wraps an untrusted label client-side for a human.
- **CLI.** `sindri context search <words> [--limit N] [--area KEY] [--repo R] [--map-version V | --draft] [--json]`. There is no MCP tool yet: `context_search` would be one more `ContextTools` entry over the same service.
- **Goldens.** `Golden/Api/context-search/`: `get-tab-manager`, `query-decision-text`, `session-own-repo`, `empty-q-006`, `limit-201-006`. Pinned by `ContextSearchApiTests` and, in the engine, `SearchMapTests`. The slug is in the UI lane's `_notRead` until the whole-map page reads it.

## S-LB1b: the subgraph (scoping spec C14)

`GET /sindri/context/subgraph?nodes=&nodes=&depth=&mapVersion=` and `QUERY /sindri/context/subgraph` (the HTTP QUERY method, the IETF safe method with a body, draft; JSON body `{nodes[], depth?, mapVersion?}`) answer `SubgraphView` (schema 1): the bands, edges, conflicts and coverage of the given node keys and of what they reach within `depth` (0 to 4, default 0) edges in either direction. One service (`GetContextSubgraphService`, over `IContextLenses.Subgraph`) and one session filter serve both transports, so the same keys answer the same bytes; the pair is pinned by `ContextSubgraphApiTests`. A demoted file's code node (the repo's `demote` globs) that only depth expansion would reach is left out with its edges and counted in the optional `demoted` field; a demoted key asked for by name is always answered.

- **Limits.** GET takes at most 20 distinct keys, each at most `QueryRules.MaxEncodedText` characters once percent-encoded; QUERY takes 1 or more (no upper bound, per owner q93, 2026-10-23). Anything else, an empty list or a malformed key is `SND-CONTEXT-006` (400; the message names the key). `depth` outside 0 to 4 is 006. An unretained `mapVersion` is `SND-CONTEXT-120`.
- **Missing keys.** A key the version does not hold, a session cannot see or a deny glob hides goes to `missing` (request order), one answer for every reason. All keys missing is a valid answer (empty bands), not 003. Edges to a hidden node are dropped.
- **Shape.** `nodes[]` echoes the keys found; bands are keyed by kind (records and decisions in `intent`, `code:` nodes in `code`, the rest in `product`), every item listed, none cut (a band has no `truncated` field; `bands.product.counts` also carries `neighbors` and `edges`). Every item carries `label`, `kind`, `trust` and `depth` (distance from the given keys). The top-level `edges` holds the cross-band edges among the returned nodes (a code file implementing or testing a feature, an intent record mentioning a node) and `bands.product.edges` the structure edges among the product nodes; the two never overlap, so draw both (checked against the stored edge rows: their union is every stored edge among the returned nodes). Bands hold only the given keys and what widening reaches (no cascade), so at depth 0 the given product nodes bring no records or decisions: `searched.bands.intent.empty` describes that band, while `coverage.intent` still counts the intent attached to the given product nodes (plan C15: coverage is over the given product nodes), and a conflict on a given node is summary-only (its `decision` and `other` keys may be outside `bands.intent`; give the decision key, or widen, to read them). A session sees only what its repo evidences (CTX §3), so the same widening can reach different nodes: a session gets a code file's `implements-by` neighbor where the human also reaches evidence-less surfaces through `part-of`. `asOf` is when the map was read for the answer; `coverage.map.created` is when that map version was written, so for a retained version they differ.
- **QUERY is a query.** It sits in the queries group (`X-Sindri-After` honoured), takes no idempotency key, writes no ledger event, refuses an `Origin` header with 403 `SND-API-008` ("a body query from a foreign Origin is refused"), and has no catalog row (queries have none, ARCH-CQRS-003; API10 means goldens only). ARCH-CQRS-001 reads "queries are GET or QUERY; a command is never GET or QUERY", with no carve-out list; future body reads use QUERY, never POST. Clients use `new HttpMethod("QUERY")` or `curl -X QUERY`; a POST to the route is 405. Owner decision 2026-10-18, replacing the POST carve-out.
- **CLI.** `sindri context subgraph <key>... [--keys-from <file|->] [--depth 0-4] [--map-version V] [--json]`; the keys file holds one key per line, `#` comments and blank lines skipped. It sends GET up to 20 keys that each fit, otherwise QUERY.
- **Goldens.** `Golden/Api/context-subgraph/`: `get-three-keys`, `query-packet-set` (the same keys as the GET), `depth-2`, `missing-after-v2`, `session-own-repo`, `too-many-006`, `not-retained-120`, `query-refused-origin`. The slug is in the UI lane's `_notRead` until U9.

## W1/W2: the whole map (`GET /sindri/context/map/whole`, Part D of the deployable-context spec)

One read answers the whole map of the repo, so the app can open on the structure instead of an empty stage and a query can zoom into it. Spec: `docs/superpowers/specs/2026-10-23-sindri-context-whole-map-design.md` (Part D). A query route (session callers cut to their repo, `X-Sindri-After` honoured), no catalog row, no MCP tool (a whole map is too large for an agent's context; `context_lenses` stays the agent's tool).

- **Code.** `Aw.Context.Core/WholeMap/`: `MapAreas.Assign(GraphSnapshot)` (pure; `AreaAssigner` holds the rules), `WholeMapBuilder.Build` (pure apart from `MapText`), `WholeMapRules` (budget 500 nodes, area cap 5000, areas cap 5000 (both raised from 500), at most 1000 `locate` keys (from 100; owner q93, 2026-10-23); constants of the code, registered as a singleton so a test can pass others) and the view records. `Aw.Context.Features/GetWholeMap/`: `GetWholeMapService` (over `MapReader.ReadViewAt`, like lenses) and `WholeMapCache`. Facade: `IContextLenses.WholeMap`. Sindri: `GetContextWholeMapService`, the endpoint, `sindri context map whole`.
- **Parameters.** `detail` (`auto`|`nodes`|`areas`; anything else is 006), `area` (one area key), `locate` (repeatable; more than 1000 is 006), `mapVersion`, `draft`, `repo`. `auto` answers nodes when the map has at most 500 nodes and areas above that; `nodes` over the budget answers areas with `"fallback": "over-budget"`. `area=KEY` answers that area's nodes and the edges among them (kind then key order, up to the area cap, then `area.truncated` with the full `area.counts`); a key the map lacks is `SND-CONTEXT-200` (404); `area:unassigned` opens like any area. `locate` answers `located[{key, area}]`, `area: null` for a key the map lacks. `repo`: a profile with one repo names it in the answer (`repo` is null when the profile has several and none was asked); a human naming one of several cuts the graph to it (`GraphSnapshot.ScopedTo`); a session is always its own repo, and another is `SND-CONTEXT-045` (checked before the map is read). No map is 030, no draft 044, a version not kept 120.
- **Shape (schema 1).** `schemaVersion, mapVersion, mapCommit?, draft, asOf, repo, detail, fallback?, counts, areas[], areaEdges[], nodes?, edges?, area?, located?, truncated, repos[]`. `asOf` is when the map version was written, so the answer is the same bytes while the map is. `counts` is the whole map's whatever the detail (`byKind` has every kind, zeros included); `areas` is sorted by key (`area:unassigned` first) and `areaEdges` is aggregated by (from, to, type) with `count` and `confirmed`, both always present. `nodes` are ordered by area, kind, key and carry `area`, `alsoIn` (at most 5), `trust` (`trusted`/`untrusted`, as lenses writes it), `provenance`, `confidence` and `confirmed`; `edges` by from, to, type with the link id. No evidence, quotes or decision text: the inspector reads those per node from `GET /sindri/context/node`. Labels follow the map's text rules (scrubbed, 200 characters); a human at the CLI or over plain HTTP reads clean text beside `trust`, every other caller gets untrusted labels fenced.
- **Repos (multi-repo pages).** `repos[{name, nodeCount}]` (always present, sorted by name, `[]` when no node names a repo) lists every repo the map holds nodes of, counted over the map before any `repo=` cut, so a repo picker stays whole while one repo is shown. `repo=R` (human callers of a profile with several repos) answers that repo's part of the map; `repos` still lists them all. Every node carries `repo` (left out when null: a node that belongs to no single repo). `NodeRepos.Index(graph, mapRepo)` decides it once per graph: the repo the node's own evidence names (`repo:<repo>/<path>`; a code file also takes the evidence of its `implements` and `tests` edges, a record its own `record:repo:<repo>/...` key), else the repo its implementing and testing files all name, else the repo the map version was built for, except decisions and records, which name none unless their evidence does; a node whose evidence names several repos is null (a feature implemented from two repos, a cross-repo decision). The same `repo` (omitted when null) is served on every node or file item the other reads list: lenses and subgraph (`bands.product.own|neighbors|affects[]`, `bands.intent.records[]|decisions[]`, `bands.code.files[]`), code (`files[]`, `tests[]`; a lexical test takes the repo of the files it names), history (`records[]`, `decisions[]`; `changes[]` and `git[]` always named their repo), neighbors and path (`nodes[].node`, `steps[].node`) and the review queue (`entries[]`; an orphaned entry is null). Not covered: `GET /node` (`NodeFact.Repo` exists but the node service does not set it yet), `code.data` things and schema files, and `filemap` (one repo per answer, its `repo` is the top-level field).
- **Home features and tags (#1365 step 3).** `HomeFeatures.Assign` gives every product file exactly one `implements` edge to a feature, chosen by directory and never by a model: the module root (nearest `package.json` or `.csproj`, else the repo) is split one directory level at a time while a group holds more than `HomeFeatures.SizeCap` (150) files, loose files of a split directory form the `<dir>._` leaf, and each child group is `part-of` its parent. Keys are `feature:<repo>.<path>` (a `_` in a name is `__`, a `.` is `_`; names equal after lower-casing all get `-` and 6 hex of the path's SHA-256; a key over 200 characters ends in `-` and 8 hex). The graphify clusters are tag features, `feature:tag:<repo>:<name>`, linked by model `tags` edges (`code tags feature`, suggestions in the review queue, never walked by a lens, never an area or a concept). A build drops what it makes again before it joins (`StructureCut`, in `BuildCuts`): home and group features, cluster features and their links; a decision on an old cluster link moves to the same file's new `tags` link or is undone, and `MapBuildReport.Structure` counts them. `sindri doctor` (`context:map`) warns on a concept pin that matches no feature.
- **Areas (`MapAreas`).** Every node gets one area. A feature's area is the root of its `part-of` chain (the smallest parent at each step; a cycle roots at its smallest key); its other parents' roots are `alsoIn`. Surfaces take their feature's area (`part-of`), actions their surface's (`on`), flows the area most of their steps are in (`step-of`), things the area of the surfaces and actions that `reads`/`writes` them most, code the area of what it `implements` or `tests`. Decisions follow what they `decides` (a decision that only `quotes` follows its records), records what they `mentions`; the two settle in passes, each reading only what earlier passes settled. One vote per edge, ties to the smallest key, the other areas reached (most edges first) in `alsoIn`. A node no vote reaches, and a node whose only votes are unassigned, is in `area:unassigned` ("Not linked to a feature yet"). The same graph gives the same areas whatever the order of its rows.
- **Cache.** `WholeMapCache` keeps the last 4 answers, each for the graph object the reader handed back, the question (version, draft, detail, area, locate) and the caller scope (fence policy, caller kind and repo, profile section). A new map version, an override or a synced record makes the reader hand back another graph, so an old answer is never served; a session's fenced answer is never served to a human.
- **CLI.** `sindri context map whole [--detail auto|nodes|areas] [--area KEY] [--locate KEY]... [--map-version V | --draft] [--repo R] [--json]`: the counts line, the kind counts, then one line per area (`label · members · kinds`), biggest first, `locate` results after; with `--area` the area's nodes. Exit 0 ok, 1 attention (not running, no map, no draft, version not kept, unknown area, repo outside the scope), 2 usage or a tool error.
- **No map yet (404 `SND-CONTEXT-030`, current map only: not with `draft` or `mapVersion`).** The problem answer carries two optional members so a client can say how far a build is and what is set up. A member that is not known is left out (never null); times are ISO-8601 UTC (`O` format).
  - `building`, only while a `BuildContextMap` job runs: `jobId` and `startedAt` (the job row), `sourcesTotal` (the engine's sources in scope), `sourcesDone` (how many of them have `LastSync` at or after `startedAt`), `lastSyncAt` (the latest `LastSync` of those sources). `sourcesDone` and `sourcesTotal` are left out together when the sources can't be read. There is no node count: the map builder keeps none while it runs.
  - `noMap`, always: `repos` (the scope's repos), `approvedAt` (the profile row), `firstRunStartedAt` (the oldest `BuildContextMap` job).
  - The CLI appends them in plain words: `[a map build is running since 2026-10-23 06:00 UTC (job JOB-7); 40 of 100 sources read; last sync 2026-10-23 05:30 UTC; repos: my-app, docs; approved 2026-10-23 05:00 UTC]`.
- **Errors.** `SND-CONTEXT-200` (404, "No such area in this map"; fix: read `areas` from `map/whole` first); 201 to 209 are reserved for this stage. Everything else reuses 006, 030, 044, 045, 120.
- **Tests.** `Aw.Context.Tests/WholeMap/` (`MapAreasTests`: every rule, ties, cycles, the cap, unassigned, determinism; `GetWholeMapTests`: the budget at 5, 6 and the default, the areas cap, `locate` at 100 and 101, fences, a session refused another repo, a draft, retained versions, the cache); `ContextWholeVerbTests`; goldens in `Golden/Api/context-whole/` (`nodes-small`, `areas-over-budget`, `area-open`, `area-truncated`, `area-unknown-200`, `locate`, `draft`, `no-map-030`, `no-draft-044`, `map-version-v1`, `map-version-not-retained-120`, `bad-detail-006`, `session-own-repo`, `session-other-repo-045`). The UI's DTOs are pinned against them.

## The concept layer's reads (slice D of the concepts note)

Concepts are the map's top band: `concept:<slug>` nodes (kind `concept`) that features, records and decisions point at with `belongs-to` edges; `concept:other` is reserved and holds every member no concept takes (the read computes it, so a map with no concept node still answers). The reads never call a model: the concepts are stored in the map.

- **`GET /sindri/context/concepts?repo=&cursor=&limit=&q=&draft=&mapVersion=`.** `{apiVersion, repo, mapVersion, state, code?, concepts[{id, name, summary, source, size{features, records, decisions, code}, citations[{key, span?}], terms?[], pinned?, aliases?[]}], other{id, name, size}, links[{from, to, weight}], next?}` through `IContextConcepts.Concepts` (`ConceptBuilder.Build`). Concepts come by name; `next` is an opaque cursor bound to the map version (another version's cursor, or a `limit` under 1, is 006; there is no upper limit). `q` keeps the concepts whose name, summary or a member's label contains it. `code` counts the distinct code nodes the member features' `implements` edges reach. `links` weigh the non-`belongs-to` edges between members of two concepts (`concept:other` counts), listed for every pair with an end on the page. `state` is `ok`, `empty` (no concept node yet, everything is in `other`), `building` or `unavailable` (no map yet: a build job runs, or not; `code` is then `SND-CONTEXT-030`). A session sees its repo's concepts, sized by its repo's members (`RepoScope.WithConcepts`). `pinned: true` marks a concept the owner pinned in the profile (`source` says `override` for it too; this is how an agent finds a pin id), and `aliases` lists the owner's other words for it (both left out when empty). A pin's `summary` and `aliases` are read from the approved profile through `PinSet`, over the summary on the map node, so an edit to either shows the moment it is approved and rebuilds nothing. A question that uses an alias (all of its words) anchors on the pin's concept, and `node=<alias>` resolves to it (`AnchorMatch`; a node's own label wins a tie, the longest alias of a pin counts).
- **`GET /sindri/context/concepts/glossary?repo=&draft=&mapVersion=`.** `{apiVersion, repo, mapVersion, state, concepts[{id, name, terms[], ownerTerms?[]}], terms[{term, concepts[]}]}` through `IContextConcepts.ConceptGlossary` (`ConceptGlossaryBuilder.Build`): the words stored on the concept nodes' `terms` attribute, by concept (key order) and by word. A pin's aliases come first in its `terms`, and `ownerTerms` names them (the owner's words, not derived ones). `state` is `ok`, or `empty` when no concept carries a term (the map was built without symbol embeddings). A map error is the concepts read's (`SND-CONTEXT-030` with no map). Not on MCP: `context_concepts` carries `terms` on its rows, and a separate tool would change the tool list.
- **`GET /sindri/context/concepts/{id}/members?cursor=&limit=&repo=`.** `{apiVersion, repo, mapVersion, concept, members[{key, kind, label}], next?}`, by kind then key; an id the map lacks is 003.
- **`GET /sindri/context/map/whole?concept=<id>`.** Opens one concept like `area=`: the members, then the nodes on the other end of an edge from them for two levels (a feature, record or decision found on the way is left out unless it is a member), the concept's own node and the edges among those. `concept:other` opens the unassigned members. An unknown concept is 200. Together with `area=` it opens that area's nodes inside the concept's scope (the concept's own node is in no area, so it is not shown); an area holding nothing of the concept is 200 like an unknown area. Concept nodes are a band of their own (`area:concepts` on each node), never an area: `AreaAssigner` skips them, no area lists them, and the counts' `byKind` has a `concept` row. `located[].concept` lists the concepts holding each located key (a list; `concept:other` for an unassigned member, empty for any other kind).
- **MCP `context_concepts {q?, cursor?, limit?, mapVersion?}`.** The same service as the route (the draft map is never read over MCP, 022 for bad arguments); `concept:<slug>` is an anchor of `context_lenses` and the `node` of `context_query` through `Cascade`.
- **`GET /sindri/context/pages?repo=&concept=&page=&marks=&suggested=&limit=&offset=&draft=&mapVersion=`.** `{apiVersion, pages[{id, key, name, repo?, mark, code, actions[{id, key, name, mark, code, concepts[]}]}], total, truncated, mapVersion}` through `IContextConcepts.Pages` (service `GetContextPagesService`: the same scope, `MapRead` and caller handling as the concepts read). `marks` is `all` (the default), `owner` or `suggested`; `suggested=true` is `marks=suggested`; the request carries `PageMarks`, not a bool, because the CLI wants owner only, `--suggested` wants suggested only and MCP and HTTP want both. `concept` takes an id or `concept:id`; text that cannot be an id, or a repo name that cannot be one, is 258 before the engine is asked. Pages come owner first in profile order, then suggested by action count; `limit` defaults to 50 (1 to 200), `offset` to 0. A page id the map lacks is `SND-CONTEXT-257`, a concept or repo it lacks `SND-CONTEXT-258` (HTTP 404; both join `ContextErrors.Attention`, so the CLI exits 1).
- **MCP `context_pages {concept?, page?, repo?, limit?, offset?, mapVersion?}`.** The same service as the route, marks `all`, under `session:mcp` (the session gate 020 and repo scope 003 apply; the draft map is never read, 022 for an unknown argument or a wrong type). Its text summary has page ids, marks, action counts and whether code was found, never a name; names are fenced in `structuredContent`.
- **CLI `sindri context page list [--suggested] [--concept C] [--repo R] [--json]`** (`ContextPageVerbs`). No flag asks for `marks=owner`, `--suggested` for `marks=suggested`; `--json` prints the `PagesView`. Setting and removing pages is the next section.
- **Doctor `context:pages`.** `ContextEngineChecks` asks `IContextEngine.CheckPages(scope)` right after `context:map`; the scope carries the approved profile's pages (each slice maps the profile itself, AW1003, so `RunDoctor` has its own copy of the page mapping `ContextScopes` holds).
- **Tests.** `Aw.Context.Tests/Concepts/` (`ConceptBuilderTests`, `GetConceptsTests`, `ConceptRepoScopeTests`), `WholeMap/WholeMapConceptTests`, `ContextConceptsMcpTests`, `GetContextPagesServiceTests`, `ContextPagesHostTests`, `ContextPageVerbsTests`, `ContextPagesDoctorTests`, and the goldens `context-concepts`, `context-concepts-id-members` and `context-whole/concept-*`.

### Setting pages (`POST /sindri/context/pages`, pages spec §3.1 and §3.6)

- **`POST /sindri/context/pages`.** The `SetPages` job (catalog row in `ContextRows`, class job, `SessionOk`), started on the generic job route; the `GET` of the same path is the read (`GetContextPagesEndpoint`). Body `{set?[{id?, name, repo?, surface?, actions?[{id?, name, action?, concepts?}]}], remove?[id], ifHash?, dryRun?}`. A `set` entry replaces the whole page with that id (so a flag left out clears that part); a missing page id is the name's slug (a name with letters or digits outside a-z/0-9 gets the first 8 hex of the SHA-256 of the name appended, so non-Latin names get different ids), a missing action id the action name's slug (made unique within the page), and the written entry always carries both. The job writes `index.pages` through `IProfileIndexWriter` (`PageEdit`), records `context.page.written` (`<id>:<created|updated|removed>` per page, the actor, the new hash) and returns `{pages[{id, name, repo?, action}], hash, written, approved: false, diff[]}`. `dryRun` does everything but the write: `written` is false, `hash` is the live hash read and `diff` holds the page rows that would change (`index.pages.<id>`, `repos.<r>.index.pages.<id>`; key paths, as `ProfileDiff` has them). It never approves.
- **Where the planner puts a page** (`PagePlanner`). An existing page is found by its explicit `id:`; it stays in the list it is in, so a page under `repos.web.index.pages` is written back with `PageEdit.Repo = "web"`, and a `repo` other than `web` is `SND-CONTEXT-252`. A new page goes top-level. With `repo` given it must be a profile repo (`no repo 'wbe' (fix: one of web, admin)`, 252); with a surface the repo comes from the map (several is 252, from `PageRules`); with neither, a profile with several repos is `index.pages[2]: say which repo this page is in, web or admin (fix: --repo)`, 252.
- **Concepts.** In an action's `concepts` an id of a pin or of a map concept (with or without `concept:`) stays as written. Otherwise an exact name, ignoring case, becomes its id: a pin's name wins over a derived concept of the same name. What matches nothing stays as written and `PageRules` refuses it (`SND-CONTEXT-250`, with the nearest id).
- **Checks, in this order.** The request (`PagePlanner.Precheck`, in the start's gate): nothing to do is `256` (`pages: nothing to set or remove`), the same id twice or set and removed in one request is `254`. Then, at the start and again in the job, over the live profile and `IMapPageFacts` (`EngineMapPageFacts` over `IContextEngine.PageFacts`: concept ids and names, the repos of each surface and action key): the placement above, then `PageRules.Check` over **every** page as it will be after the write, built without the read-time caps, with `Stored = EntryKey.Normalized` so a key typed `action:SchedulePage.submit-schedule` finds the map's lower-cased `action:schedulepage.submit-schedule` (issue 1790), and with the concept ids of the map plus the live profile's pins (a pin not yet built is still a concept); the first error is returned (`250` to `255`). Then the profile's own rules. A page that has no `id:` in the profile but whose name makes the id is not written over (the writer matches by id only): `index.pages[3].id: 'schedule-week' is already index.pages[0]'s, made from its name (fix: give index.pages[0] an id: in the profile)`, `254`. Removing an unknown page is `257` (`no page is named 'schedul-week' (fix: try schedule-week)`). A dry run is refused exactly as a write is. A profile that moved since `--if-hash` is `SND-PROFILE-003`.
- **CLI.** `sindri context page set <name> [--id ID] [--repo R] [--surface KEY] [--action "Name[=KEY][->c1,c2]"]... [--if-hash H] [--dry-run] [--detach] [--no-start] [--json]`; `sindri context page set --from-file <pages.yaml> [...]` (the profile's own `index: pages:` shape, or a bare `pages:` list, read with the profile's reader so a typo names its key path); `sindri context page remove <id> [--if-hash H] [...]` (`ContextPageWriteVerbs`, `ContextPageArguments`). `--action` reads the name up to `=` or `->`, `=KEY` is the map action, `->` starts a comma list of concepts; a malformed one is `256` (`--action "Submit->": no concept after '->'`) and nothing is posted. `set` and `remove` follow the job like `context pin set`; a human starts the control plane unless `--no-start`, a session never does. Output: `created|updated|removed <id> (<name>)` per page, then `waiting for your approval: sindri profile approve <hash>`; a dry run prints the diff rows and `dry run: nothing written`. Exit 0 written; 1 for attention (`ContextErrors.Attention`: unknown name or key, no map, Sindri not running, a session refused); 2 for invalid input or a refused write (252 to 256, `SND-PROFILE-003`).
- **Errors on the write.** 250 to 256 as the table above; `257` on remove; `258` stays the read's.
- **Tests.** `Aw.Sindri.Tests/SetPages/` (`SetPagesServiceTests`, `ContextPageWriteVerbsTests`, `ContextPageExitTests`), the `SetPages` row in `Golden/Api/commands/catalog.json`, the kind list in `Golden/Api/jobs/bad-kind.json` and `context.page.written` in `Golden/Api/events-catalog/catalog.json`.


### Add several pages (`QUERY /sindri/context/pages/preview`, pages spec §3.6)

The app's "Add several pages" sheet pastes a list and ticks what to add; the reading of the paste lives here, on the server, so the rules exist once (the spec had the app parse locally). Read-only: nothing is written or queued. Saving stays `POST /sindri/context/pages` with the ticked blocks' `page` values in `set` (one write, one approval). The row (a body query like `QUERY /sindri/context/code`, no catalog row) is called by the app; `PreviewPagesService` (`ContextCalls`) reads the pages through `IContextEngine.Pages` under the caller's scope (200 at a time until `truncated` is false, so a session sees only its repo's pages), the live profile's pins (`IProfileSource`) and the map's concepts (`IMapPageFacts`).

- **Body** `{text, repo?}`. `text` is 1 to 65,536 UTF-8 bytes (empty or whitespace-only, or longer, is `SND-CONTEXT-259`: `pages preview: nothing pasted (fix: one page per block: its name, then one action per line)` / `pages preview: 70,120 bytes; at most 65,536`). An unknown `repo` is the `SND-CONTEXT-258` text `GET /sindri/context/pages` words it; no map is whatever the pages read answers.
- **Answer** `{mapVersion, profileHash (the live profile read for the pins: save with it as `ifHash`), blocks[{block, line, name, match?{key, id, name, actions}, replaces?, sameAs?, ticked, reason?, actions[{name, match?{key, name}, concepts[{written, id?}]}], unknownConcepts[], page?}]}`. `page` is a `PageSettings` exactly as `set` takes it (null when the row is unticked). Its `id` is left out unless the block matched a suggested page whose id is not the slug of the pasted name, so the save replaces the page `replaces` and `sameAs` talk about.
- **Reading the paste** (`PageListText`, pure). Control and format characters go (a tab becomes a space); `\r\n` and `\r` are line ends. One or more whitespace-only lines separate blocks. A line loses a leading bullet (`-`, `*`, a bullet, `+`) or number (`1.`, `1)`, only when a space or the line's end follows) and the spaces after it; a line empty after that is skipped. The first line of a block is the page name; each other line is an action, `Name` or `Name -> c1, c2` (split on the first `->`, concepts on `,`, blanks dropped; nothing after `->` means no concepts).
- **Matching** (`PageListPreview`, pure). A block matches only a **suggested** page: the one whose name has the same words (`AnchorMatch.Words`: case, a plural `s` and small words ignored), else the one sharing the most words (at least one); ties go to the page with more actions, then the key in ordinal order; none is a new page. `replaces` is the id the block will get (the match's, else the name's slug) when an **owner** page has it. `sameAs` is the first earlier block with the same match key or the same id. An action matches only when the page did, by the same word rule; a matched action with no `->` concepts written keeps its own concepts. A concept resolves as `PagePlanner` does: an id (with or without `concept:`) of a pin or map concept stays; else an exact name ignoring case, pins before the map; else it is unknown.
- **Checks.** Size, name and repeat limits come from `PageRules.Check`, not from a copy of it. The row's `reason` is the first problem, in this order: `Same page as block 2`; then a name, size or repeat reason (`Not added: the name is 81 characters; at most 80`, `Not added: 41 actions; a page holds at most 40`, `Not added: action 3 relates to 21 concepts; at most 20`, `Not added: action 2's name is 81 characters; at most 80`, `Not added: two actions are called Submit`); then `No concept called "aproval": pick one`. A row with a reason is unticked and has no `page`.

### Pinning concepts (`POST /sindri/context/pins`, concept pins spec §3.6)

- **`POST /sindri/context/pins`.** The `PinConcepts` job (catalog row in `ContextRows`, class job, `SessionOk`, so every caller kind may run it), started on the generic job route (`JobStartedResponse`). Body `{set?[{name, id?, summary?, aliases?, features?}], remove?[id], ifHash?, withFolderRules?}`: in a `set` entry a missing field keeps the stored value, a list replaces it and `[]` clears it. The job writes `index.concepts` through `IProfileIndexWriter`, records `context.pin.written` and returns `{pins[{id, name, action: created|updated|removed}], hash, approved: false, dropped[]}` as the job's `result`. It never approves: `ApproveProfile` (or `sindri profile approve <hash>`) does.
- **CLI.** `sindri context pin set <name> [--id ID] [--summary T] [--alias W]... [--feature K]... [--clear-aliases] [--clear-features] [--if-hash H] [--detach] [--no-start] [--json]`; `sindri context pin set --from-file <list.txt> [--if-hash H] [...]` (the Add several format: one concept per line, or blocks with a blank line between, bullets and numbers dropped, a trailing `(also: word, word)` for aliases, at most 100 concepts, the rest left out with a notice on stderr; `ConceptList`, `Aw.Sindri.Core/Setup/`); `sindri context pin remove <id> [--with-folder-rules] [--if-hash H] [...]`; `sindri context pin list [--json]`. `set` and `remove` start the job and follow it like `repo add` (Ctrl-C cancels the job, a second Ctrl-C leaves; `--detach` prints the job id); a human starts the control plane unless `--no-start`, a session never does. The last line is `pinned billing (waiting for your approval: sindri profile approve <hash>)` (`unpinned ...` for a removal). With `--json` the output is the follower's NDJSON: the `JobStarted` body, then the final job whose `result` is the shape above. `list` reads the live profile in process (`IProfileSource`, like `profile validate`; the control plane may be down) and prints `{hash, pins[{id, name, aliases, features, setIn: "profile" | "repo <name>"}]}`: the hash to pass to `--if-hash` and the ids to remove. Exit codes: 0 written, 1 the job failed, the control plane is not running or a session was refused (`SND-API-001`, `SND-AUTH-002`), 2 invalid input.
- **Errors (171 to 177, the write).** `SND-CONTEXT-171` `index.concepts[2].name: <the rule it breaks>` (name, summary or alias refused); `172` `feature:billing.refundz is not in map v41 (fix: pick it from context_concepts or the feature picker)`; `173` `'Billing ' and 'billing' make the same id billing (fix: --id, or rename one)`; `174` `no pin is named 'bilin' in the profile (fix: try billing, auth)`; `175` `pin billing has 2 folder rules set outside the app (fix: --with-folder-rules to drop them too)`; `176` `pin billing is set in repo api's own profile (fix: edit it there)`; `177` `line 4 has no name (fix: start the line or block with the concept's name)` (`--from-file`; nothing is written). A profile that moved since `--if-hash` is `SND-PROFILE-003`. 178 is unused, 179 is the suggest read's.
- **Tests.** `Aw.Sindri.Tests/PinConcepts/` (`PinConceptsServiceTests`, `ConceptListTests`, `ContextPinVerbsTests`), the `PinConcepts` row in `Golden/Api/commands/catalog.json` and the kind list in `Golden/Api/jobs/bad-kind.json`.

### Suggesting pins (`GET /sindri/context/pins/suggest`, concept pins spec §3.6, Q7d)

What the "Pin this" and "Add several" forms offer. Read only: nothing is written or queued, and there is no catalog row (context GETs have none).

- **Modes.** Exactly one of `concept=<key>`, `search=<text>` or `line=<name>` (repeated, 1 to 100, with an optional `def=<definition words>` repeated by index: none, or one per line), plus `repo=` like the glossary's and `limit=` (search only, default 50, at most 200). The answer is `{apiVersion, repo, mapVersion, terms?, features?, matches?}`; the two modes not asked for are null.
- **`concept=`.** `terms[{word, newRecords}]`: the concept's stored terms (`PinSuggestBuilder.TermsOf`, best first, up to 20), each labelled like the glossary's. `newRecords` is the number of records the alias rule would newly link to the concept if the word became an alias: visible live records (not removed, not blocked) in the intent layer, the same set `RelinkPinAliases` scans, whose chunk text (joined with `\n` in `Seq` order) holds the word by `RulesLinker.Matching`, minus the records the layer already links to the concept by any rule. The records are walked once and every term is tested per record. A concept the map lacks is `SND-CONTEXT-003`.
- **Which records count.** The ones the caller may see, with the query pipeline's mask (`QueryPipeline.WithRecordStore`): the audiences of the repos in scope (a human naming `repo=` only that repo's; a session only its own), a human's Slack channels, and for a session the `record:` nodes of its scoped map. A stored record that names another repo than the one answered for is left out; one with no repo is judged by its audience alone. So a session's count leaves out another repo's records.
- **`search=`.** `features[{key, label}]`: the map's Feature nodes whose label or key holds the text, case-insensitive. Labels that start with the text come first, then the rest by label ordinal, then by key.
- **`line=`.** `matches[{line, concept, name}]`, one per line in order (`concept` and `name` null when none fits). Candidates are the Concept nodes except `concept:other`. First a concept whose label has the same significant words as the line's name (name only, plural 's' dropped; the larger concept when several); otherwise the concept whose terms share the most distinct significant words with the name and definition (at least one). Ties go to the larger concept (features + records + decisions), then the lower key. `'Payments'` with the definition `'how we take money from cards'` matches `concept:billing` when its terms include `card`.
- **The app batches by encoded size.** A pasted list is refused in the client before any call when it has more than 100 lines (the server's `SND-CONTEXT-179`, same text). Otherwise rows go into a call while the encoded `line=` and `def=` parameters, as the client builds them, stay at or under 6,000 characters (Kestrel refuses a request line over 8 KB, and a non-ASCII letter takes 6 or more), so an all-ASCII list goes about 25 or more rows per call and a list of 80 `é` rows about 12. A row alone always fits. Each batch's `line` index is shifted by its offset and the matches are joined (`PinSuggestBatches`).
- **`SND-CONTEXT-179`.** No mode or more than one; an empty search; a `concept=` that is not a concept key; more than 100 lines; a line with no name; a name, or name plus definition, over 80 characters once trimmed; a `def` count that is neither 0 nor the line count. The message says which and how to fix it. Nothing is read.
- **Tests.** `Aw.Context.Tests/Concepts/` (`PinSuggestTests`: the counts, the session scope, an unknown concept; `PinSuggestMatchTests`: the match rules, ties, search order and limit, `RulesLinker.Matching`), `Aw.Sindri.Tests/ContextCalls/PinSuggestQueriesTests` (179), the goldens `context-pins-suggest` (`terms`, `features`, `matches`, `invalid-179`), and the client contract in `Aw.Sindri.Ui.Testing` (`ContextPinSuggest_*`, the 60-line batching).

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

- **Tables (migration `0010_s4p_map_publish`).** `map_version_head(version, repo, commit_sha, on_default, index_commit)` (`index_commit` is migration `0025_s4_map_head_index_commit`, #1704: the code index commit the build's symbol citations were resolved against, NULL when no index held the build's commit; a store before 0025 reads NULL; cascade-deleted with the version; the commit is 7 to 40 lowercase
  hex, checked by the table too), `map_hold(version)` (one draft; cascade-deleted with it) and `map_publish_chain(id = 1, since)` (the start of the wait: tied to no draft, so a
  rebuild keeps it). Reads never migrate: a store from before 0010 holds nothing and has no heads.
- **Types (Core/Map).** `MapHead(Repo, Commit, OnDefault)` with `IndexCommit` (`IsCommit`, `Check`: 191), `MapHold(Version, Since, Heads)` (`NextSince(chain, now)`: the chain's start, never later than now),
  `MapPublishState(Hold, LatestDraft, Current, CurrentHeads)`, `MapPublishView(Repos, Hold, LatestDraft, Current, CurrentHeads)` and `MapBuildReport.Heads`.
  `MapImport.Heads` is what a build names; null keeps a replaced draft's heads, or for an import the one commit its file names (as `OnDefault`).
- **Store (`IGraphStore`).** `Heads(version)`, `Held()`, `Hold(MapHold)` (one transaction: the draft must be the latest, else 190; `since` is `MapHold.NextSince(stored chain, hold.Since)` and the clamped value is stored;
  any other hold is replaced), `Release(version)` (the chain stays) and `AppendOverrides(rows, ct, expectedDraft)` (the latest draft is checked inside the append's transaction: 190, because the overrides layer is shared across versions).
  Writes: a build (`SaveDraft`) drops the old draft, its hold and heads with it and writes the new heads; `ReplaceDraft` and `OpenDraft` copy the source's heads (a replacement that names heads keeps those);
  every `Promote` and every import (`SaveCurrent`) deletes the hold and the chain in its own transaction; retention (`Delete`) removes a version's heads, and a retained version keeps them.
- **Facade (`IContextMap`).** `Held`, `Hold`, `Release` refuse a session (020); `PublishStatus` is the read for everyone else and is cut to the caller's repos (`context.repos` for a human, the one repo for a session, the
  usual 020/003 session rules): no other repo's name, commit or count. `Promote` and `CleanDraft` take `int? expectedVersion = null` (null: the latest draft, the human path). With a value, a latest draft of another number, or none,
  is `SND-CONTEXT-190` and nothing is promoted or written; the cleanup checks it on every pass read and on its append. Without one, no draft is still 046.
- **Build.** `BuildMapService` records, for each mapped repo, the commit its extraction read and whether the default branch holds it (a default ref that can't be read counts as not on it) into `MapBuildReport.Heads` and the draft, with `IndexCommit`: the commit of the code index its symbol citations were resolved against (the first answer `RecordPass` got from `ICodeSymbolSource`, or `Heads` for a repo no citation asked about), set only when that index held the commit the build read, null when it was missing, still building or at another commit. A build that waited at `MapBuildGate` reuses the draft saved meanwhile only when every repo's saved `IndexCommit` equals the commit the index holds now (#1704); otherwise it builds again.
- **Publish gate and a draft built before its index (#1704).** `MapPublishDecision` answers `MapRebuild` (not `MapPublish`) when every gated repo's outlook is Built but a held head's `IndexCommit` is null or not its commit. The refresh job's `RefreshGate` keeps such a draft held; the `context-map-refresh` tick (`PublishStep`) starts a `RefreshContextMap` job for it (never while a refresh or publish runs, and under the refresh backoff) instead of a publish; `PublishContextMap` answers `Rebuild` and publishes nothing.
- **Profile.** `context.publish.waitForIndexMinutes` (`ContextSettings.Publish`, 0 to 240, default 15), `SND-CONTEXT-001` with the key path outside the range. A map file whose `commit` is not 7 to 40 lowercase hex is `SND-CONTEXT-031` at `commit`.

### Errors

`SND-CONTEXT-190` (`DraftChanged`, conflict, attention): the expected draft is not the latest. `SND-CONTEXT-191` (`MapHeadInvalid`): a head's commit is not 7 to 40 lowercase hex, or a repo appears twice. `192..194` stay reserved.

### Tests

`MapPublishStoreContract` (`tests/Aw.Context.Testing/Map`, run against `FakeGraphStore` and the SQLite store): heads in order, an import's one commit, invalid commits refused everywhere, the hold and its read, 190 for a wrong draft, the chain surviving a
rebuild and clamped to now, promote and import clearing the hold and the chain, replacement and open copying heads, retention, release, `expectedDraft`. `MapPublishTests`, `MapHoldTests`, `PublishSettingsTests` and `MapPublishMigrationTests`
(an old store opens and gets the tables) in `tests/Aw.Context.Tests`.

## The publish words (code index spec 6.4, F1P-c)

`GET /sindri/context/map/publish` (`GetMapPublishStatusRequest`, a query: no catalog row) answers `MapPublishStatusView`: `state` (`held`, `overdue`, `published`, `published-without-waiting`, `published-stale`, `published-by-hand`), `word`, `reason` (the decision's reason of the current version's `context.map.published` event, or null), `stale` with `staleRepos` (repo, the index's commit, the map's commit), `version`, `until` (while held), `publishedAt` (the `published` event's time), `waitingOn` (while held, `web (building)`) and `lines`. It is computed live by `MapPublishStatusRules` from the engine's `PublishStatus` (a session gets its own repos only), the code index's commits (`IIndexOutlooks`), the ledger's `published`, `promoted` and `hold.dropped` events (read from a bounded tail window and matched on repo and version, so another repo's event with the same version number is not mistaken for this map's) and the clock; nothing is stored. `overdue` is a held map whose deadline passed more than two ticks ago. Stale beats how the map was published; `published-by-hand` is a `context.map.promoted` by a `human:` actor for the current version; `published-without-waiting` is the reasons FirstMap, NoIndex (including a paused code index service), WaitOff and IndexBroken (TimedOut reads `published` once the commits match, with "waited out its wait; the code index has caught up"). Sentences are the spec's words: a held map names the repos it waits on with their state ("retrying in N min" from the outlook's RetryAt), the repos not indexed yet and the ones whose index is already newer, and when it goes ahead (the owner's time zone); an overdue one says to resume the map service or `sindri context map promote`; a stale one adds "built N min ago" when the code index port says when it built (it does not yet, so the clause is absent in the running app), and for TimedOut or IndexBroken what to look at. When a `context.map.hold.dropped` is the newest map event the lines start with "Held map vN was replaced by a newer build." Not built: the chained-wait sentence and the failing job's code in the overdue sentence (neither is stored anywhere the reader can see). The stale sentence says "older than" for a map published ahead of its index (TimedOut, FirstMap, WaitOff, IndexBroken), "newer than" for an index that moved past a matching map (IndexReady), and "not the commit of" when the event does not say. The publish tally of the spec (human-only) is not built.

`sindri context map status [--anchor K] [--json]` prints the map's status and then `publish: <short form>` (`held: v14, until 14:32, on web (building)`, `overdue: v14, was due 14:32`, `published: v14, code index matches`, `published: v14 without waiting (first map)`, `published stale: v14 (reason timed-out, web stale)`, `published by hand: v14`) with the sentences; `--json` adds `state`, `word`, `reason`, `stale`, `staleRepos`, `until`, `lines` and `publishedVersion` beside the map's own fields. Doctor's `context:publish` row and the Health page's Context map card use the same reader; the row is `warn` for an overdue map, and for a stale one only when it is still stale more than five minutes after the publish (inside that window it is `ok`), and carries the last publish's reason. `sindri jobs show <refresh>` on a refresh that ended `held: map vN …` adds a `publish:` line naming the publish job whose summary says `map vN promoted · <reason>` (or that none has run yet); the link is written and read through `MapJobSummaries`, the one place the two summaries' words live. Fixture: `scripts/sindri-ui-fixture.sh context publish <state>`; the prover reads it with `scripts/sindri-prove.sh publish [--session [--repo R]]`.


## S-8: the ranker stages and the ranker arms

The query's fusion (`RankFusion`, reciprocal rank fusion) is followed by two optional stages, both **off by default** (`context.ranker`, `docs/csharp/sindri-profile.md`); the profile enables a stage only after a backtest shows it helps, or the owner decides below 30 samples.

- **Features** (`Aw.Context.Core.Ranking.FeatureRanker`, pure). Five scores from 0 to 1 per fused record, from how the map links it to the nodes the answer reached (`GraphPart.RecordSignals`, read by `RecordLinks`): cascade distance (`1 / (1 + steps)`), edge type (decides 1, quotes 0.7, mentions 0.4, other 0.2), provenance (human 1, deterministic 0.7, model 0.4), supersession (a record whose decision a later one replaced scores 0, otherwise 1) and recency (halves every 90 days from the link's date). The boost is the weights' mean of the five, and the item's fusion score is multiplied by `1 + boost`: features can at most double a score, so they reorder neighbours and never overturn the fusion's lead wholesale. Weights start equal.
- **Rerank** (`RankerStage`, `RerankOrder`). Only for the top `rerankTop` items (default 30, at most 100) and only when there are two or more: the scrubbed, fenced titles go to the model port with purpose `rerank` (a budgeted call, 20 seconds, 4 tokens per item), and the answer is read as a JSON array of the item numbers. Numbers out of range and repeats are skipped and items the model left out keep their order after the named ones. A model that is unavailable or answers no order leaves the order as it was and the answer lists `rerank` as degraded (SND-CONTEXT-090). A local cross-encoder adapter is not built: the port is the model port, and a cross-encoder would be another `IModelPort` adapter, not a new stage.
- **The ranker arms.** `context eval backtest --arms ranker` runs `ranker-rrf`, `ranker-features`, `ranker-pin` (features plus the anchor's pin leg, `context.ranker.pinLeg`) and `ranker-rerank` over the same inputs as `all` (the as-of map and the docs at the cut) with the stage set forced by the arm (`ArmQuestion.Ranker`, never reachable from the query API or MCP); each row has dimension `ranker`. `--arms all` does not run them, because the rerank row asks a model. The rerank row's model calls are not counted against the adjudicator's token cap.

## Body queries for long anchor lists (QUERY on lenses, code and history)

`GET /sindri/context/lenses`, `/code` and `/history` carry the anchors in the query string, so a list of a few hundred keys passes Kestrel's 8 KB request line and answers 414. Each route also answers `QUERY` (the HTTP method for a safe read with a body, as `QUERY /sindri/context/subgraph`) with the same fields as a JSON body, bound into the same request record and handled by the same service: no new engine logic, same answers, same errors (`SND-CONTEXT-006` for no anchor or a draft with a `mapVersion`, 003, 044, 120, 140), same session scope and fencing, byte-identical bodies for the same set.

| Route | Body |
|---|---|
| `QUERY /sindri/context/lenses` | `{anchors[], depth?, hops?, draft?, mapVersion?}`; one distinct anchor answers schema 1, several schema 2 |
| `QUERY /sindri/context/code` | `{anchors[], draft?, mapVersion?}` |
| `QUERY /sindri/context/history` | `{anchors[], since?, before?, draft?, mapVersion?}` |

They sit in the queries group like the subgraph QUERY (`X-Sindri-After` honoured, no idempotency key, no ledger event, a foreign `Origin` is 403 `SND-API-008`, no catalog row; a POST is 405). The CLI (`sindri context lenses|code|history`) sends GET and switches to QUERY when the query string passes 4000 characters (`ControlPlaneClient.MaxGetQuery`); its output is unchanged. MCP `context_lenses` calls the service directly and has no request line. Goldens: `query-*` scenarios under `context-lenses`, `context-code` and `context-history`.

## Parallel walks (issue 1101)

The lenses set, the code lens and the history lens answer the same bytes at any degree of parallelism. `WalkOptions` (Core `Walks/`) carries the degree: `WalkOptions.Serial` is one at a time, and the engine's default is `Environment.ProcessorCount - 1`
(`AddContextFeatures`; Core cannot read the environment). The services take it as an optional last constructor argument (none means serial; `AddContextFeatures` registers the default), and `MapWorld` takes one so a test can run degree 1 against degree 8.

`BoundedParallel` (Core `Walks/`) runs independent read-only work items with at most `degree` in flight and returns one result per item at the item's index (`Map` for async work, `MapSync` for CPU work). Every use computes
per-item results in parallel and merges them serially in input order, so first-one-wins de-duplication, tie-breaks and caps pick the same items as a serial run; nothing that shapes output sits in a completion-order collection.
What runs over the cores: each anchor's scope and cascade (`AnchorSet.Resolve`; the names are still resolved one after another), each scope's product lists, intent records and decisions, conflicts and coverage counts (`LensSets`,
`AnchorSet.Coverage`, `HistoryLens.Coverage`), and the per-repo git reads of the history lens (`HistoryPipeline`; the answers are taken in repo order up to the first that ran out of time). The shared graph indexes (`GraphIndex`,
`EdgeLookup`) are built once before the workers start, so a lookup is never built twice. The code lens's per-file lexical reads keep their own `GetCode/Fanout` (at most 4 at once); the SQLite intent read stays serial.
`WalkEquivalenceTests` pins the lenses, code and history answers byte for byte at degree 1 and 8 (when no history repo hits its deadline: under load a different repo can be the one that runs out of time than in a serial run). The degree is per call, not a box-wide cap: four lens calls at once each take `ProcessorCount - 1`. A work item that throws surfaces as its own exception type at any degree; `MapSync` unwraps the `AggregateException` that `Parallel.For` raises and rethrows the first inner exception it collected.

## Map structure checks (map structure spec §4, step 5)

`MapChecks.Measure` (Core, pure) runs in `BuildMapService.Save` on the joined graph, before the concept namer and before `SaveDraft`. (a) counts leaf home features of at least 5 files with no deterministic roll-up edge (feature depends-on, or feature reads or writes a table) in either direction; it applies only with at least 20 such features and at least one file-level (code) depends-on, reads or writes edge, otherwise it is skipped with `structure: no file edges (imports and data stores not extracted)`. (b) counts leaf home features over 150 files. Degraded above 5% (a) and 2% (b), refused above 60% and 10% by default (the 5% measured on real maps, issue 1365: healthy maps sit at 1 to 1.5% isolated, a TypeScript monorepo that loses its cross-package imports at about 9%); the limits and the minimums are the profile keys `context.checks.isolated.*` and `context.checks.overCap.*` (`docs/csharp/sindri-profile.md`), whole percents, and the notes print the configured limit; the file cap is the home features' split size, not a key. A value exactly at a limit passes. A refusal is SND-CONTEXT-060 (a) or 061 (b) and writes nothing; a degraded result is a note in `MapBuildReport.Degraded` and a numbers-only reason in `map_version.checks` (migration 0017), copied on promote and printed by `sindri context map status` and the Context page header. `context.checks.mode: warn` turns a refusal into a degraded note.

`sindri context map checks [--json] [--draft | --build]` (human-only: it lists feature keys; `POST /sindri/context/map/checks`) prints each measure with both limits and the verdict, then the five largest offenders. `--build` extracts in memory with no model call, saves nothing and holds a heavy-job slot; it is how to see a refused build. Exit 0 all pass, 1 degraded (or a would-be refusal under `warn`), 2 usage or no map, 3 would be refused. The seam for the home-feature and roll-up steps is `BuildMapService.WithStructure`: a build and `checks --build` both read through `ReadRepos`, so the checks measure what the build would save.

## Pages and actions: the `expresses` edge (pages spec §3.2, P1a)

- `EdgeType.Expresses` joins an action or a surface to a concept (`EdgeRules`), many per source; a feature's single home stays `belongs-to`.
- **Suggested edges.** `ConceptPass` ends with `PageExpresses.Apply`: each action, and each surface that `PageKinds.IsPage` accepts, gets `expresses concept:<id>` (`Provenance.Deterministic`) for the concepts its implementing code files' home features `belong-to`; at most 3 per source, most implementing files first, then by id. A pin or a derived concept counts the same.
- **What is a page.** `PageKinds.IsPage(node, edges)`: a `screen` or `dialog` surface always; a `route` only when a `.tsx` or `.jsx` file implements it; never a `pane`, `api`, `command`, `keybinding`, `setting`, `menu`, `job`, `notification`, `permission`, `report` or `flag`. `PageNames.Offer` gives the name offered for one (`GET:/schedule/week` and `ScheduleWeekPage` both give `Schedule week`).
- **Schema version 3, no rollback.** An exported map holding an `expresses` edge is written as `schemaVersion: 3` (else 2 for a `tags` edge or a roll-up count, else 1). A binary from before this change refuses it by version, and its store can't parse the edge type at all (`StrictEnumConverter`): no rollback past P1a.
- **One rebuild.** `MapRuleset.RulesVersion` is `"2"`, so every box rebuilds its map once after the upgrade (0 model calls).

## Concept pins on the query: `concept=` (concept pins spec §3.2, Q2a)

`concept=` (query string; `--concept ID` on `sindri context query`; the string `concept` on MCP `context_query`; `ContextQuery.Concept`, an `init` property left out of JSON when null) names a pin: its id, `concept:<id>`, or (with Q4) one of its aliases. It combines with every shape and with `--repo`. A blank value is `SND-CONTEXT-006`.

- **Ids.** An unknown pin is `SND-CONTEXT-102` (at most 5 visible pins named), a profile with no pins `103`, a pin the map has no `concept:<id>` node for yet `104`, `concept=` with no readable map `105`. All four exit 1 like an unknown anchor (`ContextErrors.Attention`). `PinContext.Of` resolves them once per query; `QueryPipeline.Pinned` reads `inputs.Concept ?? request.Query.Concept`.
- **Notes.** `ContextResult.Notes` (left out of JSON when empty; the text form prints each as a `NOTE` line) holds, built by `PinNotes`: `scoped to concept billing (map v41)` first; `anchor concept:auth is outside concept billing, so its lens shows only billing's part` for each anchor sitting in another pin (the filter wins, no error); for the diff shape, `concept billing: 4 of 9 changed files are outside it`, and those files' nodes leave the anchor set. Without `concept=`, a text question that anchored on a visible pin's concept node says `anchored on concept:billing; add concept=billing to see only billing`.
- **Alias line.** An anchor reached through a pin's alias adds `anchored on concept:billing (alias "payments")` (`AnchorMatch.AliasLine`, the alias fenced like a label); with no `concept=` the hint follows it: `anchored on concept:billing (alias "payments"); add concept=billing to see only billing`.
- **Notes from the legs (Q2b).** A source that can't filter by pin is left out and said so: `<source>: left out under concept= (it can't be scoped to a pin)`. An empty answer under `concept=` says where the results read were (`concept billing: no results. Of the results read, 7 were in auth and 3 in no pin (fix: concept=auth, or drop concept=)`, or `... no results, and none outside it either`, or `concept billing matches no file in this map (check the paths of pin 'billing' in index.concepts)`). `AnswerNotes` joins them after the pin notes.

## Pin aliases link records (concept pins spec 3.4, 3.7, 3.8; Q9)

A concept pin's `aliases` are words the owner uses for it. The record linker turns them into links.

- **The rule (`alias`).** An alias is one word or more. The alias and the record's text are both cut into words by the linker's word pattern, lower-cased, made singular/plural-insensitive and stripped of the small words `AnchorMatch` already drops, and an alias matches when its words sit next to each other, in order, in the text's words (so `pay run` matches "the Pay Runs page"). One word is enough; there is no two-word floor. It runs after the name rule, on the same masked text (URLs, paths and quoted titles are left out). Only pins whose `concept:<id>` node is in the map are offered. A match links the record to `concept:<id>` with rule `alias`, confidence 0.8 and provenance `deterministic`; the span hash is the alias as the profile wrote it. When two pins' aliases both match one record, both link (the profile's clash rule makes equal aliases on two pins impossible).
- **`RelinkPinAliases` (job kind, no route, no verb).** `IContextEngine.RelinkPinAliases` reads the current map, the approved pins and every stored record's text, runs the alias rule per record and replaces a record's `alias` links only where they differ from the stored ones. It never touches another rule's links, calls no model, makes no embedding and marks nothing linked. A relink with no pins still runs and removes stale alias links.
- **When it is queued.** The `context-map-refresh` tick queues it as `service:context-map-refresh`, as its last step, only when the earlier steps found nothing to do: a current map exists, no `RefreshContextMap`, `PublishContextMap` or `RelinkPinAliases` job runs, and the pair (`PinAliasSet.Hash` of the approved scope's pins, the current map version) differs from the pair in the result of the newest succeeded relink, or none succeeded yet. So an alias-only profile edit queues a relink and no refresh, and a newly promoted map gets one. After failed relinks the tick waits 5, 10, 20, 40, then 60 minutes, as it does for refreshes. The service has no state row of its own; the newest succeeded job's result is the record of the pair.
- **The result** (the job's finish payload): `aliasesHash`, `mapVersion`, `records` (scanned), `changed` (records whose alias links changed), `elapsedMs`, and `pins: [{id, before, after}]`, where `before` and `after` count the distinct records linked to `concept:<id>` by any rule before and after the run (the pinned card's "+3 more records linked here" is `after - before`). Its progress line is "linking records to its words".
- **No rollback.** Migration `0027_s9_intent_link_alias` widens the intent link table's rule check to allow `alias`. After it has run, an older binary cannot read a store that holds alias links.
