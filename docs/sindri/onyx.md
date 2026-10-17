# Onyx for Sindri's context engine

Sindri's `onyx` source reads business intent (Linear issues with their comments, Slack, docs spaces) from a local Onyx
Community Edition. Onyx stays unmodified; Sindri uses it for recall only and does its own ranking (context spec D2,
D3). This page sets Onyx up so it is pinned, bound to loopback, keeps all data on the machine, and gives Sindri one
admin key on its own service account (§5). Connecting Onyx to company Linear and Slack uses company OAuth: get sign-off first. Sindri never connects a
source itself.

`$SINDRI` is the C# Sindri build (before the switch, `~/.local/bin/sindri` is still the TS one); see
[context.md](context.md).

## 1. Pin the release

Sindri is tested against **Onyx `v4.9.0`**. Use that tag, never `latest`:

```bash
git clone --depth 1 --branch v4.9.0 https://github.com/onyx-dot-app/onyx.git ~/onyx
cd ~/onyx/deployment/docker_compose
cp env.template .env
```

In `.env` set (v4.9.0's API server refuses to start while `USER_AUTH_SECRET` is empty), then make the file yours only:

```bash
IMAGE_TAG=v4.9.0
DISABLE_TELEMETRY=true
USER_AUTH_SECRET=<the output of: openssl rand -hex 32>
```

```bash
chmod 600 .env
```

The profile records the same tag (`context.sources.onyx.version`), and `$SINDRI doctor` (`context:onyx`) compares it with
what Onyx reports at `GET /api/version`. Changing the Onyx version means changing both, then re-running the manual
checks in §6.

## 2. Bind every published port to loopback

> `sindri onyx up` does steps 1-5 of this page for you (pinned files fetched and checked, the `.env` and `docker-compose.override.yml` below written, the images pulled by digest, the stack started and checked) under `$AW_SINDRI_HOME/onyx/`, and `sindri onyx down` stops it. Creating the account and Sindri's key (§5) stay manual until P3. To keep the Slack and Discord bots from starting, `up` replaces the background service's command with one that patches the image's own supervisord config in a copy and runs supervisord on it, so it needs no file to copy out of the image and mount; the copy-out-and-mount recipe below remains for a hand setup. That replacement command drops the compose file's own custom-CA step (it runs `update-ca-certificates` when `/etc/ssl/certs/custom-ca.crt` is mounted into the background container), so a box that needs a custom CA inside the background container must add it to its own override.

The stock compose file publishes nginx on all interfaces (`"${HOST_PORT:-3000}:80"` and `"80:80"`). Add a
`docker-compose.override.yml` beside it:

```yaml
services:
  nginx:
    ports: !override
      - "127.0.0.1:3000:80"
```

Leave every other service's `ports:` commented out, as shipped. Start it:

```bash
docker compose up -d
lsof -nP -iTCP:3000 -sTCP:LISTEN     # must show 127.0.0.1:3000 only
```

### A one-user dev box (optional)

The stock compose file sizes Onyx for a team. On a single-user dev machine, these settings keep it near 4.6 GB of
container memory (about 6 GB on the host with Docker Desktop's VM capped at 6 GB) and change nothing Sindri uses. Put
them in the same `docker-compose.override.yml` as §2's port binding:

```yaml
services:
  opensearch:
    environment:
      - "OPENSEARCH_JAVA_OPTS=-Xms1g -Xmx1g"      # stock: 2g
    mem_limit: 2g
  indexing_model_server:
    profiles: ["indexing-model-server"]           # not started: the inference server also indexes (below)
  code-interpreter:
    profiles: ["code-interpreter"]                # not started: Sindri doesn't use it
  background:
    depends_on:
      indexing_model_server: { condition: service_started, required: false }
    environment:
      - CELERY_WORKER_DOCFETCHING_CONCURRENCY=1
      - CELERY_WORKER_DOCPROCESSING_CONCURRENCY=1
      - CELERY_WORKER_LIGHT_CONCURRENCY=1
```

and in `.env`, `INDEXING_MODEL_SERVER_HOST=inference_model_server` (the API server and the background workers both
read it). The background container also runs Onyx's Slack and Discord bots, which Sindri doesn't need. To keep them
from starting, copy its `/etc/supervisor/conf.d/supervisord.conf` out, add `autostart=false` under
`[program:slack_bot]` and `[program:discord_bot]`, and mount the copy over the original (read-only) in the override.

## 3. Keep models local

- Embeddings: the bundled `inference_model_server` and `indexing_model_server` (the defaults). In Admin → Search
  Settings, keep a self-hosted embedding model; configure **no** cloud embedding provider.
- LLM: configure **no** cloud LLM provider. Sindri's search route needs no LLM.
- Leave web search and image generation unconfigured.

## 4. Scope the connectors

Create only these, in Admin → Connectors:

| Connector | Scope |
|---|---|
| Linear | Start date early enough to cover the backtest projects; the product's teams only |
| Slack | The product's channels only (connector-indexed; Sindri doesn't use the federated lane) |
| Docs spaces (Confluence, Drive, …) | The product's spaces only |

Check first indexing and each connector's progress in Onyx's Admin → Connectors page. Sindri doesn't read Onyx's
connector status (no connector route is on its allowlist, §7), so `$SINDRI context sources status` shows whether Onyx
answers and what the key is, not how far indexing has got.

## 5. The key

Create a **dedicated service account** in Admin → API Keys named `sindri-onyx` and copy its key (`on_…`).
The search route Sindri uses (`POST /api/admin/search`, the only v4.9.0 CE route that returns document ids without
an LLM) needs an **admin** key, so this key could change Onyx settings (owner decision 2026-10-12, context spec §8.8).
Sindri only ever sends the five requests in §7; its Onyx adapter refuses any other request before sending it, and a
test proves it. Keep the key for Sindri alone, on its own service account: doctor reads the key's account through
`GET /api/me` and warns (`SND-CONTEXT-053`) when the admin key belongs to a person's account.

Store it by pointer, never in the profile. Add the `context:` section below to your profile
(`$AW_SINDRI_HOME/profile/profile.yaml`, which already starts with `schemaVersion: 1` and `user:`; a new profile needs
both, and `docs/sindri/context.md` "Trying it in a throwaway profile" writes a whole one):

```bash
security add-generic-password -s onyx -a sindri-onyx -w      # macOS: prompts for the key
secret-tool store --label 'onyx/sindri-onyx' service 'onyx' account 'sindri-onyx'   # Linux (libsecret): prompts for the key
```

The profile pointer is the same on both machines: `keychain:onyx/sindri-onyx`. Sindri reads it with `security
find-generic-password -s onyx -a sindri-onyx -w` on macOS and `secret-tool lookup service onyx account sindri-onyx` on
Linux (install `libsecret-tools` on Debian/Ubuntu if `secret-tool` is missing; it exits 1 both for no such entry and when no keyring is reachable); on any other OS a `keychain:` pointer doesn't resolve (`SND-SECRET-002`), so use `env:`, `file:` or `op:`.

```yaml
context:
  sources:
    onyx: { url: "http://127.0.0.1:3000", token: "keychain:onyx/sindri-onyx", version: "v4.9.0" }
    linear: { teams: [ENG] }        # team keys whose ENG-n identifiers become aliases
```

Pointers Sindri accepts: `keychain:<service>/<account>`, `env:<VAR>`, `op:<vault>/<item>/<field>` (1Password CLI) and
`file:<absolute path>` (a regular file, not a symbolic link, mode 0600 or stricter). A value without one of these
schemes is refused (`SND-CONTEXT-002`). An `env:` pointer is for fixtures and tests: queries read the variable from the
control plane's environment as it was when `$SINDRI serve` started (export it first, `$SINDRI stop` a running one),
while doctor reads the calling shell's, so the two can disagree; `file:` reads the same in both, and so does `keychain:` on macOS (on Linux `serve` must run in the login session to reach the keyring, since `secret-tool` needs its D-Bus). Then,
at a terminal:

```bash
$SINDRI profile validate          # prints the profile's hash; exit 1 means valid but not approved yet
$SINDRI profile approve <hash>    # type the challenge it shows, 'approve profile <first 6 of the hash>'; also starts the control plane
```

Validate checks only the pointer's scheme and that a target follows; it neither parses the target nor resolves it, so a
malformed target (`keychain:onyx` without an account), an unset variable or a file at the wrong mode shows up in
`$SINDRI doctor` (step 6) as `SND-SECRET-001`, `002` or `003` with the fix. A query resolves the pointer under its
deadline: a locked keychain or an `op` waiting for a sign-in degrades onyx (`SND-CONTEXT-010`) instead of holding the
answer.

## 6. Check it

```bash
$SINDRI doctor            # context:onyx — loopback url, version v4.9.0, the key can search
$SINDRI context sources status
```

| Code | Meaning | Fix |
|---|---|---|
| `SND-SECRET-001`..`003` | The key's pointer is malformed (`001`), doesn't resolve (`002`, naming the variable, file or entry), or is an unsafe `file:` (`003`) | Fix the pointer or the secret as the line says (§5) |
| `SND-CONTEXT-002` | The key's pointer doesn't resolve to a usable key (queries degrade with it as the cause) | `$SINDRI doctor` names the `SND-SECRET` reason |
| `SND-CONTEXT-050` | Onyx refused the key (403: missing, wrong, or not admin) | Recreate the key as in §5 and update the pointer |
| `SND-CONTEXT-053` | The admin key isn't a service account's | Create a service account for Sindri (§5) |
| `SND-CONTEXT-055` | `onyx.url` isn't loopback, or Onyx reports a version other than the pinned one | §1, §2 |
| `SND-CONTEXT-051` | Onyx answered with a shape the pinned v4.9.0 doesn't have | Check `version` against what Onyx runs (§1) |
| `SND-CONTEXT-010` | Onyx didn't answer; queries continue without it (`degraded`) | `docker compose ps`, `sources status` |

A hit whose document Onyx no longer has when Sindri fetches its text is left out of the answer (no citation without
the record), so nothing reports it and there's nothing to fix.

**Manual checks on every Onyx version change** (the adapter never reads Onyx's settings; the allowlist has no settings route): Admin → LLM shows no cloud provider;
Admin → Search Settings shows a self-hosted embedding model; `.env` has `DISABLE_TELEMETRY=true`; §2's `lsof` shows
loopback only.

## 7. What Sindri calls

| Request | Why |
|---|---|
| `POST /api/admin/search` `{query, filters: {}}` (each term quoted as a phrase; no filter is sent: an as-of query marks a hit updated after its cut, or undated, as possibly leaked; documents Onyx marks `hidden` are dropped, so Sindri sees what a standard search would) | Candidates: document ids, titles, links, `updated_at` |
| `GET /api/document/document-size-info?document_id=` and `GET /api/document/chunk-info?document_id=&chunk_id=` | A cited record's text |
| `GET /api/version` | Doctor's version check (no key sent) |
| `GET /api/me` | Doctor shows the key's role and account type |

Nothing else: the adapter refuses any other request before sending it. Linear issues key on Linear's issue UUID
(Onyx's document id). The `ENG-482`-style identifier is stored as an alias,
read from Onyx's title prefix `[ENG-482]` and checked against the issue link. Onyx doesn't serve `updated_at` by
id, so Onyx citations show `version unchecked` until the native Linear source versions them.

Onyx has no change feed: its document listing is per connector, carries no times and isn't on the allowlist above, so
Sindri re-derives links from search hits: a document that no longer matches any query is never read again, and
`context sources status` doesn't report the gap (context spec §12).
