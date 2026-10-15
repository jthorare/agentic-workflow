# Threat Model — sindri-api-surface

**Mode:** plan
**Source:** /Users/thor/repos/agentic-workflow/docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md (S1 code read from `feat/sindri-s1-app-shell`: `LoopbackBinding.cs`, `LoopbackGuard.cs`, `Pipeline.cs`, `Server/ServeCommand.cs`)
**Reviewed:** 2026-10-10

**Threat model applied.** This review uses the repo's threat model (`docs/csharp/bugfix.md` "Threat model"): harness gates guard against agents' honest mistakes, not against hostile agents, and keeping a hostile agent contained is the sandbox's job. The owner decided that v0 has no tokens, passkeys or tailnet. A session that deliberately forges `X-Sindri-Caller`, unsets `CLAUDECODE` or otherwise sets out to beat the class check is **out of scope** and is noted only. In scope: something other than an agent session driving commands (a browser, another local process), data leaving the host, and honest-mistake paths that still let a session make a decision.

## Summary

The authorization model is sound: one action-class × caller-class table, `service` reachable only in-process, and an MCP surface with no decision, desk, protected or job tool. But v0 decides the caller class the wrong way round. **A missing `X-Sindri-Caller` header means `human`** (§6), so any HTTP call that doesn't come through the CLI's environment check or `sindri mcp` gets full human rights. That covers an agent's `curl`, a script it writes, or a test harness. This is exactly the "honest-mistake path that still lets a session make a decision", and it reaches protected approval too, because `GET /profile` serves the hash that `confirmHash` checks. The same default, together with S1's `LoopbackGuard` accepting any loopback `Origin` (C3, left for later), lets a web page on any localhost origin drive every command that takes no body (`stop`, `open`, `services run/pause/resume`, `handback`, cancels) without a CORS preflight. Both are fixed by one design change: **fail closed** (no header means `session`, and humans claim `human` explicitly), plus refusing any `Origin` on commands now. The rest are MEDIUM spec defects: the decorator order leaves refusals out of the audit, "first decision wins" is checked against a projection that may be stale, idempotency keys aren't scoped to a command, session detection depends on environment variables the spec doesn't name, and cross-repo data reaches session contexts.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 2 (HIGH ×2) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 2 (MEDIUM, LOW) |
| A04 Insecure Design | findings: 3 (MEDIUM ×2, LOW) |
| A05 Security Misconfiguration | findings: 1 (MEDIUM) |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (MEDIUM; the root cause is A01-1) |
| A08 Software & Data Integrity Failures | findings: 1 (MEDIUM) |
| A09 Security Logging & Monitoring Failures | findings: 2 (MEDIUM, LOW) |
| A10 SSRF | findings: 1 (LOW) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **A01-1: A missing `X-Sindri-Caller` header is treated as `human`, so a session's direct HTTP calls can decide, act at the desk and approve protected changes** [HIGH]
  - Where: §6 lines 269–273 ("absent → `human`"); §8 line 315 ("the 403 backs that up for a session that calls the API another way"), which holds only when the header is sent; §4.2 (the table is right, but its input is wrong).
  - Attack (honest mistake, no forgery): an agent finds `GET /commands` (it lists method and route for every command) or reads `docs/sindri`, and to unblock itself runs `curl -X POST http://127.0.0.1:7410/sindri/decisions/D-…  -H 'Content-Type: application/json' -d '{"choice":"accept","itemVersion":3}'`. With no header, the caller class is `human` and the decision is recorded. The same works for desk commands (`park`, `handback`, `start`, `open`) and for `POST /profile/approve`: `GET /profile` returns the live hash, so the agent can fill in `confirmHash` without understanding that the field stands for a human's typed confirmation. The same happens when an agent writes a helper script or test that calls the API, or when it uses an HTTP tool (WebFetch, a Playwright `request` fixture) in place of the CLI. The audit row then records `human` (see A09-1), so the mistake isn't visible afterwards. P8's proof run checks only the path *with* the header ("via `curl` with the session header gets `SND-AUTH-002`"), so it would pass with this gap open.
  - Mitigation: fail closed. Treat a missing header as `session`; only `X-Sindri-Caller: human` gives the `human` class (today any value other than `session` is a 400, so `human` must be added as a valid value). The CLI sends `human` only outside an agent session (see A07-1), and the Avalonia app always sends `human`. A human who uses raw `curl` adds the header, which costs little and is documented in `docs/sindri/errors.md` under `SND-AUTH-002`. Have `SND-AUTH-002`'s message keep telling a session to "ask the human to run: sindri …" and never mention the header. Add to P1's signal and to the §12 endpoint tests: a `POST` with **no** header to every decision, desk and protected route gives 403, and nothing but the audit row is written. Add to P8's proof run: `curl` without the header gives `SND-AUTH-002`. Deliberately sending `X-Sindri-Caller: human` from a session stays out of scope until tokens arrive (§11).

