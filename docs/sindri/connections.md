# Connections

A connection is a name, like `linear:work`, that workflows, agents and toolkits use instead of a token. Sindri keeps the name, a few non-secret settings and a pointer to the secret. The secret itself lives in your OS keychain. This guide covers adding a connection, signing in per provider, where the secrets live, and what `--key-env` and `--client-secret-env` do.

## The name

`<service>:<label>`, both parts lowercase letters, digits and `-`, 1 to 32 characters each, starting with a letter or digit. `linear:work`, `github:my-org`, `slack:team`. A bad name is `SND-CONN-008`; a name that is taken is `SND-CONN-010`.

## The four kinds

| Kind | What it signs in to | How you add it |
|---|---|---|
| `oauth` | A provider's OAuth sign-in (GitHub, Linear, Google, Spotify) | `--preset` and `--client-id`: Sindri opens your browser |
| `rest` | A REST API reached with a key | `--kind rest --key-env NAME` |
| `cli` | A command-line tool that reads a credential | `--kind cli --key-env NAME` |
| `mcp` | An MCP server | later release; the kind exists, the verb refuses it for now |

## Commands

```
sindri connection add <service:label> --preset <p> --client-id <id> [--client-secret-env NAME] [--scope <s>]...
sindri connection add <service:label> --kind rest --key-env NAME
sindri connection list
sindri connection show <service:label>
sindri connection test <service:label>
sindri connection rename <service:label> <label>
sindri connection remove <service:label>
```

Every verb takes `--json` and `--no-start`. Agent sessions may run all of them: the MCP tools `connection_list`, `connection_get`, `connection_add`, `connection_test` and `connection_remove` call the same services as the CLI and the HTTP routes under `/sindri/connections`.

`show` and `list` never print a token, a token prefix or where the secret is stored. They say whether one is stored.

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
| `slack` | | | | Slack accepts only https redirect URLs, so the browser sign-in cannot come back to your machine. `connection add --preset slack` is refused with this explanation. Make a REST connection with a user token instead: `sindri connection add slack:work --kind rest --key-env SLACK_TOKEN` |

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

`sindri connection test <name>` calls the connection's probe URL (a cheap authenticated GET the preset names) with the stored credential and updates the status. The reason it prints is the error's code and fixed title, never the provider's body, which can repeat your token.

## Errors

`SND-CONN-001` to `SND-CONN-011` are listed in [errors.md](errors.md): not found, needs sign-in, sign-in timeout, state mismatch, refresh failed, secret store unavailable, preset needs a client id, invalid name, consent denied, already exists, key-based (no browser sign-in).

## In the app

The app's Connections tab (`G O`) lists the same connections with their status, and offers Test, Sign in again, Remove and Add, the same actions as the commands above. A key you paste there is sent once and is never stored by the app. See `docs/csharp/sindri-ui.md` "PR 18a".

## Events that start workflows

A workflow whose trigger is a `connection_event` node is started by what its connection's provider reports (a merged pull request, a labelled Linear issue, a Slack message). Sindri polls; there is no webhook and no tunnel. The built-in `workflow-poll` service, every 30 seconds, makes one provider call per (connection, event kind) that any active workflow watches, then each trigger keeps the events its `filter` takes. The cursor of each (connection, event kind) is saved only after the runs for its events started, and each run's id comes from the event's delivery id, so the same event never starts a second run. After the control plane starts and after the machine wakes, every cursor is read forward at once. Intervals, the catch-up cap and the events it records: [workflows/blocks/poll.md](../workflows/blocks/poll.md).

## What this release does not do yet

The `mcp` kind refreshing on its own, and passing a connection to the nodes of a workflow come in the next changes. Until then an expired mcp token is replaced with `connection signin`.
