# Sindri-managed Onyx: Sindri stands up Onyx, the owner only logs in and connects

**Status:** design, revision 1, for the owner's review · **Date:** 2026-10-16 · **Owner decisions:** 2026-10-16 (§1:
O1, O2) · **Builds on:** the context retrieval spec `2026-10-10-sindri-context-retrieval-design.md` (CTX: D2, D3, §8.8),
`docs/sindri/onyx.md` (the manual setup this replaces), `docs/csharp/sindri-context.md`, `docs/csharp/sindri.md`
(commands, doctor, caller classes), the API surface spec `2026-10-10-sindri-api-surface-design.md` (API), and the
`sindri-app` launcher (`scripts/sindri-app.sh`, jthorare/agentic-workflow#278: the single command users enter Sindri
through) ·
**Lane:** the context lane · **Next:** the owner reviews this doc, then /specToProvenPR takes §11's phases to PRs.

## 0. Why

Today Onyx is a page of manual steps (`docs/sindri/onyx.md` §1–§6): clone a tag, edit `.env`, write a compose
override, start Docker, pick models, make a service account, mint an admin key, put it in the keychain, write the
profile section, approve it. Each step is a place to get the pinning, the loopback binding or the key wrong, and none of
it is the owner's real job. The owner's words: "all of that should be done by sindri for me. It should stand up Onyx as
a dependency and download all of these pieces if necessary. That way the only thing I need to do is login + oauth to
onyx to start ingesting data."