- **A01-2: A page in a local browser can drive commands that take no body (CSRF from a loopback origin)** [HIGH]
  - Where: S1 `LoopbackGuard.cs` (lets through an `Origin` whose host is `localhost`/`127.0.0.1`/`::1`, on any port); conflict C3 (line 360, "left as S1 built it; tightened in the token phase"); §6 (no header means `human`); the bodyless commands in §3.2: `POST /stop`, `/views/rebuild`, `/services/{name}/run|pause|resume`, `/items/{id}/handback`, `/jobs/{id}/cancel`, `/instances/{id}/cancel`, `/items/{id}/open`.
  - Attack: a page served from *any* loopback origin can reach the API. That includes a dev server on `localhost:3000` running agent-generated code or an npm dependency with a malicious install script, the toolkit's own local mockup and evidence servers (`/design-mockup-web`, `/ui-evidence`), and any local dashboard with an XSS bug. The page runs `fetch('http://127.0.0.1:7410/sindri/items/ITM-…/open', {method:'POST', mode:'no-cors'})`. That is a "simple" request: no custom header and no body, so the browser sends no preflight. `LoopbackGuard` accepts the loopback `Origin`, the request has no `X-Sindri-Caller` header so its class is `human`, and the command runs. `OpenItem` launches a Warp tab running Claude Code against a repo. `RunService` starts jobs, `stop` kills the control plane, and `pause` silently stops scheduled services. Item ids and service names can be guessed or are in the approved profile. Commands with a JSON body are protected by accident only (a JSON `Content-Type` forces a preflight, and S1 has no CORS policy). Requests from non-loopback origins are refused correctly (browsers always send `Origin` on cross-origin `POST`s), and DNS rebinding is blocked by the `Host` check. So the exposure is loopback origins only, which is in scope as "a browser on the machine driving commands".
  - Mitigation: (1) Make A01-1's fail-closed default the main fix. A browser can't send `X-Sindri-Caller: human` without a preflight, and there is no CORS policy to answer one, so every browser request falls into the `session` class. (2) Bring the C3 tightening forward to P1 for commands: refuse **any** `Origin` header on `POST` (the CLI, `sindri mcp` and the Avalonia app's `HttpClient` send none), or refuse any `Origin` on every route as the dashboard spec already requires. (3) Never add CORS middleware (an architecture test can check for it). (4) Optionally, require `Content-Type: application/json` on every command, even bodyless ones (an empty `{}`), so that every command needs a preflight. Add integration tests: a bodyless `POST` with `Origin: http://localhost:3000` gives 403, and a `no-cors`-style request with no header doesn't run a desk command.

### A02 — Cryptographic Failures

No findings. All traffic is plain HTTP on loopback by design. `confirmHash` is a deliberate typed confirmation, not a secret: it is checked against the live content hash (S1's `SND-PROFILE-003/-004`), and it is not meant to authenticate anyone (see A01-1 for why it doesn't keep sessions out). The ULIDs used for idempotency and ids aren't security tokens. When tokens arrive (§11), keep them in 0600 files and compare them in constant time.

### A03 — Injection

- **A03-1: `OpenItem` puts item text into a launch configuration and into a new agent session** [MEDIUM]
  - Where: §3.2 `OpenItem`; P10 (`WarpLauncher`, which "seeds a Claude Code session with the item's context"); `ItemId` can be "a tracker key" (§3.2 notes), so titles and bodies can come from outside, for example from Linear issues other people wrote.
  - Attack: (a) Command or config injection: if the title, body or repo is interpolated into the Warp launch YAML or into the shell command line that starts `claude`, a title containing `"; …` or a YAML break runs or changes commands. This takes no hostile agent, only odd text in a ticket. (b) Prompt injection: text from a third-party ticket reaches a fresh Claude Code session as if it were the human's own instruction.
  - Mitigation: in P10, write the context to a 0600 file under `$AW_SINDRI_HOME` and pass only its path through argv (as separate arguments, never as a shell string). Generate the launch configuration with a YAML serializer, not string templates. Resolve the repo from the approved profile's repo list (`RepoName` → path), never from item text. In the seeded context, mark the tracker text as quoted, untrusted data. Set `AW_SINDRI_CHILD` in the child's environment (§6 depends on it). Add a test with a title containing `"`, `;`, `$()`, a newline and `---`.

- **A03-2: Request text isn't cleaned of control characters or limited in size at the point of entry** [LOW]
  - Where: §3.2 `FileRequest` (`text`); §3.4 (the event is "scrubbed, ≤ 500 chars"); §3.1 `ListRequests` ("scrubbed, capped"); the CLI prints request text.
  - Attack: session text containing ANSI or OSC escape sequences, which turn up in pasted tool output, gets printed by `sindri requests` and `sindri queue` in the human's terminal and can rewrite what the human sees next to a decision. A very large `text` (Kestrel's default body limit is 30 MB) goes through the scrubber's regular expressions before it is capped.
  - Mitigation: in `FileRequest`'s validator, refuse text longer than N (for example 4 KB) with `SND-REQ-001`, and strip or refuse C0/C1 control characters except newline. Set a body size limit for the Sindri group (for example 64 KB). Have the CLI escape control characters in every string it prints that came from the API.

