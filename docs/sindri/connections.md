# Connections

A connection is a name, like `linear:work`, that workflows, agents and toolkits use instead of a token. Sindri keeps the name, a few non-secret settings and a pointer to the secret. The secret itself lives in your OS keychain. This guide covers adding a connection, signing in per provider, where the secrets live, and what `--key-env` and `--client-secret-env` do.

## The name

`<service>:<label>`, both parts lowercase letters, digits and `-`, 1 to 32 characters each, starting with a letter or digit. `linear:work`, `github:my-org`, `slack:team`. A bad name is `SND-CONN-008`; a name that is taken is `SND-CONN-010`.

## The four kinds

| Kind | What it signs in to | How you add it |
|---|---|---|
| `rest` | A REST API reached with a key: an API key or a personal access token (GitHub, Linear, Slack, or any API) | `--preset github --key-env NAME`, or `--kind rest --key-env NAME` for a service with no preset |
| `oauth` | A provider's OAuth sign-in (Google, Spotify; GitHub and Linear when you ask for it) | `--preset` and `--client-id`: Sindri opens your browser |
| `cli` | A command-line tool that reads a credential | `--kind cli --key-env NAME` |
| `mcp` | An MCP server | later release; the kind exists, the verb refuses it for now |

## Commands

```
sindri connection add <service:label> --preset github|linear|slack --key-env NAME
sindri connection add <service:label> --preset <p> [--kind oauth] --client-id <id> [--client-secret-env NAME] [--scope <s>]...
sindri connection add <service:label> --kind rest --key-env NAME
sindri connection list
sindri connection show <service:label>
sindri connection test <service:label>
sindri connection rename <service:label> <label>
sindri connection remove <service:label>
```

Every verb takes `--json` and `--no-start`. Agent sessions may run all of them: the MCP tools `connection_list`, `connection_get`, `connection_add`, `connection_test` and `connection_remove` call the same services as the CLI and the HTTP routes under `/sindri/connections`.

`show` and `list` never print a token, a token prefix or where the secret is stored. They say whether one is stored.

## A key first (GitHub, Linear, Slack)

Sindri runs on your machine, on `127.0.0.1`. An OAuth app needs a callback address it can send you back to, and a loopback address is a poor fit for that: GitHub wants the client secret on your box, and Slack refuses a plain-http callback. So for a service that offers a key, the key is the default way to connect (#4357):

| Preset | Without `--kind` | The key | Where you make it |
|---|---|---|---|
| `github` | a key (`rest`) | a fine-grained personal access token: Pull requests read and write, Contents read and write, Checks read, Issues read, Metadata read | GitHub, Settings, Developer settings, Personal access tokens, Fine-grained tokens |
| `linear` | a key (`rest`) | a personal API key | Linear, Settings, API, Personal API keys |
| `slack` | a key (`rest`) | a user token (`xoxp-`) | your Slack app's OAuth & Permissions page |
| `google`, `spotify` | the browser sign-in (`oauth`) | no key option for your own data | |

Each preset file lists its ways to connect under `auth`, the default first (`"auth": ["key", "oauth"]`). With no `--kind`, `--preset github`, `--preset linear` and `--preset slack` store a key. Pass `--kind oauth` to sign in through your own OAuth app instead (GitHub and Linear only).

**Sindri checks the key before it stores it.** It makes one call to the service with it: GitHub `GET https://api.github.com/user`, Slack `GET https://slack.com/api/auth.test` (it reads `"ok": true`), Linear a `POST` of `{ viewer { id } }` to `https://api.linear.app/graphql`. A key the service refuses is `SND-CONN-018`, and a service Sindri can't reach is `SND-CONN-019`. In every case nothing is stored and no row is written. A key for a hand-made `--kind rest` connection (no preset) is stored without a check. Sindri trims spaces and newlines from both ends of the key first, so a paste that ends in a newline still works. The check waits 15 s at most. A 429, or a 403 that carries `Retry-After` or `x-ratelimit-remaining: 0` (GitHub's secondary rate limit), is `SND-CONN-020` "rate limiting": wait a minute and try again. A plain 403 is a refused key (`SND-CONN-018`).

## Signing in (oauth)

Sindri ships presets for five providers. A preset holds the public endpoints, the default scopes and a note for the sign-in. **It ships no client id**, so you register an OAuth application with the provider once and pass its client id with `--client-id`. The client id is not a secret.

What happens when you run `sindri connection add linear:work --preset linear --client-id ...`:

1. Sindri listens on `127.0.0.1` on a free port, for one callback only.
2. It prints the consent URL and opens your browser. When no browser can open (an agent, a box with no display), hand the printed URL to the person who will click it.
3. You approve at the provider. The provider redirects to `http://127.0.0.1:<port>/callback`.
4. Sindri checks the callback's `state`, trades the code for tokens (PKCE, S256) and stores them in the keychain. Only then is the connection row written.
5. The listener closes. It also closes after 5 minutes, on a denied consent, and on a state mismatch.