**How users enter Sindri:** `sindri-app` (#278). It publishes the C# `sindri` and `sindri-ui` when the source changed,
installs the pinned SDK and fetches the pinned `temporal` when missing, then runs `sindri` with its arguments; bare
`sindri-app` starts the control plane and opens the app. Onyx joins that path: it is one more dependency `sindri-app`
brings up, not a separate tool the owner has to know about. Every command below is written as the C# verb (`sindri
onyx …`); the owner types it as `sindri-app onyx …`, which the launcher passes through unchanged.

**Success:** on a box with Docker running and the disk space it needs, the owner's first bare `sindri-app` at a
terminal (or `sindri-app onyx up`) ends with Onyx v4.9.0 running on `127.0.0.1`, the owner's own admin account (created by the owner in Onyx's sign-up
page, which the command opens), an admin service-account key in the OS secret store, the profile's
`context.sources.onyx` section approved and `sindri doctor`'s `context:onyx` row ok. The owner then connects Linear and
Slack (OAuth, in Onyx's UI). From then on every `sindri-app` (through `serve`) keeps Onyx up and `sindri-app stop` takes it down.

## 1. Decisions

| # | Decision | By |
|---|---|---|
| O1 | Sindri stands Onyx up as a managed dependency: fetches the pinned pieces, writes the config, starts and stops the containers, bootstraps the admin and the key. The owner's only manual steps are signing in and connecting sources (OAuth) in Onyx's UI | owner, 2026-10-16 |
| O2 | Sindri may send the Onyx requests needed to mint its own key **at setup only**, beyond the five-request allowlist of `docs/sindri/onyx.md` §7. The query adapter's allowlist stays exactly five (§5) | owner, 2026-10-16 |
| O3 | The owner does Onyx's initial setup, not Sindri: the owner registers their own account in Onyx's UI (Onyx makes the first registered user the admin), so connectors and their OAuth grants are the owner's. Sindri never registers an account and never stores the owner's password; to mint its key it asks for the owner's Onyx login once at the terminal (§4.1 step 7) and forgets it | owner, 2026-10-16 |
| O4 | `serve` starts a managed Onyx and `stop` stops it (containers stopped, volumes kept). An unmanaged Onyx (today's manual setup) is never touched | builder's call; owner to confirm |
| O6 | `sindri-app` (#278) is the entry point. Bare `sindri-app` at a terminal, on a box where Onyx isn't set up (no `context.sources.onyx` in the profile, or a managed one whose `onyx up` never finished), offers the setup before it starts the control plane: "Set up Onyx (Sindri's business-context search) now? [Y/n]". Yes runs §4.1 in that terminal; no starts Sindri without it and leaves a needs-you row "Set up Onyx: `sindri-app onyx up`", and the offer isn't repeated until the owner runs it. Not at a terminal (stdin isn't a tty), or in an agent session: no offer, the needs-you row only | owner, 2026-10-16 (the entry point); the offer's wording and the once-only rule are the writer's calls |
| O5 | Docker itself is a prerequisite, not something Sindri installs (Docker Desktop needs an interactive install and admin rights). Sindri detects it and says how to get it | builder's call; owner to confirm |

## 2. Goals, non-goals, anti-goals

**Goals**
- The owner's usual entry, `sindri-app`, goes from nothing to a working, pinned, loopback-only Onyx that Sindri can
  query, with no extra tool or command to learn (O6).
- Every downloaded piece pinned by version and content hash: the Onyx deployment files by commit and sha256, the images
  by digest per platform.
- The same guarantees `docs/sindri/onyx.md` documents by hand: pinned `v4.9.0`, nginx on `127.0.0.1` only, telemetry off,
  self-hosted embeddings, no cloud LLM or embedding provider, the one-user sizing of its "A one-user dev box".
- Idempotent: rerunning `sindri onyx up` on a set-up box changes nothing and ends in the same state; an interrupted run
  resumes.
- Lifecycle tied to Sindri: `serve` ensures a managed Onyx is up, `stop` stops it, doctor reports it.

**Non-goals**
- Connecting sources. Linear, Slack and docs-space connectors stay the owner's, in Onyx's UI with company OAuth and the
  company's sign-off (`docs/sindri/onyx.md` "get sign-off first"). Sindri prints the scope guidance (§4 of that page)
  and opens the connectors page, nothing more.
- Installing Docker (O5), upgrading Onyx past the pin (a version change stays a reviewed change to the pins, §6), or
  deleting Onyx's data (`docker compose down -v` stays a manual, documented step).
- Multi-user or remote Onyx. Loopback, one owner, one box (README X61 keeps the door open, nothing here closes it).

**Anti-goals** (would be a failure even if it works)
- Onyx reachable from anything but loopback, or a port published on `0.0.0.0`.
- The key, the owner's Onyx password or the session cookie in argv, a log, the ledger, the profile, stdout of a non-terminal,
  or an agent session's reach.
- An unpinned pull (`latest`, a tag without a digest) or a downloaded file used without its hash checked.
- The query path gaining any request beyond the five (O2 is setup only).
- A setup step an agent session can trigger (every mutating verb is the human's).

## 3. Terms

- **Managed Onyx:** the profile says `context.sources.onyx.managed: true`. Sindri owns its files under
  `$AW_SINDRI_HOME/onyx/` and its compose project `sindri-onyx`. Without `managed` (or `false`), Onyx is the owner's,
  as today, and none of §4 runs.
- **The pins:** `context/onyx.lock` in this repo (§6), read by the build, never fetched.
- **Provisioning:** the setup-only requests of §5.2.

## 4. The command surface

All under `sindri onyx`. Mutating verbs need the human at a terminal (the `desk` class): an agent session is refused
`SND-AUTH-002` with the command line, as `profile approve` is today.

| Command | Who | What |
|---|---|---|
| `sindri onyx up` | human, terminal | §4.1, the whole setup; on a set-up box, "ensure running" |
| `sindri onyx status [--json]` | anyone (a session sees it too) | containers and their health, the url, the pinned and running version, the key's pointer (never the key), whether the key is minted |
| `sindri onyx open` | human | opens Onyx in the browser (the sign-up page while Onyx has no user, else the connectors page) |
| `sindri onyx down` | human, terminal | stops the containers, keeps volumes and files |

`sindri onyx up` and `serve` print progress on stderr and stdout carries only the summary, as other verbs do.

### 4.1 `sindri onyx up`, in order

Each step is idempotent and checks before it acts; the run records each completed step under
`$AW_SINDRI_HOME/onyx/state.json` so a rerun resumes. Each failure names its code (§8) and the fix.

1. **Preflight.** Docker CLI present and the daemon answering (`docker version`), Compose v2 (`docker compose
   version`); Docker's memory limit ≥ the one-user sizing needs (builder measures; the manual page says ~6 GB with the VM
   capped at 6 GB); free disk at `$AW_SINDRI_HOME` and, where Docker's data root is a directory on this host (a Linux engine), at the data root
   (`docker info` DockerRootDir), the smaller of the two ≥ the pinned total image size plus headroom; on Docker Desktop the
   data root lives in its VM, so the host measurement stands and the row notes that Docker Desktop caps its disk image too
   (the builder measures the real pull and model download on v4.9.0 and records the number in the pins). The chosen
   port (default 3000, §7) free or already ours.
2. **Fetch the deployment files** listed in the pins (the compose file, `env.template`, the nginx config files the
   compose mounts, and anything else v4.9.0's compose references) from `raw.githubusercontent.com` at the pinned
   commit, each checked against its sha256 before use, written to `$AW_SINDRI_HOME/onyx/deploy/`. A mismatch stops the
   run and deletes nothing it did not write.
3. **Write the config.** `.env` (0600) from `env.template` with `IMAGE_TAG=v4.9.0`, `DISABLE_TELEMETRY=true`, a
   generated `USER_AUTH_SECRET` (32 random bytes, hex), `AUTH_TYPE=basic`,
   `INDEXING_MODEL_SERVER_HOST=inference_model_server`; and `docker-compose.override.yml` with nginx bound to
   `127.0.0.1:<port>:80`, every Onyx image replaced by its pinned digest for this platform, and the one-user sizing of
   `docs/sindri/onyx.md` (OpenSearch heap 1 GB and `mem_limit: 2g`; `indexing_model_server` and `code-interpreter`
   behind profiles; worker concurrency 1; the Slack and Discord bots off). Rewritten only when its content would change.
4. **Pull** the pinned images (`docker compose pull`), with progress.
5. **Start** (`docker compose -p sindri-onyx up -d`) and **wait** for readiness: `GET /api/health` answers, then
   `GET /api/version` reports `v4.9.0` (else `SND-ONYX-005`), under a deadline the builder sets from measurement
   (first start downloads models).
6. **The owner's initial setup** (first run only): open Onyx's sign-up page in the browser and wait at the terminal
   ("Create your Onyx account in the browser, then press Enter"). The owner registers their own account; Onyx makes
   that first user the admin. Sindri sends no registration. Onyx is on loopback only, so nothing else on the network
   can take the first-user slot; the prompt says to do it now. A rerun after this step skips it.
7. **Mint the key** (first run, or when the stored key no longer answers): ask at the terminal for the owner's Onyx
   email and password (the password hidden, read into memory only), log in, create the `sindri-onyx` API key with the
   Admin role (§5.2), verify it with `GET /api/me` (an admin service account, so doctor's `SND-CONTEXT-053` passes),
   log out, then drop the cookie and the password. A login that isn't an admin (the owner signed up second, or someone
   else did first) stops with `SND-ONYX-007` and says how to fix it in Onyx's admin users page.
8. **Store the key** (§5.3) as `onyx/sindri-onyx`. The owner's password is never stored.
9. **Write the profile section** `context.sources.onyx: { url, token: "keychain:onyx/sindri-onyx", version: "v4.9.0",
   managed: true }` into the live profile (creating the profile with `schemaVersion: 1` and `user: <OS user>` when there
   is none), keeping every other key and comment as they were. Then show the profile approval inline: the same preview
   and typed challenge `sindri profile approve` shows. Declining leaves the section written and unapproved, and says
   how to approve later.
10. **Hand over.** Run doctor's `context:onyx`; print the summary (url, version, key pointer, doctor row); open the
    connectors page; print the connector checklist (Linear: the product's teams, a start date covering the
    backtests; Slack: the product's channels; from `docs/sindri/onyx.md` §4) and the connectors page link.

### 4.2 With `serve` and `stop`

- `serve`, when the approved profile has `managed: true`: after the control plane is up, runs steps 1 (preflight
  without the disk check), 3 (only if the files changed), 5 in the background, never blocking the API; Onyx's state
  goes into `control.json` (`onyx: {project, url, state}`) beside `temporal`. A failure is a doctor and needs-you row,
  never a failed serve; queries degrade as they do today when Onyx doesn't answer (`SND-CONTEXT-010`).
- `serve` never fetches, pulls or bootstraps: a box where `onyx up` never completed gets a needs-you row "Run `sindri
  onyx up`".
- `stop` runs `docker compose -p sindri-onyx stop` after the control plane's own shutdown, bounded; a timeout is
  reported, not fatal.
- An unmanaged Onyx: nothing here runs.

## 5. Security

### 5.1 What stays as it is

The query adapter (`OnyxRequests.cs`, five requests, refusal before send, `OnyxSourceFailureTests`) is unchanged and
its "exactly five" test stays. The provisioning client is a separate type with its own allowlist; the query adapter
cannot reach it, and an architecture or unit test proves neither type uses the other.

### 5.2 The provisioning allowlist

Used only by `sindri onyx up` (steps 5 and 7) in the human's process, never by `serve`, a query or a session. No
registration request is on it (O3):

| Request | Why |
|---|---|
| `GET /api/health`, `GET /api/version` | readiness, the pin check |
| `POST /api/auth/login` (form: `username`, `password`), `POST /api/auth/logout` | the owner's session cookie (`fastapiusersauth`), held in memory only, for minting the key |
| `POST /api/admin/api-key` | the `sindri-onyx` key; the raw key is returned once (Onyx stores it hashed) |
| the role step the builder finds in v4.9.0 | Onyx's `APIKeyArgs` is `{name, group_ids}` with no role field: the builder finds, in the v4.9.0 source, how a key's service account becomes an admin (the Admin group's id, or a set-role route) and adds exactly that request here, with its source line cited in the code comment |
| `GET /api/me` | the key's account is an admin service account |

Same refusal-before-send shape and test as the query adapter. The research note for the builder: register, login and
cookie names are in `backend/onyx/auth/users.py` (fastapi-users, `CookieTransport`), first-user-is-admin at
`users.py:806-809`, the key route at `backend/onyx/server/api_key/api.py:52`, its model at `server/api_key/models.py`,
the service-account user at `db/api_key.py:157-170`; the `/api` prefix comes from nginx (verify on the running stack).

### 5.3 Writing secrets

Sindri gains a secret **writer** beside the reader (`PointerSecrets`), for exactly the one entry above (the key):

- macOS: `security add-generic-password -U -s onyx -a <account> -w`, with the secret on stdin (interactive `security -i`
  or the prompt fed through a pty: the builder picks the form that keeps it out of argv and proves it with a test that
  reads the process's argv); Linux: `secret-tool store --label … service onyx account <account>`, secret on stdin.
- Neither available (or the keychain locked and the owner declines to unlock): fall back to `file:` under
  `$AW_SINDRI_HOME/secrets/` (0600, regular file, the directory 0700) and use that pointer in the profile.
- The key and the owner's password never appear in argv, logs, the ledger, `state.json`, `control.json`, `.env` (the
  generated `USER_AUTH_SECRET` is Onyx's own and does live in `.env`, 0600, as the manual page has it today) or any
  output. The password is read with echo off and never written anywhere.

### 5.4 Pins and supply chain

Only the pinned files and digests are used; a hash mismatch stops the run. Images by digest per platform
(`linux/arm64`, `linux/amd64`), never by tag alone. The pins file is in the repo and changes only by a reviewed PR.

## 6. The pins: `context/onyx.lock`

A small text file the builder designs in the spirit of `binaries.lock` (fixed fields, a header that documents them, a
`check` in CI): the Onyx version (`v4.9.0`), the commit that tag points to, each deployment file's path and sha256, each
image's name and digest per platform, the measured disk and memory needs. A check script (or a `check` verb in the
existing `scripts/binaries-lock.sh` style) validates its shape, and a bash test covers it. Bumping Onyx means a PR that
changes the lock and reruns `docs/sindri/onyx.md` §6's manual checks.

## 7. Profile

New key `context.sources.onyx.managed` (bool, default `false`); with it `true`:
- `url` must be `http://127.0.0.1:<port>` (default port 3000; another port when 3000 is taken by something not ours,
  chosen at step 1 and written to the url);
