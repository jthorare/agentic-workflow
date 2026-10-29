# Linear for Sindri's context engine

Sindri's native `linear` source reads Linear directly, read-only, with hand-written GraphQL (context spec D4, D18). It
is one of the index's sources beside GitHub and Slack (see [sources.md](sources.md)). Sindri used to read Linear through Onyx;
it now reads it itself. Nothing is written to Linear, and Sindri never connects a source on its own: you create the key and put its
pointer in the profile.

`$SINDRI` is the installed `sindri`; see [context.md](context.md).

## What it reads (D17)

The source reads:

| Record | Key | Why it matters |
|---|---|---|
| Projects | `linear:project/<uuid>` | project structure: which issues belong to which project |
| Linear Documents | `linear:doc/<uuid>` | where PRDs and TDDs live; cited with their `updatedAt` |
| Project updates | `linear:update/<uuid>` | status history of a project; its latest five are listed in the project's record (an update has no title, so search never lists one by itself) |
| Issue attachments | on the issue | the PRs that shipped an issue, and the Slack threads, Figma files and links behind it |
| Issues with their comments | `linear:issue/<uuid>` | the issues of the profile's teams, comments included, cited once (below) |

Every record keys on Linear's immutable UUID; the human identifier (`ENG-482`) is an alias, because it changes when an
issue moves team. Text search over issues runs on the owned index; the Linear calls do title and key lookup and the project,
document and attachment reads. A source that fails degrades the query instead of failing it (a Linear outage never blocks an
answer).

## 1. Create a read-only key

In Linear: Settings, Account, Security and access, Personal API keys. Create a key with **read-only** permission and
copy it. Sindri only ever sends read queries.

## 2. The profile

List the teams first (the token comes in step 3):

```yaml
context:
  sources:
    linear: { teams: [ENG, APP] }
```

| Key | Meaning | Default |
|---|---|---|
| `context.sources.linear.token` | the secret pointer to the key, written by `sindri index connect linear`; required for the native source to run | none |
| `context.sources.linear.teams` | team keys (a capital, then capitals or digits: `ENG`) that scope every read; empty reads every team the key can see | `[]` |
| `context.sources.linear.url` | Linear's GraphQL endpoint | `https://api.linear.app/graphql` |

`url` is for tests: it accepts only Linear's own host over https, or a loopback address (the fixture's fake server) when
the token is an `env:` or `file:` pointer, so the real keychain key never goes to a local listener (`SND-CONTEXT-070`).
A profile that names `url` also needs a `token` to load, so a fixture profile starts with an `env:` pointer that
`index connect` then replaces. The full key list of the `context:` section is in [../csharp/sindri-profile.md](../csharp/sindri-profile.md).

## 3. Connect the key

At your own terminal:

```bash
$SINDRI index connect linear
$SINDRI profile approve
```

It asks for the key once (not shown) and checks it can read every team in `teams` (with no team listed, at least one)
before saving anything. Only then does it store the key in the OS keychain (service `sindri-index`, account `linear`; an
owner-only file under `$AW_SINDRI_HOME/secrets/` when there is no keychain, and always the file for a loopback `url`) and
write the pointer into `context.sources.linear.token`. It is the human's verb: an agent session is refused
(`SND-AUTH-002`). A key Linear refuses is `SND-INDEX-009`; a listed team it can't read, or no team at all, is
`SND-INDEX-010`, naming the teams; an unreachable Linear passes `SND-CONTEXT-010` through. Nothing is saved on any failure.

To point at a key you stored yourself instead, write the pointer by hand: `keychain:<service>/<account>`, `env:<VAR>`,
`op:<vault>/<item>/<field>` or `file:<absolute path>` (a regular file, mode 0600 or stricter). A value that is not a
pointer is refused (`SND-CONTEXT-002`) without repeating it; rotate a key that was ever written into a profile.

```bash
$SINDRI profile validate          # prints the profile's hash; exit 1 means valid but not approved yet
$SINDRI profile approve <hash>    # type the challenge it shows
```

Validate checks the pointer's scheme and the url; it does not resolve the pointer. Doctor does.

## 4. Check it

```bash
$SINDRI doctor                          # context:linear
$SINDRI context sources status          # reads projects, documents and issues of the listed teams
```

`context:linear` makes no call to Linear. It reports whether the source is configured, the teams, and whether the token
pointer resolves:

| Result | When | Fix |
|---|---|---|
| `ok` | the profile names no `context.sources.linear.token` (`not configured`); or the pointer resolves (`configured: teams ENG, APP; keychain:sindri-index/linear resolves`) | — |
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

An issue's stored text is its description, then `Comments:`, then `Attachments:` (every attachment, one line each: `- <title> (<sourceType>) <url>`; a missing title shows the url's host and a missing source type shows `link`), then `Images:`. The ingest sanitizer removes markdown and HTML images from stored text, so every image found in the description, the comments and the image attachments (png, jpg, jpeg, gif, webp, or an `uploads.linear.app` url) is listed again as `- <alt or image> <url>`, once per url. The images themselves are not fetched or read: only their alt text and link are kept. The text is kept whole, with no cut: the `Project: <name>` line, the `Relations:` section (`- <type> <ID> <title>` for the issue's relations, `- (from) <type> <ID> <title>` for those pointing at it), the `Attachments:` section and the `Images:` section follow the description and the comments. Of the issue's own fields only the project and the relations are added; labels, cycle, assignee and state are not in the stored text. (Changed for owner q105, 2026-10-24.)

An `env:` pointer is read from the control plane's environment as it was when `serve` started, while doctor reads the
calling shell's, so the two can disagree; `file:` and (on macOS) `keychain:` read the same in both.

## 5. The record feeds and the one-time re-read

With Linear configured the owned record index runs two feeds, each with its own cursor, lock and row in `sindri index status` and in the `record index: ...` detail of `context:index-sync`:

- `linear`: the issues of the profile's teams. Its first read (no watermark) takes the recent window plus the old open issues of open projects; every later read has a watermark and never carries that clause. Issues are stored with a `;f2` version suffix.
- `linear-projects`: the projects, as `linear:project/<id>` records. After each tick of this feed the order is: the decision pass for the feed, then the viewer step (who the Linear key belongs to).

Upgrading to the build with migration 0020 resets the `linear` feed to Recent with no watermark, so every stored issue is re-saved once (it gains its project link) and the old open issues of open projects come in. While that runs, `sindri index status` shows the `linear` row as `re-reading for project links, <n> issues stored` and `context:index-sync` warns with the same words. A source in Recent that already holds records counts as re-reading; the count is what the store holds, not how many have been re-saved. The row goes back to `usable` once the read reaches the end and sets the watermark.
