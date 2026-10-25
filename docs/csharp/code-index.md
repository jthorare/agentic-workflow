# Aw.CodeIndex (C# build)

`Aw.CodeIndex` is Sindri's code index block: it indexes a repo's symbols, callers and callees, built in the background when the repo's trunk moves. Spec: `docs/superpowers/specs/2026-10-19-sindri-code-index-design.md`. This page is a stub from CI-1 (scaffold and contracts); later units fill it in (CI-11 completes it).

## Layout

| Project | Holds |
|---|---|
| `Aw.CodeIndex.Core` | The facade `ICodeIndex`, the records, the ports (`IRepoSnapshot`, `IIndexStore`, `ISymbolParser`, `IGraphRunner`, `IEmbedder`, `IRepoHeads`, `IBodyScrubber`), `CodeIndexSettings`, `CodeIndexErrors`, `CodeIndexRules` |
| `Aw.CodeIndex.Features` | The `BuildCodeIndex` slice (CI-6) and the `EmbedCodeIndex` slice (no heavy slot; `ICodeIndex.Embed`) |
| `Aw.CodeIndex.Infrastructure` | The adapters (CI-2 to CI-8) |
| `Aw.CodeIndex.Testing` | The fakes, the temp-repo builder (`GitRepoBuilder`) and the contract bases |

## Sindri services and jobs