- `version` must equal the pins' version (`SND-CONTEXT-055` already covers a mismatch with the running Onyx; the
  validator also refuses a `managed` profile whose `version` differs from the pins, with the fix).
`docs/csharp/sindri-profile.md` documents the key (its check, `scripts/check-profile-doc.mjs`, requires it).

## 8. Errors and doctor

A new code range `SND-ONYX-0xx` in `SindriErrors`, each with a one-line fix:

| Code | Meaning |
|---|---|
| `SND-ONYX-001` | Docker CLI missing or its daemon not answering (fix: install or start Docker Desktop / Engine) |
| `SND-ONYX-002` | Compose v2 missing |
| `SND-ONYX-003` | Not enough disk or Docker memory (says how much is needed and where) |
| `SND-ONYX-004` | A downloaded file or image failed its pin |
| `SND-ONYX-005` | Onyx didn't become ready in time, or reports another version |
| `SND-ONYX-006` | Port taken by something not ours and no free fallback |
| `SND-ONYX-007` | The Onyx login given isn't an admin (fix: make that account an admin in Onyx's users page, or log in as the first user) |
| `SND-ONYX-008` | Minting or verifying the key failed |
| `SND-ONYX-009` | Writing a secret failed (and the `file:` fallback did too) |

Doctor gains `onyx:stack` (managed only): the compose project's containers and their health, the bound address (fails
on anything but loopback), the image digests running vs the pins. The existing `context:onyx` row is unchanged. The
UI's Health page shows both rows as it shows every doctor row; nothing UI-specific is required.

