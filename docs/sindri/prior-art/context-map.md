# Prior art: Context map (scope a change in a 10M-line repo)

This file is the prior-art ledger for the Context surface of Sindri (rule: #2989). It lists the small daily jobs of scoping a change, who solves each best today, what we copy, and what Sindri does on origin/main.
It was written 2026-11-12 from primary docs and READMEs read that day. A claim from a vendor's own page is marked "vendor claim". A thing I could not confirm is marked "unconfirmed".
Surface job: turn a plain question ("add a refund reason to orders") into the ranked files to touch, with one line of why each, in under a minute, in a 10M-line repo where LSP and types can't be trusted (Effect-style TypeScript).
Direction issue: #2959. Related: #2974 (question returns a scope), #2976 (flat card grid), #2983 (TS edges stop at barrel files), #2984 (graphify scale), #2978 (row/peek/full grammar).

Licence note for builders: we may read and copy ideas from MIT / Apache-2.0 code (aider, Zoekt, dependency-cruiser, madge, repomix, scip-typescript, effect-analyzer). Serena is GPL-3.0 (its SolidLSP part is MIT): read for ideas, copy no code. Sourcetrail and NumbatUI are GPL-3.0: ideas only.

---

## 1. Type a feature word, get the files that hold it

**Best today**
- Sourcegraph search on Zoekt. Filters `file:`, `repo:`, `lang:`, `type:symbol`, `count:N`; default timeout 10 s. Docs: https://sourcegraph.com/docs/code-search/queries
- Zoekt itself (Apache-2.0): positional trigram index, substring and regex, boolean operators. Design doc says the index is about 3 to 3.5x the corpus size, RAM about 1.2x the corpus, and sub-50 ms is a stated goal for Android/Chrome-size code (a design goal, not a benchmark). https://github.com/sourcegraph/zoekt and https://github.com/sourcegraph/zoekt/blob/main/doc/design.md
- GitLab runs Zoekt: median exact query about 100 ms (p50 119 ms), from GitLab's handbook (read earlier, secondary): https://handbook.gitlab.com/handbook/engineering/architecture/design-documents/telescope/engine-underneath/
- GitHub code search / Cursor `grep` are the same job for a single repo; no extra detail confirmed.

**What we take**
- One text box, results as 32 px rows (file path, best matching line, hit count), 25-30 per screen, arrows to move, Enter to open, Space to peek the matches.
- Path and symbol filters as typed tokens (`file:`, `lang:`, `sym:`), the Sourcegraph grammar, because staff engineers already know it.
- Exact-substring recall is the floor: a feature word that appears in the code must be found even when no concept card mentions it.

**Where one app wins**: the same box also searches decisions, tickets and PR titles from the Sindri ledger, so "refund" returns the Linear issue and the PR that touched it next to the files. Sourcegraph can't see the ticket.

**Desktop gain**: a local trigram or FTS5 index answers with no network round trip. Target: first result row under 100 ms at 10M lines, measured from keypress to first row painted.

**Sindri today**: the Context search narrows concept cards only and returns no file names (#2974). A code search exists in Core (`src/Context/Aw.Context.Core/Code/CodeSearchQuery.cs`), but it is not the page's answer.

**Verdict**: take and change (Sourcegraph's filter grammar, rows from our grammar).

---

## 2. Rank definitions above mentions

**Best today**
- aider repo map (Apache-2.0). Reading `aider/repomap.py`: tree-sitter queries (`{lang}-tags.scm`) give `def` and `ref` tags. A directed graph is built, file to file, referencer to definer. Edge weight is `sqrt(num_refs)` times multipliers: x10 if the identifier is mentioned in the chat, x10 for long snake/camel/kebab names (8+ chars), x0.1 for names starting `_`, x0.1 for names defined in more than 5 files, x50 when the referencer is a file in the chat. Personalized PageRank, then a binary search for how many ranked tags fit the token budget. https://raw.githubusercontent.com/Aider-AI/aider/main/aider/repomap.py and https://aider.chat/docs/repomap.html
- Zoekt ranking: the doc lists match on a symbol definition (via ctags), word-boundary quality, proximity, filename length, comment/string match, and file update time. https://github.com/sourcegraph/zoekt/blob/main/doc/design.md
- Sourcegraph: keyword ranking "enabled by default and cannot be configured"; personalized boost for repos you recently contributed to (Enterprise). https://sourcegraph.com/docs/code-search/features . No factor list beyond that.

**What we take**
- aider's weights as the first ranker, written as a table in the spec so a benchmark can tune them: sqrt on ref count, x10 for a word the question used, x10 for long distinctive names, x0.1 for ubiquitous names (defined in more than 5 files; in Effect this kills `map`, `pipe`, `make`).
- Zoekt's cheap signals as tie-breakers: symbol definition beats comment match; shorter filename beats longer; recently changed beats stale.
- Seed files = files the question matched (item 1), PageRank personalized on them. This is aider's `mentioned_fnames` idea without a chat.

**Where one app wins**: personalize on more than words. Seeds can include the files of the open PR, the issue's linked branch and the agent session's recent edits, all of which Sindri holds.

**Desktop gain**: ranking runs on the local graph in SQLite, no round trip. Target: re-rank after pinning or dropping a file under 100 ms on a 25-file scope.

**Sindri today**: ranker settings exist (`src/Context/Aw.Context.Core/Engine/RankerRules.cs`, `RankerSettings.cs`) but rank concepts and records, not files by def-over-mention. No benchmark yet: the scoping benchmark is the context lane's step 1 on #2959.

**Verdict**: take and change.

---

## 3. Find by meaning when you don't know the word

**Best today**
- Cursor: chunks code, embeds it, stores vectors remotely. Vendor/partner claim: Turbopuffer customer page says one namespace per codebase and that seeding from a teammate's index cut median time to first query from 7.87 s to 525 ms. https://turbopuffer.com/customers/cursor . Cursor's own indexing doc was unreachable (the old docs URL redirects to a page with no indexing content; unconfirmed beyond the partner page).
- Augment Code: vendor claim of a per-developer real-time index updating "within seconds"; the MCP docs describe a local mode that indexes as you edit. https://www.augmentcode.com/blog/a-real-time-index-for-your-codebase-secure-personal-scalable and https://docs.augmentcode.com/context-services/mcp/overview . Scale figures (400k files, sub-200 ms) appear only on third-party sites: unconfirmed.
- Sourcegraph Cody mixes keyword search (queries rewritten with more terms), Sourcegraph search, and a "code graph". No numbers in the docs. https://sourcegraph.com/docs/cody/core-concepts/context

**What we take**
- Embeddings are the second-chance path: run only when exact/symbol search (item 1) returns fewer than ~10 files or the top score is low. Show "found by meaning" as a muted tag on the row so a wrong guess is visible.
- Share the index by commit: one build per sha, reused by a teammate or a second lane (Cursor's 7.87 s to 525 ms is the reason).

**Where one app wins**: the embedding corpus includes the decisions and ticket text, so "customer got charged twice" lands on `Refund` code through the ticket that mentions both.

**Desktop gain**: local vectors (the repo already pins an Ollama embedding model in `context/code-index.lock`), so semantic search works offline. Target: query embedding + top-50 lookup under 300 ms at 10M lines (to be measured; no number exists today).

**Sindri today**: code index has symbol vectors (`src/CodeIndex/Aw.CodeIndex.Core/Facade/SymbolVectorsQuery.cs`). Whether the page uses them for scoping is not confirmed. No issue found.

**Verdict**: take as is (second pass only).

---

## 4. Callers and callees without an LSP

**Best today**
- Sourcegraph with `scip-typescript` (Apache-2.0): runs the real TypeScript compiler, so generics, `exports` maps and namespace re-exports resolve. Run from the folder with `tsconfig.json`; workspaces via `--yarn-workspaces` / `--pnpm-workspaces`; out-of-memory fixes are `--no-global-caches` and a bigger `--max-old-space-size`. No timings in the README. https://github.com/sourcegraph/scip-typescript
- GitHub code navigation: tree-sitter name search, no build, "Definition" and "References" tabs on a clicked call, limit 100,000 files per repo, active branches only. Docs list TypeScript. https://docs.github.com/en/repositories/working-with-files/using-files/navigating-code-on-github . Its precise resolver, stack graphs, is archived (read-only since 2025-09-09, "no longer supported"): https://github.com/github/stack-graphs . Not an option to build on.
- RepoGraph (paper, CC BY 4.0): tree-sitter line-level graph with def/ref nodes and invoke/contain edges; ego-graph around a search term. On SWE-bench Lite it lifted GPT-4o Agentless 27.33 to 29.67 and SWE-agent 18.33 to 20.33 (Table 2). https://arxiv.org/abs/2410.14684
- Serena (GPL-3.0) gives agents LSP "find referencing symbols"; exactly the path the owner says is weak for Effect. https://github.com/oraios/serena

**What we take**
- Two tiers: tree-sitter name edges everywhere, labelled "by name"; scip-typescript edges per package where `node` exists, labelled "exact". Show the label on each caller row so the person knows which to trust. Cache scip output by package hash.
- RepoGraph's lesson: return a small ego-graph (one hop, 20-50 nodes), not the whole graph.

**Where one app wins**: callers include tests, Linear tickets that mention the symbol, and past PRs that changed it, not only code.

**Desktop gain**: callers of a symbol from a local SQLite edge table: target under 50 ms for 1-hop, under 200 ms for 2-hop on a graph of 10M lines.

**Sindri today**: TS callers are `calls` edges only, from graphify; `GraphJsonParser` drops imports/re_exports/references edges, and `yield* Tag` is not a call (#2983 comment). The code index has one real parser, C# Roslyn (`ISymbolParser` / `CSharpParser`). TS import resolution lives only in the map (`src/Context/Aw.Context.Core/Extract/Imports/Ts*.cs`).

**Verdict**: take and change (name edges first, scip as opt-in precise source, measured on the #2959 benchmark).

---

## 5. What a service provides and needs (Effect)

**Best today**
- `@effect/language-service`: hover a `Layer` and get a graph of what each service provides/requires; `layerGraphFollowDepth` option (default 0). CLI: `diagnostics`, `overview` (exports of a file or project, "meant for LLMs"), `layerinfo --file --name` (one named layer plus a suggested composition order). For TypeScript 7+ the README says use `@effect/tsgo`. It runs inside the TypeScript checker, so it inherits the slow type resolution. No benchmark numbers. Licence not stated in the README: unconfirmed. https://github.com/Effect-TS/language-service/blob/main/README.md
- `effect-analyzer` (MIT, ts-morph + type checker, "your code never runs"): Mermaid outputs `mermaid-services`, `mermaid-layers`, `mermaid-errors`, a `--diff` mode, JSON for agents. Effect v4 only, Node 22+. 30 stars, last commit date not visible: treat as young. https://github.com/jagreehal/effect-analyzer
- Nothing else found that lists services as a table with consumers. Unconfirmed that any tool does it without the type checker.

**What we take**
- The view: a ranked table of services: name, defining file, layer files that provide it, count of files that need it. Click a row to open a one-hop subgraph (provides -> service -> needs). Effect-analyzer's `mermaid-services` and `layerinfo`'s "composition order" are the two outputs to match.
- Extraction without the checker (tree-sitter, as context planned on #2983): service definition (`Context.Service`, `Context.Tag`, `Context.GenericTag`, `Effect.Service` class) -> provides; `yield* X` / `X.pipe` / `Effect.flatMap(X,…)` -> needs; `Layer.effect|succeed|scoped(X,…)` -> implements; `Layer.provide|provideMerge|mergeAll` -> wiring. Both v3 `Context.Tag` and v4 `ServiceMap.Service`-style forms (the latter from one skills page: unconfirmed) need handling.
- Use effect-analyzer as the oracle in tests: run it on the Effect fixture, diff its service list against ours. It is MIT, so a fixture answer key may be derived from its output.

**Where one app wins**: the service row also shows the tests that provide a test layer for it, the decisions that name it, and the open PRs touching its layers.

**Desktop gain**: the table is a SQL query over edge rows, so opening it is a local read. Target: service table for the whole Effect repo (1.1M lines, 40,292 graph nodes per #2984) paints under 300 ms.

**Sindri today**: missing. graphify emits no service/layer/provide edge (#2959 context comment, #2983). Extractors are context lane step 3.

**Verdict**: take and change (the table + one-hop view are ours; extraction is ours; effect-analyzer is the test oracle).

---

## 6. Blast radius: what breaks if I change this file or symbol

**Best today**
- Nx `affected`: files changed via git; a task is affected if its inputs contain a changed file or it reads the output of an affected task; `nx affected -t build --explain` prints why. Lock-file changes affect everything by default. https://nx.dev/docs/features/ci-features/affected
- dependency-cruiser (MIT): `--reaches <regex>` shows a module and its transitive dependents; `--affected` does the same since a git revision; `--focus` + `--focus-depth` (default 1) shows neighbours; `--highlight`. https://github.com/sverweij/dependency-cruiser/blob/main/doc/cli.md . The later part of that page was a summary, not verbatim: confirm flags before building on them.
- Greptile: vendor claim that it builds a graph of every function, class and dependency and uses it in PR review. Method, storage and accuracy not documented: unconfirmed. https://www.greptile.com/docs/introduction

**What we take**
- Nx's `--explain`: every affected item shows the path that put it there ("A imports B imports C"). That is our "one line of why".
- dependency-cruiser's `reaches` as the traversal, with depth 1 by default and a "+1 hop" key.
- Split the answer in three groups: code dependents, tests that cover the file, and "outside the concept" callers (what #2974 asks for).

**Where one app wins**: dependents include other repos in the profile, open PRs editing those files (conflict risk), and running agent sessions that hold the same files.

**Desktop gain**: reverse-reachability over a local edge table. Target: 2-hop dependents of a file in under 200 ms at 10M lines.

**Sindri today**: missing as a scope view (#2974 asks for callers outside the concept). Map has import edges; code index has call edges.

**Verdict**: take and change.

---

## 7. Find the sibling pattern to copy

**Best today**
- Sourcegraph search with `file:` and `repo:` scoping plus structural/regex matches. The `patterntype:structural` option is not on the queries page I read: unconfirmed. https://sourcegraph.com/docs/code-search/queries
- Embedding search (Cursor, Augment; see item 3): "find code like this".
- aider's map shows every file's top definitions at once, so you spot the sibling by reading names. https://aider.chat/docs/repomap.html
- Nothing found that answers "the existing feature most like what I'm adding, with the files it touched". Unconfirmed that any tool does.

**What we take**
- Use git history, which no tool above uses: find past commits/PRs whose message or diff resembles the question, list the files they touched together. "Add a refund reason" -> the PR that added the last reason-like field, with its 9 files as a template scope.
- Row key: `m` on a scope file = "more like this" (matches the #2959 direction text).

**Where one app wins**: Sindri has the PR, the ticket and the review comments for that past change, so the sibling comes with the decisions people made.

**Desktop gain**: co-change counts from a local git log table. Target: top-5 sibling commits in under 500 ms on 100k commits.

**Sindri today**: missing. The scope's "more like this" is in the direction issue only.

**Verdict**: ours to build (no best-in-class exists to copy).

---

## 8. Orient in an unfamiliar huge repo

**Best today**
- Nx graph: `nx graph` opens a browser graph; group by folder (composite nodes that expand in place), search box, `--focus <project>` with proximity control, click a node for details, pick Start and End to trace a path; click an edge to see which files created it. https://nx.dev/docs/features/explore-graph (the page has no static screenshot; live demo https://nrwl-nx-examples-dep-graph.netlify.app/?focus=cart)
- GitHub's repo visualizer (circles sized per folder) is named in #2976's comment; I did not open it: unconfirmed here.
- Sourcetrail (GPL-3.0, archived 2021-12-14): symbol-centered view, code on one side and a small graph of the symbol's neighbours on the other. Fork NumbatUI (Quarkslab) calls itself unstable. https://github.com/quarkslab/NumbatUI
- aider repo map: whole repo in ~1k tokens, ranked, files with their key definitions. https://aider.chat/docs/repomap.html

**What we take**
- Landing = ranked areas as rows with a size bar (code lines and 30-day churn), not equal cards (answers #2976). Enter drills into the area; "Other" is a muted last row.
- Nx's two moves: group-by-folder composites that expand in place, and "click an edge to see the files behind it".
- Sourcetrail's layout for a selected symbol: definition code left, one-hop neighbours right.
- The aider-style text outline (area -> file -> top definitions) as the zoom-out that needs no layout engine.

**Where one app wins**: areas carry owners, open tickets, red CI and recent agent activity, so "orient" also says where the live work is.

**Desktop gain**: 500 areas virtualized at 60 fps; landing first frame under 300 ms (the #2959 budget).

**Sindri today**: flat grid of equal concept cards, 3 per row at 1280 (#2976, folded into #2959).

**Verdict**: take and change.

---

## 9. Save, name and share the scope

**Best today**
- No product found that saves "the files to touch for this change" as a named object. Closest: Nx `--file=output.json` exports a graph; Cursor/Aider keep a per-session file list that dies with the session. Unconfirmed that anything better exists.
- Repomix (MIT) writes one packed file of chosen files for any agent, with `--token-count-tree`. https://github.com/yamadashy/repomix
- Swimm (vendor claim, from earlier research, not re-read): ties docs to paths and flags drift in CI.

**What we take**
- A scope is a row in the ledger: id (`context_scope`), question, files with why-lines, pins and drops, the commit sha it was built at. Named, listed on the landing as "recent scopes" rows, openable by an agent by id.
- Repomix's token-count tree on the scope: each file shows its token cost, a total in the header.

**Where one app wins**: the scope hangs off a Linear issue or PR, so the issue shows its scope and the scope shows its issue.

**Desktop gain**: open a saved scope with no recompute: under 100 ms from the local store.

**Sindri today**: missing. `context_scope` is in the direction issue only. `context pin` and `context page` verbs name concepts and pages, not file sets.

**Verdict**: ours to build.

---

## 10. Hand the scope to an agent inside a token budget

**Best today**
- aider: default `--map-tokens 1024`; with no files in the chat the budget is multiplied by 8 (`map_mul_no_files`); binary search to fit; stops when within 15% of budget; lines cut at 100 chars. https://raw.githubusercontent.com/Aider-AI/aider/main/aider/repomap.py
- Repomix: `--compress` keeps tree-sitter-extracted signatures, `--token-count-tree`, `--token-budget` (exit code on overflow, per a summarized section: unconfirmed). https://github.com/yamadashy/repomix
- code2prompt (MIT): per-file token estimates only; no budget fitting or compression found. https://github.com/mufeedvh/code2prompt

**What we take**
- Three levels per file in the handoff: path + why (about 20 tokens), + signatures (aider-style `⋮...` / `│` outline), + full text. Fill in rank order until the budget, show "dropped N files" so nothing is silently cut.
- Handoff by id (the agent calls `context_scope <id>`) and by "Copy as context" button (#2974).

**Where one app wins**: Sindri launches the agent, so it knows the model's window and what the session already holds, and sets the budget itself.

**Desktop gain**: build the packed context locally: target under 500 ms for 25 files at 8k tokens, no network.

**Sindri today**: token budgets were retired from the engine (`maxBudget (retired)` in `docs/sindri/context.md`). No scope handoff yet (#2974 asks for "Copy as context").

**Verdict**: take and change.

---

## 11. Keep the index fresh as commits land

**Best today**
- aider: tag cache keyed on file mtime, SQLite via diskcache; whole-map reuse only when the last build took more than 1.0 s. https://raw.githubusercontent.com/Aider-AI/aider/main/aider/repomap.py
- dependency-cruiser: `--cache`, strategies `metadata` (git) or `content`; cache default location from a summarized section: unconfirmed. https://github.com/sverweij/dependency-cruiser/blob/main/doc/cli.md
- Augment: vendor claim, updates "within seconds" of a change. See item 3 for the sources.
- Zoekt/Sourcegraph: default branch indexed; up to 64 branches per repo if configured; scoped searches "always up-to-date". https://sourcegraph.com/docs/code-search/features

**What we take**
- Re-extract only files whose content hash changed since the indexed sha; merge into the stored graph (#2984 proposal 2). On a 10M-line repo a main move touches 10-200 files.
- One extract per commit shared by the code index and the map (#2984 proposal 1).
- Show freshness in the header: "indexed at <sha>, 3 commits behind".

**Where one app wins**: Sindri sees the checkout, the worktrees of running agents and the PR heads, so it can index the branch a lane works on, not only main.

**Desktop gain**: target from #2984: rebuild after a 50-file change under 2 min; unchanged-file cost zero. Measured baseline today: graphify takes 2:59 and 2.76 GB on 1.1M lines, one core, twice per commit.

**Sindri today**: full re-extract per commit, twice (`GraphifyRunner.cs:119`, `GraphifyClusterSource.cs:88`) per #2984.

**Verdict**: take and change.

---

## 12. Show structure visually without a hairball

**Best today**
- Nx graph: folder-grouped composite nodes, focus with proximity, search; the answer to a hairball is "never show all at once". https://nx.dev/docs/features/explore-graph
- dependency-cruiser: `--collapse <depth|regex>` to summarize folders, `--include-only`, reporters `dot`, `archi` (folder-level), `ddot`, `mermaid`, `d2`, `html`. https://github.com/sverweij/dependency-cruiser/blob/main/doc/cli.md
- madge (MIT): `.dot()`, `.svg()`, `--circular`, `--orphans`, `--leaves`; needs GraphViz; TypeScript needs `tsConfig`. Plain graph, no collapse: the hairball case. https://github.com/pahen/madge
- CodeSee (commercial) drew folder/file maps and call paths. Its team announced closing commercial operations (a LinkedIn post, dated Feb 22 without a year); GitKraken announced the acquisition on 2024-05-14. No stated reason found: unconfirmed. Do not claim why it died. https://sdtimes.com/software-development/gitkraken-acquires-codesee-launches-new-devex-platform/
- Practitioner posts describe whole-repo call graphs as dense clusters with overlapping labels (https://supermodeltools.com/blog/weekend-codegraph-bs). I found no controlled study: opinion, not data.

**What we take**
- Never more than the screen can label: a hard cap (say 60 labeled nodes), then collapse the rest into "+N in <folder>" nodes that expand in place (Nx + dependency-cruiser `--collapse`).
- Default view = the scope's neighbourhood in 3 bands (entry -> core -> data/effects), as the direction says; the whole-repo view is a treemap for orientation only.
- Every drawn edge click-through to its evidence (Nx: click an edge, see the files). Every graph also has an outline (text list) twin, so keys and screen readers work.
- Draw it ourselves, in Avalonia; the Mermaid/dot outputs are for export.

**Where one app wins**: node colour can carry live state from other sources (red CI, open PR, running agent), which a static graph can't.

**Desktop gain**: pan/zoom a 60-node view at 60 fps; first frame under 300 ms (#2959).

**Sindri today**: map exists as a whole-repo graph view; flat card landing (#2976); the visual bar is called "ass" by the owner in #2959. Frames of today's map are with design.

**Verdict**: take and change.

---

## 13. See the decisions, tests and docs that constrain the area

**Best today**
- Cody mixes search, a code graph and @-mentioned files/symbols/URLs as context, but lists no decisions or tests. https://sourcegraph.com/docs/cody/core-concepts/context
- Swimm couples docs to code paths (vendor claim, earlier research, not re-read).
- No tool found that returns "the ticket decisions and the tests for these files" next to the files. Unconfirmed that any does.

**What we take**
- The scope's rows come in three sections, as #2974 specifies: files/symbols, decisions and records (with their text), tests that cover the files. Test -> subject is one extractor (naming + imports).

**Where one app wins**: this is the case where one app is the product: only Sindri holds the code index, the ledger and the tickets together.

**Desktop gain**: all three sections come from local stores in one query: under 300 ms.

**Sindri today**: concept cards show "2 decisions, 1 code file" counts only (#2974).

**Verdict**: ours to build.

---

## Closing table

| Problem | Best tool | Verdict | Sindri today (issue) |
|---|---|---|---|
| 1 Feature word -> files | Sourcegraph / Zoekt | take and change | cards only, no files (#2974) |
| 2 Definitions over mentions | aider repo map | take and change | ranks concepts, not files; no benchmark yet (#2959) |
| 3 Find by meaning | Cursor, Augment | take as is (second pass) | symbol vectors exist; page use unconfirmed |
| 4 Callers/callees without LSP | scip-typescript + tree-sitter names | take and change | TS callers = calls only (#2983) |
| 5 Effect provides/needs | @effect/language-service, effect-analyzer | take and change | missing (#2983) |
| 6 Blast radius | Nx affected `--explain`, depcruise `reaches` | take and change | missing (#2974) |
| 7 Sibling pattern | none good | ours to build | missing |
| 8 Orient in huge repo | Nx graph, aider map | take and change | flat card grid (#2976) |
| 9 Save/share scope | none | ours to build | missing (#2959) |
| 10 Scope to agent in budget | aider, repomix | take and change | budgets retired; no handoff (#2974) |
| 11 Keep fresh | aider mtime cache | take and change | full extract x2 per commit (#2984) |
| 12 Structure without hairball | Nx, dependency-cruiser collapse | take and change | whole-map view, owner calls it bad (#2959) |
| 13 Decisions/tests/docs | none | ours to build | counts only (#2974) |

## Not confirmed (do not cite as fact)
- Cursor's own indexing mechanism (docs unreachable); only the Turbopuffer partner page.
- Augment's scale and latency numbers (third-party sites only).
- Why CodeSee shut down.
- Sourcegraph precise vs search-based navigation docs (both URLs returned 404).
- `@effect/language-service` licence; `effect-analyzer` last commit date.
- Greptile's indexing method.
- Whether stack-graphs' TypeScript rules ever modelled `exports` maps (repo archived anyway).