### A04 — Insecure Design

- **A04-1: "First decision wins" is checked against an asynchronous projection, so a race between two decides can record both** [MEDIUM]
  - Where: §2.1 line 39 (the handler "loads … a projection row" to decide); §2.3 (the projector is asynchronous); §3.2 notes line 149 ("Two decisions on one decision id: the first wins"); §3.4 (the event idempotency key is "the command's key plus the event type").
  - Attack (honest concurrency, not malice): the human clicks *accept* in the UI while a `sindri decide … decline` from a terminal is in flight. Both handlers read `decision_view` before the projector has applied the other's `decision.recorded`. Both pass, and both append, because their client idempotency keys differ. The ledger then holds two conflicting `decision.recorded` events, plus `request.accepted` *and* `request.declined`, and the engine acts on whichever it reads first. The same window exists for every `itemVersion` check that reads `item_view`.
  - Mitigation: enforce uniqueness in the ledger, not in the view. Give `decision.recorded` a **natural** idempotency or unique key of `decision.recorded:<decisionId>`, independent of the client's key, so the second append fails or replays and the handler returns `SND-ITEM-409 decided elsewhere`. For item commands, append `item.state-changed` with a natural key of `item:<id>:v<version+1>`. Alternatively, make deciding handlers read the ledger's latest event of the type (§2.1 already allows that), inside the append transaction. Add an integration test: two concurrent `Decide` calls on one id give exactly one `decision.recorded`.

- **A04-2: Session requests can flood the needs-you queue** [LOW]
  - Where: §3.4 "Requests become decisions" (every `FileRequest` creates a `decision.requested`); §4.2 (`request` is open to sessions).
  - Attack: an agent stuck in a retry loop calls `sindri_request` dozens of times. Each call becomes a decision the human has to clear, the real needs-you rows are buried, and approving in bulk without reading becomes likely.
  - Mitigation: refuse a duplicate open request (same kind, item and normalized text) with a 409 that points at the existing one. Limit open requests per item (and per session actor, once actors are distinguished) and return a clear code. Show the count in `doctor`.