## 9. Architecture (where the code goes)

Inside the Sindri package, by the C# rules (`.agents/rules/csharp.md`): slices `OnyxUp`, `OnyxStatus`, `OnyxDown`,
`OnyxLogin` in `Aw.Sindri.Features`; ports `IContainerRuntime` (P1 has `Inspect`, the read-only facts; P2 adds the compose operations pull, up, stop and ps),
`IOnyxProvisioning` (§5.2), `ISecretWriter` (§5.3), `IDeployFiles` (fetch and verify against the pins) in Core, with
fakes and contract tests in `Aw.Sindri.Testing`; the Docker CLI, HTTP and keychain adapters in Infrastructure; CLI
commands in Host. The first-run offer (O6) lives in the C# bare start (`StartCommand`, before it launches `serve`),
not in `scripts/sindri-app.sh`: the launcher stays a thin build-and-exec wrapper, and a published `sindri` started any
other way behaves the same. The launcher's only change is its header and `docs/csharp/sindri-ui.md`'s paragraph
naming Onyx among what bare `sindri-app` brings up. The serve and stop hooks sit beside the Temporal ones (`TemporalDevServer` is the model for detach,
readiness, bounded stop). `deps.yaml` models edges between this repo's packages only, so the external executables
(`docker`, `security`, `secret-tool`, `pbcopy` and the Linux clipboards) are not declared there: `docs/csharp/sindri.md`
lists them instead.

