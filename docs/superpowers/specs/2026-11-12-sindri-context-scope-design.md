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
4. **Edges come from syntax.** Each edge kind is a tree-sitter pass with a fixture and an answer key (§5.1). An LSP or SCIP index, when there
   is one, only adds precision; nothing waits on it.
5. **One extract per commit.** The code index and the map read the same extract, stored in SQLite, updated per changed file (§5.2, #2984).
6. **Measured.** Every index or ranking change reports its delta on the scoping benchmark the context lane owns (§7). Answer keys live on real
   repos (Effect-TS/effect at 1.1M lines, this repo); the 10M copy is for budgets only; precision is reported next to recall (#2959 comments).
7. **Same object for people and agents.** `context_query` returns the scope; `context_scope` reads and edits one; the app opens an agent's
   scope by id and an agent opens the owner's (§8).

## 3. The scope

A scope is a named, saved answer to one question.

| Field | What it holds |
|---|---|
| `id` | `scp_<ulid>` |
| `question` | the words the person or agent typed |
| `repo`, `commit` | the repo and the indexed commit it was answered at |
| `groups[]` | one per concept or area: `name`, `why` (one line), `rows[]` |
| `rows[]` | one per file: `path`, `role` (`touch` / `read` / `test`), `why` (the edge or evidence that put it there, one line), `score`, `services` (provides / needs, Effect), `pinned`, `dropped` |
| `edits[]` | what the person or agent changed: pin, drop, move to touch/read, "more like this", "what calls this", in order, with who |
| `asOf` | when it was answered; a later commit marks rows whose file changed as `stale` |

Roles: **touch** = the change edits it; **read** = needed to understand it (callers, the service it uses, the schema); **test** = the tests
that cover a touch row. The answer-key files of the benchmark are the touch rows.

Scopes are stored by Sindri (ledger events `scope.created`, `scope.edited`; a view of the latest state). They never write to the repo.
The exact wire shape is the context lane's proposal; this spec fixes the fields above and the behaviour, not the JSON names.

## 4. Answering a question

### 4.1 Legs

1. **Lexical:** trigram search over paths, symbol names and text. Seeds: the question's words, their identifier forms (`refundReason`,
   `refund_reason`, `RefundReason`), and the concept aliases from the map.
2. **Structural:** the seeds' definitions, then their edges (§5.1): who provides the service, who uses it, the layer that wires it, the
   tables a file reads and writes, the routes, the tests.
3. **Semantic (optional):** embeddings over tree-sitter chunks when `index.embeddings` is on; off, the scope says "meaning search off" in
   its footer, not an error.

### 4.2 Ranking

Personalized PageRank over the file graph, seeded by the lexical and semantic hits (aider's method), with edge weights per kind (a provides
edge beats an import). Definitions beat mentions. A file reached only through a barrel counts as reaching the defining file (§5.1).
Grouping: rows group by concept when the map has one for them, else by top-level package. The touch / read split: a row is **touch** when the
question's nouns hit its definitions (it defines the entity, the schema field, the route, the service); **read** when it is reached only by
edges; **test** when a test -> subject edge points at a touch row.

### 4.3 Budgets (10M-line copy, the box this runs on)

| Step | Budget |
|---|---|
| question -> first rows on screen | p95 < 1 s (lexical leg alone) |
| question -> full ranked scope | p95 < 5 s |
| "more like this", "what calls this" on a row | p95 < 1 s |
| page first frame with a saved scope | < 300 ms |
| recall of the answer-key files in the top 25 | >= 0.8, precision reported next to it |

## 5. Engine requirements (context lane)

### 5.1 Edges from syntax

Each is a tree-sitter pass with a fixture repo, an answer key and a test. Order is the context lane's plan on #2959; this spec requires:

0. **Fix the TS resolver first** (context's correction on #2983: graphify is not the cause of the missing edges). Two small Core fixes
   come before any new pass:
   - `export * as X from "./y"` is dropped today: `TsExports.cs:36` filters out every star export with a namespace group. It becomes a
     namespace re-export, so `X.Foo` resolves to the file that defines `Foo`.
   - `TsWorkspace.cs:29` turns a package.json `exports` map into one entry file: `Target` follows the first condition key it knows and
     never keeps the subpath keys. Subpath entries (`"./sql"`, `"./*"`) become their own entries, so `@pkg/x/sql` lands on a file.
   scip-typescript stays opt-in and benchmarked (ledger 4); it is not on the default path.
1. **Barrels collapse.** An import of `x/index.ts` that re-exports `Foo` becomes an edge to the file that defines `Foo` (#2983). Done first:
   every edge kind below depends on it.
2. **Effect services.** `Context.Tag("id")`, `Context.GenericTag`, `Effect.Service<X>()("id", {...})` (and v4's `ServiceMap.Service`) ->
   a service node; `dependencies: [...]` -> needs edges; `Layer.succeed/effect/scoped(X, ...)` -> provides; `Layer.provide/merge/provideMerge`
   -> wiring; `yield* X` inside `Effect.gen`, `X.pipe(...)`, `Effect.flatMap(X, ...)` -> uses.
3. **Schemas and errors.** `Schema.Struct/Class/TaggedError`, `Data.TaggedError` -> schema and error nodes, so "add a field to orders"
   finds the schema.
4. **SQL and routes.** `sql` template reads and writes -> table edges; `HttpRouter` / `HttpApiEndpoint` -> route nodes.
5. **Tests.** A test file -> the subject files it imports (after barrel collapse).
6. **C#** keeps its Roslyn parser; DI registrations and endpoint maps are the C# equivalents of 2 and 4 (later, not in ship 1).

### 5.2 One extract, incremental, in SQLite

One extract per commit, shared by the code index and the map (today they run graphify twice, #2984). It is stored in SQLite and updated per
changed file, so a commit that touches 20 files re-parses 20 files. A full build is only for a new repo or a new extractor version.
Budget: full build of 10M lines < 30 min and < 4 GB peak; a 20-file commit < 30 s.

## 6. The page

### 6.1 Layout

```
┌ Context ─────────────────────────────────────────────────────────────── repo ▾ ┐
│ [ Ask what you want to change… e.g. add a refund reason to orders          ] ⏎ │
│ Saved: refund reason · 12 files  ·  retry policy · 31 files  ·  …       All ▸   │
├──────────────────────────────────────────────┬──────────────────────────────────┤
│ add a refund reason to orders   23 files  2.1 s  [Touch 6] [Read 12] [Test 5]   │
│ ▾ Orders (domain)        why: defines Order, OrderSchema                         │
│   ● touch  orders/Order.ts           defines OrderSchema       provides Orders   │ peek: the file's
│   ● touch  orders/OrderRepo.ts       writes table orders       needs SqlClient   │ outline, the
│   ○ read   orders/index.ts→Order.ts  barrel, collapsed                           │ matched lines,
│ ▾ Refunds                ...                                                     │ its edges in and
│   ● touch  refunds/RefundService.ts  uses Orders (yield*)                        │ out, why it is
│   ◆ test   refunds/Refund.test.ts    tests RefundService                         │ here
│ ▸ Payments  4 files                                                              │
├──────────────────────────────────────────────┴──────────────────────────────────┤
│ p pin · x drop · t/r touch↔read · m more like this · c what calls this · n neighbourhood · s save │
└──────────────────────────────────────────────────────────────────────────────────┘
```

- The question box is the full width of the page and has focus when the page opens. Its placeholder is whole at 1000 px.
- Below it: the saved scopes as one line of chips, newest first; All opens the list.
- The scope header: the question, the file count, the answer time, and the role filters as toggles with counts.
- Rows (#2978 grammar, 32 px): role glyph, path (the file name in ink, the folder muted, cut from the left so the name stays whole), why
  (one line), services (Effect: provides / needs), pinned mark. Groups fold.
- The key bar lists only the keys that work on the selected row.

### 6.2 Peek (Space) and full (Enter)

- **Peek**, a side pane at 40% width: the file's outline (top-level symbols), the lines that matched with two lines of context, its edges
  in and out grouped by kind (each one a link that adds that file to the scope as read), and the why.
- **Full**, Enter: the file in a read-only code view at the matched line, with the same edges in a gutter. "Open in editor" is one key (`o`)
  and runs the configured editor command.

### 6.3 Neighbourhood (n)

A node-link view of one row and what touches it, one hop out, two on request: at most 40 nodes, every label whole, laid out in the three
bands entry points -> core -> data / effects. Services are nodes; edges are drawn in ink with their kind on hover. One accent marks the
seed and the rows already in the scope. Arrows walk edges; Enter adds the focused node to the scope; Esc goes back to the list with the same
row selected.

### 6.4 Orientation (the whole repo)

A treemap of the repo by top-level package and folder, area = lines, with the scope's files lit in the accent. It answers "where in the repo
is this", not "what calls what". It replaces the concept-card landing (#2976) as the view when no question is open: the treemap plus the
saved scopes plus the question box.

### 6.5 States

| State | What shows |
|---|---|
| first index building | the question box disabled with "Indexing agentic-workflow: 41% · about 3 min left"; the treemap fills in as files land; no Build map button while a build runs |
| index ready, no map | questions work (lexical + structural need no map); groups fall back to packages; a quiet line offers "Name areas" (the map) |
| a question with no hits | "Nothing in agentic-workflow matches 'refund reason'" plus up to 3 near words that do match, as plain words, never raw keys (#2809) |
| answer slower than 1 s | the lexical rows show first with "ranking…" in the header; the order settles once, no reshuffle after that |
| index behind the checkout | a line in the header: "answered at 7f01aca, 3 commits behind" with Refresh |
| a failed read | "Couldn't read the code index" plus Retry and the log path; never an empty list that looks like "no matches" |

### 6.6 Keyboard

`/` focuses the question. `j`/`k` or arrows move rows, `h`/`l` fold groups, Space peeks, Enter opens, Esc goes back keeping the selection,
`p` pin, `x` drop, `t` / `r` set touch / read, `m` more like this, `c` what calls this, `n` neighbourhood, `s` save (asks for a name, default
the question), Ctrl+K has every action by name. Every action has a menu item for mouse use.

### 6.7 Visual bar

One accent (selection, the seed, lit treemap cells). Ink for structure. No band or concept colours (#2813). Labels never cut (#2809). Type
scale and spacing from the tokens. Inspora is the reference (#2959 point 6).

## 7. The benchmark (context lane, already planned on #2959)

`tests/fixtures/scoping/<repo>/questions.yaml`: a question, its answer-key files with their role, a why per file. Fixtures: Effect-TS/effect
pinned at a sha, and this repo. 5 questions each to start, 10 later. A runner prints precision and recall at 25, p50 / p95 latency, and the
follow-ups needed. This spec adds one thing: the runner also counts **touch** recall separately, because a scope that finds every read file
and misses the one to edit has failed.

## 8. Agents

- `context_query` returns a scope (it is saved, with `who = session:<id>`). An agent that wants the old text answer passes `shape: text`.
- `context_scope get|edit|list` (MCP and `sindri context scope …`): read one, apply pin / drop / role edits, list saved ones.
- The app shows an agent's scopes in the saved list with the session's name; a Work item or a todo can link a scope ("Scope this" from a
  row, #2961 point 5).
- A scope handed to an agent session is a short block: the question, then the touch / read / test files with their why, cut to a token budget.

## 9. Errors

New codes in the SND-CONTEXT range, numbers fixed at build time: scope not found; scope for a repo not in the profile; index not ready
(with the build's progress); edit on a stale row. Each has a sentence for the app and a `fix:` for the CLI.

## 10. Goals and non-goals

Goals: the job in §0 inside the §4.3 budgets; the page in §6; the agent surface in §8.
Non-goals for this spec: precise (compiler-grade) TS types; cross-repo scopes; editing code from the page; the concept map's authoring
tools (pins, pages) beyond keeping them reachable from a menu.

## 11. What happens to today's views

| Today | After |
|---|---|
| concept card grid landing | gone; the treemap + saved scopes + question box |
| concept map (three bands) | the neighbourhood view (§6.3), opened from a row |
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
- A person (ux) and an agent each scope the 5 Effect questions and get the touch files without a follow-up.

## 14. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Lane | Starts | Needs |
|---|---|---|---|---|
| P0 | benchmark + answer keys + runner; baseline numbers | context | now | — |
| P1 | the two TS resolver fixes (§5.1 item 0), then barrel collapse, then the Effect services pass | context | now | P0 for its delta |
| P2 | one extract per commit, incremental, SQLite (#2984) | context | with P1 (second builder) | — |
| P3 | the scope: events, view, `context_query` returns it, `context_scope` MCP + CLI | api (+ context for the query) | now, against a stub ranker | the §3 fields |
| P4 | ranking + touch/read/test split | context | after P1 | P0, P1 |
| D1 | design: the §6 layout, rows, peek, neighbourhood, treemap as mockups, contact sheet vs Inspora | design | now | #2978 row tokens |
| U1 | the page: question box, scope rows, peek, keys, states (§6.1, §6.2, §6.5, §6.6) on a fake scope | ui | after D1's row shot | the P3 wire shape |
| U2 | neighbourhood + treemap (§6.3, §6.4) | ui | after U1 | P1 edges for real data |
| F1 | perf: the §4.3 budgets on the 10M copy, one issue per broken budget | perf | after P2 + U1 | — |
| Q1 | ux + qa walk the 5 questions, person and agent | ux, qa | after P4 + U2 | — |

P0, P1, P2, P3 and D1 start the same day, so five lanes are busy from day one. U1 builds on a fake scope from the P3 wire shape, so it
never waits on ranking.
