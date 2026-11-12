# Sindri's Context page: ask a question, get the files to touch

Status: draft (ideas lane, 2026-11-12). Serves #2959 (context map direction) and uses the row / peek / full grammar of #2978.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

## 0. Why

The owner's words: "the visual experience is ass. The agent driven experience is good enough but not great. Imagine having to scope out a
project in a 10 million line repo, it gets really really hard to find what files to use." LSP navigation and types can't be relied on; Effect
TypeScript is the named case.

What the Context page does on main today (driven 2026-11-12 on a real index of this repo, screenshots in §13):

- It opens on a grid of equal concept cards (#2976). A question only narrows that grid; it never returns files, tests or decisions (#2974).
- The concept map draws six colours and cuts its labels (#2813, #2809). Clicking a concept doesn't open the three-band view (#1281).
- Near-duplicate suggested concepts show as separate cards with the same terms (#2975).
- While the first index and map build run, the page says "No map yet" with a Build map button and no progress; the Files tab shows an
  error row with a CLI fix (#2766, and the new first-run issue in §12).
- Under the page, the engine misses the edges that matter in Effect code: 32% of TS import edges stop at a barrel `index.ts`, so 43% of the
  files that `yield* SqlClient` have no edge to `SqlClient.ts` (#2983). graphify takes 3 min and 2.76 GB on 1.1M lines and runs twice per
  commit; at 10M lines that is ~25 GB (#2984).

The job, from #2959: from a plain question ("add a refund reason to orders") to **the set of files to touch, with why, in under a minute**,
in a repo of 10M lines. Everything on this page serves that job or goes.

## 1. The bar

The research is the prior-art ledger, `docs/sindri/prior-art/context-map.md` (#3009). "Ledger N" below is its problem N; each row
there has the links, the sources and the numbers. This section only says which rows this spec builds and where.

| Step of the scoping loop | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Type a feature word, get matching files | 1 | Sourcegraph on Zoekt (trigram index) | a lexical leg that answers in < 300 ms on 10M lines | §4.1 |
| Rank definitions above mentions | 2 | aider repo map (def/ref tags + personalized PageRank, cut to a budget) | the ranking model | §4.2 |
| Find a concept by meaning | 3 | Cursor, Augment (chunk embeddings) | the semantic leg, optional (embeddings may be off) | §4.1 |
| Callers and callees without an LSP | 4 | scip-typescript; tree-sitter name resolution | syntactic edges first, SCIP opt-in and benchmarked | §5.1 |
| What a service needs and provides | 5 | `@effect/language-service`; effect-analyzer (MIT) | the same facts from syntax; effect-analyzer as the test oracle | §5.1 |
| What breaks if I change this | 6 | Nx affected `--explain`, dependency-cruiser `reaches` | the "why" line on each row | §3, §6.1 |
| Find the sibling feature to copy | 7 | **nobody** | ours | §3 (read rows) |
| Save and share the scope | 9 | **nobody** | the scope is a saved object | §3 |
| Hand the scope to an agent in a budget | 10 | aider, repomix | `context_scope` with a token budget | §8 |
| Keep it fresh per commit | 11 | aider mtime cache | one incremental extract | §5.2 |
| Structure without a hairball | 8, 12 | Nx graph, dependency-cruiser collapse | the neighbourhood (<= 40 nodes) and the treemap | §6.3, §6.4 |
| Decisions, tests and docs beside the files | 13 | **nobody** | test rows; decisions and docs in peek | §3, §6.2 |

What not to build on (ledger, "What not to build on"): stack-graphs is archived (2025-09-09); Serena and Sourcetrail are GPL, so ideas only.

Two lessons on pictures: whole-repo call graphs fail as a working surface (dense clusters, overlapping labels, layouts that choke on cycles),
and CodeSee, the product built on the visual map, shut down. Ranked lists with one line of why, plus a small subgraph around one seed, are
what the working tools hand to people and models.

**The bar in one line:** a question returns a ranked, grouped list of files in under 5 s (p95, 10M lines), each with the edge that put it
there and the Effect services it provides and needs; one key opens a small neighbourhood around any row; the list is saved as a scope an agent
can open.

## 2. Decisions

1. **The scope is the object; the map is a view of it.** The page opens on the question box. The answer is a scope (§3). The concept map stays
   as an orientation view, never the landing.
2. **Rows, not cards.** The scope is a list in the #2978 row grammar (32 px rows, Space peeks, Enter opens). Cards go.
3. **Never draw more than the screen can label.** The neighbourhood view (§6.3) shows at most 40 nodes, every label whole. The whole repo is
   a treemap for orientation (§6.4), never a node-link graph.
4. **Edges come from syntax, with no new parser in ship 1.** Each edge kind is a pass with a fixture and an answer key (§5.1). TS passes
   build on the import scanner that ships today (`TsExports.cs`, `TsWorkspace.cs`); C# passes build on CodeIndex's Roslyn parser.
   tree-sitter is not in the repo. It is a later decision of its own, behind a spike (platforms, NativeAOT, locked restore) that has to
   beat the pattern passes on the benchmark. An LSP or SCIP index, when there is one, only adds precision; nothing waits on it.
5. **One extract per commit, owned by CodeIndex.** The code index and the map read the same extract, stored in SQLite, updated per changed
   file (§5.2, #2984).
6. **Measured.** Every index or ranking change reports its delta on the scoping benchmark the context lane owns (§7). Answer keys live on real
   repos (Effect-TS/effect at 1.1M lines, this repo); the 10M copy is for budgets only; precision is reported next to recall (#2959 comments).
7. **Same object for people and agents.** `context_query` stays as it is. `sindri context scope ask` and the MCP `context_scope_*` tools
   answer, read and edit scopes. The app opens an agent's scope by id, and an agent opens the owner's (§8).
8. **The page ships behind the floor.** The new page becomes the Context page only when the benchmark meets the §7 floor. Until then,
   the card landing stays the default, and the new page is behind the profile key `context.landing: scope` (default `cards`). Phase R1 (§14) deletes the
   cards once the floor holds and Q1 passes. The bad case this stops: a ranker that misses the files to edit replaces a page that at
   least lets you browse.

## 3. The scope

A scope is a named, saved answer to one question.

| Field | What it holds |
|---|---|
| `id` | `scp_<ulid>` |
| `name` | empty until someone keeps the scope (`s`, `rename`); unique per repo |
| `version` | starts at 1, +1 per edit; every write names the version it read (§8, code 264) |
| `question` | the words the person or agent typed, run through `Aw.Redaction` before the ledger event |
| `repo`, `commit`, `extractorVersion` | the repo, the indexed commit it was answered at, and the extract's version |
| `who`, `parent` | who asked (the owner or a session); the scope it was copied from, when it is a copy |
| `groups[]` | one per concept or area: `name`, `why` (one line), `rows[]` |
| `rows[]` | one per file: `path`, `fileHash` (the extract's hash of the file at `commit`), `role` (`touch` / `read` / `test`), `why` (the edge or evidence that put it there, one line), `score`, `services` (provides / needs, Effect), `pinned`, `dropped` |
| `edits[]` | what the person or agent changed: pin, drop, move to touch/read, "more like this", "what calls this", in order, with who |
| `searched[]`, `degraded[]` | the legs that ran, and what was missing: `meaning search off`, `edges building 41%`, `3 commits behind` |
| `asOf` | when it was answered; a row is `stale` when the file's hash at the indexed commit differs from `fileHash` |

Roles: **touch** = the change edits it; **read** = needed to understand it (callers, the service it uses, the schema); **test** = the tests
that cover a touch row. The answer-key files of the benchmark are the touch rows.

Scopes are stored by Sindri (ledger events `scope.created`, `scope.edited`, `scope.kept`, `scope.handed`, `scope.deleted`; a view of the
latest state). They never write to the repo. The C# type is `FileScope`, because `ContextScope` (`Aw.Context.Core/Engine/ContextScope.cs`)
is already the caller's profile and `ScopeGraph` is the scoping graph. "Scope" stays the word on the page.

**Saved and kept.** Every answer is saved, so an agent's scope can be opened by id. A scope with no name is dropped 7 days after its
last edit (`context.scopes.keepDays`, default 7). Naming it (`s` on the page, `rename` for agents) keeps it until someone deletes it.
The saved menu lists named scopes first, then the last 10 unnamed ones; an agent's scopes carry the session's name.

**Copies.** A session's first edit of the owner's scope (or another session's) makes a copy: a new id, `parent` set to the original.
The edit's result returns the new id, and later edits use it. The original never changes behind its author's back.

The exact wire shape is S0's (§14): a JSON schema and an API golden, the first PR of P3. This section fixes the fields and the behaviour.

## 4. Answering a question

### 4.1 Legs

1. **Lexical:** trigram search over paths, symbol names and text. Seeds: the question's words, their identifier forms (`refundReason`,
   `refund_reason`, `RefundReason`), and the concept aliases from the map. The index is an FTS5 table with `tokenize='trigram'` and
   `detail=none`, in the extract's SQLite file (§5.2). `detail=none` gives up phrase queries; seeds are single words, so nothing is lost.
   The extract keeps each file's scrubbed text, and the FTS5 table indexes it as external content, so peek and full read the same text.
   A changed file is a delete plus an insert. Size: ~400 MB of text at 10M lines, plus the trigram index at about 1-3x the text
   (~0.4-1.2 GB). The benchmark reports the real size per fixture. It replaces the file and path rows of today's in-memory
   `SqliteMapSearchIndex`, which keeps concept titles only.
2. **Structural:** the seeds' definitions, then their edges (§5.1): who provides the service, who uses it, the layer that wires it, the
   tables a file reads and writes, the routes, the tests.
3. **Semantic (optional):** embeddings over chunks of one top-level declaration each, cut by the same passes that make the edges, when
   `index.embeddings` is on. Off, the scope says "meaning search off" in its footer and in `degraded[]`, not an error.

### 4.2 Ranking

Personalized PageRank over the file graph, seeded by the lexical and semantic hits (aider's method), with edge weights per kind (a provides
edge beats an import). Definitions beat mentions. A file reached only through a barrel counts as reaching the defining file (§5.1).
Grouping: rows group by concept when the map has one for them, else by top-level package. The touch / read split: a row is **touch** when the
question's nouns hit its definitions (it defines the entity, the schema field, the route, the service); **read** when it is reached only by
edges; **test** when a test -> subject edge points at a touch row.

The file graph is held in memory as compressed rows (CSR: one offset array, one target array, one weight array), loaded once per
extract version and swapped when a new one lands. PageRank stops at 20 iterations or a change under 1e-4. The benchmark reports the
graph's node and edge counts and its memory at 1.1M and 10M lines; above 1.5 GB it is a perf issue (F1).

### 4.3 Budgets (10M-line copy, the box this runs on)

| Step | Budget |
|---|---|
| first build: new repo -> questions work (lexical ready) | < 3 min at 1.1M lines, < 10 min at 10M |
| question -> first rows on screen | p95 < 1 s (lexical leg alone) |
| question -> full ranked scope | p95 < 5 s |
| "more like this", "what calls this" on a row | p95 < 1 s |
| page first frame with a saved scope | < 300 ms |
| recall of the answer-key files in the top 25 | >= 0.8, precision reported next to it |

## 5. Engine requirements (context lane)

### 5.1 Edges from syntax

Each is a pass with a fixture repo, an answer key and a test. Order is the context lane's plan on #2959. The parser per pass, in ship 1:

| Pass | Parser |
|---|---|
| 0, 1, 5 (resolver, barrels, tests) | the TS import scanner that ships today (`TsExports.cs`, `TsWorkspace.cs`) |
| 2, 3, 4 (Effect services, schemas, SQL and routes) | pattern passes over the same scanner's tokens: each pattern below is a call shape, not a type fact |
| 6 (C#) | CodeIndex's Roslyn parser (`CSharpParser`) |

A pattern pass is wrong where a type checker is needed (a service passed through a variable). Each pass reports its precision and recall
on its answer key, so a tree-sitter or TS compiler spike has a number to beat. This spec requires:

0. **Fix the TS resolver first** (context's correction on #2983: graphify is not the cause of the missing edges). Two small Core fixes
   come before any new pass:
   - `export * as X from "./y"` is dropped today: `TsExports.cs:36` filters out every star export with a namespace group. It becomes a
     namespace re-export, so `X.Foo` resolves to the file that defines `Foo`.
   - `TsWorkspace.cs:29` turns a package.json `exports` map into one entry file: `Target` follows the first condition key it knows and
     never keeps the subpath keys. Subpath entries (`"./sql"`, `"./*"`) become their own entries, so `@pkg/x/sql` lands on a file.
   scip-typescript stays opt-in and benchmarked (ledger 4); it is not on the default path.
1. **Barrels collapse.** An import of `x/index.ts` that re-exports `Foo` becomes an edge to the file that defines `Foo` (#2983). Done first:
   every edge kind below depends on it. The walk follows at most 8 re-export hops, keeps a visited set (a cycle ends the walk with the
   last file reached), and caches each file's resolved exports per extract version. `export * as X` from item 0 goes through the same walk.
2. **Effect services.** `Context.Tag("id")`, `Context.GenericTag`, `Effect.Service<X>()("id", {...})` (and v4's `ServiceMap.Service`) ->
   a service node; `dependencies: [...]` -> needs edges; `Layer.succeed/effect/scoped(X, ...)` -> provides; `Layer.provide/merge/provideMerge`
   -> wiring; `yield* X` inside `Effect.gen`, `X.pipe(...)`, `Effect.flatMap(X, ...)` -> uses.
3. **Schemas and errors.** `Schema.Struct/Class/TaggedError`, `Data.TaggedError` -> schema and error nodes, so "add a field to orders"
   finds the schema.
4. **SQL and routes.** `sql` template reads and writes -> table edges; `HttpRouter` / `HttpApiEndpoint` -> route nodes.
5. **Tests.** A test file -> the subject files it imports (after barrel collapse).
6. **C#** keeps its Roslyn parser. DI registrations (`AddScoped<IX, X>()` and friends) are the C# equivalent of 2, and endpoint maps
   (`MapGet`, `MapPost`) are the equivalent of 4. They are in ship 1, as P1's second wave. This repo is a benchmark fixture and mostly
   C#, so the floor (§7) can't be met without them.

### 5.2 One extract, incremental, in SQLite

One extract per commit, shared by the code index and the map (today they run graphify twice, #2984). It is stored in SQLite and updated per
changed file, so a commit that touches 20 files re-parses 20 files. A full build is only for a new repo or a new `extractorVersion`.
Budget: full build of 10M lines < 30 min and < 4 GB peak; a 20-file commit < 30 s.

- **Owner.** `Aw.CodeIndex` owns the extract file and is its only writer. It holds files (path, content hash), symbols, edges and the
  lexical table (§4.1). `Aw.Context` reads it through a port declared in `Aw.Context.Core` and bound by the Sindri host, the pattern the
  vectors use. Context never opens the file itself, so `deps.yaml` gains no project edge between the two; `node scripts/check-deps.mjs`
  must still pass.
- **Order of a first build.** Files and the lexical table are written first and committed as one milestone: questions work from then on
  (§4.3, §6.5 row 2). Symbols and edges follow, file by file.
- **Readers never see half a build.** A full build writes a new file and swaps it in, so readers see the old extract or the new one.
  Per-file updates run in WAL mode, and each question reads in one read transaction, so a question sees one commit's graph.
- **What goes away.** graphify no longer extracts files or edges. It stays for concept clusters only (the map's names), runs once per
  commit off the question path, and reads the extract. The file and path rows of `SqliteMapSearchIndex` go (§4.1). `context.db` keeps
  the map: concepts, pins, pages.
- **Secret files never enter it.** The extract takes CodeIndex's input rules as they are: `.gitignore`, `InputRules.SecretGlobs` and the
  profile's added globs (`InputRules.IsDenied`). Every file body goes through `IBodyScrubber` before the lexical insert. Peek, full and
  `handoff` read text only from the extract, never straight from disk, so a path outside the repo root or a denied file has no text to
  show.
- **Upgrade.** A box with an `index is ok` map keeps answering from it until the first extract finishes. `sindri doctor` shows
  "extract building" until then.

## 6. The page

### 6.1 Layout

Design's pick on #2959 (comment 6817891316) is direction 3, **map-led bands**, with two pieces taken from the other directions. qa agreed
and measured it (comment 6818215657). The reference frame, drawn on the real `Tokens.axaml` values at 1440x900:

![direction 3, map-led bands](https://github.com/jthorare/agentic-workflow/blob/9a2989fc7042f8eb373d3ce14be826f493deb809/map-variant-3.png?raw=true)

```
┌ Context ──────────────────────────────────────────────── 48,112 files · answered in 2.1 s ┐
│ [ Add per-workspace rate limits to the export API                          Ask again ⏎ ] │
│ Rate limits on exports  scp_01JC…  6 touch · 8 read · 5 tests · 2 decisions, 1 conflicts  [Hand to agent] │
├ Scope, ranked ─────┬ Entry points ──────┬ Core ───────────────┬ Data and effects ────────┤
│ [filter      /]    │ ▾ Export API 3     │ ▾ Rate limiting 2   │ ▾ Quota storage 2        │
│ ▾ Export API     3 │  ExportRoutes.cs   │ ┃RateLimiter.cs ┃───┼─reads IQuotaStore.get──▶ │
│  ExportHandler.cs  ├─calls RateLimiter.check─▶              │  QuotaStore.cs           │
│ ▾ Rate limiting  2 │  ExportJobRunner   │  WorkspaceQuota…    │  table workspace_quotas  │
│ ┃RateLimiter.cs   ┃│ ▾ Request pipeline │ ▾ Export request 2  │ ▾ Effects 1              │
│ ┃ Touches · Why · ┃│  RateLimitMiddle…──┼─provides RateLimiter layer─▶  event quota.exceeded │
│ ┃ actions         ┃│                    │ ▸ Billing plans 4   │ ▸ Tests 5                │
│ ...                │                    │                     │                          │
│ Suggested, not in scope yet: PlanLimits.cs [Add] · ...         │ Whole repo  3 of 31 lit  │
└────────────────────┴────────────────────┴─────────────────────┴──────────────────────────┘
```

- **The question box** is the full width of the page and has focus when the page opens. Its placeholder is whole at 1000 px. A quiet
  line under it names the repo it asks about: the top bar's repo.
- **The scope header**: the scope's name and id (`scp_…`, Copy id beside it), its counts ("6 touch · 8 read · 5 tests · 2 decisions,
  1 conflicts"), and Hand to agent. Saved scopes are a menu on the name (named first, then recent; empty: "No saved scopes yet").
- **The ranked rail** (left, 280 px) is the scope as a list: groups that fold, one file per row. A row's columns, left to right: the role
  glyph (`T` touch, `R` read, `✓` test, in ink, never colour alone), the file name, the score. The why is not a column: 280 px can't
  hold a name and a why on one line without cutting one (§6.7). The selected row shows its why in the in-place open, and Space shows it
  in peek. Inside a group, touch rows come first. There is no role filter chip in ship 1; `/` filters by name. Score is the rank normalised to 0-1 for this scope; a solid dot means an edge put the file there, a hollow dot means only the
  lexical or semantic leg did. There is no other badge colour: the frame's green "Confirmed link" becomes the word, in ink. This is aider's
  repo map (ledger problem 2 of `docs/sindri/prior-art/context-map.md`): rank by graph, then one line of reason.
- **The bands** (centre and right) draw the same files as cards in three columns: Entry points -> Core -> Data and effects. A file's band
  comes from its strongest node kind: route, job, middleware or CLI command -> Entry points; service, layer, domain type -> Core; table,
  schema, event, migration -> Data and effects; tests fold into one Tests card there. Anything else goes to Core. The bands draw at most
  6 cards each: touch cards first, then by score. More fold into "+ N more". Groups fold to one card ("Billing plans · 4 files"). Every edge carries its verb ("calls RateLimiter.check", "reads IQuotaStore.get").
- **The in-place open** (taken from direction 1): the selected rail row grows in place to show *Touches*, *Why here*, the decisions that
  cite the file (with a conflict marked as one, "D-37 conflicts with D-41") and its actions.
  It replaces direction 3's bottom strip, which pulled the eye away from the row you are on.
- **The Suggested tray** (taken from direction 2): "Suggested, not in scope yet", up to 4 files with Add. A scope grows without a new
  question.
- **Whole repo** (bottom right, 200 px): the treemap of §6.4, with the scope's areas lit. Orientation only.
- The key bar lists only the keys that work on the selected row.

**What the redraw must fix** (qa, each measured on the frame above):
1. **Every drawn edge has a verb, or it isn't drawn.** Two grey curves have none today: ExportHandler -> ExportRequest.cs (under the
   `calls RateLimiter.check` pill) and WorkspaceQuotaPolicy.cs -> `table workspace_quotas`. A grey line with no label is exactly what
   fails on today's map (#2959, "what fails" frame 3).
2. **Edges are routed orthogonally, one row gap each.** The `provides RateLimiter layer` edge crosses ExportHandler -> ExportRequest at
   x ~640. Temporal's and n8n's canvases route this way.
3. **Group counts use ink-subtle** (138,143,152: 5.62:1 on the rail, 6.13:1 on the canvas), not ink-3 (4.12:1).
4. **The selected row's second line stays at or above 4.5:1 on the selected fill** (it is 4.64:1 today), held by a check in the Frames
   suite, not by eye.
5. **Rail rows are 32 px and one line** (the #2978 grammar), not 38 px and two. The why leaves the row (it shows in the in-place open
   and on Space; qa's "why column" doesn't fit 280 px, see the rail above). The frame's 10 open rows, 7 headers and one in-place open
   then fit the rail's 640 px at 1440x900, with no scroll (10 x 32 + 7 x 23 = 481 px, plus the open row).

The redraw also brings the frame in line with this text where qa's list doesn't name it: the role glyph column, the "Confirmed link"
pill as a word in ink, the id as `scp_…`, the header counts above, and the key bar of §6.6. The frame's "Saved" label and ticket line go.

**One cursor, owned by the rail.** The 14 files show twice: in the rail and as band cards. There is one selection, and the rail owns it.
The bad case this stops: a band card highlighted while the rail shows another row, so Enter opens a file the person didn't mean.

| Click on | Cursor goes to | Focus |
|---|---|---|
| a file card | that file's rail row (its group opens) | the rail, so `j` / `k` work next |
| a group card, "+ N more" or the Tests card | that group's header row, opened | the rail |
| a rail row | that row | the rail |

The card of the selected row is the one accent, with its edges. When the selected file is inside a folded group card or past the cap of
6, the folding card carries the accent. Space peeks from the rail; a band card has no keyboard focus of its own (the rail is the keyboard
path).

### 6.2 Peek (Space) and full (Enter)

- **Peek**, a side pane at 40% width: the file's outline (top-level symbols), the lines that matched with two lines of context, its edges
  in and out grouped by kind (each one a link that adds that file to the scope as read), the decisions and docs that cite it, and the why.
  While it is open, the bands lay out again in the 60% left over: narrower cards, same order, every edge label whole. They never scroll
  sideways, and the peek never covers the selected card.
- **Full**, Enter: the file in a read-only code view at the matched line, with the same edges in a gutter. "Open in editor" is one key (`o`).
  It runs the profile's `context.editor` as an argv list, with the path as its own last argument after `--`, never through a shell.

### 6.3 Neighbourhood (n)

A full view opened from a row (the #2978 grammar: row, peek, full; meta's call). It is a node-link view of one file and what touches it, one hop out, two on request: at most 40 nodes, every label whole, laid out in the three
bands entry points -> core -> data / effects. Services are nodes; every edge carries its verb and is routed orthogonally, as in the bands (§6.1, fixes 1 and 2). One accent marks the
seed and the rows already in the scope. When more than 40 nodes are in reach, a line says "Showing 40 of 112 nearest · narrow by kind",
with kind chips. Arrows walk edges; Enter adds the focused node to the scope; Esc goes back to the list with the same row selected.

### 6.4 Orientation (the whole repo)

A treemap of the repo by top-level package and folder, area = lines, with the scope's files lit in the accent. It answers "where in the repo
is this", not "what calls what". It replaces the concept-card landing (#2976) as the view when no question is open: the treemap plus the
saved scopes plus the question box.

### 6.5 States

| State | What shows |
|---|---|
| first index building, lexical not ready | the question box disabled with "Indexing agentic-workflow: 41% · about 3 min left"; the treemap fills in as files land; no Build map button while a build runs |
| lexical ready, edges building | questions work; the header says "Answers get better as indexing finishes · 41%"; rows have hollow dots until edges land |
| index ready, no map | questions work (lexical + structural need no map); groups fall back to packages; a quiet line offers "Name areas" (the map) |
| a question with no hits | "Nothing in agentic-workflow matches 'refund reason'" plus up to 3 near words that do match, as plain words, never raw keys (#2809) |
| answer slower than 1 s | the lexical rows show first with "ranking…" in the header; the order settles once, no reshuffle after that; the cursor stays on the same file, not the same row index |
| index behind the checkout | a line in the header: "answered at 7f01aca, 3 commits behind" with Refresh |
| a failed read | "Couldn't read the code index" plus Retry and the log path; never an empty list that looks like "no matches" |

### 6.6 Keyboard and focus

The keys follow the shared grammar on #2978, in short: a list of 32 px rows; Space opens a peek pane beside the list; Enter opens the
full view; Esc goes back one layer. Its keys: `j`/`k` move, `/` filters, `c` creates, `.` opens the row's actions, Ctrl+K has every action
by name. `e`, `m`, `w`, `n` and `s` are this page's own; the ui lane adds them to #2978's table of page keys in U1.

| Key | Where | Does |
|---|---|---|
| (typing) | question box | edits the question; Enter asks and moves focus to the first rail row |
| Down | question box | moves focus to the rail without asking |
| Esc | question box | unchanged text: moves focus to the rail; changed text: puts the asked question back |
| Esc | rail, nothing open | moves focus to the question box |
| `c` | rail | create: a new question, with focus in an empty question box |
| `e` | rail | edit the current question (focus in the box, text kept) |
| `/` | rail | filter the scope's rows by name |
| `j` / `k`, arrows | rail | move the cursor; `h` / `l` fold and open groups |
| Space / Enter | rail | peek / open full, keeping the selection |
| Esc | peek, full, neighbourhood | closes the topmost one, back to the rail on the same row |
| `.` | rail | the row's actions: pin, drop, set touch / read |
| `m` | rail | more like this |
| `w` | rail | what calls this ("who uses it") |
| `n` | rail | the neighbourhood (§6.3) |
| `s` | rail | keep and rename the scope |

Letters act only while focus is in the rail; in the question box they type. Tab order: question box, rail, Suggested tray, header
(saved menu, Copy id, Hand to agent). Every action has a menu item for mouse use and the same action id in Ctrl+K and in
`context_scope_edit` (§8): `scope.pin`, `scope.drop`, `scope.touch`, `scope.read`, `scope.more`, `scope.callers`, `scope.add-suggested`,
`scope.keep`, `scope.copy-id`, `scope.handoff`. The frame's key bar still shows the old `p` / `x` / Space = expand group: the
redraw takes the table above.

Screen readers: the rail is a tree (group -> rows); a row reads "touch, ExportHandler.cs, calls RateLimiter.check, score 0.96". The bands
and the treemap repeat the rail and are hidden from the accessibility tree. "ranking…" and "answered at …" are a polite live region.

### 6.7 Visual bar

One accent (the selected file and its edges, lit treemap cells). Every text meets 4.5:1 on its own fill and every meaningful line or mark
3:1, held by a contrast check in the Frames suite over every rail row, band card and edge label (§6.1 fixes 3 and 4 among them). Ink for structure. No band or concept colours (#2813). Labels never cut (#2809). Type
scale and spacing from the tokens. Inspora is the reference (#2959 point 6).

## 7. The benchmark (context lane; P0 merged in #3068)

The answer keys are `tests/fixtures/scoping/<repo>/questions.yaml` (`effect` and `agentic-workflow` today), and their README defines
the fields. Each file names its `repo`, `commit` and `source`. Each question has a `plain` text (what a person types, no file names), a
precise `question`, and `touch`, `read` and `decoys` lists of `{path, why}`. A decoy is a file whose words match but which a right change
leaves alone, so it measures precision. For a "where is X" question, touch is the files that do X. One entry, cut short:

```yaml
- id: aw-01
  plain: "where is the heavy job slot lock taken and released"
  issue: 3047
  split: held-out          # tune | held-out; P0 follow-up, ranking changes are tuned on `tune` only
  touch:
    - { path: scripts/heavy.sh, why: "takes a slot in its wait loop and frees it in release() on the EXIT trap" }
    - { path: config/lib/locks.sh, why: "acquire_any_lock takes slot 1 with mkdir; release_lock removes it" }
  decoys:
    - { path: skills/_shared/skill-lock.sh, why: "a different lock, one skill at a time" }
```

The runner is `node scripts/scoping-bench.mjs --questions <file> [--k 25] [--json]`. It asks a running Sindri `sindri context query
--json` per question and prints touch recall, recall and P@25, decoys hit, and p50 / p95. Exit 0 means every question was answered,
1 a query failed, 2 usage. `--check` verifies every key path exists at the key's commit. It runs by hand, by the context lane or the
owner, never on CI, because the Effect fixture needs a clone of about 3 GB and an index. Results go on #2959 with the run's setup
(embeddings on or off, map version, sha).

P0 follow-ups the floor needs:

1. `split:` on every entry, then `--split held-out` and `--floor 0.6` on the runner (exit 1 below the floor).
2. 10 questions per fixture, half `tune` and half `held-out`. #3047's question is in already (aw-01).
3. The extract's size, the graph's node and edge counts and its memory in `--json` (§4.1, §4.2).
4. A 10M-line copy, for budgets only: its repos, licences, disk and build time listed in `tests/fixtures/scoping/README.md`.

**Floor for decision 8:** touch recall at 25 >= 0.6, averaged over the held-out questions of both fixtures together (10 questions
to start). Before R1 deletes the cards, held-out grows to 10 per fixture (20 total), and the floor must still hold. The target stays at
0.8 (§4.3). One miss on 5 questions moves a fixture by 0.2; averaging over both fixtures and growing to 20 keeps a single question from
deciding the ship.

A scope whose edits changed its touch set is offered as a new benchmark question in `sindri doctor`; the owner accepts it into the
answer key by hand, so the benchmark grows from real use. `sindri doctor` also counts scopes made, rows dropped and scopes handed to
agents, so use shows next to the benchmark.

## 8. Agents

`context_query` is unchanged. Scopes have their own verbs: one MCP tool per op, `context_scope_<op>` (the `workflow_*` pattern), and
`sindri context scope <op>` on the CLI.

| Op | Arguments | Does | Exit |
|---|---|---|---|
| `ask` | `<question> [--repo R] [--json]` | answers and saves a scope, `who` = the caller | 0 ok, 1 attention (index not ready, not running, empty question SND-CONTEXT-006 as on main), 2 invalid |
| `show` | `<id or name> [--json]` | one scope, with its `version` | 0, 1 not found or not running, 2 invalid |
| `list` | `[--repo R] [--mine] [--max N] [--json]` | saved scopes, named first, then newest (default 20) | 0, 1 not running, 2 invalid |
| `edit` | `<id> --op pin\|drop\|touch\|read\|more\|callers --path P --version N [--json]` | one edit; the ops are the §6.6 action ids; on another's scope it returns the copy's new id (§3) | 0, 1 version conflict, stale row or not running, 2 invalid |
| `refresh` | `<id> --version N [--json]` | asks again at the indexed commit, keeping pins and drops; a pinned file that is gone becomes a `stale` row with "file gone" | 0, 1 attention, 2 invalid |
| `handoff` | `<id> [--max-tokens N] [--format md\|json] [--to <session>]` | the block for an agent; with `--to`, records `scope.handed` | 0, 1 not found or session gone, 2 invalid |
| `rename` | `<id> --name T --version N` | names it, which keeps it (§3) | 0, 1 name taken or version conflict, 2 invalid |
| `delete` | `<id> --version N` | deletes it | 0, 1 not found or version conflict, 2 invalid |

- Ids print as `scp_` plus the ulid; `show` also takes the name. `--json` carries keys, text carries labels (#2809).
- Every answer carries `searched[]` and `degraded[]` (§3), so an agent sees "meaning search off" or "3 commits behind" as data.
- A session reads and edits scopes of the repos its `X-Sindri-Repo` header names, and no other repo's. A session's edit of a scope it
  didn't make edits a copy (§3).
- Repo text in every op's result (matched lines, why lines, the question, names) is fenced as untrusted, like every other Sindri MCP
  result: `ask`, `show`, `list`, `refresh` and `handoff`, over MCP and in `--json`.
- `handoff` returns the question, then the touch / read / test files with their why, cut to `--max-tokens` (default 6000, counted with
  the same tokenizer as `context_query`'s budget).
- The app's Hand to agent opens a small menu (new session, or one of the repo's running sessions), shows "about 6k tokens, 14 files",
  and leaves a row "Handed to <session>" with a link. With no running session, the menu offers a new one only. A Work item's "Scope this"
  (#2961 point 5) runs `ask` on the item's title and links the scope to the item (`ask --item <id>`).
- P3 moves the skills that call `context_query` to find files over to `context_scope_ask` (a skills PR), so agents see scopes from day one.

## 9. Errors

Reserved now: SND-CONTEXT-261 to 279 (250-260 are taken; 261-279 are free on main).

| Code | Meaning | HTTP | CLI exit |
|---|---|---|---|
| 261 | scope not found | 404 | 1 |
| 262 | scope for a repo the session's header doesn't name, or not in the profile | 403 | 2 |
| 263 | index not ready; the error carries `percent` and `etaSeconds` | 409 | 1 |
| 264 | write names an old version; the error carries the current one | 409 | 1 |
| 265 | edit on a stale row (the file changed since the answer) | 409 | 1 |
| 266 | unknown edit op | 400 | 2 |
| 267 | `--max-tokens` too small for the question and one file | 400 | 2 |
| 268 | the scope's commit is no longer in the clone; `refresh` fixes it | 409 | 1 |
| 269 | the name is taken in this repo | 409 | 1 |
| 270 | `handoff --to` names a session that is gone | 404 | 1 |
| 271 | `edit --path` names a file that is not in the extract (denied, ignored or outside the repo) | 400 | 2 |

Each has a sentence for the app and a `fix:` for the CLI, written in S0. An empty or over-long question reuses SND-CONTEXT-006 at its
exit on main (1).

## 10. Goals and non-goals

Goals: the job in §0 inside the §4.3 budgets; the page in §6; the agent surface in §8.
Non-goals for this spec: precise (compiler-grade) TS types; cross-repo scopes; editing code from the page; the concept map's authoring
tools (pins, pages) beyond keeping them reachable from a menu.

## 11. What happens to today's views

| Today | After |
|---|---|
| concept card grid landing | stays the default until decision 8 holds; R1 then replaces it with the treemap + saved scopes + question box. Links to a concept open its band in the new page |
| concept map (three bands of concept cards) | the scope's bands of files (§6.1) on the page; one file's neighbourhood (§6.3) opened from a row |
| Files tab | the scope's rows, or all files in the treemap's selected folder when no question is open |
| Pages / pins / review panels | a menu on the page header (Map tools), unchanged |

## 12. Gaps found driving main (2026-11-12)

One first run on this repo (main 7f01aca3b8, `scripts/context-first-run.sh up --no-ui --no-embeddings`, 2026-11-12). Each gap is an
issue, `Part of #2959`:

| What happened | Number | Issue | Spec part that fixes it |
|---|---|---|---|
| The first index took ~18 min (graphify ran twice) | 1080 s to `index is ok` | #2984 | §5.2 one incremental extract |
| The page says "No map yet / Build map" while the first build runs, no progress | — | #3015 | §6.5 row 1 |
| After publish, the header says "v1" and the body still says "No map yet" | v1: 11,768 nodes, 236,033 edges | #3046 | §6.5 |
| "where is the heavy job slot lock taken and released" ranks C# interfaces; `scripts/heavy.sh` and `config/lib/locks.sh` never show, though both are nodes | 0 of 2 touch files | #3047 | §4.1, §4.2; a P0 answer key |
| repo-docs misses the default deadline on every query | 240 records found, 0 read | #3048 | §4.3 budgets |
| The missing-profile fix text has a typo | — | #3016 | — |
| The Files tab still shows its error row with a CLI fix | — | #2766 | §6.5 |

## 13. Proof

- The benchmark numbers before and after each phase, on #2959.
- Screenshots of every §6.5 state at 1280 and 1440 wide, plus the neighbourhood with 40 nodes, every label whole.
- A side-by-side with design's direction 3 frame (§6.1) at 1440x900: no unlabelled edge, no crossing, rail rows 32 px, and a click on a band card
  moving the rail cursor.
- A person (ux) and an agent each scope the held-out questions of both fixtures and get the touch files without a follow-up.

## 14. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Lane | Starts | Needs |
|---|---|---|---|---|
| P0 | benchmark + answer keys + runner (merged, #3068); then the §7 follow-ups: split, floor flag, 10 per fixture, sizes | context | now | — |
| P1 | wave 1: the two TS resolver fixes (§5.1 item 0), barrel collapse, the Effect services pass. Wave 2: C# DI registrations and endpoint maps (§5.1 item 6) | context | now (wave 2 on a second builder) | P0 for its delta |
| P2 | one extract per commit, owned by CodeIndex, incremental, SQLite, lexical table first (§5.2, #2984) | context | with P1 (third builder) | — |
| P3 | the scope: events, view, retention, `context_scope_*` MCP + `sindri context scope` CLI; `context_query` unchanged; skills move to `ask` | api (+ context for the ranker call) | now, against a stub ranker | the §3 fields |
| P4 | ranking + touch/read/test split | context | after P1 | P0, P1 |
| D1 | design: the contact sheet is done (direction 3 picked, #2959). Left: the redraw with qa's 5 fixes (§6.1), then peek, neighbourhood and the §6.5 states | design | now | #2978 row tokens |
| S0 | the wire shape as a JSON schema + API golden (scenario row + Ui GoldenContract slug), the ledger migration (aot-smoke `ledger schema N/N`), `scope.*` events (EventCatalogTests), PublicAPI files, `context_scope_*` in aot-smoke's tools/list, routes through the integration lane | api | first PR of P3 | §3, §8, §9 |
| U1 | the page behind `context.landing: scope`: question box, ranked rail with in-place open, bands, Suggested tray, one cursor, peek, keys, states (§6.1, §6.2, §6.5, §6.6) on a fake scope; the Frames contrast check (§6.7); the page keys added to #2978 | ui | after D1's redraw | the P3 wire shape |
| U2 | neighbourhood + treemap (§6.3, §6.4) | ui | after U1 | P1 edges for real data |
| F1 | perf: the §4.3 budgets on the 10M copy, one issue per broken budget | perf | after P2 + U1 | — |
| Q1 | ux + qa walk the held-out questions, person and agent | ux, qa | after P4 + U2 | — |
| R1 | the cards go: `context.landing` defaults to `scope`, then the card landing and its key are deleted | ui | after Q1, with the floor holding on 20 held-out | §7 floor |

The page becomes the default only in R1, on the §7 floor (decision 8). Each phase updates its docs: `docs/csharp/sindri-context.md`, `docs/sindri/context.md` and the CLAUDE.md Commands entries for new verbs.

P0, P1, P2, P3 and D1 start the same day, so five lanes are busy from day one. U1 builds on a fake scope from the P3 wire shape, so it
never waits on ranking.