**Renaming.** `sindri connection rename <service:label> <label>` (POST `/sindri/connections/{name}/rename` with the body `{"label":"<label>"}`; the app's rename action) changes the label only: `slack:old` becomes `slack:new`, the service part stays. The keychain entry is keyed by the name, so Sindri stores the tokens again under the new name, inserts the new row, then removes the old row and its entry; a step that fails first undoes what it did, so the old connection keeps working. The preset, settings, status and creator carry over, and a connection that needs a sign-in keeps its Needs-you row under the new name. Refusals: an unknown name is `SND-CONN-001`, a new name that another connection uses is `SND-CONN-010` (naming it), a bad label is `SND-CONN-008` and the same label is `SND-CLI-003`. A connection that an active workflow's `connection_event` trigger watches is `SND-CONN-015`, naming those workflows: the trigger holds the name as text, so after a rename it would watch nothing and never fire. Pause those workflows first (`sindri wf pause <slug>`), rename, point each trigger at the new name, then activate them again. Renaming to or from `slack:messages` is `SND-CONN-017`: that name belongs to `sindri messages connect`. Anything else that names the connection by its old text (a profile key, a node that is not a trigger) is not rewritten; point it at the new name.

**Signing in again.** `sindri connection signin <name>` (POST `/sindri/connections/{name}/signin`, MCP `connection_signin`) runs the same loopback sign-in for a stored oauth or mcp connection, from the row's own preset, client id, scopes (or server address) and the client secret kept beside its tokens. The row stays (name, creator, creation time); its tokens are replaced when the callback lands, and the Needs-you row clears. A failed try leaves the row and its old secret as they were, and `connection show` reports the failure until the next try: a row that is not connected answers the failure's code instead of the row, and a connected row (it still works on its old tokens) answers the row with the failure beside it as `lastSignInFailure` (`code`, `at`), null otherwise. `connection signin` and the app's "Sign in again" stop waiting on either. `connection signin` waits for its own sign-in (the row's `signedInAt` setting changes), so a background refresh of the same connection does not end the wait early.

