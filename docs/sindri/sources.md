# Sources for Sindri's context engine

Sindri reads business intent (GitHub issues and pull requests, Slack threads, Linear issues and projects) itself, read-only,
into its own record index: one SQLite store under `$AW_SINDRI_HOME` that holds the records, their chunks and vectors
(owned index spec `docs/superpowers/specs/2026-10-27-sindri-owned-index-design.md`). Nothing is written back to a source,
and Sindri never connects a source on its own: you make a read-only token, `sindri index connect` stores it, and the
profile lists exactly which repos and channels Sindri may read. Connecting a company's Slack or GitHub needs the company's
sign-off first.

`$SINDRI` is the installed `sindri`; see [context.md](context.md). Linear has its own page: [linear.md](linear.md).

## Quick start

1. **List the sources in the profile** (`$AW_SINDRI_HOME/profile.yaml`; [profile.md](profile.md)):

   ```yaml
   context:
     sources:
       github:
         repos: [acme/api, acme/web]          # owner/name, at most 500
       slack:
         workspace: acme                      # the name in https://acme.slack.com
         channels:
           - { id: C0123456789, name: eng }   # C… or G… ids; direct messages are refused
   ```

   A record outside these lists is dropped at the connector, before it reaches the store.

2. **Connect each one, at your own terminal:**

   ```bash
   $SINDRI index connect github
   $SINDRI index connect slack
   ```

   Each asks for the token once (not shown), checks it **and** every listed repo or channel before saving anything, then
   stores it in the OS keychain (service `sindri-index`, account `github` or `slack`; an owner-only file under
   `$AW_SINDRI_HOME/secrets/` when there is no keychain) and writes the pointer into the profile's `token` key. A wrong
   scope fails here, not hours into a sync. It is the human's verb: an agent session is refused with `SND-AUTH-002`.

3. **Approve the profile:** `$SINDRI profile approve`. The sources and the token pointers are profile changes, so Sindri
   waits for your approval before it reads anything.

4. **Sync:** the built-in `index-sync` service runs every 5 minutes while `serve` runs. To run it now and follow it:

   ```bash
   $SINDRI index sync [<source>] [--detach]
   $SINDRI index status
   ```

The app's setup page has the same order: approve the profile, then the **Connect your sources** step, which is done when
every GitHub and Slack source the profile names has its token.

## The tokens

| Source | Token | What it may read |
|---|---|---|
| GitHub | A fine-grained personal access token you make | Contents, Issues, Pull requests and Metadata read, on the listed repos only (`SND-INDEX-010` names a listed repo it can't read) |
| Slack | A bot token, in a Slack app you install | `channels:history` and `channels:read`; `groups:history` only for listed private channels |
| Linear | The read-only API key in [linear.md](linear.md) | The profile's teams |

The profile holds a secret pointer (`env:NAME`, `file:/path` at mode 0600, `keychain:service/account`,
`op:vault/item/field`), never the token itself; anything else is refused and never echoed. GitHub Enterprise: set
`context.sources.github.host` (for example `ghe.example.com`); a private address also needs `allowPrivateHost: true`.

## What a sync does

Each source is read in phases: the **recent** 30 days first, newest first, so the first answers come within minutes; then
older records **back to the window's start** (`context.index.windowDays`, default 183 days) and no further; then changes
since the last sync every 5 minutes; and a daily reconcile that catches deletes and edits. You see two milestones per
source in `index status` and on Home: **usable** (the recent window is in) and **complete** (all of it is in and embedded).
`index status` ends with one `concepts:` line: how many cross-source concepts exist, how many cite two or more sources, how many wait for a name, how many embedded records they cover, and how many records have no vector yet.

```
github   usable, filling older history · last sync 14:05
slack    complete · last sync 14:05
linear   last sync failed: SND-INDEX-009 (…) · last sync 13:40
```

A record is searchable by its words as soon as its page is stored. Search by meaning needs its vectors, which a separate,
resumable embed job makes with the local Ollama model (`$SINDRI index embed`, `index embed pause|resume`). While vectors
are missing, a query answers from what exists and says so ("meaning search is off: Ollama isn't running").

`$SINDRI index purge <source>` deletes one source's records, chunks and vectors after showing the counts and asking you to
type `yes`; the next sync refills only what the source still has.

## Health

`$SINDRI doctor` has one row per piece (with no approved profile, or a source the profile doesn't name, a row is `ok ...
not configured`):

| Row | What it checks | Fix |
|---|---|---|
| `context:index-store` | the record store answers | the code on the row |
| `context:index-github`, `context:index-slack`, `context:index-linear` | the source's token pointer resolves (never its value; no call reaches the source) | `$SINDRI index connect <source>` |
| `context:index-ollama` | the local embedder answers with its model | start Ollama; `scripts/install-deps.sh` |
| `context:index-sync` | each source's last sync, as `index status` shows it | per code |
| `index:onyx-leftover` | an old Onyx install left containers, volumes, files or keychain entries | `$SINDRI onyx down --purge` (below) |

The codes: `SND-INDEX-009` the token pointer doesn't resolve or the source refused the token; `SND-INDEX-010` a listed repo
is unreadable with this token; `SND-INDEX-011` and `SND-INDEX-013` a source, repo, host or channel isn't in the profile
(no request was sent). All of them are in [errors.md](errors.md).

## A box that ran the old Onyx

Sindri used to read these sources through a local Onyx. It doesn't any more, but Onyx's volumes on a box that once ran
`sindri onyx up` still hold a raw, unscrubbed copy of the Slack and Linear text. Run this once, at your terminal:

```bash
$SINDRI onyx down --purge
```

It lists what it will delete (the containers and volumes carrying the compose label Sindri recorded, `$AW_SINDRI_HOME/onyx/`
and the keychain entries Sindri created, nothing else), asks for your yes, then removes them. If a step fails
(`SND-INDEX-014`, usually Docker not running), fix it and run it again; it resumes from what is left. Then reconnect GitHub
and Slack with `index connect` and approve the profile once more.

Until then `index:onyx-leftover` fails; set `context.index.acknowledgeOnyxLeftover: true` to make it a warning instead. A
box that never ran `onyx up` sees none of this.
