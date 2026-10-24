# The Sindri code index block: symbols, callers and callees, built in the background when main moves

**Status:** **approved by the owner on 2026-10-20, every §15 default as written**; revision 3 amends §5.5, §9 and §15 Q1 from the CI-0d graphify spike (`tests/fixtures/code-index/graphify/SPIKE.md`), **approved by the owner on 2026-10-20** (both relayed by sindri-meta); **revision 4 (2026-10-20, owner decision relayed by sindri-meta) adds CI13 and §6.4: the context map and the code index publish together, approved by the owner on 2026-10-20 with §15 Q11 to Q15 at their defaults (relayed by sindri-meta)** · **Date:** 2026-10-19 · **Owner decisions:** 2026-10-19 (§1: CI1, CI2,
CI4; the shared trigger is the owner's F1) · **Builds on:** the first Sindri design `2026-10-07-sindri-design.md` (D), §6.2
(the TypeScript index this replaces), the factory addendum `2026-10-09-sindri-factory-addendum.md` §17.29.4 step 4 (the code
index block is its own block), the context retrieval spec `2026-10-10-sindri-context-retrieval-design.md` (CTX), Phase 9 and
S-9, the scoping station spec `2026-10-15-sindri-scoping-station-design.md` (SC), C3, `index-not-built` and Q15, the API
surface spec `2026-10-10-sindri-api-surface-design.md` (API), the sandbox spec `2026-10-18-sindri-sandbox-design.md` (SB),
`docs/csharp/sindri-context.md`, `docs/csharp/sindri.md`, `docs/sindri/index.md` (what the TS index does today) ·
**Lane:** the ideas lane writes it; the context lane owns S-9 and the shared trigger; the API lane owns the job runtime ·
**Next:** /specToProvenPR takes §14's phases to PRs.

## 0. Why

The scoping station's code lens cannot say who calls what. `GET /sindri/context/code` answers `callers` and `callees` with
`unavailable`, reason `index-not-built`, and the station records that as a limit (SC §5.1, C3). Context Phase 9 (S-9) wants
symbol-level `implements` links and an `symbols` column in the file map, and it is marked "blocked: the code index block"
(CTX phase table). The block has no C# build and no spec.

What exists is the TypeScript index (`sindri/src/index`, 3,356 lines, documented in `docs/sindri/index.md`). It is built by
hand with `sindri index build`, or by an hourly and a nightly launchd job. It parses only TypeScript and JavaScript, reads
the working tree, and its one graph, from graphify, is stored but read by nothing. Its freshness is a clock ("older than 24
hours is stale"), not a fact about the repo.

This spec rebuilds it as a Sindri block in idiomatic C#. It is not a translation (AGENTS.md, `.agents/rules/csharp.md`
"Porting a TS package"): the TS package tells us what the pieces are for (symbols, clones, dependencies, embeddings, a
graph, offline) and nothing about how. The owner's rule for how it runs (CI1) is the main change: **the owner never builds
it.** Sindri builds it in the background whenever a registered repo's main checkout moves, shows the job in the app, and the
rest of Sindri reads it.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| CI1 | **The build is never a command the owner runs.** No `sindri index build`, no hourly or nightly timer. A built-in Sindri service ticks every minute and starts a background job when a profile repo's main checkout has moved (§6). `sindri services run code-index-main-move` stays as the generic "tick now" every service has; there is no build verb | Owner, 2026-10-19. A step the owner must remember is a step that is stale by Thursday |
| CI2 | **One trigger for everything built from main, not one per feature.** Whether "the head moved" is one pure function in `Aw.Sindri.Core/Scheduling/` (`MainMoveDecision`), shared by the context map's service and this one, with a job kind each. The index's kind is `BuildCodeIndex`, its service `code-index-main-move` | Owner's F1: the context lane is building the shared trigger now. Two copies of "did main move, back off, raise needs-you" would drift |
| CI3 | **A build reads git objects at one commit, never the working tree.** The inventory is `git ls-tree` at the recorded head; contents come from `git cat-file --batch`; graphify's snapshot is written from the same filtered inventory. The index says exactly which commit it describes. Git runs hardened (§5.1); graphify's snapshot is built from the same filtered file list, not a raw `git archive` (§5.5) | The TS index reads whatever is checked out, so "uncommitted work there becomes base data". The trigger is a commit; the data should be that commit |
| CI4 | **A new package, `CodeIndex`, tier 1, shape Core · Features · Infrastructure (no Host).** It is a sibling of `Context`, separable, with no Sindri imports. Sindri composes it (`compose` edge) and reaches it through `ICodeIndex` in `Aw.CodeIndex.Core`. Core and Features import only `Aw.Kernel` (ARCH-DIR-005), so everything Sindri-side comes in through ports the package owns: `IHeavyJobLock` (adapted over `IHeavySlots`, as `SindriHeavyJobLock` does for Context), `CodeIndexSettings` (its own record, built by Sindri from the profile's `index:` section), `IBodyScrubber` (implemented in Infrastructure over `Aw.Redaction`). The job request and result records (`BuildCodeIndexRequest`, which must implement `IJob<TResult>`) and the `BuildCodeIndexService` wrapper that calls `ICodeIndex` live in `Aw.Sindri.Features`, the way Context's job records do. Infrastructure sets `AwAotCompatible=false` with its reason (Roslyn is not trim-safe; precedent `Aw.Sindri.Workflows.csproj`), and the Roslyn package gets its own pin comment (today's pin is the analyzer API, referenced only by `Aw.Analyzers`) `Context` does not reference it: S-9 adds a port to Context's Core and Sindri's Host adapts the facade to it, as it does for the labeler's model port (`deps.yaml`, "the labeler's model port") | Owner, 2026-10-19 (a block). Tier-1 packages cannot reference each other (project edges point strictly down), and the port keeps both separable |
| CI5 | **Per-repo jobs.** The service starts one `BuildCodeIndex` job per moved repo, key `index:<repo>`, so one repo's failure never holds back another's head, and the jobs list reads "code index · repo · commit" | A single job over many repos has to be Failed or Succeeded as a whole, and the trigger compares against the newest Succeeded job |
| CI6 | **Dependencies are local and pinned, installed by `setup.sh`, only checked by the build.** graphify at an exact version with an `--exclude-newer` date, the Ollama embedding model by name and digest; both pins in `context/code-index.lock` (same shape and check script style as `context/onyx.lock`). The service never installs, pulls or downloads; a missing piece makes that layer `unavailable` with a fix line | The owner never runs `index setup` either (CI1), and a background job that downloads is a supply-chain hole |
| CI7 | **Layers fail alone.** Structure, clones and deps are in-process and always built. Graph and embeddings are optional: when either is missing, pinned wrongly, over its cap or fails, its layer is `unavailable` with a code and reason and the job still Succeeds. Only a failure that leaves no usable index (git unreadable, store damaged beyond rebuild) fails the job | Same rule as the TS index, and the callers/callees consumers need structure and the call edges, not vectors |
| CI8 | **Reads never wait for a build.** A build writes a new database file under a new name and publishes it by atomically replacing a small pointer file (§4.1); readers open the file the pointer names read-only, and never see a path reused under them, so they see the whole old index or the whole new one. The index keeps one commit per repo (§4.3) | A build of a big repo takes minutes under the heavy-job lock; the station's gather step must not stall on it |
| CI9 | **Repo text is untrusted, always.** Names, signatures, paths and bodies are fenced and escaped on the way out; bodies are scrubbed (`Aw.Redaction`) before they are stored; deny globs are enforced at ingest and again at read. Agent sessions read through the same repo scope as the context lenses | CTX §7 egress rules; the TS index's `<untrusted>` fence |
| CI10 | **No byte or state compatibility with the TS index.** New schema, new files under a new directory, no migration; the TS index keeps running until the switch and the two never share a file | AGENTS.md "C# build"; the owner's rule for every port |
| CI12 | **Roslyn parses untrusted text inside the host, so it is bounded.** Every syntax walker checks `RuntimeHelpers.EnsureSufficientExecutionStack()` and a depth cap, a file's parse has a time limit, and the parse runs on a thread with a fixed large stack; a file that trips any of them is stored as `parsed = 0` and counted `too-deep`, never an error. A stack overflow cannot be caught in .NET and a thread cannot be aborted, so the build writes the blob id it is about to parse to `<repo>.inflight` before each file and deletes it after; a start that finds one appends that blob id to the durable `<repo>.quarantine` list (fsynced, kept until the blob is no longer in the head, not cleared by a parser-id change, capped at 100 entries with a count in the structure layer's note) and parses it as `parsed = 0`, `too-deep`, so two hostile files in one repo, a build that never publishes, and an out-of-memory kill that was not the file's fault all still converge instead of crash-looping on the 1-minute tick. `<repo>.building` is reset at the start of every build. **Gate:** the CI-4 PR does not merge unless the hostile-parse fixtures (CI-0c: a 100,000-term `a+b+...`, deeply nested parentheses and generics) pass under these guards in the host; if they cannot, a child-worker unit CI-4b is added to the plan and CI-6 waits for it (the owner is told in the PR) | Security review of round 1: one hostile file must not stop `serve` or crash-loop on the 1-minute tick |
| CI13 | **The context map and the code index publish together.** Both still build on their own when trunk moves. A new context map version becomes the current one only once the code index for the same commit is built, with a bounded wait (`context.publish.waitForIndexMinutes`, default 15); after the wait the map goes ahead and its code rows read stale with both commits named. One pure decision, `MapPublishDecision`, beside `MainMoveDecision`, decides; staleness is a read, never a stored state (§6.4) | Owner, 2026-10-20. A scoping answer that mixes a map of Tuesday's commit with callers of Monday's is worse than one that is late or says so |
| CI11 | **Shape signals, the pre-commit overlay, the spool and `evolve`'s identifier gate are not part of this block.** They are consumers of an index and are their own work; the block exposes `Similar` (§7) so they can be rebuilt on it | They are record-only today and bound to the frozen hook contract (spec §4c). Mixing them in would make this spec the size of the TS package |

## 2. Goals, non-goals, anti-goals

**Goals**
- Callers and callees of a symbol, and the symbols a file defines, answered from an index of the commit that is on the
  default branch in each registered repo, with the commit named in the answer.
- Built with no owner action: first build after a repo is registered and approved, then a new build per new main commit.
- Visible: each build is a ledger-recorded job in the app's background-jobs list; each repo's index shows its commit, age
  and layer states in Health and in the Context screens.
- Answers from one commit: a context map version is published only once the index for its commit is built, with a
  bounded wait, and says both commits when it could not wait (CI13, §6.4).
- Offline. Code goes only to a process on this machine; graphify has no network; Ollama is on loopback.
- Honest about confidence: a call edge says whether it came from syntax or from graphify, and how sure it is. "Unknown
  callers" is never shown as "no callers".
- Sized for the owner's repos: a first build of a 200 MB tracked tree finishes in the background without holding more than
  one heavy-job slot, and a build after one merge touches only the changed files.

**Targets** (measured and reported, not gated; a miss goes to the owner, nothing happens automatically)

| Measure | Target |
|---|---|
| First build of this repo's C# (structure layer) | under 10 minutes, one heavy slot |
| Build after one merge (structure) | under 1 minute |
| A quiet 1-minute tick | under 200 ms per repo, no parsing |
| Time from a commit on main to a published index | under 2 minutes after the tick plus the build |
| Call sites left unresolved / left ambiguous | shown on Health per repo; no cut-off until the quality probe has numbers |
| `callers` / `callees` precision and recall (50 hand-marked pairs) | reported from CI-4 on; the owner decides from them (Q3) |

**Non-goals**
- Semantic call resolution through the compiler (Roslyn `SemanticModel`, `dotnet build` of the target repo). The build never
  restores, builds or runs the indexed repo's code (§9, anti-goals).
- A history of past indexes: one commit per repo (§4.3). An as-of read of an older map version is answered with the current
  index, marked `stale`, never silently (§7.3).
- Shape signals, the pre-commit overlay, the spool, evolve's identifier gate (CI11); the bare mirror; `sindri repo onboard`.
- A search UI of its own. The surfaces are the existing ones (§10).
- Cross-repo callers. Edges stay inside one repo; a call into another repo's package is a dependency row, not an edge.

**Anti-goals** (a failure even if it works)
- A build that runs because the owner typed something, or one that never runs because the owner didn't.
- Any request that leaves the machine: a non-loopback embedding URL, a cloud model, graphify with a network, a redirect followed.
- A symbol body, path or name in a ledger payload, a log line, a job progress line or an error message. Counts, shas, repo
  names and codes only.
- A build that changes the indexed repo: no checkout, no `git fetch`, no `git gc`, no file written under the repo.
- A build that starts from a branch the default branch does not contain.
- Two builds of one repo at once, or a build that holds a heavy-job slot while it waits for another.

## 3. Terms

- **Registered repo:** a repo in the approved profile, with its main checkout path (`repos[*].path`).
- **Head:** the commit `HEAD` of that checkout names. **Default branch:** the first of `origin/HEAD`'s target, `main`,
  `master` that exists as a local ref (no fetch). A head is **on main** when it is the default branch's tip or one of its ancestors.
- **The shared trigger:** CI2 and §6.
- **Layer:** one kind of data in an index: `structure`, `clones`, `deps` (always), `graph`, `embeddings` (optional).
- **Symbol:** a function, method, constructor, property accessor, local function, lambda that is assigned a name, class,
  record, struct, interface or enum, with a stable **ref**: `<repo>:<path>#<qualified name>[/<arity>][@<h>]`, where `@<h>` is
  the first 4 hex digits of the hash of the parameter-type text and separates same-name overloads in one file (equal
  hashes in one file take `-2`, `-3` by order). A ref survives edits to other overloads; it does not survive a signature
  change, and consumers must not store refs across commits.
- **Edge:** a directed `calls` or `inherits` relation (imports are the deps layer; containment is the qualified name) between two symbols or a symbol and an external
  name, with a **basis** (`syntax` or `graphify`) and a **confidence** (`certain`, `likely`, `ambiguous`, `unresolved`).
- **Build report:** the counts and layer states a build returns; the job's result.

## 4. What is stored

### 4.1 Where

One SQLite file per repo, `$AW_SINDRI_HOME/code-index/<repo>.db` (mode 0600, directory 0700), opened through
`Aw.Kernel.Data` with `Microsoft.Data.Sqlite`, foreign keys on; the build file is written in WAL (the factory forces it) and
converted before publishing, below. A build writes a new file `<repo>.<ulid>.db`, copying the
current one first when it is an incremental build, and publishes it by writing the new name to `<repo>.current.tmp` and
renaming that over the pointer file `<repo>.current` (a one-line text file, the only thing ever replaced; this also works
where a database file cannot be renamed over while open). An incremental build runs `PRAGMA quick_check(1)` on the file it is about to copy; a failure there is `SND-CODEIDX-010` and the build starts from scratch. Before publishing, the build runs `PRAGMA quick_check(1)`,
`wal_checkpoint(TRUNCATE)`, `journal_mode=DELETE`, closes, and deletes any `-wal` and `-shm` beside it, so a published file
is immutable and has no sidecars. Readers read the pointer, then open that file `mode=ro`; a missing pointer is "no index".
Superseded files and leftovers matching exactly `<repo>\.[0-9A-HJKMNP-TV-Z]{26}\.db(-wal|-shm)?` (and no looser glob, since a repo may be called `a.b`) are deleted when no pointer names them, they are not the build file the running build
recorded in `<repo>.building`, and they are older than 10 minutes (readers hold a file only for one call), at the start and end of each build; the heavy lock means no two builds of a repo run. Files are
created 0600 explicitly. A repo name is `[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}` (no leading dot; refused otherwise, `SND-CODEIDX-001`) before it goes into any file name,
and the pointer is opened without following symlinks and must match a ULID, else the index is `damaged`. An incremental build and each
embed chunk copy the current file once (the cost of immutable files; a large index is hundreds of MB).
The index is derived data: a different `user_version` is rebuilt from scratch, never migrated (CI10).

### 4.2 Schema (version 1)

```sql
CREATE TABLE meta   (key TEXT PRIMARY KEY, value TEXT NOT NULL);         -- commit, default_branch, built_at, schema, parser_id, lock_sha
CREATE TABLE layers (layer TEXT PRIMARY KEY, stamp TEXT NOT NULL, status TEXT NOT NULL,      -- ok | unavailable | disabled | pending
                     code TEXT NULL, detail TEXT NOT NULL, built_at TEXT NOT NULL);
CREATE TABLE files  (path TEXT PRIMARY KEY, blob TEXT NOT NULL, size INTEGER NOT NULL,       -- blob: the git object id, so "changed" costs no hashing
                     lang TEXT NOT NULL, parsed INTEGER NOT NULL);                            -- parsed: 0 when only graphify or deps read it
CREATE TABLE symbols (
  id INTEGER PRIMARY KEY, file TEXT NOT NULL REFERENCES files(path) ON DELETE CASCADE,
  ref TEXT NOT NULL UNIQUE, name TEXT NOT NULL, qualified TEXT NOT NULL, kind TEXT NOT NULL, arity INTEGER NOT NULL,
  start_line INTEGER NOT NULL, end_line INTEGER NOT NULL, exported INTEGER NOT NULL, utility INTEGER NOT NULL,
  signature TEXT NOT NULL,                                                            -- scrubbed and cut at 2,000 characters (raised from 400 per owner q93, 2026-10-23)
  shape_hash TEXT NOT NULL, token_count INTEGER NOT NULL, complexity INTEGER NOT NULL,
  minhash BLOB NULL);                                                                         -- null under 30 tokens; such symbols have no clone rows (a tiny getter would make a huge exact-clone group)
CREATE INDEX symbols_name ON symbols(name); CREATE INDEX symbols_file ON symbols(file); CREATE INDEX symbols_shape ON symbols(shape_hash);
CREATE TABLE edges (
  src INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
  dst INTEGER NULL REFERENCES symbols(id) ON DELETE SET NULL,                                 -- null: the callee is not in this repo or not resolved
  dst_name TEXT NOT NULL,                                                                     -- always kept, so an unresolved call still reads
  kind TEXT NOT NULL, basis TEXT NOT NULL, confidence TEXT NOT NULL, line INTEGER NOT NULL,
  candidates INTEGER NOT NULL);                                                               -- how many symbols the name matched, kept even when only 8 edges are
CREATE UNIQUE INDEX edges_uq ON edges(src, dst_name, kind, basis, line, IFNULL(dst, 0));   -- NULL dst included; writes are INSERT OR IGNORE (two identical calls on one line are one edge)
CREATE TABLE call_sites (                                                                      -- raw syntax calls, so edges can be re-resolved without re-parsing
  src INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE, kind TEXT NOT NULL,         -- calls | inherits
  name TEXT NOT NULL, arity INTEGER NOT NULL, receiver TEXT NOT NULL, line INTEGER NOT NULL);                                              -- receiver: this | base | type:<name> | other
CREATE INDEX call_sites_name ON call_sites(name);
CREATE INDEX edges_src ON edges(src); CREATE INDEX edges_dst ON edges(dst); CREATE INDEX edges_name ON edges(dst_name);
CREATE TABLE bands  (key TEXT NOT NULL, symbol_id INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
                      PRIMARY KEY (key, symbol_id));                                          -- 64 hashes, 16 bands of 4 rows; near clone: estimated Jaccard >= 0.8, at least 30 tokens
CREATE INDEX bands_key ON bands(key);
CREATE TABLE deps   (manifest TEXT NOT NULL, name TEXT NOT NULL, version TEXT NOT NULL, kind TEXT NOT NULL, tags TEXT NOT NULL,
                     PRIMARY KEY (manifest, name));                                          -- package.json, *.csproj, Directory.Packages.props
CREATE TABLE bodies (symbol_id INTEGER PRIMARY KEY REFERENCES symbols(id) ON DELETE CASCADE, text TEXT NOT NULL);   -- scrubbed; dropped once embedded
CREATE TABLE embeddings (symbol_id INTEGER PRIMARY KEY REFERENCES symbols(id) ON DELETE CASCADE, model TEXT NOT NULL, vector BLOB NOT NULL);
CREATE TABLE graph_nodes (id TEXT PRIMARY KEY, file TEXT NULL, name TEXT NULL, line INTEGER NULL, community INTEGER NULL);
CREATE TABLE graph_edges (src TEXT NOT NULL, dst TEXT NOT NULL, relation TEXT NOT NULL, confidence TEXT NOT NULL);
```

The `edges` rows with `basis = syntax` are rebuilt from `call_sites` after every structure write, and the `basis = graphify` rows are added in the same build right after the graph layer runs (§5.7 order: structure, graph, resolve and merge), from `graph_nodes` and `graph_edges`, which are emptied at the start of every build and refilled by that build's graphify run; when the graph layer is not `ok` they stay empty and no stale graph edge is ever served (one set-based pass over all call sites, so a callee that
moved, appeared or vanished in a changed file corrects the edges of callers in files that were not re-parsed); an edge
whose `dst` was set null by a cascade is re-resolved, never left `certain`. `bodies` is a separate table so no read path loads text by accident, and so the text can be dropped once the embeddings layer
has used it (§15 Q7). `graph_nodes` and `graph_edges` keep graphify's output as it came, trimmed to the fields below; the
`edges` table holds the merged, resolved view that callers/callees read (§5.4).

### 4.3 One commit

`meta.commit` is the 40-hex commit the index describes. A build of a new head replaces the index; it does not keep the old
one. So an as-of read (a map version's commit that is not the index's) gets the current index with `commit` in the answer and
`stale: true` when they differ; the context lane decides how to label that (§7.3). Keeping several commits is a later
change to §4.2, not a reason to design for it now. The published file's commit is also the only place a recorded head is
read from (§6.2), never a job result.

## 5. Components and data flow

```mermaid
flowchart LR
  subgraph Sindri["Sindri (tier 3)"]
    SV["code-index-main-move<br/>service, every 1 min"]
    MM["MainMoveDecision<br/>(Core, pure, shared)"]
    JR["job runtime<br/>BuildCodeIndex, key index:repo"]
    LG[("ledger<br/>job.* and codeindex.built")]
    UI["background-jobs list<br/>Health, Context screens"]
    AD["Host adapter<br/>ICodeIndex to Context's port"]
  end
  subgraph CI["CodeIndex (tier 1)"]
    FA["ICodeIndex facade"]
    BS["Build slice"]
    SN["RepoSnapshot<br/>git ls-tree, cat-file"]
    PA["Parsers<br/>Roslyn C#, tokenizer"]
    GR["GraphRunner<br/>graphify, sandboxed"]
    EM["Embedder<br/>Ollama, loopback"]
    ST[("code-index/repo.db")]
  end
  CX["Context engine<br/>code lens, file map, symbol shape"]
  HL["heavy-job lock<br/>IHeavySlots"]
  SV -->|"heads, recorded heads, jobs"| MM
  SV -->|"start BuildCodeIndex repo"| JR
  JR --> BS
  BS --> HL
  BS --> SN --> PA --> ST
  BS --> GR --> ST
  BS --> EM --> ST
  JR -->|"events"| LG --> UI
  FA -->|"read-only, WAL"| ST
  AD --> FA
  CX -->|"ICodeSymbolSource"| AD
```

### 5.1 The snapshot (`RepoSnapshot`, Infrastructure)

For repo path P and commit C: `git -C P ls-tree -r -z --full-tree C` gives mode, type, blob id, size (`--long`) and path. The
snapshot keeps regular files only (mode 100644 and 100755); symlinks (120000) and submodules (160000) are skipped and
counted. A path that matches a deny glob (the built-in secret globs, then the profile's `index.denyPaths`, then the repo's;
case-insensitive; additions only, as in the TS index) is skipped as `denied`, a file over `index.maxFileKB` as `too-large`.
The sum of the kept code files' sizes (docs, assets and lockfiles do not count, and the number of files has no cap, removed per owner q93 (2026-10-23)) over `index.maxTotalMB` stops the build with `SND-CODEIDX-003` before any content is read. Contents
come from one long-lived `git cat-file --batch` process per build, fed blob ids; no file is read from the working tree, so
there is no symlink or race to defend against, and a path swapped after the commit does not matter. The git calls clear the
environment variables `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES`, `GIT_NAMESPACE`, `GIT_CEILING_DIRECTORIES`, `GIT_COMMON_DIR` and every `GIT_CONFIG_*` the way `Aw.Context.Infrastructure.Git.GitCli` does (that class is
`internal` to Context, so CodeIndex has its own thin copy with a shared contract test), and add: `GIT_OPTIONAL_LOCKS=0` (no index lock
in the owner's checkout), `--no-replace-objects` (a replace ref in the repo must not swap what a blob id means),
`-c protocol.allow=never` plus `GIT_NO_LAZY_FETCH=1` where git knows it (git 2.44 and later; this box has 2.43, which ignores it, so `protocol.allow=never` is the guard and a contract test with a promisor-repo fixture proves a missing blob is a `missing` skip and a layer note, never a network fetch),
`GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL=/dev/null`, `-c core.fsmonitor=false -c core.hooksPath=/dev/null`, and never
`safe.directory=*`; a repo git refuses as unsafe is `SND-CODEIDX-002`. Roslyn's depth and time guards are CI12. Every path from `ls-tree` is validated at inventory: a component that is empty, `..`, `.git` (any case), contains a backslash or NUL, or an absolute path is skipped as `unsafe-path`, and every write of the graphify snapshot asserts the resolved path is under the snapshot root. Fixed ceilings live in `CodeIndexRules`: 100,000 symbols and 500,000 call sites per file (a file past one is left unparsed; raised from 5,000 and 20,000 per owner q93, 2026-10-23). The 100,000-file ceiling, the 2 GB index file and the 2-hour build that this spec once listed are gone (removed per owner q93 (2026-10-23); the last two were never enforced).

### 5.2 Incremental planning (`BuildPlanner`, Core, pure)

Inputs: the snapshot's `(path, blob)` list, the live index's `files` rows, the **structure stamp**, and each layer's
stamp. Output: files to parse, files to drop, and per layer whether it is current. The structure stamp is
`parser_id + sha8(utilityGlobs, scrub.extraPatterns)`: a new parser version, new utility globs or a new scrub pattern
re-parses everything, because they change stored rows. A file is re-parsed when its blob id differs. Call edges are not planned here: they are re-resolved from `call_sites` for the whole repo after every structure write (§4.2),
so an unchanged caller is always right. The graph layer's stamp includes the commit: graphify runs on the whole snapshot
each build, so "touches only the changed files" holds for structure, clones and deps, and the graph layer is the cost of every
build. The planner is a pure
function with a table of cases; the TS `writeStructure` has the same idea, written here as data in and plan out.

### 5.3 Parsers (`ISymbolParser`, one per language)

- **`CSharpParser`** (Infrastructure, Roslyn `Microsoft.CodeAnalysis.CSharp`, already pinned in `Directory.Packages.props`):
  syntax only, no compilation and no references. It yields symbols with their span, signature text, exported flag
  (`public` or `protected` on a visible type), cyclomatic complexity (decision nodes plus short-circuit operators), the
  normalized shape hash (node kinds, identifiers and literals blanked), the token list for MinHash, base types (as `inherits` edges), and call sites
  (`InvocationExpression`, `ObjectCreationExpression`, `ConditionalAccess`, method group conversions) as
  `(name, arity, receiver, line)` into `call_sites`.
- **`TokenParser`** (Core, pure): for any other source file (`ts tsx js mjs py go rs java kt rb php c cc cpp h swift`) it
  yields no symbols itself. Those files' symbols and edges come from graphify (§5.5), because a lexical symbol finder
  would be worse than the tree-sitter one graphify already runs. The tokenizer is still used for the clones layer on the
  symbols graphify names, using their line spans.
- A parser is chosen by file extension through a table in Core. For a language only graphify covers, callers and callees
  come from graphify's call edges (§5.5; the CI-0d spike measured TypeScript and Python, §15 Q1); a graphify language the spike did not measure is `unavailable` for callers and callees, and says so, never an empty list. The gate is a `measured` flag per language in the Core parser table: the merge holds back the `calls` edges of an unmeasured language, and `coverage` has three values, `full`, `partial` and `unavailable`, each with its own test. A language joins the measured set with a fixture, its expected calls and the CI-0d pass bar. Adding a language is one parser plus one table row.

### 5.4 Resolving calls (`CallResolver`, Core, pure)

Syntax gives `(caller, name, arity, line)`. The resolver looks the name up in the repo's symbols:
exactly one candidate of that arity gives `certain` when the call is on `this`, an unqualified name in the same type, or a
type name that matches the candidate's container, and `likely` otherwise; several candidates give one `ambiguous` edge per
candidate, ranked by closeness (same file, then same namespace, then same project directory (the nearest manifest), then path order; all derivable from stored rows;
at most 8; `candidates` keeps the true count, so `Handle` with 300 matches reads "1 of 300", not a guess); none gives an `unresolved` edge with `dst = NULL`
(a call into a library, or generated code). Graphify edges (§5.5) are merged in as `basis = graphify`, never replacing a
syntax edge for the same pair. `Callers` can follow `inherits` edges from an interface or base member to its implementations when asked (`includeImplementations`), because dispatch through an interface is the common case in this repo. A reader asking for callers gets the confidence with every row and a count of unresolved
callees of the symbol, so the caller can tell "no callers" from "unknown".

### 5.5 Graph layer (`IGraphRunner`, Infrastructure)

graphify runs on a snapshot of the commit written from the filtered inventory of §5.1 (so the index's deny globs apply and
`export-ignore` does not change what is read) into a temp directory (the same shape as
`Aw.Context.Infrastructure.Extract.GraphifySnapshot`, which is `internal` to Context, so this is a copy kept honest by a shared contract test (the one deliberate difference, the index's private directory bound writable, is a named parameter of the wrapper that the test pins on both sides): every file of the filtered inventory is copied, with no file or total cap of its own (removed per owner q93 (2026-10-23)), the reserved
`graphify-out/` and `.sindri-tmp*` paths never copied), with `extract <dir> --code-only --no-viz`, a private temp and cache
directory beside the snapshot (below), a cleaned environment, a time limit, and at most 64 KB kept from each output stream. It runs
only under a sandbox that denies the network and confines writes to the snapshot and its private directory (bubblewrap on Linux,
`sandbox-exec` on macOS, absolute paths, root-owned, never from `PATH`); with none, the layer is `unavailable`
`SND-CODEIDX-007` and graphify never runs unsandboxed. `graph.json` is size-capped (`index.graph.maxMB`) and read as a regular
file through no symlink, parsed with `System.Text.Json` into a strict DTO (a wrong top-level shape is `SND-CODEIDX-008`; a malformed record is dropped and counted, below), and
treated as untrusted. Nodes with a file and line are matched to `symbols` by file and the enclosing span; an unmatched node
becomes a `symbols` row (ref as §3, `signature` empty, `end_line` = its line, no clone rows) when graphify's file is a
language no parser covers (every language but C#): a node marked `_callable_class` is stored as kind `class` (never embedded, it keeps no text), every other node as kind `graph-symbol`. Node ids and files are kept only if the file is in the snapshot's
file list; names are scrubbed and cut like signatures, and a name with a control character or a newline is malformed; a colon is allowed, since the ref is read from the right (`path:name:startLine`). `graph.json` has the byte cap only: the node and edge caps (`index.graph.maxNodes` 500,000, edges four times that) are removed per owner q93 (2026-10-23), and so is the `graph.maxNodes` key. graphify runs under a 2-hour time limit (raised from 30 minutes per owner q93); a run that times out leaves the last good graph in place and the layer `unavailable` (`timeout`). The graphify memory kill (`RLIMIT_AS`, a run-size cap) that an earlier revision of this spec described was never built and is not to be built (owner q93: build nothing new; CIX-L63). What graphify emits was measured, not guessed: the CI-0d spike ran the pinned graphify (0.9.67) on a TypeScript and a Python fixture (`tests/fixtures/code-index/graphify/`, `SPIKE.md`), and the recorded `graph.json` files are the contract fixtures CI-7's tests replay. `graph.json` is NetworkX node-link JSON: top-level `directed` (false), `multigraph` (false), `graph` (an object), `hyperedges` (an array, ignored), `nodes` and `links` (the edge list is `links`). The strict DTO requires these fields on each record, ignores any other field (the size cap still counts it), and drops a malformed record rather than failing the layer:

- **node**: `id` (string, required), `label` (string, required), `source_file` (string, required: relative to the snapshot root with `/` separators; an absolute path is cut textually, with no filesystem call, at a path-segment boundary of the snapshot root (compared against the root as given and as resolved, since graphify may report `/private/tmp` for `/tmp` on macOS) (`snap` is not a prefix of `snap2`) and made relative; a path with a `..` segment, a backslash or a NUL is malformed; a node without one, or naming a path not in the snapshot's file list, is dropped and counted), `source_location` (string `L<n>`, required, 1 <= n <= `int.MaxValue`: the start line; there is no end line), `community` (integer, optional), `file_type` (string, optional), `_callable` and `_callable_class` (booleans, optional). A wrong type on a known field, or a missing `id`, `label` or `source_location` (or one that is not `L<n>`), makes that node malformed: dropped and counted. NetworkX merges equal ids before `graph.json` is written, so a repeated `id` should not occur (two symbols whose derived ids coincide are merged inside graphify, which the layer cannot see: a precision limit that `basis = graphify` and `coverage = partial` already tell the reader about); if one does, every node with that `id` is dropped and counted; a `graph-symbol` ref is `path:name:startLine`, so two nodes cannot collide on it. There is no kind field: a node with neither `_callable` nor `_callable_class` is a file node and is not stored as a symbol; `_callable_class` is a class; the target of a `method` edge is a method; any other `_callable` node is a function. A label is the name with `()` for functions and methods (`.name()` for a method) and bare for a class or file; the stored name drops the `()` and the leading `.`.
- **edge**: `source` and `target` (strings naming node ids, required), `relation` (required: a missing one makes the edge malformed, dropped and counted; `calls`, `contains`, `method`, `imports` and `imports_from` are known, any other value is skipped, not an error), `confidence` (`EXTRACTED` or `INFERRED`; a missing or any other word is treated as `INFERRED`; stored in the `edges` vocabulary of §3 as `EXTRACTED` = `certain`, `INFERRED` = `likely`; `basis = graphify` is what marks the edge as the coarser source, so a reader weighs it by basis and confidence together), `source_file` and `source_location` (string `L<n>`; required on `calls`, where they are the call site in the caller's file, and a `calls` edge without them is malformed), `confidence_score` (number, optional). Only `calls` edges become `edges` rows: `source` is the caller and `target` the callee, both ends must be nodes that kept their file and line (an edge naming an id that is not a node, or one that was dropped, is dropped quietly and counted, not an error), and a `calls` edge whose target is a class is a construction (stored with the class as the callee). A `calls` edge whose caller is a file node (a call at module level) has no caller symbol and is dropped, counted with the others. The graph is not a multigraph, so a caller and callee pair carries one edge and one call site; later call sites of the same pair are not visible, and the layer does not claim they are: `Callers` and `Callees` rows from graphify languages carry `coverage = partial`, and an empty answer for such a language reads "none found", never "none", and the code lens, the file map, the CLI's first line, the JSON field and `GET /sindri/context/code` carry the same words (any partial row marks the whole answer partial; the marker is text, never colour alone; the CI-10a goldens and the CI-10b and S-9 accessible-name and text-fit checks include it), with `basis` shown next to a `likely` confidence so a graphify edge is not read as parser-grade. Graphify TypeScript symbols have no end line (the end equals the start), so a symbol-level `implements` for a graphify language matches by name and file, not by span. The graph is declared undirected but every edge keeps its direction (all 13 `calls` edges in the spike ran caller to callee).

Every drop is counted by reason (`droppedNodes`, `droppedEdges`, `moduleLevelCalls`, `duplicateIds`: codes and counts only, §9) in the layer `detail` and the job result. The trust ratio is taken per language, so one noisy language cannot fail the others, over nodes and over `calls` edges only: a malformed node counts in the denominator and against the ratio (it cannot be classed as symbol-bearing), an edge counts against it when it is malformed or names a missing node or a file outside the snapshot, and an edge dropped only because its node was dropped counts once, with the node; file nodes, `moduleLevelCalls`, unknown relations and known non-call relations never count, so a script-heavy repo cannot trip it. When more than a quarter of either set counts against it (and the set has at least 20 records, so one bad record cannot fail a tiny graph; a graph with no usable node left but some dropped is untrusted at any size), the graph is not trusted: the layer is `SND-CODEIDX-008`. Fewer is `ok`, with the counts shown in `sindri doctor` and the Health card ("graph ok, 3 dropped"; above 10% dropped the Health word is "graph ok, many dropped"). The edge `source_file` gets the same path normalization and the same drop-and-count handling as the node's.

On the fixtures, TypeScript's 8 known calls were all `calls` edges with a file and line at both ends (6 `EXTRACTED`, 2 `INFERRED` where the call goes through a local variable). Python resolved 3 of 5: plain-function, imported and `self.` calls, but not a method called on a local assigned from a constructor. So callers and callees for the graphify languages come from graphify, carry `basis = graphify` and the mapped confidence, and a Python answer can be incomplete. The fixture proves the shape and that call edges exist, not recall on a real repo.

The private temp and cache directory (`TMPDIR`, `XDG_CACHE_HOME`, `UV_CACHE_DIR`, `PYTHONPYCACHEPREFIX`) lives **beside** the snapshot, not inside it, and is the one other path the sandbox binds writable: inside the snapshot graphify scans it (the spike saw 98 `.pyc` files listed as not classified). Each run gets a fresh parent directory, `$AW_STATE_DIR/tmp/code-index/run-<pid>-<start>`, made with an exclusive mkdir at mode 0700, that holds only the snapshot and this directory; only those two are bound (after the mask), the directory is never reused (a stale `.pyc` cannot poison a later run), and its size is watched during the run and the run is killed at `index.graph.maxRunMB` (default 512; `SND-CODEIDX-008`), with `RLIMIT_FSIZE` in the wrapper as well so a burst write cannot outrun the poll. The parent `$AW_STATE_DIR/tmp/code-index` is created at 0700 and refused if another user owns it. It is removed on every exit path including timeout and kill; the sweep at the next start touches only own-uid entries with that prefix, follows no symlink, and skips a run whose process is still alive. Call edges become
`edges` rows with `basis = graphify`. Community ids are stored on the nodes for the day Context's cluster source reads them
(§15 Q2). The version is checked against the pin in `context/code-index.lock` before every run (`SND-CODEIDX-009`); the check runs the binary under the same wrapper and the same read-only environment bind, so graphify never runs outside the sandbox, not even for `--version`. `ProbeLayers` does not launch it: it reuses the last sandboxed check, keyed by the pin's hash in `context/code-index.lock`, the binary's path, mtime and size, and the environment's install stamp (a shim alone can stay unchanged across a package change), and runs a fresh one (bounded to 10 s) only when that key changed.

### 5.6 Embeddings layer (`IEmbedder`, Infrastructure)

`POST {url}/api/embed` through a `SocketsHttpHandler` with `UseProxy = false` and `AllowAutoRedirect = false`, on the profile's URL, which must be `127.0.0.1` or `[::1]` with no credentials (never `localhost`:
it can resolve elsewhere) and not a model whose name contains `cloud`; redirects are errors; a bad URL or model is
`SND-CODEIDX-005`. The model must be present with the pinned digest (`/api/tags`), else the layer is `unavailable`
`SND-CODEIDX-009`. Texts are `name + scrubbed signature + scrubbed body`, cut at 6,000 characters (raised from 2,000 per owner q93, 2026-10-23), in batches of 32, written to the
store as each batch returns. Embedding is its own operation (`EmbedCodeIndex`, §5.7a), outside the heavy slot: a build only stores bodies,
carries the vectors of unchanged files and records the layer `pending`, "N of M embedded" (or `ok` when none is missing, `disabled` when the profile turns it off);
the embed run then works through the pending symbols in chunks of 4,096 until none remain, so a repo is fully embedded without waiting for a HEAD move (the service starts it whenever the layer is `pending`, §6.3). The
embedding step skips `class`-like symbols, as the TS index does. A graph symbol's embed text is its source span from its line to the line before the next graph symbol in the same file (or the end of the file), scrubbed then cut; this is an approximation, since graphify gives no end line. A file whose blob cannot be read leaves its graph symbols without text, and the graph layer's detail counts those files. Nothing in the unit lane talks to Ollama (§11).

### 5.7 The build slice (`BuildCodeIndex`, Features)

1. Take a heavy slot (`IHeavySlots`, holder `sindri code-index:<repo>`), waiting up to 10 minutes; busy past that ends the job `Succeeded` with `deferred: heavy-busy` and no commit (§6.3 throttles the retry),
   never Failed: lane gates hold slots by design, and a busy box must not raise the owner's needs-you row. `SND-CODEIDX-004` is the result's code.
2. Snapshot at the requested commit (re-checked: it must still be on main; a head that left main since the tick is a
   `Succeeded` job with `skipped: not-on-main` and no change).
3. Plan, parse, write structure, call sites, clones and deps into the build file in one transaction per 200 files, reporting a
   progress line each time (`structure 1,400 of 3,100 files`; counts only).
4. Graph layer (it empties and refills the graph tables), then resolve: one set-based pass builds the syntax edges from `call_sites` and merges the graphify edges; then the embeddings layer is recorded from the counts (bodies stay stored until embedded; the build never calls the embedder). Each layer is in its own try; failure becomes layer status.
5. Write `meta`, check, convert and close the file, switch the pointer (§4.1), append `codeindex.built` (§8). `result.commit` is set
only when an index at that commit was published, else null.
Cancellation is honored between files and between batches; a cancelled build deletes its build file and leaves the live
index alone. The slot is released in a `finally`.

### 5.7a The embed slice (`EmbedCodeIndex`, Features)

No heavy slot. It embeds the repo's pending symbols (those with a stored body and no vector) in chunks of 4,096, each chunk one copy-embed-publish of the live
index, so progress survives a cancel or a crash and a chunk is the most a cancel loses; it continues until no symbol is pending, the model fails (the layer
is `unavailable` with the code, the vectors made stay) or the call is cancelled, and answers `Embedded`, `Embeddable` and the layer's state. A build and an embed chunk of the same repo
never interleave: the store takes a per-repo code-index lock (`IRepoIndexLock`, not a heavy slot) when it starts a build file and the build releases it when it is disposed, so
neither discards the other's work (a wait past 10 minutes is `SND-CODEIDX-016`, retried later). A move needs no extra work to stay differential: the build already cascades away the
symbols and vectors of changed and removed files and keeps those of unchanged files, so only changed and new symbols are pending and a move never re-embeds the repo.

## 6. The background trigger

### 6.1 The shared pure function

```csharp
// Aw.Sindri.Core/Scheduling/MainMoveDecision.cs — one decision for every kind built from main (CI2, owner F1)
public static MainMoveAction Decide(
    string? head,                  // this repo's checkout HEAD, null when it is not on the default branch
    string? recordedHead,          // the commit the kind's own stored state says it built; null: none
    bool running,                  // a job of this kind and key is Running
    IReadOnlyList<MainMoveAttempt> finishedNewestFirst,   // this key's ended jobs: Failed?, EndedAt
    DateTimeOffset now);           // -> Nothing | JobRunning | BackoffNotDue | Start
```

It is the `SyncGapDecision` pattern: a moved head (a head that differs from the recorded one, or with none recorded) with nothing running starts a job; after N failed jobs in a row the next one waits 5, 10, 20, 40, then 60
minutes from the newest failure (`MainMoveDecision.Wait` is `SyncGapDecision.Wait`); any other ending resets the count.
The context lane writes it and its tests first (owner F1); this spec uses whatever signature it lands with, and the index
adds no variant of its own. One thing is asked of that signature (round 1 finding, to settle with the context lane before
they merge): the decision is **per repo and per key**, called once per repo with that repo's recorded head and that
key's attempts, because `SyncGapDecision` is global and one repo's failures must not back off another (CI5); a test with
two repos, one in backoff, is part of its suite. If it lands set-based, the index's service calls it once per repo with a
one-entry set. Reading heads is also shared (`IRepoHeads`: `git rev-parse HEAD`, then `git merge-base
--is-ancestor HEAD <default>` against **local refs only**, no fetch, and the default branch found as `origin/HEAD`'s target,
else `main`, else `master`). A repo whose checkout is on a branch main does not contain is not in `heads`, so it is never
indexed from a feature branch, and the Health row says why ("checkout is not on main").

### 6.2 The service

`code-index-main-move` is an `IScheduledService` registered as a `BuiltInService` with `Cadence` 1 minute (a profile
`services:` entry of the same name overrides the cadence, as for `context-sync-gaps`). One tick:

1. Scope: no approved profile is a quiet run (not a failed run).
2. Heads: `IRepoHeads` for the profile's repos (a few `git` calls per repo; nothing is built or parsed on a quiet tick).
3. Recorded heads: **`ICodeIndex.Heads` is authoritative** (the pointer and file header, no git: a few small reads); `view_code_index` is for display only, so a stalled projector can never cause a rebuild of the same commit. A missing, damaged or `user_version`-mismatched index counts as no recorded head, so a deleted file rebuilds. Attempts: the newest 10 ended jobs of the key per repo (a bounded read by key and kind, never the whole list), fed to `Decide` for backoff and to `IndexContinuationDecision` (§6.3).
4. For each repo: `MainMoveDecision.Decide(...)`; on `Start`, run the `BuildCodeIndexRequest(repo, commit)` command chain as
   `service:code-index-main-move` (gate, audit, authorization, `command.handled`; never the runner directly). `SND-JOB-409`
   with `runningJob` is a quiet run; another refusal fails the run.
5. `BackoffNotDue` is a quiet run (no per-minute failure record while a repo waits out its backoff). After **3 failed jobs in a
   row** for any repo, the run is a failed service run and the needs-you row `failure:service:code-index-main-move`
   appears (existing behavior for any service), once, not again on every tick. A `deferred: heavy-busy` job is not a failure.

### 6.3 Pending layers and first builds

Embedding is not a build: a repo whose embeddings layer is `pending` gets an `EmbedCodeIndex` job (key `embed:<repo>`, no heavy slot, §5.7a) that runs until the layer is ok, with no HEAD move needed. The built-in `code-index-embed` service (every 1 minute) starts it from `ICodeIndex.Status` and the repo's `embed:` jobs through the pure `EmbedDecision` in `Aw.Sindri.Core/Scheduling`: a running job holds the start, a layer that is not `pending` starts nothing, then the wait after the newest ended job (failures in a row: the 5, 10, 20, 40, 60 minute backoff; `SND-CODEIDX-016` or `013`, a busy or unbuilt index, ends the job `later`, not failed, and waits 2 minutes; a job that ended `pending` with no more vectors than the job before waits 10 minutes; a job that embedded more is followed at once). A build never embeds, so nothing here continues a build: `code-index-main-move` starts a build only for a moved head, a retry after failures (the backoff) or a layers-only build, and a `deferred: heavy-busy` build (which publishes nothing and writes no `codeindex.built`, so `Decide` alone would start it every minute) holds the repo's next start for 10 minutes. (A large repo's first embeddings take many 4,096-symbol chunks; Health shows the percentage.) A layer that was `unavailable` for a fixable cause
(`SND-CODEIDX-007` to `009`: graphify, sandbox, Ollama or pin missing) is probed at most every 10 minutes (`ICodeIndex.ProbeLayers`:
the same lock check the build does, no work) and, when it has become available, starts a layers-only build, so
fixing the box with `./setup.sh` is enough; nobody has to wait for the next commit to main. A repo registered and approved for the first time has no recorded head, so the next tick starts its first
build; the owner does nothing.

### 6.4 Publishing together with the context map (owner decision, 2026-10-20, revision 4)

**The decision.** The context map and the code index still build on their own when trunk moves (CI1, CI2 stand: two
services, two jobs, neither waits for the other to start). What changes is when a new map version becomes the current
one: **only once the code index for the same commit is built, with a bounded wait; after the wait the map goes ahead and
its code rows read stale with both commits named.** So an answer comes from one commit, or says plainly that it does
not. The map side lives in the context retrieval spec (§8.7a: where a held version is stored, what a human can still do,
the error code); this section is the decision itself, and the one place its inputs, outputs, bound and words are written.

**What "index built for commit C" means.** `ICodeIndex.Heads` (stored state only: the pointer and the file header, no
git, no live probe; §6.2 step 3) names C for that repo. That is the only test. A published index always has its
structure, clones and deps layers (CI7: a build with no usable structure fails and publishes nothing), and its graph
layer is already decided, `ok` or `unavailable` or `disabled`, because the pointer switches after the graph step (§5.7
step 4 then 5). So **a graph layer that is unavailable, or embeddings that are `pending`, never hold the map**; only a
missing, damaged or other-commit index does. The index keeps one commit per repo (§4.3), so "built for C" is true only
while C is the newest. Once the index has moved past a held map's commit that map can never be `Built`; it is not waited on for itself, it waits only for the newer map that replaces it (a refresh is started by the next tick), and the bound still ends it (below, Supersede).

**The gate (the map side holds a draft, not a version).** The refresh job (F1) still builds the draft and lets the cleanup
agent settle it, but it no longer promotes unconditionally: it asks `MapPublishDecision` (below). On `Publish` it
promotes as today. On `Wait` it ends `Succeeded` with `held: true` and stores a **hold** next to the draft in `context.db`
(retrieval spec §8.7a): the draft's version, each mapped repo's commit **as the build read it** (the build's own report, `MapBuildReport.Heads`, a new
field: the pre-build `MapHeads` read can differ when trunk moves in between) with whether it was on the default
branch, and `since` (when the hold began, kept across supersedes, below). The held version is an ordinary `draft`: no
new status, no change to `MapVersionStatus`, and the previous version stays `current` and keeps answering. The
`context-map-refresh` service's tick (every minute, already there) re-asks the decision while a hold exists; the answer
`Publish` or `Drop` starts a small job `PublishContextMap` (key `map-publish:<repo>`, started as
`service:context-map-refresh` through the command chain, like the refresh), which asks again under its own grant, runs
the cleanup pass once more (a scoping proposal may have joined the draft while it waited; with nothing weak it spends no
model call; the cleanup rules and who decides are F1's, so a proposal joining a held draft gets no more authority than it has when the refresh re-applies it today) and promotes through the engine. **`Promote` and `CleanDraft` take an `expectedVersion`** (F1P changes
`IContextMap`): the engine refuses with 046 (reworded for a hold) when the latest draft is not that number, so a refresh or a human
`map build` that replaced the draft between the check and the promote can never publish a draft the gate did not see. The two job kinds follow one rule: **the tick starts no `RefreshContextMap` while a `PublishContextMap` runs, and no second `PublishContextMap` while one runs** (the move check's `running` covers both kinds); a due publish **may** start while a refresh runs, and is safe because of `expectedVersion` (it wins, or the refresh's build has replaced the draft and it ends 190). A start refused with the runner's 409 is quiet. Nothing here is a timer in memory: the hold row, the clock and the stored
index heads are all the decision needs.

**Who decides: one pure function in `Aw.Sindri.Core/Scheduling/`, beside `MainMoveDecision`.**

```csharp
// MapPublishDecision.cs: when a refreshed map becomes the current one (owner decision 2026-10-20)
public static MapPublishAction Decide(
    MapHoldState hold,               // version, since, and per mapped repo: the commit the build read and whether it was on the default branch (Core record; Features maps it from Context's MapHold and the version's heads)
    int? draftVersion,               // the latest draft's number now; null: none
    int? currentVersion,             // the current version's number; null: no map has been promoted yet
    IReadOnlyDictionary<string, IndexOutlookDetail> outlooks,   // by repo name, for the hold's repos
    TimeSpan wait,                   // context.publish.waitForIndexMinutes (0: do not wait)
    DateTimeOffset now);             // -> Wait(until, waitingOn) | Publish(reason, staleRepos) | Drop(reason)

public enum IndexOutlook { Built, Coming, Backoff, Broken, Absent }

// IndexOutlookRules.cs (pure): the classifier's one input record, per repo; deferred and failed come from the BuildCodeIndex job's result
public static IndexOutlookDetail Classify(IndexOutlookInput input, DateTimeOffset now);
public sealed record IndexOutlookDetail(IndexOutlook Outlook, RepoWait Word, DateTimeOffset? RetryAt);
public enum RepoWait { None, Queued, Building, WaitingForSlot, Retrying, NewerIndex, Damaged, NotIndexed }   // the owner's per-repo word (below)
public sealed record IndexOutlookInput(string? IndexCommit, string HoldHead, bool HoldOnDefaultBranch, string? LiveHead, bool InScope, bool ServiceActive,
    bool BuildRunning, bool NeverBuilt, IReadOnlyList<IndexAttempt> FinishedNewestFirst);   // IndexAttempt(bool Failed, bool Deferred, DateTimeOffset EndedAt): its own record, SyncAttempt (shared with SyncGapDecision) is untouched
// NeverBuilt: no index has ever been published for the repo (no file, and no Succeeded job of the key that published a commit)
// IndexOutlookRules.FailuresToBroken = 3, the same number as the code index service's alarm; the rules count Failed over IndexAttempt themselves

// Sindri Core port, implemented in Host by CI-9 (F1P-b2 registers NoIndexOutlooks: every repo Absent)
ValueTask<Result<IReadOnlyDictionary<string, IndexOutlookDetail>>> Read(MapHoldState hold, CancellationToken ct);
ValueTask<Result<IReadOnlyDictionary<string, string>>> IndexCommits(IReadOnlyList<string> repos, CancellationToken ct);   // the live comparison for Health, status and doctor; NoIndexOutlooks returns none
```
F1P-b1 owns `IndexOutlook`, `IndexOutlookDetail`, `IndexOutlookInput`, `IndexOutlookRules`, `IIndexOutlooks` and `NoIndexOutlooks` with their table of cases; CI-9 owns the Host adapter that fills the input (and its own table-of-cases test: job results to `Failed` and `Deferred`, service state to `ServiceActive`).

`IndexOutlook` per repo is classified by `IndexOutlookRules.Classify` (also pure, `Aw.Sindri.Core/Scheduling/`), from what
the index service already stores: the index's commit from `ICodeIndex.Heads`, the hold's head, the repo's live head, whether the
`code-index-main-move` service is running (not paused) and the repo is in its scope, whether a `BuildCodeIndex` job of key
`index:<repo>` is running, and that key's newest ended jobs (failed count in a row from `MainMoveDecision.FailedInARow`,
the `deferred` flag) with the clock:

| Outlook | When | Meaning for the map |
|---|---|---|
| `Built` | (`Classify` takes the rows in this order: Built, Absent, Broken, then Backoff, then Coming as the default, so the table is total) the index commit equals the hold's head (checked before every other row, so a paused service with the index already at the commit is `Built`, not `Absent`) | ready |
| `Coming` | the default for a repo that is not `Built`, `Absent`, `Broken` or `Backoff`: a build is running (word `Building`) or due (`Queued`); the index is at another commit with nothing due yet, which the next tick fixes (`NewerIndex` when the repo's head moved past the hold's); a damaged file (`Damaged`) | wait |
| `Backoff` | not built, the newest job failed (1 or 2 in a row; word `Retrying`, `RetryAt` from the backoff) or ended `deferred: heavy-busy` (word `WaitingForSlot`), a retry is due later | wait, bounded like `Coming` |
| `Broken` | not built, 3 failed jobs in a row (the service's needs-you row is up). A damaged file reads as "no index" from `Heads` and so as `Coming`: the next tick rebuilds it, and the bound applies | do not wait |
| `Absent` | no index will be built, or none exists to wait for, for this repo: **its first build is under way (`NeverBuilt`, word `NotIndexed`; checked before `Broken`; waiting 15 minutes for a large repo's first index would hold every other repo's fresh map for nothing, and its rows read `index-not-built`)**, `ICodeIndex` is not composed (before CI-9), the service is paused by the owner (a deliberate stop is not waited on; a pause during a hold turns the repo `Absent` on the next tick), the repo is not in its scope, or the repo's head was not on the default branch when the map read it (the index never builds from a feature branch, §6.1) | not part of the gate |

**A map that was passed still waits.** An index at another commit than the hold's, with the repo's head moved on, is `Coming`, not a separate outlook: the replacing refresh is started by the next tick (at most a minute later) and takes the hold over, so the wait costs nothing, and if no replacement comes (the refresh is in backoff) the bound still publishes. Trunk that moves faster than the index builds makes `TimedOut` the normal outcome; that is the trade of consistency for freshness the owner chose, and the publish tally in `context map status` shows it.

`Decide`, in this order (the first line that applies):

1. `Drop(AlreadyCurrent)` when `currentVersion` is the hold's version (a defensive guard: every promote deletes the hold in its own transaction, so a live hold row cannot name the current version; the case table covers it as a guard, not as a normal path; tested first because that promote leaves no draft);
   then `Drop(Gone)` when `draftVersion` is not the hold's version (a rebuild replaced the draft, or a human cleared it). A repo of the hold with no entry in `outlooks` is read as `Absent`.
2. Take out the `Absent` repos. **`Publish(IndexReady)`** when every remaining repo is `Built`; **`Publish(NoIndex)`**
   when none remain (the old behavior until CI-9 composes the index; only the new events and fields differ).
3. **`Publish(IndexBroken, staleRepos)`** when any remaining repo is `Broken`: waiting on an index that has given up
   buys nothing, and the code index already told the owner (the needs-you row).
4. **`Publish(FirstMap)`** when `currentVersion` is null: the first map of a repo is worth more than a matching one, and its code rows are
   `index-not-built` or stale anyway.
5. **`Publish(WaitOff)`** when `wait` is 0.
6. **`Publish(TimedOut, staleRepos)`** when `now` is at or past `hold.Since + wait` (`Since` is clamped to at most `now` first, so a clock stepped backwards cannot lengthen the wait).
7. Otherwise **`Wait(until: hold.Since + wait, waitingOn: the count of repos not Built)`**.

`staleRepos` is the repos not `Built` at the moment of publishing (a count in events, names in the answer text). The function
is clock-in, nothing-out, and has a table of cases (§11).

**The wait bound.** Profile key **`context.publish.waitForIndexMinutes`**, an integer from 0 to 240, default **15** (0 turns the
wait off: the map promotes when it is ready, as before this decision). It is counted from `hold.Since`, which is the moment the
cleaned draft was ready to promote (the refresh job's own `now`), **not** from the head move: the index has been building since the
move, and the owner's wait starts only when the map has nothing left to do but wait. 15 minutes covers an incremental
build with graph (under 2 minutes plus the tick) and a deferred retry (10-minute suppress, §6.3) once. **A repo's first index is never
waited on** (`NeverBuilt` is `Absent`): its rows read `index-not-built` until it lands. What does time out on purpose is a later
build that is slow (a big incremental build, a busy box): the owner reads that as stale rows for a while, not as a stuck map. The key lives in the context section of the profile
(retrieval spec §8.8), not in `index:`, because it is the map's rule; `ContextSettings.Publish`, its range check and `docs/csharp/sindri-profile.md` land together in F1P-a (§14), and a value outside 0 to 240 is `SND-CONTEXT-001` with the key path.

**The tick, in order** (`ContextMapRefreshService`, one service, two job kinds; no step ends the tick for the later ones): (1) scope, as today; (2) read `Held`; if there is a hold,
read the outlooks and ask `MapPublishDecision`; a `Publish` or `Drop` **starts** `PublishContextMap` only when none runs and the publish backoff is not holding (5, 10, 20, 40, 60 minutes over the newest `PublishContextMap` failures; other than 190, a failed publish leaves the hold); otherwise it falls through; (3) the move check as today, over the
`MappedHeads` below, with the refresh backoff over `RefreshContextMap` jobs only; (4) the alarm, the only step that merges the kinds: the newest 8 ended jobs of **both kinds** (two `jobs.List` reads by kind, merged by end time and capped at 8) give the failure count (`MainMoveDecision.FailedInARow`) and the code shown is
the newest failure's; three in a row raise the one row `failure:service:context-map-refresh` ("the last 3 map refreshes or publishes failed (code)", the one string everywhere). So a publish that keeps failing (046 for an undecided item that a joined proposal needs a person for, or a store error) cannot freeze the map or silence the alarm: the move check can still start a replacing refresh, and the alarm still counts. `RefreshContextMapResult` gains `Held` (bool) and `Version`; a
`PublishContextMapResult` carries `Version`, `Reason` and `Code` (string?, `SND-CONTEXT-190` when the draft was no longer the expected one).

**Supersede, both sides.**
- *A newer trunk move while a map is held.* The move check (F1's `MainMoveDecision`) compares the live heads with **`MappedHeads`**, a pure function of stored map state (`MapPublishState`): the held draft's heads if there is a hold, else the current version's, else none. A draft that is not held (a failed refresh's, or a person's) does not count, so a failed refresh is retried on its backoff and a map behind trunk is rebuilt, as F1 does today (a person's decisions are overrides, which a rebuild re-applies). It does not read job results, so renumbering and human promotes cannot confuse it; a version with no recorded heads (built before this change, or imported) reads as none and gets one refresh. The moved head starts a new refresh whose build replaces the draft; the old hold goes with the draft it described (its row is `ON DELETE CASCADE`), but the wait clock survives it: **`since` lives in its own one-row table `map_publish_chain`, set when a hold is first written and cleared by any promote** (`MapHold.NextSince(chain, now)` = `chain?.Since ?? now`, clamped to at most `now`). So a trunk that moves every ten minutes cannot keep a map from ever promoting: the newest ready draft is published at most `wait` after the first hold of the chain, ready or not. A person's `map build` in the middle of a chain leaves the chain's `since` in place, so the next hold waits less than a full `wait`; that is bounded and harmless, and the next promote clears it. While a `RefreshContextMap` runs the tick ignores `Drop(Gone)` and still acts on `Publish` (one rule, above), so a stuck refresh cannot hold a due map back.
- *The index side.* Nothing changes in §6 or §5.7. An index build in flight for the older commit finishes and publishes it (still on
  trunk), and the next tick builds the newer one; the held map for the older commit now sees an index at neither its head nor
  one that can become it, so it reads `Coming` until the newer map's hold replaces it or the bound passes. The map never makes the
  index hurry, skip a layer, or wait for the map.
- *A human `map build`* replaces the draft, so the hold is gone (`Drop(Gone)` for the tick that still sees it); *a human `map promote`* is not gated (retrieval
  spec §8.7a) and clears the hold and the chain in the same transaction.

**After the timeout.** The map promotes (`Publish(TimedOut)`), and the code rows read stale, **derived at read time, never
stored**: the map version's commit for the repo and the index's commit are compared by every answer (§7.3), so nothing
needs to be re-promoted. When the index for that commit lands later, the same read finds the two equal and the rows are
fresh with no event and no job; if instead trunk moved on, the next refresh holds the next map the same way. The simplest
correct rule: **the gate runs once per map version, at promotion; staleness is a read, not a state.** The exact fields: each
`ICodeSymbolSource` call takes `asOf` (the map version's recorded commit for that repo, or null for the head) and every row
returned carries `commit` (the index's), `builtAt`, `state` (`stale` when they differ, §7.3) and `asOfCommit` (the commit asked
for); the code lens' `searched.sources[code-index]` is `{state: degraded, reason: stale, code: null, asOf: <index commit, 7 hex>}`,
each `callers` and `callees` row is marked `asOfMismatch: true`, and the text reads "from abcdef0, not 1234567" (the index's
commit, then the map's). **Every mapped repo's commit is recorded with the version** (F1P-a: a `map_version_head(version, repo, commit, on_default)` table written by the build, beside
`MapVersion.Commit`, which stays the first repo's; the hold itself stores only its version, and its heads are these rows), so `asOf` is per repo and a second repo's stale rows are as exact as the first's. A version
imported or built before this change has no heads and keeps today's rule for its repos, the repo's head (Q14).

The second way the rows stop being stale is that the index moves on past the map's commit: the index keeps one commit, so it never builds the map's, and the rows clear when the next map (for the newer commit) is published. §7.3's "clears itself" means exactly: the index commit equals the map's, or the map is replaced.

**Never block forever.** Every route out of `Wait` ends at line 2, 3, 6 or 1 of `Decide`, and line 6 needs only the clock and a
stored `since`:

| Case | What happens |
|---|---|
| index disabled, repo not indexed, `ICodeIndex` not composed, service paused | `Absent`: the repo is not part of the gate; all absent is `Publish(NoIndex)` |
| index broken (3 failed jobs, damaged and not rebuilt) | `Broken`: publish at once, `IndexBroken`; the owner already has the code index's needs-you row |
| index job failing, backoff 5 to 60 minutes | `Backoff`: wait until the bound, then `TimedOut` |
| graph layer unavailable, embeddings pending | not a hold at all: the index was published with those layers decided |
| index deferred (`heavy-busy`) | `Backoff`: wait until the bound |
| head moved again | see Supersede |
| the index service is paused mid-wait | the repo turns `Absent` on the next tick; the map publishes at once (`NoIndex` if every repo is) and says so (below) |
| Sindri stopped or restarted mid-wait | the hold row, the chain row and the stored index state survive; the first tick after the restart re-asks with the real clock, so downtime counts toward the wait and a long stop publishes at once; a refresh job killed mid-run leaves no hold and is retried by the move check |
| `PublishContextMap` fails (store error) | retried on the same backoff as a failed refresh (5, 10, 20, 40, 60 minutes); three in a row raise the existing `failure:service:context-map-refresh` row (the string above) |
| a draft that cannot become ready (undecided items the cleanup cannot settle) | not a gate case: the refresh job fails as today and holds nothing |

**What the owner sees.** One small vocabulary. `context map status --json` carries `state` as one of the ids `held`, `overdue`, `published`, `published-without-waiting`, `published-stale`, `published-by-hand`, plus separate fields `reason` and `stale` (with both commits when true); the words below are for display. The state is computed live: `held` (the map is ready and waiting for the code index); `overdue` (its deadline passed more than two ticks ago and it is still held: the map service is paused or the publish is failing); otherwise, if the map's commits and the index's differ for any repo, `published-stale`; otherwise by how it was published. `published-by-hand` is a `context.map.promoted` with a human caller (derived from the caller on that event alone, so a crash between `promoted` and `published` cannot turn an automatic publish into one; a version promoted by the older F1 service, or imported, with no `published` event reads `published`). The index's own states in §7.3 keep their words (`waiting` there means a repo's build is deferred or in backoff). Wherever the stale fact is written as a sentence it is this one, the index's commit first, with which is newer (Q11 and Q14 quote it): "Code answers are from abcdef0 (built 12 min ago, older than map v14, which is for 1234567)." The lens rows keep their short form "from abcdef0, not 1234567" (§7.3); it is the same fact, and the sentence applies only to an as-of mismatch, not to "head moved, build not done". With several repos each line names its repo, at most two and then "and N more", and a `published` line says which repos match and which are "not indexed yet". **A session sees only its own repos' lines, no count of others, and (as the human-only part) no publish tally**; if none of its repos is waited on, a `held` map reads "Map v14 is held; none of your repos are waiting." Session reads go through a read-only, repo-scoped `IContextMap.PublishStatus` (F1P-a), never `Held`. The screen is the Health page's Context map card (F1P-c); the Code index card is unchanged.

| State | Health, Context map card | `sindri context map status` | `sindri jobs` | needs-you | doctor `context:publish` |
|---|---|---|---|---|---|
| `held` | "Map v14 is ready. Waiting for the code index of web (building), api (retrying in 12 min); web2 is not indexed yet and is not waited for. It goes ahead by about 14:32 (your time) unless a newer build replaces it; v13 is current until then. To publish now: sindri context map promote." A chained wait adds "a newer commit arrived; the wait did not restart"; a repo whose index is already newer says "code answers for api are already from the newer commit". Per-repo words are `RepoWait`: queued, building, waiting for a free build slot, retrying in N min, index is newer (waiting for the next map), index damaged (rebuilding), not indexed yet | `held: v14, until 14:32, on web (building)` | `RefreshContextMap` ends `succeeded`, summary `held: map v14 waits for the code index (2 repos)` | none: waiting is normal | `ok`, "v14 held until 14:32" |
| `overdue` | "Map v14 should have gone ahead at 14:32. The map service is paused, or publishing is failing (code). v13 is current. Resume the map service or see the failing job; or sindri context map promote." | `overdue: v14, was due 14:32` | the failing `PublishContextMap` job, if any | the map service's own row once the publish failed three times | `warn` |
| `published` | "Map v14 is current; the code index matches (abcdef0)." (reason `IndexReady`) | `published: v14, code index matches` | `map v14 promoted` | none | `ok` |
| `published-without-waiting` | by reason, with the live stale sentence instead whenever the commits differ: `FirstMap` "Map v14 is the first map; the code index is not part of it yet." `NoIndex` "Map v14 is current; there is no code index to wait for." (when the service is paused: "the code index service is paused; the map did not wait.") `WaitOff` "Map v14 is current; waiting for the code index is turned off." | `published: v14 without waiting (first map)` | `map v14 promoted · first map` | none | `ok` |
| `published-stale` | the stale sentence, plus "the code index did not finish in time" or "the code index is broken, see Code index". Live: once the index for the map's commit lands it reads `published` with "Map v14 waited out its wait; the code index has caught up."; if the index moved past the map's commit it clears with the next map | `published stale: v14 (reason timed-out, web stale)`; after the index lands `published: v14 (waited out; code index caught up)` | `PublishContextMap`, summary `map v14 promoted · timed out waiting for the code index (2 repos)` (`index broken` for that reason) | none: a broken or failing index already raises `failure:service:code-index-main-move`; a failing publish raises the map service's row | `warn` only when the rows are still stale more than five minutes after the publish (the short index-ahead window is `ok` with the sentence); the last publish's reason is an info line from the latest `context.map.published` event |
| `published-by-hand` | "Map v14 was published by hand." (the stale sentence while the commits differ) | `published by hand: v14` | none (no job) | none | `ok` |

A hold that a newer build replaced, or a person's `map build` dropped, is not a state: `context map status` prints "held map v14 was replaced by v15" while that `context.map.hold.dropped` is the newest map event. Before CI-9 registers the real outlooks the live comparison has no index commit to read (`IndexCommits` returns none), so `published-stale` can render only from the event's reason; after CI-9 it is live. `sindri context map status` (human callers) also prints how the last 10 publishes ended by reason (for example "7 IndexReady, 2 TimedOut, 1 WaitOff, 1 ByHand"), so the owner can tune the wait or switch it off with numbers. `jobs show` on a held refresh names the publish job that finished it.

**Ledger events** (typed payload records, `SindriJsonContext` entries, catalog rows and `ApiViews` names, all in F1P-b2; payloads hold
repo names, version numbers, reason words and counts, never a path or text):
- `context.map.held`: `repo`, `version`, `repos` (count), `waitingOn` (count), `waitMinutes`, `chained` (true when a chain row already existed).
- `context.map.published` (public): `repo`, `version`, `reason` (`IndexReady`, `NoIndex`, `IndexBroken`, `FirstMap`, `WaitOff`, `TimedOut`), `via` (`refresh` or `check`),
  `repos`, `staleRepos`, `waitedSeconds`, and `heads` (each mapped repo's head, as `context.map.refreshed` has them). It follows `context.map.promoted`, which every committed promote still appends.
- `context.map.hold.dropped`: `version`, `reason` (`Gone`; `AlreadyCurrent` is the defensive guard).
- `context.map.refreshed` gains `held` (bool; false when the refresh promoted itself).
The `heads` field is in the stored payload only; the public form of `context.map.published` leaves it and the repo names out, as `repo.added` leaves its path out, and a session's `map status` and lens text are cut to its own repos. One event per map version, not per repo: `repo` is the version's own repo (`MapVersion.Repo`) and `repos` counts the mapped ones. Idempotency keys (the `v` as in `context.map.refreshed`) `context.map.held:<repo>:v<version>`, `context.map.published:<repo>:v<version>`, `context.map.hold.dropped:<repo>:v<version>:<ulid>`. Public form: `held` and `hold.dropped` are not public (they name repos); `published` is public without repo names, heads and counts of repos (`repos` and `staleRepos` become one boolean, `anyStale`); `refreshed` keeps its existing public form. `sindri jobs` and `jobs show` text is as visible to a caller as the jobs list already is to that caller; this section adds no reader.

**Errors.** The code index range is untouched: `SND-CODEIDX-017..029` stay reserved and this section adds no code (the
index is read through `Heads`, whose failure is "no index", which is `Absent` or `Coming`, never an error to the map). The map
side reserves **`SND-CONTEXT-190..194`** (explain owns 180..189) and uses **190** (Conflict): an `Error` the engine returns from `Promote`, `CleanDraft` and `AppendOverrides` when `expectedVersion` is given and the latest draft is another number (a rebuild replaced it, or it was promoted). The publish job maps it to a `Succeeded` result with `Code` 190, drops the hold, and raises nothing. **`SND-CONTEXT-046` keeps its own meaning (no draft, or undecided review items)**, so the job can tell the two apart: 046 fails the job, leaves the hold, and backs off for a person to decide. A
bad profile value is 001. `191..194` are reserved. The range row in the retrieval spec (§14 phases note) is updated in F1P-b2.

**Who may do what.** `Hold`, `Release`, `Held` and the `PublishContextMap` job are service-only (`service:context-map-refresh`): the engine refuses a session caller on the three store calls, as `MapHeads` and `BuildMap` do (020), the catalog row is a Job row of the same class as `RefreshContextMap` with no HTTP route, MCP tool or CLI verb of its own beyond the job pattern, and a catalog test pins that a session's start is refused (`SND-AUTH-002`); `sindri jobs cancel` follows the existing job rules. `Hold` also refuses a version that is not the latest draft. A human keeps `map promote`, ungated. The publish job promotes through the engine, so `PromoteReadiness` still refuses undecided review items (046): a proposal that joined the held draft and needs a human decision is never promoted by the service; the job fails, backs off, and the person decides.

**How the neighbours behave.**
- *The cleanup agent* (owner F1) runs before the hold, once per draft, in parallel with the index build (that is the point of
  holding after cleanup, not before: the model time overlaps the wait). `CleanupPolicy.MaxTokens` is unchanged. A draft that is
  superseded wastes its cleanup spend; that is the price of not delaying the publish by a cleanup pass, and the supersede rule bounds
  how often it happens. The publish job's second pass costs nothing unless a proposal joined the draft.
- *Explain's carry (explain spec D3(c)).* "After an F1 refresh promotes a new version" means **after `context.map.promoted` with a
  service as the caller**, which a publish appends exactly as a refresh did; it must not start from `context.map.refreshed`, which
  can now be a held refresh. While a version is held, the current version is the old one, so the card's first-open start (D3(a))
  and its summaries are unchanged. The carry's "previous version" is the one `MapPromoted.Retained` names. X2 builds to this.
- *A human* may still `map promote` a held draft (ungated, retrieval spec §8.7a), `map review` it, and `map build` over it.
- *Scoping proposals* join the held draft like any draft (retrieval spec §8.7), which is why the publish job runs the cleanup pass.

## 7. The facade (`ICodeIndex`, Aw.CodeIndex.Core/Facade)

Core records only; every call takes a `CodeIndexScope` (`CodeIndexSettings`, the repos and the caller)
and returns `Result<T>` (`Aw.Kernel`). Reads open the repo's file read-only and never create it.

| Call | Answers | Used by |
|---|---|---|
| `Build(scope, repo, commit, progress)` | the build slice (§5.7): the only write; called by Sindri's `BuildCodeIndexService` | the job |
| `Status(scope)` | per repo, **from the files only**: `state` (`missing`, `ok`, `damaged`; `stale` once Sindri compares the commit with the checkout's head), `building` and `waiting` are overlaid by Sindri from its jobs and service state, `commit`, `builtAt`, `layers[]` (status, code, detail), counts (files, symbols, edges; stored in `meta`, never counted per call) | Health, doctor, the code lens' `searched.code-index` row |
| `Heads(scope)` | per repo: the commit the index describes, from stored state only (no git, no live probe), or none when the file is missing or damaged | the service's recorded heads (§6.2); doctor |
| `ProbeLayers(scope)` | per repo: which unavailable layers can now be built (lock and tool check, no work) | §6.3 |
| `FindSymbols(scope, SymbolQuery)` | by exact name, `path`, `path#name` or ref; `prefix` search; ordered, 200 by default and any limit from 1 (removed per owner q93 (2026-10-23)); each hit with ref, kind, span, signature, exported | S-9 `symbol` shape; `context query --symbol` |
| `FileSymbols(scope, repo, paths[])` | the symbols each of up to 200 files defines | S-9 symbol-level `implements`; the file map's `symbols` column |
| `Callers(scope, SymbolRef, CallDepth)` | edges into the symbol, depth 1 to 8, at most 1,000 per level (raised from 3 and 100 per owner q93, 2026-10-23), each with basis and confidence, plus `unresolvedCount` (for a graphify language it includes the module-level calls dropped for want of a caller symbol); every row carries `coverage` (`full` for C#, `partial` for a graphify language, §5.5) | SC C3 (code lens `callers`) |
| `Callees(scope, SymbolRef, CallDepth)` | edges out of the symbol, same shape, `coverage` per row | SC C3 (code lens `callees`) |
| `Similar(scope, SymbolRef)` | exact clones (same shape hash) and near clones (MinHash bands, then estimated Jaccard at or above the threshold) | later shape signals (CI11); `index query` in the TS build |
| `Neighbors(scope, text, k)` | the k nearest symbols by embedding; the text is embedded by the same loopback model; `unavailable` with the layer's code when the layer is | CTX `code` source "embedding neighbors" |

### 7.1 Answers

Every answer carries `repo`, `commit`, `builtAt`, `state`, and the layer it depends on. Names, signatures and paths are
returned as data with `untrusted: true` on the record; control and bidirectional-override characters are stripped from anything the CLI prints; symbol name search uses exact and range-prefix matches, never `LIKE` with caller-supplied wildcards; Sindri's presentation layer fences them for sessions (CTX "Egress by
caller") and the CLI prints them as plain text for humans. Limits are fixed in `CodeIndexRules` (not profile keys), like
`CodeRules` in Context.

### 7.2 Context's port (S-9)

`Aw.Context.Core` gets `ICodeSymbolSource` (`FileSymbols`, `FindSymbols`, `Callers`, `Callees`, `Heads`) with its own
records and a fake; Sindri's Host adapts `ICodeIndex` to it in a `CodeIndexSymbolSource` (the `JudgeCatalog` pattern: the
index's container stays its own). Context never sees SQLite or a path of the index. The code lens fills `callers` and
`callees` from it, and the `code-index` row of `searched.sources` goes from `unavailable / index-not-built` to
`ok`, `degraded` (reason `stale`, `checkout-not-on-main`, or a layer `unavailable`) or `unavailable` with the real reason. `ICodeIndex` itself is unchanged by revision 4: its answers already carry `commit`, `builtAt` and `state`. The new part is Context's port: each `ICodeSymbolSource` call takes `asOf` (the map version's recorded commit for that repo, or null), and the adapter in Sindri's Host sets `asOfCommit` and `state: stale` on the rows when it differs from the index's `commit` (§6.4 "After the timeout"). S-9a owns those record changes; no CI-1 edit is needed. The lens' source state vocabulary is the existing one (`ok`, `not-configured`, `unavailable`, `degraded`; `SearchedState`); this block's `state` words (§7.3) map onto it in the adapter, and `stale` is never a state of the lens.

### 7.3 Stale, missing, as-of

| Situation | What the consumer sees |
|---|---|
| no index file yet (new repo) | `unavailable`, reason `index-not-built`, `SND-CODEIDX-013`; Health says "first build queued" or "building" |
| index commit is the checkout's head | `ok` |
| head moved, build not done yet | `stale` (answers still served from the old commit, `commit` and `builtAt` in the answer) |
| as-of commit (a map version's) differs from the index's | `stale`, with both commits named (`commit` and `asOfCommit` on every row); the lens marks the rows `asOfMismatch` and reads "from abcdef0, not 1234567". This is the state a map published after its wait (§6.4) shows, and it clears itself the moment the index for the map's commit lands, with nothing re-promoted: it is computed at read time |
| the index is ahead of the current map (the index built a newer head, the map for it is still building or held) | the same `stale` row, with the same two commits; the window closes when the held map publishes (§6.4) |
| file damaged or unreadable pages | `damaged`, `SND-CODEIDX-010`, and the next tick deletes and rebuilds it |
| graph layer unavailable | structure-only callers and callees (`basis = syntax`), the answer says `graph: unavailable`; the lens source is `degraded` |
| checkout not on main | the old index is served and `stale`; Status `stale`, Health "checkout is not on main", the lens `degraded` with reason `checkout-not-on-main` |
| _lens mapping_ | block state to `SearchedState` (the `Reason` word and, where there is one, the `Code`): `ok` to ok, including a layer that is `disabled` by the profile or embeddings still `pending` (a note, not a degradation); `stale`, `checkout-not-on-main` or a layer `unavailable` to degraded (reason `stale` / `checkout-not-on-main` / the layer, code the layer's `SND-CODEIDX`); `missing`, `damaged`, `building` (no old index) to unavailable (reason `index-not-built` / `index-damaged` / `index-building`, code `-013` / `-010` / none); a repo with no approved profile entry to not-configured (structure is always on, so a layer setting never makes it not-configured). `Degraded` is documented as "last run failed" today; its comment widens to "answered from an older or partial index" in S-9a |
| `waiting` (a repo whose build is deferred or in backoff) | the old index, if any, `stale`; Health says "waiting for a free build slot" or "retrying in N min" |
| a repo outside the caller's scope, or no such repo | the same `SND-CODEIDX-013`/`-011` not-found as a repo with no index; `Status` lists only the caller's repos, so a session learns nothing about others |

## 8. Ledger, jobs, events

- **Job kind** `BuildCodeIndex`, registered with `AddJob<BuildCodeIndexRequest, BuildCodeIndexResult, BuildCodeIndexService>`
  keyed `index:<repo>`; so a second start for the same repo is `SND-JOB-409` with `runningJob`. The two shares are the success measure after shipping: the share of call sites that resolve to nothing in the repo, and the share that stay ambiguous, shown on Health. Its summary line:
  `<repo> @ abcdef0: 3,100 files, 41,000 symbols; graph ok, embeddings pending`.
- **Job events** (the P6 runtime writes them): `job.started` (kind, key, request digest), `job.progress` (lines from §5.7,
  throttled by `JobProgressBudget`), `job.finished` (state, summary, code).
- **Domain event** `codeindex.built`, appended by the job's work (with its held grant) just before the runner appends `job.finished`; the two are separate appends, so the projector tolerates a built event with no finished job. Idempotency key `codeindex.built:<repo>:<commit>:<ulid>`; it needs a typed payload record, a `SindriJsonContext` entry and an `ApiViews` name, listed in CI-9. Payload: `repo`, `commit`, `layers` (name,
  status, code), `files`, `symbols`, `edges`, `unresolvedShare`, `ambiguousShare`, `embeddedPercent`, `ms`, `incremental`. No name, path or text. It joins `GetEventCatalog` and
  invalidates the views `ApiViews.CodeIndex` and the jobs list.
- **View** `view_code_index` (projector): the newest `codeindex.built` per repo, so Health and the Context screens read one
  row, not the job history. Rebuildable by `sindri views rebuild` like every view.
- **Needs-you:** only the service-failure row of §6.2. A map held for this index, or published after waiting for it, raises no row of its own (§6.4); `ICodeIndex.Heads` is what the map's gate reads. A layer `unavailable` is a Health warning, not a queue item.
- **Doctor rows:** `code-index:<repo>` (ok / warn stale / fail damaged, with the fix line), `code-index:graphify` (version
  and sandbox against the pin), `code-index:ollama` (reachable, model digest against the pin; `warn`, never `fail`, when
  embeddings are off or the box has no Ollama), `code-index:lock` (the lock file parses and matches the build's embedded copy).

## 9. Security and the sandbox

- **Pins** (CI6): `context/code-index.lock`, rows `graphify <version> <exclude-newer-date> <requirements-sha256>`, `ollama-model <name> <digest>`,
  `need disk-bytes <n>`; its graphify version must equal the TS pin in `sindri/package.json` (the check script compares them, so the
  two cannot drift before the switch); checked by `scripts/code-index-lock.sh check` (shape) in CI and embedded into the build, as
  `onyx.lock` is. A version change is a reviewed change to the file. `scripts/install-deps.sh` (run by `setup.sh`) reads it
  to install graphify into its own virtual environment from a hash-pinned requirements file (`uv pip install --require-hashes`, transitive packages included, `--exclude-newer <date>`) and to `ollama pull` the model (a new step: today's script has no Ollama step);
  that script is the only thing that touches the network.
- **graphify's isolation** is the TS index's, kept: no network, writes confined to its private snapshot, credential stores
  hidden by name and by real path, `/run` masked so D-Bus and docker.sock are unreachable (Linux), no `launchctl` and no
  LaunchServices (macOS), clean environment. This is the existing wrapper in `Aw.Context.Infrastructure.Extract.GraphifySandbox`, which is `internal` to Context. Be exact about what it does: on Linux it binds the root read-only and hides named credential paths and `/run`; it is a deny list, not a read allowlist, and macOS runs `(allow default)` with denials. A compromised graphify could read other files on the box and put them in `graph.json`. So the build treats `graph.json` as hostile: node ids and files are kept only if they name a path in the snapshot, names are scrubbed and cut, and the node and edge caps of §5.5 apply. Pins are checked against the binary at an absolute path inside the dedicated environment (never `PATH`), by version and by the requirements hash; the model digest comes from the local Ollama server, so a local process that fakes port 11434 is outside this threat model (the toolkit guards against honest mistakes, not a local attacker).
  **The sandbox spec (SB) governs agent sessions** (`ISandbox`, SB12); it does not cover a Sindri-run tool like graphify. Two
  consequences, both settled here: (1) the index build runs host-side, as a Sindri job, never inside an agent session's
  sandbox (SB14: heavy work goes through Sindri); (2) when SB-1's `ISandbox` lands, the graphify wrapper moves onto it in one
  change and the copy goes away (§15 Q2). Until then this block carries its own wrapper (a copy) and a contract test, shared with Context's wrapper so the two cannot drift, that a
  request from inside it fails (Context's tests only compare argv strings today). The test-only sandbox seam is not reachable from a profile key or an environment variable.
  **The graphify environment inside the sandbox.** The wrapper masks `~/.agentic-workflow` (a credential path) with an empty tmpfs, and the pinned environment lives under `$AW_STATE_DIR/cache/code-index/`, inside it by default; the CI-0d spike reproduced the failure (`bwrap: execvp .../bin/graphify: No such file or directory`). So the environment directory is bound **read-only after the mask** (`--ro-bind <env> <env>` placed after the `--tmpfs` of `~/.agentic-workflow`; on macOS a `(allow file-read*)` for that subpath after the denial), exposing only the environment directory, never its parent. The configured path is `lstat`ed first and a symlink is refused, then it is resolved with `realpath` and the resolved path is bound; only the per-pin directory under the code-index cache root (`$AW_STATE_DIR/cache/code-index/<pin>/`, never the root itself, and a bind test asserts a sibling file in the root is invisible) or a path outside every masked path is allowed, the pin check and the run both execute the resolved path that was bound, and anything else inside a masked path, or a directory that contains one, is refused (`SND-CODEIDX-009`). The pin check of `SND-CODEIDX-009` runs against the binary at that absolute path, inside the sandbox. A binary that cannot start inside the sandbox is `SND-CODEIDX-008` with a doctor fix that points at the bind and the mask, not at the install. The spike ran with the mask removed, not with this bind, so CI-7 proves the bind itself: a unit test asserts the argv order (`--ro-bind` after the `--tmpfs` mask; on macOS the allow after the deny), and a heavy-lane test runs the pinned graphify under the real wrapper on Linux and on macOS and fails if it cannot start (the layer is enabled on a platform only once that test passes there, recorded as a per-platform `sandboxStart` row in `context/code-index.lock`, and a platform without it answers `SND-CODEIDX-007`; at run time a binary that will not start under the wrapper is `SND-CODEIDX-008`). The venv is created with its interpreter copied in (`--copies`); the base interpreter's standard library must also sit at a path the mask does not cover, and the lock install and the heavy test assert both.
- **Roslyn parses text, nothing else.** No `MSBuildWorkspace`, no analyzer or source generator of the indexed repo is loaded,
  no `dotnet restore`. A repo's `.csproj` is read as XML for the deps layer only.
- **Scrubbing:** every stored free-text field (bodies, signatures, deps `version` and `tags`, graph node names) is scrubbed (`Aw.Redaction`) and then cut, in that order, while the key columns (paths, symbol names, refs, `dst_name`, call-site names) are git and parser identifiers and are never scrubbed (a scrubbed ref could collide on `UNIQUE`; the deny globs keep secret-named files out), before it is stored; layer `detail`, job results and progress lines hold codes and counts only, never the stderr of git, graphify or Roslyn; and the embedded text is the scrubbed one; a literal in a signature (`string key = "AKIA..."`) does not reach disk.
- **Egress:** the facade returns repo text marked untrusted; sessions read only their own repo's index, enforced in Sindri's
  query handlers as for `context lenses`, not by the caller's arguments; the repo inside a ref (`<repo>:<path>#...`) is scope-checked the same way, a mismatch
  answers the uniform not-found, and every query is parameterized (a ref is parsed into parts, never concatenated into SQL or a path).
- **Disk:** the state directory holds source text (bodies, until embedded). Keep `$AW_SINDRI_HOME` out of cloud sync and
  backups you do not control (the TS index says the same).

## 10. Surfaces

| Surface | What | Notes |
|---|---|---|
| Jobs (CLI) | `sindri jobs` lists a row per build: `code index · <repo> · @abcdef0`, state, elapsed, the progress line, the summary or code | the app has no job view today (`docs/csharp/sindri-ui.md` says `job/` links open a "not served yet" placeholder), so this block builds none: the owner sees builds in `sindri jobs` and on the Health card, and a job view is the UI lane's own work |
| Health | (the map side's words while a map waits for an index, or went ahead of it, are §6.4's and live on the Context map card) a `Code index` card: per repo the commit, age, layer words (`structure ok · graph ok · embeddings pending 31%`), the unresolved and ambiguous call shares, or "checkout is not on main" / "first build queued" / "waiting for a free build slot" / "retrying in N min" / "index damaged, rebuilding" / "no repos in the profile" / "building the first index" | reads `view_code_index` and `Status`. The shipped Health page is a fixed table of status parts, so this is its own UI slice (CI-10, with the `ISindriApi` method and labels): a stale or unavailable layer is a warning word on the card and never changes the overall pill; a state is always a word, never colour alone |
| Context screens | the file map's `symbols` column (S-9) and the code lens's callers and callees panels | the mockup `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/context-filemap.html` has only a disabled "symbols · index not built" lens checkbox, not a column; S-9 adds the column design to that mockup and the screen. An unresolved-call count reads "N calls not resolved", and an as-of mismatch reads "from <short commit>, not <asked commit>" |
| CLI (read only) | `sindri code-index status [--json]`, `sindri code-index symbols <query> [--repo R] [--json]`, `callers <ref>`, `callees <ref>` | `status` takes `--repo R`; reads go through the running control plane like the workflow reads (exit 1 `SND-API-001` when it is down, never starting it); exit 0 ok (for `symbols`, `callers`, `callees` a stale-but-served answer is ok and says `stale` on its first line; for `status`, any repo not `ok` is 1), 1 attention (missing index, serve down, and the not-found answers `SND-CODEIDX-011`/`-013`, so a repo out of scope looks the same as one never built), 2 error (including `-012`). `--json` on every command; `callers`/`callees` take `--depth 1..3` (default 1) and `--repo R`. No `build`, no `setup` (CI1, CI6). Text is repo text: fenced when a session calls it |
| HTTP | `GET /sindri/code-index/status`, `/symbols`, `/callers`, `/callees` (query class, repo-scoped for sessions) | catalog rows and goldens in the same PR as the handler |
| MCP / blocks | none in version 1; read blocks `code.callers` and `code.symbols` for Steps are a later row of the blocks-over-MCP list | keeps the tool list fixed (BM) |

## 11. Testing

Per `.agents/rules/csharp-testing.md`: red first, fakes with contract tests, builders, golden files, architecture tests.

- **Unit lane (under 10 s for the package):** `BuildPlanner` (table of cases: new file, changed blob, removed file, stamp
  change, empty repo), `CallResolver` (every confidence), shape hash and MinHash (known vectors; two
  files that differ only in identifiers and literals hash equal), `InputRules` (deny globs, caps, symlink and submodule
  skip), tokenization, `CodeIndexRules` limits, error construction, the lock-file parser.
- **Fakes** in `Aw.CodeIndex.Testing`: `FakeRepoSnapshot` (an in-memory tree at a commit), `FakeGraphRunner`,
  `FakeEmbedder` (deterministic vectors from a hash of the text), `FakeIndexStore`, `FakeRepoHeads`. Each fake has a
  **contract base class** the real adapter's tests also derive from (`RepoSnapshotContract`, `GraphRunnerContract`,
  `EmbedderContract`, `IndexStoreContract`, `SymbolParserContract`; `FakeRepoHeads` is the context lane's fake and contract): same cases, so a fake cannot drift.
- **Integration lane (under 60 s):** the real `SqliteIndexStore` on a temp file (schema, a build and an incremental build
  over `FakeRepoSnapshot`, pointer swap with a reader open across it, a stale `-wal` beside a file, a copy of a published file, damaged file, user_version change), the real `GitRepoSnapshot` against a temp repo
  made by the test with `git init` (symlink, submodule, deny glob, over-cap, a path swapped after the commit), `CSharpParser`
  on fixture files, the facade over a built fixture index, and the architecture tests (no Sindri import in `CodeIndex`; Context does
  not reference `CodeIndex`).
- **Sindri-side tests** (in `Aw.Sindri.Tests`, their own lane budgets, not CodeIndex's): the service over a fake clock and a fake jobs view
  (heads moved, running, per-repo backoff at 5/10/20/40/60 with a second repo unaffected, the third failure raising needs-you once, a
  head not on main, a first build, a deleted index file rebuilding, a deferred job, a continuation), the `codeindex.built` projector and its API
  goldens, the `IHeavyJobLock` adapter.
- **Publishing with the map (§6.4).** Owning units: the decision, rules, `MappedHeads` and port cases are F1P-b1's; the jobs, tick, events and goldens F1P-b2's; the store contract (hold, chain, heads, `expectedVersion`, `NextSince`, renumbering, cascade, session refusal, commit validation) F1P-a's, which also covers the Context-side integration; the session-scoped status text and the words F1P-c's; the adapter table CI-9's; the lens rows going stale and fresh S-9's.
  - *Unit, `MapPublishDecision` (a table of cases, fake clock; Sindri Core):* every repo `Built` publishes `IndexReady`; one `Coming` inside the wait is `Wait` with
    `waitingOn` 1 and `until = since + wait`; the same past the bound publishes `TimedOut` naming that repo; `Backoff` waits and then times out; `Broken` publishes
    `IndexBroken` at once even inside the wait, and with another repo `Coming`; `Absent` repos are ignored (all absent is `NoIndex`; `Absent` with `Built` is `IndexReady`; `Absent` with
    `Coming` waits on the `Coming` one); a repo that was not on the default branch is `Absent`; no current version is `FirstMap`; `wait = 0` is `WaitOff`; `Built` outranks every
    other rule (including `wait = 0` and `FirstMap`); a draft number that is not the hold's is `Drop(Gone)`; a current version equal to the hold's is `Drop(AlreadyCurrent)`; the
    clock exactly at the bound publishes (inclusive); two repos, one `Built` and one `Coming` past the bound, `staleRepos` is 1.
  - *Unit, `IndexOutlookRules`:* each outlook from its inputs, including the index at a newer commit than the hold's head (`Coming`), a deferred job (`Backoff`), two failures (`Backoff`) and three (`Broken`),
    a paused service (`Absent`), but `Built` when the index is already at the commit (precedence), a damaged file (`Coming`), `NeverBuilt` (`Absent`), and the cases of the lens-free live comparison.
  - *Unit, `MapHold.NextSince` (Context, F1P-a) and `MappedHeads` (Sindri Core, F1P-b1):* the chain's `since` survives a replaced hold and is clamped to `now`; the held draft's heads win over the current version's; a draft with no hold does not count; a version with no heads reads as none;  a held result whose version is neither held nor current counts as no heads; a published result counts.
  - *Unit (Sindri), the refresh job:* publishes at once when the decision says so (today's behavior, the existing tests keep passing with the no-index outlooks); otherwise stores the hold, appends `context.map.held`, ends `held: true`
    and never calls `Promote`; the cleanup pass runs before the hold. The publish job: `Publish` runs the cleanup pass and promotes and appends `promoted` then `published`; a promote refused with 190 (the draft was replaced) ends `Succeeded` with `Code` 190 and drops the hold, while a promote refused with 046 (an undecided item) fails the job, keeps the hold and backs off; `Drop` releases it with `context.map.hold.dropped`.
    The service tick: starts a publish on `Publish` or `Drop`, is quiet on `Wait` (summary "map v14 held for the code index"), ignores `Drop(Gone)` while a refresh runs but acts on `Publish`, starts no refresh while a publish runs, and backs the publish off after failures.
  - *Context (store contract, fake and SQLite):* the hold row and the one-row `map_publish_chain` are written and read; `Hold` refuses a version that is not the latest draft; a build over a held draft drops the hold and keeps the chain's `since`; any promote (including one that renumbers the draft) clears both and keeps the version's heads; `OpenDraft` and `ReplaceDraft` copy the heads; deleting a version cascades its heads and hold; `expectedVersion` mismatch is 190 on `Promote`, `CleanDraft` and `AppendOverrides`; `Held`, `Hold` and `Release` refuse a session; `PublishStatus` is scoped to the caller's repos; an old `context.db` opens and gets the tables (a named migration).
  - *Integration:* a held version promotes when the index lands (a fake index store whose `Heads` changes between two ticks); a held version times out and promotes with `staleRepos` and the lens rows then `stale` with both commits named, and become fresh when the index for the map's commit lands later with no republish; a restart mid-wait (the host rebuilt over the same files and a later clock) resumes with the stored `since`;
    a chain of two moves publishes at most `wait` after the first hold; a human promote clears the hold and the chain, and a human build drops the hold and the next tick rebuilds only if the map is behind trunk; a refresh replacing the draft between the publish check and the promote (046, not a promote of the new draft); a two-repo map where one repo is stale; the event payloads hold only enum reasons, counts, version numbers and profile repo names; a session-scoped `context map status` (text and `--json`) shows only its own repos and no count of others; a promote that renumbers the draft (an accepted proposal) keeps the heads and clears the hold; a held draft with a proposal needing a human decision is not promoted (046) and the alarm still counts; a publish in its backoff lets the move check and the alarm run; a commit string that is not 7 to 40 lowercase hex is refused at the build report, `Hold` and import.
  - *Hand test, first half (CI-9's, running app, scratch home):* hold every heavy-job slot (the way the review API tests do with a bash helper) so the index build cannot start, and commit to a registered repo's trunk; `sindri jobs` shows the refresh `succeeded` with `held: map v+1 …` and the ledger has `context.map.held`; release the slots; the index builds, within one tick `PublishContextMap` runs and the ledger has `context.map.promoted` then `context.map.published` (reason `IndexReady`), and `sindri code-index status` names the same commit. (The `held` and `published` words are F1P-c's.)
  - *Hand test, second half (CI-11's owner-check):* set `context.publish.waitForIndexMinutes: 1`, hold the slots again and commit: after a minute `context map status` says `published-stale` with the index's commit and the map's (F1P-c has merged by then); release the slots and it reads `published` again with nothing republished. The lens' `searched.sources` `code-index: degraded / stale` then `ok` is S-9's check, and the Health card's words are F1P-c's.
- **Parser safety (integration):** a 100,000-term `a+b+...`, deeply nested parentheses and generics: the file ends `parsed = 0`, `too-deep`, the process lives (CI12).
- **Incremental correctness (unit and integration):** change a callee's file only; the unchanged callers' edges are re-resolved (`certain`, not null).
- **Heavy lane (nightly, and once by hand in each adapter PR):** real graphify at the pin inside the real sandbox on a
  fixture repo (graph layer ok; a request from inside the sandbox is denied; a graph.json over the cap is `SND-CODEIDX-008`);
  real Ollama with the pinned model (vectors are the model's dimension; `Neighbors` ranks a known near-duplicate first;
  a non-loopback URL and a cloud model are refused before any request); a full build and an incremental build of this repo
  (the heavy-job lock is taken, the slot released on cancel, the live file is untouched by a cancelled build).
- **End to end (owner check, `scripts/owner-check.sh code-index up`):** a scratch Sindri home with a fixture repo
  registered and approved; move its main (`git commit` on the default branch); within two minutes a `BuildCodeIndex` job
  appears in `sindri jobs`, finishes, and `sindri code-index callers <ref>` returns a known caller; a second commit gives an
  incremental build; moving the checkout to a feature branch gives "checkout is not on main" and no job.
- **Quality probe:** `callers` and `callees` precision and recall on a labeled sample of this repo's C# (symbol pairs the
  owner or the lane marks by hand, 50 each), reported by the heavy lane with the numbers, not gated at first. The
  scoping station's recall bars that exclude callers and callees (SC §10, "callers and callees excluded while…") are
  revisited with these numbers (§15 Q3).

## 12. Errors

Area `SND-CODEIDX` (new; free: the TS `SND-INDEX-*` codes stay documented until the switch and are not reused). Declared in
`Aw.CodeIndex.Core/CodeIndexErrors.cs` and listed in the package doc `docs/csharp/code-index.md` and the C# error table in `docs/csharp/sindri.md`. `docs/sindri/errors.md` is generated from the TS `errors.ts` and has no C# section, so it is not edited. Kinds are `Aw.Kernel`'s `ErrorKind` (`Invalid`, `NotFound`, `Unavailable`, ...).

| Code | Kind | Meaning | Fix line |
|---|---|---|---|
| `SND-CODEIDX-001` | Invalid | the `index:` section has a bad key or value (the message starts with the key path) | edit the profile, `sindri profile validate` |
| `SND-CODEIDX-002` | NotFound | the repo path is not a git repo, or the commit is not in it | check the repo's path in the profile |
| `SND-CODEIDX-003` | Invalid | the tracked code input is over `index.maxTotalMB` | add generated or vendored paths to `index.denyPaths`, or raise the cap |
| `SND-CODEIDX-004` | Unavailable | every heavy-job slot stayed busy past the wait (returned only as the job's result code, never as an error to a caller; the job ends `deferred`, not Failed) | none needed; the service retries after 10 minutes |
| `SND-CODEIDX-005` | Invalid | the embedding URL is not loopback, or the model is a cloud model | set `index.embeddings.url` to `http://127.0.0.1:11434` or turn embeddings off |
| `SND-CODEIDX-006` | Unavailable | the embedding server failed or answered in an unexpected shape (a layer code) | start Ollama; `sindri doctor` shows the pin |
| `SND-CODEIDX-007` | Unavailable | no network sandbox for graphify (a layer code) | Linux: install bubblewrap (`./setup.sh`); or set `index.graph.enabled: false` |
| `SND-CODEIDX-008` | Unavailable | graphify is missing, failed, timed out, would not start under the sandbox, wrote a wrong top-level shape, a graph with over a quarter of its records untrusted, or a too large graph (more than `graph.maxMB`), or filled its private directory past the cap (a layer code; `detail` names which, from a closed set: `missing`, `failed`, `timeout`, `no-start`, `shape`, `untrusted`, `too-large`, `dir-full`, each with its own doctor fix line) | `./setup.sh` installs the pin; raise `index.graph.maxMB` for a big graph; for a start failure `sindri doctor` prints the resolved environment path and says to check the bind and the mask, not the install |
| `SND-CODEIDX-009` | Unavailable | a tool or model does not match `context/code-index.lock` (a layer code) | `./setup.sh` re-installs the pin |
| `SND-CODEIDX-010` | Unavailable | the index file is damaged; it is deleted and rebuilt on the next tick | none |
| `SND-CODEIDX-011` | NotFound | the symbol ref names no symbol in this index | `sindri code-index symbols <name>` |
| `SND-CODEIDX-012` | Invalid | a query is empty, too broad, or over a limit | narrow it |
| `SND-CODEIDX-013` | NotFound | no index for this repo yet | none; the first build is queued |
| `SND-CODEIDX-014` | Unavailable | the index describes an older commit than the repo's main; only a health row on `GET /status`, the index still answers | rebuild it from the System page; the next tick also builds it |
| `SND-CODEIDX-015` | Unavailable | a build failed and is retrying after its backoff; only a health row on `GET /status` | `sindri jobs` shows the failed build; `sindri doctor` names the cause |
| `SND-CODEIDX-016` | Unavailable | the repo's index is locked by another build or embed run past the wait | none; retried later |
| `SND-CODEIDX-017..029` | | reserved | |

Revision 4 adds none: the map's gate reads `Heads`, whose failure is "no index" (`Coming` or `Absent`), never an error to the map (§6.4 "Errors"; the map side reserves `SND-CONTEXT-190..194`).

Reused: `SND-JOB-409` (a build already running), `SND-AUTH-*` (the caller), `SND-API-*`. Layer codes (006 to 009) are carried in
the layer row and the build report only (no HTTP status of their own); they fail a request only on the call that depends on the layer (`Neighbors`).

## 13. Profile

The existing `index:` section (`IndexSettings`, today only `denyPaths`, for map extraction) grows; unknown keys still fail
validation. A repo entry's `index:` unions its `denyPaths` and `utilityGlobs` with the profile's, and replaces every scalar key (and each `embeddings.*` / `graph.*` key) it names. The nested `embeddings` and `graph` records carry the unknown-key rejection attribute, so `scripts/check-profile-doc.mjs` checks their keys. Validation of a bad key is the profile's own (the existing `sindri profile validate` error with the key path); `CodeIndexSettings.Create` in `CodeIndex.Core` re-checks and raises `SND-CODEIDX-001`.

| Key | Default | Meaning |
|---|---|---|
| `denyPaths` | `[]` | added to the built-in secret globs (never replacing them) |
| `utilityGlobs` | `[]` | globs of internal utility modules; their exports are flagged `utility` |
| `maxFileKB` | 4096 | larger files are skipped as `too-large` |
| `maxTotalMB` | 4096 | larger code input (code files only) stops the build (`SND-CODEIDX-003`) |
| `embeddings.enabled` / `.url` / `.model` | true / `http://127.0.0.1:11434` / the lock's model | loopback only; a cloud model is refused |
| `graph.enabled` | true | off leaves the graph layer `disabled` |
| `graph.maxMB` | 1024 | largest `graph.json` read |

Revision 4's one new key is not an `index:` key: `context.publish.waitForIndexMinutes` (int 0 to 240, default 15) belongs to the context section because it is the map's rule (§6.4; listed in `docs/csharp/sindri-profile.md` by F1P).

`maxAgeHours` is gone: freshness is the head, not a clock. `docs/csharp/sindri-profile.md` lists every key
(`scripts/check-profile-doc.mjs` enforces it).

## 14. Phases and the lane plan (for /specToProvenPR)

Each phase is one PR, built from layered commits (Core, Infrastructure, Features, Host), each green alone. Every unit starts
red: the test, the request and response records and a stub service that throws `NotSupportedException`, committed first
(`/new-slice`). Builders run on Sonnet (owner), four at a time; a unit may stack on one whose PR is open (up to three deep)
so a lane is never idle waiting on a merge.

Not every phase shows in the app: CI-1 to CI-8 have a test or heavy-lane signal as their observable, and the first in-app signal is CI-9.

**Prerequisite, not ours:** `MainMoveDecision` and `IRepoHeads` from the context lane (owner F1), called per repo (§6.1). CI-9 is the first
unit that needs them; every earlier unit is independent of it. CI-9 may start on a local stub behind the same signature, but does not merge until the real one has.

| Phase | Unit | Depends on | Observable signal |
|---|---|---|---|
| **CI-0a** | Pins: `context/code-index.lock` (graphify version and date, Ollama model name and digest, disk need), `scripts/code-index-lock.sh check` and its bash test, and `scripts/install-deps.sh` reading it (graphify into its own environment from the hash-pinned requirements with `uv`; a new `ollama pull` step for the model; the graphify row must equal `sindri/package.json`'s pin), modelled on `scripts/onyx-lock.sh` and its tests | none | `./setup.sh --dry-run` prints the graphify and model steps from the lock; `bash scripts/tests/code-index-lock.test.sh` passes (shape, and the lock-vs-`package.json` parity) |
| **CI-0d** | The graphify spike (§5.5): the pinned graphify on a small TypeScript and Python fixture, the real `graph.json` recorded as a fixture, §5.5 filled in from it, and a written go/no-go with pass criteria: **pass** means the TypeScript fixture's known calls appear as call edges whose both ends carry a file and line; otherwise TypeScript and the other graphify-only languages answer `unavailable` for callers and callees. Done: the spike passed (§5.5, §15 Q1) | CI-0a | the recorded `graph.json`, the §5.5 text and the verdict are in the PR |
| **CI-0b** | Profile keys: `IndexSettings` (in `Aw.Sindri.Core`) grows the §13 keys with the profile's own validation, nested records marked unknown-key-rejecting, and `docs/csharp/sindri-profile.md` (each nested key listed as its own backticked name, since `scripts/check-profile-doc.mjs` matches constructor parameters by name, or the checker learns dotted paths in this PR) | none | `sindri profile validate` accepts the new keys and rejects a bad one with its key path |
| **CI-0c** | Test assets: the fixture C# project with known calls (`tests/fixtures/code-index/`), the hostile-parse files (CI12) and the 50-pair labeled sample for the quality probe (marked by the lane, reviewed by the owner) | none | the builder makes a repo the tests can read; none in the app |
| **CI-1** | Package scaffold (`/new-package`: `Aw.CodeIndex.Core · Features · Infrastructure`, tests, testing, `deps.yaml` node and edges, `docs/csharp/code-index.md` stub); Core contracts: records, ports (`IRepoSnapshot`, `IIndexStore`, `ISymbolParser`, `IGraphRunner`, `IEmbedder`, `IRepoHeads`, `IHeavyJobLock`, `IBodyScrubber`), `CodeIndexSettings`, `CodeIndexErrors`, the facade interface (including `Build`); the temp-repo builder (`git init`, commits, symlinks, a submodule entry, a swapped path, a promisor-clone fixture) in `Aw.CodeIndex.Testing`; the `deps.yaml` project edge turns `current` when Features first references Core, the compose edge in CI-9; the Roslyn pin comment and `AwAotCompatible=false` for Infrastructure | none | `node scripts/check-deps.mjs` passes with the new node; `dotnet test` shows the one red test the scaffold leaves |
| **CI-2** | `GitRepoSnapshot` and `InputRules` (ls-tree, cat-file batch, deny globs, caps, symlink and submodule skip, hook env cleared); `FakeRepoSnapshot` and `RepoSnapshotContract` | CI-1 | the contract tests run against a temp repo and the fake; none in the app |
| **CI-3** | `SqliteIndexStore`: schema v1, writes, atomic swap, sweep, damage detection, user_version rebuild; `FakeIndexStore`, `IndexStoreContract`; `BuildPlanner` (pure) | CI-1 | a store contract run on a real temp file, including a killed build that leaves the live file intact; none in the app |
| **CI-4** | `CSharpParser` (Roslyn syntax: symbols, spans, signatures, complexity, shape hash, call sites) and `CallResolver` (pure) with fixture files; a separate Roslyn version for CodeIndex.Infrastructure (`VersionOverride`: the central 5.9.0 serves `Aw.Analyzers`, which must stay on the SDK's compiler); the CI12 guards and crash quarantine | CI-1, CI-0c | golden symbols and edges for a fixture project, including each confidence and the hostile-parse files; the precision and recall of the resolver on the 50-pair sample are printed (reported, not gated) so the weak spots show before CI-6; none in the app |
| **CI-5** | Clones and deps: tokenizer, MinHash/LSH (`Clones`), `Similar` logic, manifest readers (`package.json`, `.csproj`, `Directory.Packages.props`) | CI-1 | known vectors and a near-duplicate pair found; none in the app |
| **CI-6** | `BuildCodeIndex` slice (structure; clones and deps register into it when CI-5 merges, which does not hold CI-6 back) over the fakes and real store: heavy slot, incremental plan, progress lines, cancel, report; the facade `Build` and the reads that need only these layers (`Status`, `FindSymbols`, `FileSymbols`, `Callers`, `Callees`; `Similar` after CI-5) | CI-2, CI-3, CI-4 merged (its Core and Features parts start on CI-1's fakes before they do) | a heavy-lane test that builds this repo and prints the report (files, symbols, edges, shares, ms) and asserts `Callers` on a known method; no separate host; first thing a person can run, still not in the app |
| **CI-7** | `GraphRunner` (graphify, sandboxed, pin check, snapshot, strict `graph.json` parse, merge into `edges`); `FakeGraphRunner`, contract; (pins from CI-0a, DTO from the CI-0d spike) | CI-0a, CI-0d, CI-1 (contract); CI-6 to merge | heavy lane: graphify at the pin on a fixture repo yields call edges, a request from inside the sandbox is denied; the heavy-run output is pasted in the PR |
| **CI-8** | `OllamaEmbedder` (loopback, redirect refusal, digest check, batches, resume), the embeddings layer and `Neighbors`; fake and contract | CI-1 (contract); CI-6 to merge | heavy lane: real Ollama at the pinned model; `Neighbors` ranks a known near-duplicate first; the heavy-run output is pasted in the PR |
| **CI-9** | (revision 4: also the Host adapter `CodeIndexOutlooks`, which fills F1P-b1's `IndexOutlookInput` from `ICodeIndex.Heads`, the `index:<repo>` jobs (`deferred` and `failed` from each job's result) and the service state; it replaces F1P-b2's `NoIndexOutlooks` registration, which is what turns the gate on; no new event of its own) Sindri job and service: `BuildCodeIndex` registered as a job (`index:<repo>`), `code-index-main-move` (built-in, 1 min), `codeindex.built` event, catalog rows, the `view_code_index` projector; `SindriFeatures` and Host registration (compose edge flips to `current`; `Aw.Sindri.Features` reaches the project fan-out limit of 4 with this edge), the `BuildCodeIndexRequest`/`Result` records and `BuildCodeIndexService` wrapper, the `IHeavyJobLock` adapter, the event's payload record, key and `ApiViews` name | CI-6; the context lane's `MainMoveDecision` and `IRepoHeads` (merged); **F1P-b1 and F1P-b2 merged** (b1 owns `IIndexOutlooks`, b2 its registration; CI-9 waits for the merges, so its stack stays at three: CI-9, CI-10a, CI-10b) | **In the running app:** commit to a registered repo's main; within two minutes a `BuildCodeIndex` job is in `sindri jobs` , runs, and ends `succeeded` with the repo and commit; a second commit builds incrementally; a checkout on a feature branch starts nothing; **and, with F1P-b2 merged,** the map that refresh started is held until the job ends and publishes within a tick after it (§11 hand test, first half; it needs no lens) |
| **CI-10a** | Reads: HTTP routes and goldens, `sindri code-index status|symbols|callers|callees`, scope checks | CI-9 | **In the running app:** `sindri code-index callers <ref>` answers over a built index; a session gets the uniform not-found for another repo |
| **CI-10b** | Health and recovery: the Health card (its UI slice, `ISindriApi` method, words keyed by the §7.3 state, text-fit canary and accessible names), doctor rows, `IndexContinuationDecision` wiring and layer recovery (§6.3), the failed-job backoff and the needs-you row proven end to end | CI-10a, CI-7, CI-8 (so its signal shows real graph and embeddings states) | **In the running app:** Health shows "Code index: repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%"; `sindri code-index callers <ref>` answers; breaking graphify on purpose (wrong pin) shows `graph unavailable` and the job still succeeds; three failed jobs in a row raise `failure:service:code-index-main-move` |
| **CI-11** | (revision 4: the owner-check also runs the publish-together hand test's second half of §11, with `waitForIndexMinutes: 1`) Proof: `scripts/owner-check.sh code-index`, the heavy-lane suite wired to the nightly, `docs/csharp/code-index.md`, `docs/sindri/index.md` (the C# note), the error table in `docs/csharp/sindri.md`, the README X-row; the `code-index` stage added to `scripts/owner-check.sh` and its test | CI-7, CI-8, CI-10a (its docs and owner-check parts open on main; the Health parts rebase on CI-10b) | **In the running app:** `scripts/owner-check.sh code-index up` runs the §11 end-to-end on a scratch home; `sindri doctor` is all ok on the owner's box |
| **S-9a** | (revision 4: `ICodeSymbolSource` calls take `asOf` and rows carry `commit`, `asOfCommit` and `state`, §7.2 and §6.4; the fake and its contract include a stale and an index-ahead case; the empty-state design adds "from abcdef0, not 1234567" for a map published ahead of its index) The Context side on a fake: `ICodeSymbolSource` in Context's Core with its fake and contract, the lens and file-map changes and their goldens, the `Degraded` comment, and the file-map `symbols` column designed in `mockups/context-filemap.html` with its empty states (not built, building, stale, as-of mismatch) | CI-1 (the interface only) | goldens in the Context tests with the fake; none in the app |
| **F1P-a** | **The map's store half of §6.4, the context lane (revision 4).** `Aw.Context`: the `map_hold`, `map_publish_chain` and `map_version_head` tables (one named migration, with a test that an old `context.db` opens), `IContextMap.Hold/Held/Release` (service-only) and the scoped read `PublishStatus` (sessions allowed, own repos), `MapPublishState`, `expectedVersion` on `Promote`, `CleanDraft` and `AppendOverrides` (the new `SND-CONTEXT-190`), `MapBuildReport.Heads` and commit validation (7 to 40 lowercase hex), every promote clearing the hold and the chain, `ReplaceDraft` and `OpenDraft` copying the heads, cascade deletes for retention, `ContextSettings.Publish` (range check, `SND-CONTEXT-001`, test, PublicAPI) with its line in `docs/csharp/sindri-profile.md` and the check, `MapHold.NextSince` with its unit test, `FakeContextMap` and the store contract, `PublicAPI.Unshipped.txt` for `Aw.Context.Core`, retrieval spec §8.7a made as-built in `docs/csharp/sindri-context.md` | F1 (#494) merged; none of CodeIndex | Context goldens and the store contract on a real temp file; none in the app |
| **F1P-b1** | **The map's pure Sindri half.** `Aw.Sindri.Core`: `MapPublishDecision` (over a Core record `MapHoldState`), `MappedHeads`, `IndexOutlook`, `IndexOutlookDetail`, `RepoWait`, `IndexOutlookInput`, `IndexAttempt`, `IndexOutlookRules`, the ports `IIndexOutlooks` and `NoIndexOutlooks`, each with its table of cases; `PublicAPI.Unshipped.txt` for `Aw.Sindri.Core` | none (the types it uses are its own); starts in wave 0 beside F1P-a | Unit-lane tables of cases; none in the app |
| **F1P-b2** | **The map's Sindri jobs and tick.** `Aw.Sindri.Features` and Host: the refresh job's hold, the `PublishContextMap` job (catalog row of the same class as `RefreshContextMap`, key, summary text, its 190 mapping), the service tick (hold read, publish start and backoff, move check over `MappedHeads`, merged alarm), the Features mapping from Context's `MapPublishState` to `MapHoldState`, the registration of `NoIndexOutlooks`, the events (`context.map.held`, `published`, `hold.dropped`, `refreshed.held`) with payload records, `SindriJsonContext` entries, catalog and golden changes (the existing `context.map.refreshed` goldens change), `PublicAPI.Unshipped.txt` for `Aw.Sindri.Features`, the retrieval spec's code range row; the catalog pin in `Aw.Sindri.Ui.Tests` and the `ISindriApi` goldens move by the new rows and the changed `context.map.refreshed` payload, so **its gate runs `Aw.Sindri.Ui.Tests`** | F1P-a and F1P-b1 (stacks on one of them, with the other merged: a stack of two) | Host tests with fake outlooks and a fake clock (the integration cases of §11); **the in-app proof is CI-9's**, because until CI-9 the gate is off by design (every repo `Absent`), so the running app publishes at once as today |
| **F1P-c** | **The words.** (also the fixture: `scripts/sindri-ui-fixture.sh` / `sindri-prove.sh` gain a held draft and its states, with their bash tests) The Health Context map card, `sindri context map status` and its `--json` `state`, the doctor row `context:publish`, the `jobs show` link (UI slice: `ISindriApi` method, labels, text-fit canary and accessible names, the session-scoped text); its gate runs `Aw.Sindri.Ui.Tests` | F1P-b2; the stale states render live only after CI-9 | **In the running app** (a scratch home with the prove fixture's held draft): the six states render with the §6.4 words |
| **S-9** | **Final phase, the context lane (merges after CI-11):** `CodeIndexSymbolSource` in Sindri's Host over the real facade (S-9a's port); the code lens fills `callers` and `callees`; the `code-index` source row goes live; file map `symbols` column and `coverage.code.level` from `file` to `symbol`; symbol-level `implements`; `context query --symbol path:name`; **built to §6.4 (revision 4):** the adapter passes the map version's commit as `asOf`, every row says `asOfMismatch` and both commits when they differ, the `code-index` row is `degraded / stale` then, and a test shows the rows turn fresh when the index lands at the map's commit with nothing republished | CI-10a (S-9 stacks on it; S-9a is its half) | **In the running app:** `sindri context lenses <anchor> --json` and `GET /sindri/context/code` show real `callers` and `callees` with `basis` and `confidence` and `searched.code-index: ok`; the file map shows symbols; `context query --symbol` on a C# symbol returns the features it implements (the TypeScript example of CTX Phase 9, `src/layout/SplitPane.ts:setOrientation`, is the signal: graphify gave TypeScript call edges in the CI-0d spike); a scoping run no longer records `index-not-built` |

### 14.1 The lane plan

Four builders, four lanes (A to D). A lane takes the next unit the moment its last PR opens. "Red" is the first commit of
each unit.

| Wave | Lane A | Lane B | Lane C | Lane D | Context lane (own builders, four) |
|---|---|---|---|---|---|
| 0 | **CI-1** scaffold and contracts | **CI-0a** pins and install | **CI-0b** profile keys, then **CI-0d** the graphify spike | **CI-0c** test assets | **F1P-a** store half, **F1P-b1** pure half (in parallel) |
| 1 | **CI-2** snapshot | **CI-3** store and planner | **CI-4** Roslyn parser and resolver | **CI-5** clones and deps | **F1P-b2** jobs and tick (after a and b1 are open or merged) |
| 2 | **CI-6** build slice (starts on CI-1's fakes; real adapters join as CI-2 to CI-4 merge; CI-5's layers plug in later) | **CI-7** graph layer (on the CI-0a pins) | **CI-8** embeddings layer | **S-9a** (it needs only CI-1, not the context lane's trigger) | **F1P-c** the words (stacks on F1P-b2) |
| 3 | **CI-9** job and service (with the outlooks adapter; F1P-b1 and b2 have merged), then **CI-11** install and proof | **CI-10a** reads (stacks on CI-9) | **CI-10b** Health and recovery (stacks on CI-10a) | **S-9** (adapter in Host over the real facade, the signals above; stacks on CI-10a) | free: reviews and the hand tests |

S-9a and S-9 stay in lane D (the table's builders); the F1P units are the context lane's own work in the last column, run beside lanes A to D, touch no CodeIndex code, and deliver what CI-9 needs (F1P-b1 and b2 merged) by the end of wave 1. Wave 0 keeps all four lanes busy while the scaffold lands: CI-0a, CI-0b and CI-0c touch no CodeIndex code, so they run beside CI-1 and merge in any order; CI-2 to CI-5 open as soon as CI-1 opens (they stack on it). Dependencies that decide the order: CI-6 needs CI-2, CI-3 and CI-4 (CI-5 plugs in later); CI-7 and CI-8 need only the CI-1 contract to be
built and the CI-6 slice to merge; CI-9 needs CI-6 and the shared trigger (merged); F1P-b2 stacks on F1P-a or F1P-b1 and F1P-c on F1P-b2 (a stack of three at most); no stack is deeper than three open PRs: CI-10 stacks on CI-9, CI-11's owner-check and doc parts open on main and rebase once CI-10b merges; CI-10a needs CI-9, CI-10b needs CI-10a, CI-7 and CI-8; CI-11 needs the adapters and
CI-10a; S-9's adapter needs CI-10a. S-9 stays the final phase because its observable result needs the real data; its
Context-side half (S-9a) depends only on the CI-1 interface and runs in parallel with wave 2. If the context lane's shared
trigger is late, CI-9 starts on a local stub of `MainMoveDecision` behind the same signature and swaps it in when it merges.

Stage rules from the owner (2026-10-12, 2026-10-18, memory): each phase ships as one PR, layered commits; a lane's gate runs
the build and the unit lane of the projects it touched, and the merges go through the meta's batch; Sindri PRs that add
catalog rows or action ids also run `Aw.Sindri.Ui.Tests` in their own gate (CI-9, CI-10a, CI-10b, F1P-b2, F1P-c).

## 15. Open questions for the owner (plain words)

Defaults are what the builders do if you say nothing.

- **Q1. Which languages need callers and callees in version 1?** The index reads C# itself, with the same parser the .NET
  tools use (accurate for names, not for which overload a call hits). For TypeScript, Python, Go and the rest it can only
  use what graphify finds, which is coarser. *Default:* C# first-class, everything else through graphify and marked
  lower confidence. If your work repos are mostly TypeScript, say so: a TypeScript parser (a small Node tool, pinned) would
  be a phase of its own. This one decides the plan: the TS index you have today parses TypeScript itself, so the C# build
  would give TypeScript less than you have now unless graphify's call data (checked in CI-0d) is good enough. *Settled:* the owner approved this spec with every question at its default (relayed by the meta session, 2026-10-20), and the CI-0d spike passed (TypeScript 8 of 8 known calls as call edges with file and line; Python 3 of 5), so no TypeScript parser phase is added. The fixtures are small, so CI-7 also measures recall on one real TypeScript repo: 30 symbols that have callers in the TS index, the share of those callers graphify finds, reported in the CI-7 PR body (not a gate). Under half is what would reopen a TypeScript parser phase, and the owner decides. The owner picks the repo, the 30 are drawn with a seeded random draw by a script kept beside the CI-7 tests, the TS index's callers are the reference (so the figure is a comparison with what exists, not independent ground truth), and the number is read before CI-7 merges.
- **Q2. Graphify now runs in two places.** The context map already runs it to group files into features, with its own
  safety wrapper; the index will run it again on the same commit. *Default:* the index carries its own copy for now (its own
  wrapper and its own safety tests) and, once the sandbox work lands, Context's map reads the index's graph instead and its
  copy is deleted. Say if you want the sharing first, which delays CI-7 by one cleanup PR.
- **Q3. Is "callers and callees" good enough to count in the scoping station's recall bars?** Today the bars leave them out.
  The heavy lane will report precision and recall on 50 hand-labeled pairs of this repo. *Default:* report only; you decide
  from the numbers whether to put them in the bars.
- **Q10. Clones, dependencies and embeddings have no reader in version 1.** The code lens needs symbols, callers and callees; clone
  detection, dependency rows and vectors wait for later consumers (CI11), and together they are two of about fifteen units (CI-5, CI-8). *Default:* build
  them. CI-5 and CI-8 sit in waves 1 and 2 but nothing else waits on them, so they cost the callers and callees nothing. Dropping them removes CI-5 and CI-8, the `embeddings.*` keys, the Ollama pin and doctor row, and `Neighbors` and `Similar`; the structure path is unchanged.
- **Q4. Do we build embeddings at all in version 1?** Callers and callees do not need them. They serve "find code like this"
  (the context spec's `code` source) and cost an Ollama model on the box and a longer first build (a large repo takes about ten background continuations, an hour or two). *Default:* build them (CI-8), Decided together with Q10 (what a drop removes is listed there).
- **Q5. Who pulls the Ollama model?** The background job must never download. *Default:* `./setup.sh` pulls the pinned model
  and installs the pinned graphify (CI-0a); the build only checks. A box set up before this lands shows "embeddings
  unavailable, run ./setup.sh" in Health.
- **Q6. A repo whose checkout is on a feature branch main does not contain is never indexed** (your F1 rule). The index of
  that repo stays at its last main commit, marked stale, and Health says why. Is that what you want when you are working
  in the main checkout itself on a long branch? *Default:* yes; the worktrees are where work happens.
- **Q7. Keep symbol text on disk?** The index stores each symbol's text (secrets scrubbed) so it can embed it later.
  *Default:* keep it only until the embedding is made, then drop it; the cost is that a changed model means re-reading the
  file from git, which is cheap. Say if you would rather keep it for faster rebuilds.
- **Q8. Sessions reading the index.** An agent session may ask for symbols and callers in its own repo, with names fenced as
  untrusted, the same as the context lenses. *Default:* yes.
- **Q9. One commit per repo.** The index keeps only the newest commit, so a scoping run that asks "as of last month's map"
  gets today's callers marked stale. *Default:* accept that for version 1.
- **Q11. How long should a new map wait for the code index?** (Revision 4, from your 2026-10-20 decision.) When trunk moves, the new map and the code index are both rebuilt. The new map is kept back until the index for the same commit is ready, so what you read comes from one commit. *Default:* wait up to 15 minutes after the map itself is finished, then go ahead anyway; code answers then say "Code answers are from abcdef0 (built 12 min ago, older than map v14, which is for 1234567)" until the index catches up. Is 15 minutes right? (The setting is `context.publish.waitForIndexMinutes`, 0 to 240; 0 means never wait.) A repo's very first index is never waited for (its code answers say "not built yet"); a slow later build can take longer than 15 minutes, and then you will see the stale words for a while, by design.
- **Q12. Can you still publish a waiting map by hand?** *Default:* yes: `sindri context map promote`, the command you already use to publish a map, does not wait, because you asked for it yourself; the code answers then read stale until the index catches up. Only the automatic path waits.
- **Q13. When the map goes ahead of the code index (Q11), should that ever put something in your list of things that need you?** *Default:* no. It shows on Health and in `sindri context map status`, with a count of how the last 10 publishes ended; a broken or failing index already puts its own row there, and a failing publish does too. The alternative is a row whenever the map goes ahead stale (say, three times in a row), which on a busy box can be noise.
- **Q14. With several repos in the map, should each one's exact commit be remembered with the map version?** Today a version remembers one commit. *Default:* yes, all of them, so the words "Code answers are from abcdef0; map v14 is for 1234567" are exact for every repo, not only the first. This is a small addition to the map's store. Saying no keeps one commit, and the second repo's stale words may then name the wrong commit.
- **Q15. Do the first map, and a repo's first code index, wait?** *Default:* no to both. A repo with no map yet gets its first map as soon as it is ready, and a repo whose code index has never been built is not waited for; its code answers say "not built yet" or stale. Waiting would leave the screens empty for the whole wait on a new repo.

## 16. Settled here, left to the builder

- Package name `CodeIndex` and project prefix `Aw.CodeIndex`; table, column and ref formats of §3 and §4.2 (any change is a
  `user_version` bump); the 200-row batch, 32-text embedding batch and 4,096-symbol cap carried from the TS index because they
  were measured there, to be re-measured in the heavy lane.
- The service and job names (`code-index-main-move`, `BuildCodeIndex`) and the event name `codeindex.built`; the CLI group
  `code-index` (the TS `index` group stays the TS build's).
- `CodeIndexRules` limits (200 hits, 100 edges per level, depth 3; raised to 200 by default, 1,000 and 8 per owner q93, 2026-10-23).
- File layout inside the package follows the slice recipe (`Features/BuildCodeIndex/`, `Features/Status/` and so on); every
  file under 300 lines, every method under 40, one top-level type per file (`.agents/rules/csharp.md`).