**Replacing a key.** `sindri connection set-key <name> --key-env NAME` (PUT `/sindri/connections/{name}/key`, body `{"key": "..."}` from the CLI or `{"keyEnv": "NAME"}` read from serve's environment) gives a stored key connection (rest or cli) a new key without removing it (#4358). The new key is trimmed and, for a rest connection with a preset, checked with its service like an add (`SND-CONN-018` refused, `SND-CONN-019` not reached, `SND-CONN-020` rate limited). Nothing is written until that passes, so a refused key leaves the old key in the keychain and the row as it was. A good key overwrites the connection's keychain entry, and the row ends `connected` with no reason; a row that needed a sign-in clears its Needs-you row. A row whose preset has left the catalog is refused like an add of an unknown preset (`SND-CLI-003`). An oauth or mcp connection is refused (`SND-CLI-003`, pointing at `signin`). The answer is `{connection, checked, uncheckedReason}`: a cli connection, a rest one with no preset and a preset with no key check store the key unchecked (`checked` false, with the reason), and the CLI then prints "replaced (not checked: ...)". There is no MCP tool for it yet.

**MCP connections do not refresh on their own.** Only oauth connections are refreshed in the background (the `connection-refresh` service, every 5 minutes, for tokens that end within 10 minutes). An `mcp` connection keeps only its server address, so when its token expires the test shows `needs-sign-in` and you sign in again by hand: `sindri connection signin <name>`.

**An agent's `connection_signin` opens your browser.** The consent address comes from the MCP server's own metadata (or the preset), not from the agent. It is allowed for agent sessions because the sign-in still needs you to approve it in that browser page; the tokens go to the keychain, never to the agent.

**What moves a connection to `needs-sign-in`.** Only the provider refusing the grant: an `invalid_grant`, `invalid_client` or `unauthorized_client` answer (or any other error code on a 400 or 401), or no refresh token stored. A provider that cannot be reached, a timeout, a 429, a 5xx or `temporarily_unavailable` leaves the connection `connected` and the next tick tries again. The move adds one Needs-you row, whichever of the test, the sign-in or the refresh made it. A rest or cli connection is `SND-CONN-011`. Removing a connection that needed a sign-in clears its Needs-you row too.

A sign-in that fails leaves **no row and no secret** (for a new connection): denied consent is `SND-CONN-009`, no callback in 5 minutes `SND-CONN-003`, a callback that is not this sign-in's `SND-CONN-004`. The CLI shows the failure as soon as it happens. An agent that started the sign-in without waiting can ask `connection_get` for that name and gets the same failure code until the name is added again (Sindri keeps it in memory; a restart forgets it).

### Per preset

| Preset | Register at | Redirect to allow | Client secret | Notes |
|---|---|---|---|---|
| `github` | GitHub, Settings, Developer settings, OAuth Apps | `http://127.0.0.1` | **Needed** | GitHub OAuth apps want the client secret at the token endpoint: pass `--client-secret-env NAME`. Default scopes `repo`, `read:org` |
| `linear` | Linear, Settings, API, OAuth applications | `http://127.0.0.1` | Not needed | Public client with PKCE |
| `google` | Google Cloud, Credentials, OAuth client ID of type Desktop app | (a desktop client allows the loopback address by itself) | **Needed** | Google wants the desktop client's secret at the token endpoint: pass `--client-secret-env NAME` |
| `spotify` | Spotify Developer Dashboard | `http://127.0.0.1` (the IP literal, not `localhost`) | Not needed | Public client with PKCE |
| `slack` | | | | Slack accepts only https redirect URLs, so the browser sign-in cannot come back to your machine. `connection add --preset slack --kind oauth` is refused with this explanation. Connect with a user token: `sindri connection add slack:work --preset slack --key-env SLACK_TOKEN` |

If a provider's page rejects the redirect, the preset's note (shown in the error of a failed add) says what to change.

## Keys (rest and cli)

```
export ACME_KEY=...
sindri connection add acme:key --kind rest --key-env ACME_KEY
```

`--key-env NAME` names an environment variable. **The CLI reads it in your shell**, at the moment you run the command, and sends the value to the running Sindri over the loopback connection. That means the variable does not have to exist where `sindri serve` was started. If it is unset or empty in your shell, the CLI says so and sends nothing. The value is stored once, in the keychain; it is in no row, no log line, no ledger event and no answer.

`--client-secret-env NAME` works the same way for an OAuth client secret (the GitHub and Google presets need one). The secret is stored in the same keychain entry as the tokens, and Sindri sends it with the token exchange and with every refresh.

**For agents (MCP):** `connection_add` takes the *name* of the variable (`keyEnv`, and `clientSecretEnv` for a client secret), and Sindri reads it from the environment of the **running `sindri serve`**, because an MCP call has no shell of its own. If that variable is not set there, the error says so. Pass a variable only when you know it was exported where Sindri runs.

## Where secrets live

- **macOS:** the login keychain, service `sindri-connection`, account `<service>.<label>` (the `:` becomes `.`).
- **Linux:** the Secret Service (libsecret) with the same service and account, written with `secret-tool`.
- **Without a keychain** (none installed, locked, or it did not read back): an owner-only file under `$AW_SINDRI_HOME/secrets/` (directory `0700`, file `0600`).

One entry per connection holds the access token, the refresh token, when the token expires and, when you gave one, the client secret, as one JSON document encoded as base64url. It is passed to the keychain tool on standard input, never on a command line. `sindri connection remove` deletes the entry first, then the row; if the keychain will not delete it (locked, no D-Bus), the row stays so the pointer is not lost (`SND-CONN-006`).

The row in `ledger.db` (table `connection`) has the name, kind, preset, non-secret settings (client id, scopes, probe URL), a pointer such as `keychain:sindri-connection/linear.work`, the status and its reason. It has no token column.

## Status

| Status | Meaning |
|---|---|
| `connected` | The secret is stored and last worked |
| `needs-sign-in` | The provider refused the credential; sign in again |
| `error` | The last test failed for another reason (see the reason) |

`sindri connection test <name>` calls the connection's probe URL (a cheap authenticated GET the preset names) with the stored credential and updates the status. A key connection with a preset runs the same check as the add. When that check can't reach the service (`SND-CONN-019` no answer, `SND-CONN-020` rate limited), the test answers "not connected" and leaves the status as it was, because a network failure says nothing about the key. The reason it prints is the error's code and fixed title, never the provider's body, which can repeat your token.

## Errors

`SND-CONN-001` to `SND-CONN-011`, `SND-CONN-018` to `SND-CONN-020` are listed in [errors.md](errors.md): not found, needs sign-in, sign-in timeout, state mismatch, refresh failed, secret store unavailable, preset needs a client id, invalid name, consent denied, already exists, key-based (no browser sign-in), the service refused the key, the service could not be reached to check the key, and the service is rate limiting Sindri.

## In the app

The app's Connections tab (`G O`) lists the same connections with their status, and offers Test, Sign in again, Remove and Add, the same actions as the commands above. The GitHub, Linear and Slack tiles ask for a token first, with three steps that say where to make it; GitHub and Linear also offer "Use an OAuth app instead", which shows the client ID fields. The Google and Spotify tiles sign in through the browser. A key you paste there is sent once and is never stored by the app. See `docs/csharp/sindri-ui.md` "PR 18a".

## Events that start workflows

A workflow whose trigger is a `connection_event` node is started by what its connection's provider reports (a merged pull request, a labelled Linear issue, a Slack message). Sindri polls; there is no webhook and no tunnel. The built-in `workflow-poll` service, every 30 seconds, makes one provider call per (connection, event kind) that any active workflow watches, then each trigger keeps the events its `filter` takes. The cursor of each (connection, event kind) is saved only after the runs for its events started, and each run's id comes from the event's delivery id, so the same event never starts a second run. After the control plane starts and after the machine wakes, every cursor is read forward at once. Intervals, the catch-up cap and the events it records: [workflows/blocks/poll.md](../workflows/blocks/poll.md).

## What this release does not do yet

The `mcp` kind refreshing on its own, and passing a connection to the nodes of a workflow come in the next changes. Until then an expired mcp token is replaced with `connection signin`.