## 10. Proof

- **Unit:** each step against fakes: idempotence (a second run sends nothing and writes nothing), resume after each
  step's failure, refusal of off-list requests in the provisioning client, the query adapter's five unchanged, secrets
  absent from argv, logs and every file but the secret store, session refusal of every mutating verb, profile write
  keeping comments and other keys.
- **Integration:** the provisioning client against a fake Onyx HTTP server (`Aw.Context.Testing` has `FakeOnyxServer`;
  extend it with login, logout, key and role), the Docker adapter against a stub `docker` on PATH, the secret writer
  against a stub `security` / `secret-tool`.
- **Heavy (nightly, and once by hand for the PR):** a real first bare `sindri-app` (accepting the offer) on a clean `AW_SINDRI_HOME` on macOS arm64
  (the owner's box, which needs the measured free disk first; today it has 11 GiB free), then: `lsof` shows
  `127.0.0.1:<port>` only; `GET /api/version` is `v4.9.0`; running digests equal the pins; doctor `context:onyx` and
  `onyx:stack` ok; `sindri context sources status` shows onyx answering; `sindri stop` stops the project and `serve`
  brings it back; a rerun of `onyx up` is a no-op; the sign-up page opens on first run, the owner's account is the admin,
  and the minted key's account is a separate admin service account.

## 11. Phases (for /specToProvenPR)

1. **P1 — pins and preflight:** `context/onyx.lock` with the measured numbers, its check and test; `IContainerRuntime`
   and the preflight (its memory check is the `MemTotal` floor only); `sindri onyx status`; `SND-ONYX-001..003, 006`.
2. **P2 — files, config, start:** fetch and verify, `.env` and the override, pull, start, readiness, `sindri onyx down`,
   doctor `onyx:stack`; `SND-ONYX-004, 005`; the memory check that counts what Docker's existing containers already use (§12),
   moved here from P1 by owner decision 2026-10-17.
3. **P3 — owner setup, key, secret, profile:** the sign-up hand-off, the provisioning client and its allowlist, the
   secret writer, the profile write and inline approval, `sindri onyx open`; `SND-ONYX-007..009`.
4. **P4 — the entry path, serve and stop:** the first-run offer in bare `sindri-app` (O6), the lifecycle hooks,
   `control.json`, the needs-you rows; this phase's PR needs #278 merged (or stacks on it); docs: `docs/sindri/onyx.md` rewritten
   around bare `sindri-app` and `sindri-app onyx up` (the manual steps kept as an appendix for an unmanaged Onyx), `docs/csharp/sindri.md`
   commands, a README X-row (the next free, X64) recording O1–O5.

## 12. Settled in review, and left to the builder

Settled after the builder check (2026-10-16, the writer's calls):
- **Another Onyx already on the box** (an unmanaged stack, or anything else on port 3000): never adopted, migrated or
  stopped. Step 1 picks the next free loopback port for the managed one and preflight's memory check counts what Docker
  already uses, refusing with `SND-ONYX-003` when two stacks won't fit. A compose project named `sindri-onyx` is Sindri's
  own and is reused.
- **Disk:** preflight refuses with the measured need, where it's needed (Docker's data root, `$AW_SINDRI_HOME`) and how
  much is free; there is no reduced-model fallback. The owner's box (11 GiB free on 2026-10-16) needs space freed before
  the heavy proof.
- **Clipboard:** nothing is put on the clipboard (no password is handed over, O3).

Left to the builder:

The exact disk, memory and readiness numbers (measured on v4.9.0); the admin role mechanism in §5.2; the macOS no-argv
secret-write form; the pins file's field layout; whether the `security`/`secret-tool` calls
share code with the reader. Each is recorded in code comments or the docs where it lands.
