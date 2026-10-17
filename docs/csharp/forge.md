# Aw.Forge (C# build)

`Aw.Forge` is the forge port (PR babysitter spec `docs/superpowers/specs/2026-10-15-sindri-pr-babysitter-docs-sync-design.md` §10.2; PB10, CFG-13, W27). Recipes, blocks and Sindri's services reach a code forge only through `IForge`; nothing in a recipe names GitHub. It is a tier-1 package with no Features project (the spec lists none: the port is called directly by Sindri's services and effects, so there is no slice to host). Sindri does not reference it yet; the first caller is PB-W.

## Layout

| Project | Holds |
|---|---|
| `Aw.Forge.Core` | `IForge` and `IForgeSeed`, the DTOs (`PullSnapshot`, `ForgeThread`, `PushRequest`, ...), `ForgeRef.Parse`, `ForgeHosts`, `ForgeCredentialSpec`, `ForgeMarker`, `ForgeErrors` |
| `Aw.Forge.Infrastructure` | `GitHubForge` (REST and GraphQL over `HttpClient`), `FileForge` (a JSON world under a directory), `GitPusher`, the two credential sources, registration |
| `Aw.Forge.Host` | `aw-forge-fake`, the CLI over the file adapter (and, for reads, GitHub) |
| `Aw.Forge.Testing` | `FakeForge` (the in-memory world), `ForgeReadContract`, `ForgeContract`, `ForgeWorlds` |

## The port

