# Linear for Sindri's context engine

Sindri's native `linear` source reads Linear directly, read-only, with hand-written GraphQL (context spec D4, D18). It
sits beside the `onyx` source (see [onyx.md](onyx.md)) and adds what Onyx's Linear connector does not read. Nothing is
written to Linear, and Sindri never connects a source on its own: you create the key and put its pointer in the profile.

`$SINDRI` is the installed `sindri`; see [context.md](context.md).

## What Linear adds over Onyx's connector (D17)

Onyx's Linear connector reads issues and comments only. The native source also reads:

| Record | Key | Why it matters |
|---|---|---|
| Projects | `linear:project/<uuid>` | project structure: which issues belong to which project |
| Linear Documents | `linear:doc/<uuid>` | where PRDs and TDDs live; cited with their `updatedAt` |
| Project updates | `linear:update/<uuid>` | status history of a project; its latest five are listed in the project's record (an update has no title, so search never lists one by itself) |
| Issue attachments | on the issue | the PRs that shipped an issue, and the Slack threads, Figma files and links behind it |
| Issues with their comments | `linear:issue/<uuid>` | the same issues Onyx returns, cited once (below) |

Every record keys on Linear's immutable UUID; the human identifier (`ENG-482`) is an alias, because it changes when an
issue moves team. An issue that arrives through both Onyx and the native source is deduplicated before ranking and cited
once, carrying both sources. Onyx still does the text search over issues; the native source does title and key lookup and
the project, document and attachment reads. A source that fails degrades the query instead of failing it (a Linear
outage never blocks an answer).

## 1. Create a read-only key

In Linear: Settings, Account, Security and access, Personal API keys. Create a key with **read-only** permission and
copy it. Sindri only ever sends read queries.

Store it by pointer, never in the profile:

```bash
security add-generic-password -s linear -a work-read -w      # macOS: prompts for the key
secret-tool store --label 'linear/work-read' service 'linear' account 'work-read'   # Linux (libsecret): prompts for the key
```

The profile pointer is the same on both machines: `keychain:linear/work-read`. The other pointers are the ones Onyx
takes: `env:<VAR>`, `op:<vault>/<item>/<field>` and `file:<absolute path>` (a regular file, mode 0600 or stricter). A
value that is not a pointer is refused (`SND-CONTEXT-002`) without repeating it; rotate a key that was ever written into a
profile.

## 2. The profile

```yaml
context:
  sources:
    linear: { token: "keychain:linear/work-read", teams: [ENG, APP] }
```

| Key | Meaning | Default |
|---|---|---|
| `context.sources.linear.token` | the secret pointer to the key; required for the native source to run | none (without it only `teams` is read, as Onyx's identifier aliases) |
| `context.sources.linear.teams` | team keys (a capital, then capitals or digits: `ENG`) that scope every read; empty reads every team the key can see | `[]` |
| `context.sources.linear.url` | Linear's GraphQL endpoint | `https://api.linear.app/graphql` |

`url` is for tests: it accepts only Linear's own host over https, or a loopback address (the fixture's fake server) when
the token is an `env:` or `file:` pointer, so the real keychain key never goes to a local listener (`SND-CONTEXT-070`).
The full key list of the `context:` section is in [../csharp/sindri-profile.md](../csharp/sindri-profile.md).

```bash
$SINDRI profile validate          # prints the profile's hash; exit 1 means valid but not approved yet
$SINDRI profile approve <hash>    # type the challenge it shows
```

Validate checks the pointer's scheme and the url; it does not resolve the pointer. Doctor does.

## 3. Check it

```bash
$SINDRI doctor                          # context:linear
$SINDRI context sources status          # reads projects, documents and issues of the listed teams
```

`context:linear` makes no call to Linear. It reports whether the source is configured, the teams, and whether the token
pointer resolves:

| Result | When | Fix |
|---|---|---|
| `ok` | the profile names no `context.sources.linear.token` (`not configured`); or the pointer resolves (`configured: teams ENG, APP; keychain:linear/work-read resolves`) | — |
| `warn` | linear is named but no repo is mapped, so no context source runs | map a repo in `repos` and `context.repos` |
| `fail` | the pointer doesn't resolve (`SND-SECRET-001`..`003`: the scheme and the target are named, never a value); or the settings are refused (`SND-CONTEXT-070`) | fix the target as the message says |

Whether Linear accepts the key is `context sources status`, not doctor:

| Code | Meaning |
|---|---|
| `SND-CONTEXT-071` | Linear refused the key (missing, revoked, or it can't read): create a new read-only key |
| `SND-CONTEXT-072` | Linear answered with an unexpected shape; the query goes on without it |
| `SND-CONTEXT-073` | Linear has no record at the cited version (deleted, left the teams, or changed) |
| `SND-CONTEXT-010` | Linear was unreachable, missed the deadline or said `rate limited, retry later` (HTTP 429 or a `RATELIMITED` error); the query goes on without it |

The profile's `teams` are applied by Linear in the filter of each read, so the source finds its records however many other projects the workspace has. Lists are read a page at a time (250 nodes, at most 20 pages; the log says when that cut one). An issue's first 50 comments and attachments are read, and its record ends with `(more comments not shown)` when there are more.

An issue's stored text is its description, then `Comments:`, then `Attachments:` (every attachment, one line each: `- <title> (<sourceType>) <url>`; a missing title shows the url's host and a missing source type shows `link`), then `Images:`. The ingest sanitizer removes markdown and HTML images from stored text, so every image found in the description, the comments and the image attachments (png, jpg, jpeg, gif, webp, or an `uploads.linear.app` url) is listed again as `- <alt or image> <url>`, once per url. The images themselves are not fetched or read: only their alt text and link are kept. The text is kept whole, with no cut: the `Project: <name>` line, the `Relations:` section (`- <type> <ID> <title>` for the issue's relations, `- (from) <type> <ID> <title>` for those pointing at it), the `Attachments:` section and the `Images:` section follow the description and the comments. Of the issue's own fields only the project and the relations are added; labels, cycle, assignee and state are covered by Onyx. (Changed for owner q105, 2026-10-24.)

An `env:` pointer is read from the control plane's environment as it was when `serve` started, while doctor reads the
calling shell's, so the two can disagree; `file:` and (on macOS) `keychain:` read the same in both.