- `code-index-main-move` (built-in, every 1 minute) starts the `BuildCodeIndex` job (key `index:<repo>`, **no heavy slot**: the build takes the repo's index lock only, `IRepoIndexLock`, so two builds of one repo never overlap; owner 2026-10-24) when a repo's trunk moved.
- `code-index-embed` (built-in, every 1 minute) starts the `EmbedCodeIndex` job (key `embed:<repo>`, **no heavy slot**) for each repo whose embeddings layer is `pending`, and again after each job until the layer is `ok`, so a repo is fully embedded without a HEAD move. The job's result and detail line (`sindri jobs show`) say how many vectors the run **made** and how many it **carried** over from the previous index (`embedded 312, carried 41,228 of 41,540`), so a full re-embed after a main move is visible; `of` counts only symbols that can have a vector (a stored text or a vector), so a symbol whose file could not be read does not keep the layer pending. `EmbedDecision` (`Aw.Sindri.Core/Scheduling`) decides per repo: a running job, then a layer that is not pending, then the wait after the newest ended job (failures in a row: 5, 10, 20, 40, 60 minutes; a busy or unbuilt index, `SND-CODEIDX-016` or `013`: 2 minutes, not a failure; a pending layer with no more vectors than the job before: 10 minutes).

## Errors

Area `SND-CODEIDX` (`CodeIndexErrors` in `Aw.CodeIndex.Core`); 017 to 029 are reserved. Layer codes (006 to 009) are carried in a layer row and the build report and fail only the call that depends on the layer.

| Code | Kind | Meaning |
|---|---|---|
| `SND-CODEIDX-001` | Invalid | the `index:` section has a bad key or value (the message starts with the key path), or a repo name is not file-safe |
| `SND-CODEIDX-002` | NotFound | the repo path is not a git repo, git refuses it as unsafe, or the commit is not in it |
| `SND-CODEIDX-003` | Invalid | the tracked code input is over `index.maxTotalMB` |
| `SND-CODEIDX-004` | retired | was: every heavy-job slot stayed busy (a build takes no heavy-job slot now) |
| `SND-CODEIDX-005` | Invalid | the embedding URL is not loopback, or the model is a cloud model |
| `SND-CODEIDX-006` | Unavailable | the embedding server failed or answered in an unexpected shape (a layer code) |
| `SND-CODEIDX-007` | Unavailable | no network sandbox for graphify (a layer code) |
| `SND-CODEIDX-008` | Unavailable | graphify is missing, failed, timed out, or wrote no usable or too large graph (a layer code) |
| `SND-CODEIDX-009` | Unavailable | a tool or model does not match `context/code-index.lock` (a layer code) |
| `SND-CODEIDX-010` | Unavailable | the index file is damaged; it is deleted and rebuilt on the next tick |
| `SND-CODEIDX-011` | NotFound | the symbol ref names no symbol in this index (also a repo outside the caller's scope) |
| `SND-CODEIDX-012` | Invalid | a query is empty, too broad, or over a limit |
| `SND-CODEIDX-013` | NotFound | no index for this repo yet (also a repo outside the caller's scope) |

## Contracts (CI-1)

- Typed values: `RepoName` (file-safe, at most 64 characters), `CommitSha` (40 lower-case hex, never an abbreviated SHA: an adapter resolves and passes full SHAs), `SymbolRef` (`<repo>:<path>#<qualified name>`).
- Every Callers/Callees row carries `coverage` (`full` for C#, `partial` for a graphify language, `unavailable` for an unmeasured one), and the answer carries one too (`CallCoverage.Of`): any partial row, or a partial language, marks the whole answer partial, and an empty partial answer reads "none found", never "none".
- An incremental build carries the vectors of every unchanged file, graph files included: the graph layer rewrites a non-C# file only when its blob or the symbols graphify plans for it differ from the stored ones (rewriting a file drops its symbols' vectors), so a main move re-embeds only what changed (#990).
- Ports, each documented with what an implementation must guarantee: `IRepoSnapshot` (+ `IBlobReader`), `IIndexStore` (+ `IIndexReader`, `IIndexBuild`), `ISymbolParser`, `IGraphRunner`, `IEmbedder`, `IRepoHeads`, `IBodyScrubber`.
- The facade `ICodeIndex`: `Build`, `Status`, `Heads`, `ProbeLayers`, `FindSymbols`, `FileSymbols`, `Callers`, `Callees`, `Similar`, `Neighbors`; every call takes a `CodeIndexScope` (the caller's repos, each with its effective settings, and the caller).
- `Aw.CodeIndex.Testing`: `GitRepoBuilder` / `TempGitRepo` (git init, commits, symlinks, a submodule entry, a path swapped after the commit, a promisor clone), `FakeRepoSnapshot`, `FakeIndexStore`, `FakeRepoHeads`, `FakeBodyScrubber`, `FakeSymbolParser`. The contract bases come with the adapters (CI-2, CI-3, ...).
- `Aw.CodeIndex.Infrastructure` is `AwAotCompatible=false`: the C# parser (CI-4) uses Roslyn, which is not trim or NativeAOT safe.

## Read surfaces (CI-10a)

Sindri's reads of the facade live in `Aw.Sindri.Features/CodeIndexReads` (the slices) and `Aw.Sindri.Host` (routes, CLI, text); the facade is `ICodeIndex`, scoped by `CodeIndexScopes` from the approved profile's repos plus every live-profile repo it doesn't name (owner q102, CIX-L59: a repo added with `sindri repo add`, by the owner or an agent session, is indexed on the next `code-index-main-move` tick with no re-approval; `index.denyPaths` and the embeddings address come from the approved profile only, a live-only repo's own `denyPaths` add to them and its entry never changes the embeddings address or model, it may only turn embeddings off; status health rows and doctor rows list live-only repos too; no approved profile is still SND-PROFILE-005 and nothing indexed; `CodeIndexJobScopes` is the jobs slice's own copy, AW1003) and the caller (a session's scope holds every repo, approved and live-only, like a human's, since owner q93, 2026-10-23; a repo outside the profile, and one never built, answer the same SND-CODEIDX-013; the jobs slice's scope holds only a session's own repo).

| Surface | What |
|---|---|
| `sindri code-index status [--repo R] [--json]` | each repo's state, commit, layers and counts; exit 1 when a repo is not `ok` |
| `sindri code-index symbols <query> [--repo R] [--prefix] [--json]` | symbols by name, path, `path#name` or ref (exact), or by name prefix |
| `sindri code-index callers\|callees <ref> [--depth 1..8] [--repo R] [--json]` | edges with basis, confidence and coverage; `--repo` lets a ref be written `path#name` |
| `GET /sindri/code-index/status`, `/symbols`, `/callers`, `/callees` | the same, query class, goldens in `tests/Aw.Sindri.Tests/Golden/Api/code-index-*` |

Exit codes: 0 ok; 1 attention (a repo not ok, the control plane down, SND-CODEIDX-011 and -013); 2 error (including -012). Callers and callees carry `coverage` (`full`, `partial` or `unavailable`) per row and for the answer, both taken from the facade's `CallEdge` and `CallsAnswer` (the answer rule is `CallCoverage.Of`, in `Aw.CodeIndex.Core.Facade`): any partial row, or a partial language, makes the answer `partial`, and an empty partial answer reads "none found", never "none". The CLI prints the answer's coverage in the header line, each row's after its basis (`certain (syntax, full)`), and the note line (`none found (coverage partial: ...)`); `--json` and the routes carry the `coverage` and `note` fields. Sessions get the repo text (names, signatures, paths) fenced as untrusted by the server. There are no MCP tools in version 1 (spec §10). Until CI-9 registers the real facade, the host answers every repo as `missing` through `UnbuiltCodeIndex`.