| Member | Meaning |
|---|---|
| `Parse(link)` | A pull request link to a `ForgeRef`; refuses, naming the part, a link that is not https, carries credentials or a port, or whose host is not allowed. Sends nothing |
| `GetPull`, `GetPullDigest` | The snapshot (status, draft, base, head, mergeable, review decision, checks) and the poller's cheap digest with a version that changes when the pull changes |
| `ListThreads` | Threads, review bodies and top-level comments with association, bot, resolved and outdated flags. Text from the forge is untrusted |
| `CheckLog` | A check's log tail, capped at `ForgeLimits.LogExcerptCharacters` and scrubbed |
| `Reply`, `React` | One reply or reaction per idempotency marker (`ForgeMarker`); a second call with the marker returns `AlreadyDelivered` and sends nothing |
| `Push` | An explicit sha to the profile's URL with a lease on the branch tip: `Pushed` or `HeadMoved` |
| `FindPull`, `OpenPull`, `UpdatePull`, `Retarget` | Open, find (by head branch, author and marker), edit and re-point a pull request |
| `Merge` | `Merged`, `Blocked` (with the forge's reason) or `HeadMoved` when the head is not the expected sha |
| `RerunChecks`, `DefaultBranch` | Re-run failed checks of a commit; a repository's default branch |

`IForgeSeed` (file adapter and `FakeForge` only) seeds a world for tests and proofs: threads, check states and out-of-band head moves.

### Hardened push

`GitPusher` pushes from a fresh empty bare git directory that borrows the repository's objects, so nothing in the repository's own config (credential helpers, `core.sshCommand`, `url.*.insteadOf`, hooks) reaches git. System and global config are off, `core.hooksPath` is `/dev/null`, `--no-verify` is set, the credential travels in `GIT_CONFIG_*` environment variables (never an argument), and the lease is `--force-with-lease=refs/heads/<branch>:<sha>` (an empty lease means the branch must not exist). A refusing `pre-push` hook in the worktree and a repo-local `url.*.insteadOf` therefore have no effect. A stale lease is `HeadMoved`; any other git failure is `AW-FORGE-008` with a scrubbed, capped tail of git's output.

## Adapters

| Adapter | Selected by | Use |
|---|---|---|
| `github` | `AW_FORGE_ADAPTER=github` (profile `workflow.forge.adapter: github`) | github.com and GitHub Enterprise (`https://<host>/api/v3`), a bearer token, one re-ask of the credential on a 401 |
| `file` | `AW_FORGE_ADAPTER=file` (the default) | A JSON world under `$AW_STATE_DIR/forge/file`, with a lock file; real git for a push. Tests and proofs |
| `FakeForge` | `ForgeWorlds.Fake()` in `Aw.Forge.Testing` | The same rules over an in-memory store; a push is simulated. Not shipped in the production assemblies |

Every adapter refuses a host outside `AW_FORGE_HOSTS` with `AW-FORGE-002` before any store access or request.

## CLI

```bash
dotnet run --project src/Forge/Aw.Forge.Host -- <command> [options]
```

| Command | Meaning |
|---|---|
| `parse <link>` | Parse a pull request link |
| `open --repo <host/owner/name> --head <branch> --base <branch> --head-sha <sha> --title <text> [--body <text>] [--draft]` | Open a pull request |
| `pull <link>` | Print the snapshot |
| `threads <link>` | Print the conversation |
| `seed thread <link> --author <login> --body <text> [--kind thread\|review\|comment] [--association <text>] [--path <file>] [--bot]` | Seed a thread (file adapter) |
| `seed checks <link> --state none\|pending\|passing\|failing [--failing <name,name>]` | Seed the head's checks (file adapter) |
| `seed advance <link> --sha <sha>` | Move the head out of band (file adapter) |

Each command prints one JSON document on stdout. Exit codes: 0 ok; 1 invalid input or not found (`error: ... (AW-FORGE-NNN)` on stderr); 2 usage; 3 refused or the forge unavailable (`refused: ... (AW-FORGE-NNN)`).

## Errors

| Code | Kind | Meaning |
|---|---|---|
| `AW-FORGE-001` | invalid | A link, ref or request failed validation; the message names the part |
| `AW-FORGE-002` | forbidden | The host is not in the allowed hosts; nothing was sent |
| `AW-FORGE-003` | forbidden | The credential is missing, expired or rejected (also an HTTP 403 that is not a rate limit) |
| `AW-FORGE-004` | not found | No such pull request, repository, thread or check |
| `AW-FORGE-005` | unavailable | The forge or the network is unavailable (transport failure, timeout, HTTP 5xx) |
| `AW-FORGE-006` | rate limited | HTTP 429, or a 403 with no rate limit left |
| `AW-FORGE-007` | invalid | The forge refused a well-formed request |
| `AW-FORGE-008` | unavailable | git could not push (not a lease refusal, which is `HeadMoved`) |

## Configuration and credentials

| Variable | Meaning |
|---|---|
| `AW_FORGE_ADAPTER` | `file` (default) or `github`; anything else fails at composition (exit 2) |
| `AW_FORGE_HOSTS` | Comma list of allowed hosts; default `forge.test` (file) or `github.com` (github) |
| `AW_FORGE_CREDENTIAL` | `env:NAME` or `command:<argv>`; default `env:AW_FORGE_TOKEN` |
| `AW_FORGE_TOKEN` | The token the default `env:` source reads |
| `AW_FORGE_GITHUB_REPO` | `owner/name` of the scratch repository for the heavy-lane contract |
| `AW_FORGE_GITHUB_WRITE` | `1` to let the heavy lane also run the write contract (it writes branches and a pull request) |
| `AW_STATE_DIR` | Root of the file adapter's world (`forge/file`) |

In Sindri the same three settings are the profile's `workflow.forge.adapter`, `.hosts` and `.credential` (`docs/csharp/sindri-profile.md`); they are re-approved like any profile change. The credential itself is never in the profile.

- A credential is bound to one host (spec 10.2): both sources hand out nothing for a host other than the one they were built for, and the `github` adapter accepts exactly one host in `AW_FORGE_HOSTS` (more is refused at composition, exit 2), so one token never reaches a second host. Several hosts means several adapters, each with its own credential, when that is needed.
- `env:NAME` reads a long-lived token from the variable on each call.
- `command:<argv>` runs the program (split on spaces, no shell, 30 s limit) and reads a bare token or JSON `{"token": "...", "expiresAt": "..."}`. A token with an expiry is cached until five minutes before it; a bare token for five minutes. A 401 drops the cache and runs the command once more, so a one-hour App token from the owner's launcher outlives no babysitter.
- A token is never written to a log, an error or a result: failures say that the credential command failed, or name the variable, never the value; GitHub text and git output pass through `Aw.Redaction` first. Agent Steps never hold a credential; only Sindri's services and effects do (PB11, LN-LCH1).

## Tests

| Lane | What |
|---|---|
| unit | `ForgeContract` over `FakeForge`; `GitHubForge` over a scripted `HttpMessageHandler` (URL, bearer header, an unlisted host sends nothing, 401 refresh once, 403/404/429/5xx mapping, pagination, merge outcomes); `ForgeRef.Parse`; credential specs and the env source; the host's DI validation |
| integration | `ForgeContract` over `FileForge` in a temp directory; `GitPusher` against a local bare repository (lease, stale lease, new-branch lease, ignored `pre-push` hook, ignored `url.insteadOf`, `AW-FORGE-008`; the credential header reaching git through a `file://` push and appearing in no output); `GitHubForge.Push` through a git shim (header in the environment, none in an argument); `CommandCredentialSource` caching with `FakeTimeProvider` |
| heavy | `GitHubForgeContractTests`: the read side of the contract against a real scratch repository; `GitHubForgeWriteContractTests`: the write side (push with a lease, open, find, reply and react once per marker, update, retarget, merge) |

### The owner's heavy-lane GitHub check

GitHub cannot be seeded from the outside, so the real adapter is proven by the owner, once per box, against a scratch repository (never a work repository):

1. Create a scratch GitHub repository with at least one open pull request, and a token that can read it.
2. `export AW_FORGE_GITHUB_REPO=<owner>/<name> AW_FORGE_TOKEN=<token>` (or set them in the shell that runs the tests; the test never prints them).
3. `dotnet test --project tests/Aw.Forge.Tests --filter-trait "Lane=heavy"` (under a heavy-job slot: `scripts/heavy dotnet test ...` where the lock is in use).

Without both variables every heavy test skips and nothing is sent. With them the read side runs on the real API: an unlisted host sends no request, link parsing, a missing pull is `AW-FORGE-004`, the repository's default branch, and `FindPull` with no match.

The write side is a second test, `GitHubForgeWriteContractTests`, and runs only when `AW_FORGE_GITHUB_WRITE=1` is also set, because it writes to the repository. Use a scratch repository you do not mind writing to (the token needs contents and pull-request write). It clones the repository, pushes two branches (`aw-forge-contract-<time>` and `...-base`), opens a pull request from the first, finds it by marker, replies and reacts twice with one marker each (the second is `AlreadyDelivered`), pushes a second commit on a lease (`Pushed`, then `HeadMoved` for the stale lease), updates the title, retargets to the other branch and back, tries a merge on a stale head (`HeadMoved`) and then on the current head (`Merged`, or `Blocked` when the repository's rules say so). Both branches are deleted at the end, which closes the pull request if it did not merge. The same cases run against the fake and file adapters in `ForgeContract`, and GitHub's transport and error mapping also have stubbed-HTTP unit tests. The write test has not yet run against a real repository; the first owner run is its first proof.