- **A04-3: The data that queries and MCP tools return isn't limited to the session's repo** [MEDIUM]
  - Where: §8 (`sindri_queue`, `sindri_item`, `sindri_item_timeline`, `sindri_pending_decisions` have no repo scope); §3.1 `GET /queue`, `GET /requests` (request text).
  - Attack (data leaving the host): a Claude Code session in repo A calls `sindri_queue` or reads requests, and titles, timeline lines and request text from every other repo Sindri tracks enter A's model context, which is sent to the provider. A session might also file a request against another repo's item by mistake. Scrubbing removes secrets, but not text that is confidential by context (for example a work repo and a personal repo on one machine).
  - Mitigation: have `sindri mcp` work out the session's repo (git top level of its working directory, matched against the profile's repos) and send `repo=` on every query by default. Give the query endpoints a `repo` filter (`ListItems` already has one) and add it to `GetQueue` and `ListRequests`. Let `FileRequest` from a session target only items in that repo. An optional `allRepos` argument can stay for the human UI.

### A05 — Security Misconfiguration

- **A05-1: Leaving C3 for later keeps `Origin` checking too loose on the endpoints that change state** [MEDIUM]
  - Where: §10 C3 (line 360); S1 `LoopbackGuard.cs`.
  - Attack: see A01-2. This finding is the configuration side of that one: v0 doesn't need tokens to close the hole, because no first-party client sends `Origin`.
  - Mitigation: move the C3 change into P1: `LoopbackGuard` refuses any `Origin` header with 403, or at least does so on every non-`GET` route and on `/events`. Keep the `Host` check as it is. Record it in `docs/sindri/errors.md` (it is not an SND code; the response is a plain 403 with no body, as in S1).

### A06 — Vulnerable & Outdated Components

No findings. The MCP C# SDK is added in P8, pinned and at least 14 days old, from nuget.org only, with a locked restore and `scripts/dotnet-verify-packages.sh`, and the AOT spike approved it. It is a young package, so P8 should re-run the NativeAOT smoke test whenever it bumps the version (already part of the §12 AOT row).

### A07 — Identification & Authentication Failures

- **A07-1: Detecting an agent session from the environment depends on variables the spec doesn't name, and fails open** [MEDIUM]
  - Where: §6 line 271 ("`CLAUDECODE`, `AW_SINDRI_CHILD`, or the Codex or Cursor session variables").
  - Attack (honest mistake): the "Codex or Cursor session variables" aren't named. If Cursor's agent terminal or Codex's runner doesn't set the variable that gets chosen, or a wrapper such as a `sudo`/`env -i` launcher, a container exec or a Makefile target drops it, then `sindri decide` run by the agent sends no header and is treated as a human. That is the same harm as A01-1, through the sanctioned CLI.
  - Mitigation: name the exact variables for each provider in the spec, and test that `config/hooks/adapters` or the provider adapters actually set them (a provider test in `providers/tests`). Once A01-1 is in, the CLI must **claim** `human`. Have it do so only when no agent variable is set **and** stdin and stdout are TTYs, since agents' shell tools don't run under a TTY. Otherwise it sends `session`. A human who scripts the CLI without a TTY can set `AW_SINDRI_HUMAN=1`. That is a deliberate act, and a session doing it is out of scope (forgery). Unit-test every combination of variables and TTY state.

### A08 — Software & Data Integrity Failures

- **A08-1: Idempotency keys aren't scoped to a command, so a reused key can silently swallow a different write** [MEDIUM]
  - Where: §2.1 item 3 ("the client's `Idempotency-Key` … becomes part of every ledger key the command writes. The same key with a different request body: `SND-API-005`"); §3.4 ("the command's key plus the event type").
  - Attack (honest client bug): the UI reuses one key across two clicks, or the CLI generates a key per process instead of per invocation. `POST /decisions/D-1 {"choice":"accept"}` and `POST /decisions/D-2 {"choice":"accept"}` have **identical bodies** (the decision id is in the route), so `SND-API-005` doesn't fire. Both write `decision.recorded` under `<key>:decision.recorded`, so the second is reported as `AlreadyRecorded: true` with the first command's sequence, and D-2 is never decided, with no sign of it. The same goes for every route-parameterized command (`park`, `hold`, `services/{name}/run`).
  - Mitigation: build the idempotency fingerprint from the command type, method, the **route values**, the canonical body and the caller class, and scope the stored key to the command type (`<CommandName>:<key>:<eventType>`). A replay must match the whole fingerprint, and anything else is `SND-API-005`. Add a unit test: the same key on two decisions with the same body gives `SND-API-005`, not `AlreadyRecorded`.

### A09 — Security Logging & Monitoring Failures

- **A09-1: The decorator order stops refusals from being audited, and the actor recorded for HTTP callers isn't reliable** [MEDIUM]
  - Where: §2.1 lines 44–52 ("outermost first: `ValidatingService`, `AuthorizingService`, `IdempotentService`, `AuditingService`") against the same section's "appends `command.handled` … for every command, accepted or refused" and P1's signal ("the ledger gains only the `command.handled` row with outcome `refused`"); §2.1 line 55 (actors `human:cli` / `human:ui`).
  - Attack: `AuditingService` is the **innermost** decorator, so an `AuthorizingService` refusal (403) and a `ValidatingService` refusal (400) return before auditing runs. A session's attempted decision, the event the audit exists to record, leaves no trace. Separately, the host has no way to tell `human:cli` from `human:ui` (neither identifies itself), and under today's §6 a session's `curl` is recorded as `human` (A01-1), so the record of who did what is wrong.
  - Mitigation: make `AuditingService` the outermost decorator, or at least wrap it around `ValidatingService` and `AuthorizingService`, so that every outcome, including validation errors, writes one `command.handled` row. Have the CLI and UI send an `X-Sindri-Client: cli|ui|mcp` header that only sets the *label* (`human:cli`), never the class. Treat it as a hint and record a missing one as `human:http` or `session:http`. Add a unit test over the composed chain: a refused session call writes exactly one audit row with outcome `refused` and code `SND-AUTH-002`.

- **A09-2: Requests that `LoopbackGuard` refuses aren't logged** [LOW]
  - Where: S1 `LoopbackGuard.cs` (returns 400 or 403 with no telemetry).
  - Attack: a page in a browser probing the API (A01-2) or a DNS-rebinding attempt leaves no record, so the human never learns that something on the machine is aiming at Sindri.
  - Mitigation: emit a scrubbed telemetry warning (`sindri.guard.refused`, with the reason and the `Origin`/`Host`) and count refusals in `doctor`. Don't add a ledger event, since these requests come from no caller.

### A10 — SSRF

- **A10-1: Clients trust the URL in `control.json` without checking that it is loopback** [LOW]
  - Where: §7 (a client reads `control.json {url, pid, epoch}` and checks the epoch against `GET /status` *at that URL*); §8 (`sindri mcp` uses the same lookup).
  - Attack (misconfiguration, data leaving the host): a stale, hand-edited or wrongly placed `control.json` (for example `AW_SINDRI_HOME` pointing at a synced or shared folder) with a non-loopback `url`. The epoch check goes to the URL in the file, so a server there passes it, and the CLI and MCP server send request text, decisions and `Idempotency-Key`s off the host.
  - Mitigation: in the shared client, refuse any `url` whose host isn't `127.0.0.1`, `::1` or `localhost`, or whose scheme isn't `http`, with `SND-API-001` and a fix line. Have `doctor` check that `control.json` is 0600 and owned by the current user.

## STRIDE findings

### S — Spoofing

- **The caller class is spoofed by default, not by forgery** [HIGH]: see A01-1. A missing header is treated as `human`, so any HTTP client that isn't the CLI or `sindri mcp` (an agent's `curl`, a script, a browser page) passes as the human. Fix: make no header mean `session` and require an explicit `human`.
- **A browser page passes as the human** [HIGH]: see A01-2.
- **A session deliberately sending `X-Sindri-Caller: human`, or unsetting `CLAUDECODE`** (note, out of scope for v0): the owner's v0 decision accepts this. §11's per-class tokens and the `aw:sindri-auth` guard hook cover it. Make sure no error message, `GET /commands` payload or doc that a session reads tells it how to claim `human`.
- **Other OS users on the same Mac** (note, LOW): loopback is shared by every local user, and the default port 7410 is well known. On a single-user machine this doesn't matter; tokens (§11) close it.

### T — Tampering

- **Two conflicting decisions recorded** [MEDIUM]: see A04-1.
- **Reused idempotency keys drop writes** [MEDIUM]: see A08-1.
- **`views.db` tampered with or corrupted**: projections can be rebuilt (`sindri views rebuild`, and a rebuild on a schema mismatch). Because of A04-1, nothing that has to be correct should depend on a view row alone. No further finding.

### R — Repudiation

- **Refused commands aren't audited, and actors can't be told apart** [MEDIUM]: see A09-1.
- **Session actions recorded as human** [HIGH, as part of A01-1]: the audit trail credits the human with whatever a header-less session call did.

### I — Information Disclosure

- **Cross-repo data reaches session contexts and the model provider** [MEDIUM]: see A04-3.
- **The event stream**: only public types are sent and their payloads carry ids, states and short scrubbed titles (§5.3), while `command.handled` and request text stay private. Cross-origin browser reads are blocked because there is no CORS policy (SSE included). No finding. Keep it that way: never add CORS headers.
- **The `control.json` URL isn't checked** [LOW]: see A10-1.

### D — Denial of Service

- **The 16 stream slots are shared by every caller** [LOW]: an agent that runs `curl -N …/events` in the background, or a UI whose reconnects leak, can take all 16 slots, and the human's own app then gets 429 (`SND-API-004`). Mitigation: reserve slots for `human` callers (for example, sessions get at most 8), close a stream when its client is idle past N pings, and list the open streams in `doctor`.
- **The needs-you queue gets flooded** [LOW]: see A04-2.
- **Request bodies are too large** [LOW]: see A03-2 (body size limit).
- **A local page stops the control plane** [HIGH, as part of A01-2]: `POST /stop` takes no body.

### E — Elevation of Privilege

- **Session → human through a missing header** [HIGH]: see A01-1. This reaches the `decision`, `desk` and `protected` classes, the last because `GET /profile` exposes the hash that `confirmHash` checks.
- **Browser page → human through a bodyless command** [HIGH]: see A01-2. It can launch Claude Code sessions (`open`) and services (`run`).
- **The `service` caller's desk commands** (note): §4.2 leaves the list to S2. When S2 fills it in, make it an explicit allowlist checked by the same `AuthorizingService` (one row for each recipe-step command), with a test that a `service` caller can never call `Decide` or a `protected` command.

## Verdict

- **Critical findings:** 0
- **High findings:** 2 (A01-1 fail-open caller class; A01-2 bodyless commands run from a local browser page). Both are fixed by one change in P1: a missing header means `session`, humans send `X-Sindri-Caller: human` explicitly, and any `Origin` header is refused on commands.
- **Medium:** 7 (A03-1, A04-1, A04-3, A05-1, A07-1, A08-1, A09-1). **Low:** 5 (A03-2, A04-2, A09-2, A10-1, the shared stream limit).
- **Recommendation:** FIX-CRITICAL-FIRST (amend §6, §10 C3, §2.1's decorator order, §2.1/§3.4's idempotency keys, and the signals for P1 and P8 before `/specToProvenPR` starts P1)

verdict: NEEDS_WORK
