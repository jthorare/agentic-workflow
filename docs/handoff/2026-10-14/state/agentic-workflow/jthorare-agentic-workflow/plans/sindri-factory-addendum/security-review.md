# Threat Model — Sindri factory addendum (§17), round 2

**Mode:** plan
**Source:** ~/repos/agentic-workflow/docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md (rev 3, with §17.19–§17.21), against the main spec's safety boundary (`2026-10-07-sindri-design.md` §8) and the round-1 review (`round1/security-review.md`)
**Reviewed:** 2026-10-09
**Out of scope (decided):** C# port, stacked epics, swappable host, ring order, core tenet. Generic shell-script hygiene isn't flagged.

## Summary
§17.19 fixes the structural problems from round 1. These now hold:
- The capability ceiling (profile ∩ lens ∩ task), with capability fields always approval-tier and never model-set.
- The pattern-vs-boundary table.
- Holdouts mounted only into evaluator Steps.
- The overlay in the read-only mount.
- Desk-only enrollment and per-decision signing.
- Host tags and the ring-1 defaults table.
- Lowest-trust consolidation.

Of the nine round-1 HIGHs, five are closed and four are reduced to MEDIUM residuals (table below).

The new HIGHs come from three places:
- **A §17.19 developer-experience convenience that bypasses invariant 13.** Micro-packets are auto-frozen with a model-derived write scope.
- **Two gaps in the phone fix.** A "separate origin" on the single stable tailnet name shares the passkey RP ID and cookies. The offline decision queue implies client-chosen challenges.
- **The new §17.20 and §17.21 sections, which move trust boundaries without restating them:**
  - P2 worker hosts have no enrollment, token-placement or verifier-placement rules.
  - P3 federation ships evidence providers (code) and workflow templates (capability fields) across the work/personal line.
  - The unified API would replace the host-verb authority (OS user + interactive TTY + typed confirmation, §8.7) without saying what replaces it.

The §8 boundary holds for code execution: containers, the model proxy, sindri-mediated push and the two-phase verifier are all unchanged. Everything here can be fixed in the spec.

## Round-1 HIGH status
| Round-1 HIGH | Status | Where resolved / residual |
|---|---|---|
| Compiled-policy ceiling, lens capability tier | **Closed** | Invariant 13; §17.19 capability split and ceiling. One gap remains: micro-packets (new H1) |
| Phone tier raise | **Closed** | §17.12 swipe list excludes tier raises, capability changes, adoptions and freezes. LOW residual: enforce the list in code (A01) |
| Epic merge gate inputs | **Reduced → MEDIUM** | §17.10 verified SHA, `--match-head-commit`, human or trusted-bot threads, ring-1 off. Still missing: required checks by id, an epic ruleset, and who executes the merge (M1) |
| Context-graph taint laundering | **Reduced → MEDIUM** | §17.19 lowest-trust consolidation and trust-tagged records. Still missing: the pack renders below-trusted graph sections as `<untrusted>`; trust only from immutable ids (M2) |
| Walkthrough origin | **Partly closed; reopened as part of H2** | "Separate origin, no cookies or decision endpoints". On one stable hostname that means a different port, which shares the passkey RP ID and cookies |
| Phone enrollment and binding | **Partly closed; reopened as part of H2** | Desk enroll and revoke; signs `(item id, version, nonce, choice)`. The nonce source is unstated, and the offline queue implies a client-side nonce. User verification isn't required |
| Imported lens supply chain | **Closed** | §17.19: capability fields dropped, lowest trust, upgrades never self-adopt. MEDIUM residual on first import and federated packs (M6) |
| Handover integrity | **Reduced → MEDIUM** | §17.19 shared lease checked before every outward action; signed snapshot. Still missing: key model, secrets transfer, mirror re-fetch, WIP re-verification, lease-store authentication (M5) |
| Ring-1 data on the personal host | **Closed** | §17.19 host tags; ring 1 never on a `personal` host. Residual: the lease store lives "on the always-on box" (M5), and P2 worker class matching (H3) |

## Coverage
| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 4 |
| A02 Cryptographic Failures | findings: 1 |
| A03 Injection | findings: 2 |
| A04 Insecure Design | findings: 3 |
| A05 Security Misconfiguration | findings: 2 |
| A06 Vulnerable & Outdated Components | findings: 1 |
| A07 Identification & Authentication Failures | findings: 2 |
| A08 Software & Data Integrity Failures | findings: 2 |
| A09 Security Logging & Monitoring Failures | findings: 2 |
| A10 SSRF | findings: 1 |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **H1. Micro-packets are frozen without the desk, with a write scope taken from model triage** [HIGH]
  - Where: §17.19 Developer experience, "Micro-packets" (lines 708–709). This conflicts with invariant 13 (lines 90–96: "Freezing and re-freezing are desk actions"; no model output sets write scope) and invariant 14.
  - Attack:
    - An S-sized tracker item gets "write scope from triage" and is "frozen without a desk step". The triager is a model workflow (§17.9, `propose` tier) that reads the item's untrusted text.
    - §17.19 itself lists write scope as a capability field. So a capability field is set by model output and frozen with no human in the loop, which is exactly what invariant 13 forbids.
    - Anyone who can file a ticket ("small fix in `src/billing/tenancy.ts`") steers which files an auto-started session may write. Validation only checks that the scope exists and doesn't overlap other tasks.
    - The text doesn't say that §8.3's auto-start trust rule (creator, editors and commenters all trusted) applies to micro-packets.
    - The path guard still parks profile-protected paths, but everything else in the repo is reachable.
  - Mitigation:
    - Micro-packets are created only for items that pass §8.3 auto-start trust. All others go to `assist`.
    - The write scope comes from rung-1 code: paths and symbols named by trusted fields, resolved through the code index. It is capped by a profile `microPacket.maxWriteScope` (file count, and no new top-level dirs).
    - Model triage may *suggest* a scope, but a suggestion beyond the deterministic one sends the packet to the desk.
    - Add micro-packets to the ring-1 table as **Off** until the workplace allows auto-started work.
    - Amend invariant 13 with the one exception and its bounds, so the invariant and §17.19 don't contradict each other.

- **Team-mode authorization lacks separation of duties** [MEDIUM]
  - Where: §17.21 Authorization (line 765)
  - Attack: Roles are per tenant, and two-person approval is "available per kind". Nothing stops one owner from requesting a capability change (or authoring the packet) and approving it themselves. A compromised owner account, or one owner's own agent session acting through the API (see H5), widens a tenant's ceiling alone.
  - Mitigation: Require requester ≠ approver for every approval-tier kind. In work tenants, make two-person approval the **default** for capability changes, trust-tier changes and role grants (opt-out only by policy-as-code PR). Role grants are themselves an approval-tier decision kind.

- **Epic merge: required checks, epic ruleset and the merge actor are unstated** [MEDIUM] (round-1 HIGH, reduced)
  - Where: §17.10 (lines 420–425); §8.1 "Always denied … undraft, merge"
  - Attack:
    - "CI green" still doesn't name the required checks, so a session-authored workflow can add a passing status context.
    - Nothing protects `sindri/epic/*`.
    - "Undrafting a task PR in an epic is an explicit allowlist entry (§8.1)" puts undraft in the **session** gate's allowlist. But §8.1 is the in-container gate, and the session holds no write token. The text leaves open which identity actually undrafts and merges.
  - Mitigation:
    - The gate's checks are named by app id and workflow path in the profile.
    - `sindri/epic/*` gets a ruleset: no direct push, no force push, merge only by the sindri app.
    - Undraft and merge are host-side outbox commands (§17.21) run by the coordinator after it re-evaluates the gate. They are never a session allowlist entry. Reword §17.10 accordingly.

- **Phone decision list is prose, not an enforced allowlist** [LOW]
  - Where: §17.12 swipe inbox row (line 485)
  - Mitigation: The decision API refuses any `(decision kind, artifact tier)` pair outside a code-defined phone allowlist with "use the desk". This becomes the §17.21 "role per decision kind" table plus a `surface` column.

### A02 — Cryptographic Failures

- **Signing keys for handover snapshots, verdicts and the audit chain have no stated owner** [MEDIUM]
  - Where: §17.19 Hosts ("signs the transferred ledger snapshot", line 658); §17.21 Audit (line 771); §8.2 "HMAC key … never mounted"
  - Attack: If one shared HMAC key signs snapshots on every host, compromising any host (including a P2 worker that holds a copy) forges every snapshot and verdict. A hash chain with no signer or external anchor can be rewritten wholesale by whoever holds the store.
  - Mitigation:
    - Use asymmetric per-host (later, per-service-identity) signing keys, and record the signer id on every snapshot, verdict and audit checkpoint. Verifiers hold only public keys.
    - Keep the verdict key only on hosts that run phase 1 (see H3).
    - Store phone decisions as raw WebAuthn assertions (authenticatorData, clientDataJSON, signature, credential id), verifiable against the enrolled public key.

### A03 — Injection

- **Context-graph sections are not stated to render as untrusted in the pack** [MEDIUM] (round-1 HIGH, reduced)
  - Where: §17.3, §17.8 ("Memory arrives through the context graph as cited pack sections"), §17.19 Context graph boundaries; §8.3 pack layout
  - Attack:
    - Consolidation now keeps the lowest trust, but nothing says how a low-trust record is *rendered*. If "read context" sections go into the pack outside `<untrusted>`, a hostile chat thread or error string reaches the implementer as plain context.
    - Trust taken from display names (common in chat and meeting-note sources) can be spoofed.
    - In team mode (§17.21), comments on items, packets and walkthroughs by contributors and viewers become new graph inputs.
  - Mitigation:
    - The pack renderer puts every record below `trusted` inside `<untrusted source=… author=…>`. Only the Task section carries instructions.
    - Trust comes only from immutable account ids in `trustedAuthors`.
    - Team-mode comments are trusted only when their author's role allows it in that tenant.
    - Add a backtest probe: a seeded hostile thread never appears outside `<untrusted>`.

- **Telemetry and incident text in comms drafts** [LOW] (carried over)
  - Where: §17.9 incident workflows
  - Mitigation: Wrap telemetry excerpts as untrusted. Run the §8.5 outward-write scrubber and URL allowlist on comms drafts.

### A04 — Insecure Design

- **H3. P2 worker hosts: no enrollment, token placement, verifier placement or class matching** [HIGH]
  - Where: §17.20 P2 row (line 730), seams (lines 742–748); §17.21 Identity and Secrets (lines 764, 772)
  - Attack:
    - Workers are hosts that run hostile session code in containers (container escape is out of scope for v1, §8.2). They "register over the tailnet", but no enrollment step is defined. Any tailnet node, including a compromised cloud box, could register, pull Steps and receive packs that hold workplace code and context.
    - "They hold no tokens beyond their session proxies." But the session proxy is the model proxy, which holds the real subscription bearer (§8.2). Either each worker holds the bearer, which conflicts with §8.2's token-never-near-session-code intent on a box that runs hostile code, or the proxy stays on the coordinator. The text doesn't say which.
    - §17.21 adds "short-lived credentials are issued to workers per Step" without saying which credentials.
    - The spool and WIP bundles "ship back to the coordinator". If phase-1 verification runs on a worker (a worker "runs containers", and the verifier is a container), a compromised worker writes its own passing verdict.
    - Host class tags exist (§17.19), but the scheduler isn't required to match a Step's ring to the worker's class. A personal cloud worker could pull a ring-1 Step.
    - A worker on the personal tailnet with an escaped container can also reach the lease store, ntfy and the dashboard unless ACLs deny it.
  - Mitigation:
    - `sindri worker enroll` at the desk: typed confirmation, a per-worker keypair and a host class tag, recorded in the ledger. `sindri worker revoke` takes effect immediately.
    - The scheduler hands a Step only to a worker whose class matches the item's ring.
    - The model proxy and every long-lived token stay on the coordinator. Worker containers reach the proxy through a per-Step dummy credential over the tailnet.
    - Phase 1 and verdict signing run only on the coordinator, over the bundle snapshot it copied and hashed. Worker output is untrusted spool and goes through the §8.2 ingestion rules on arrival.
    - Tailnet ACLs: workers can reach only the coordinator's work-queue and proxy ports. They can't reach the lease store's write path, the dashboard, ntfy or other workers. `doctor` probes this from a worker container.

- **Outbox commands: provenance and re-authorization at dispatch** [MEDIUM]
  - Where: §17.21 "Exactly-once outward actions" (line 763)
  - Attack:
    - Every outward action becomes a ledger row that one dispatcher executes with Sindri's tokens. Anything that can append a command row then holds outward-write authority. In P2 and beyond, that includes worker-originated events.
    - A command queued under one policy or lease epoch can run after the approval was revoked, the item version changed or the lease moved (TOCTOU).
  - Mitigation:
    - Only coordinator code paths emit commands, and each command carries the id of the decision or gate evaluation that authorized it.
    - The dispatcher re-checks, at execution time: lease epoch, the current approved profile hash, item version or head SHA, and the per-command allowlist (kind × target). It refuses stale commands with a code.
    - Worker events can never be commands.

- **Tenant isolation is application-level only; shared caches and images cross tenants** [MEDIUM]
  - Where: §17.21 Tenancy (line 766); §8.2 mounts (`/cache`, `/mirror`, `/index`, images keyed by repo, lockfile and toolchain)
  - Attack:
    - "A query without a tenant fails" is a coding convention, and one missed predicate leaks across tenants.
    - The package cache, image cache, mirrors, code index snapshots and vector stores are keyed without a tenant. A session in tenant A can poison a cache entry that tenant B's session later mounts, or read B's artifacts through a shared image layer.
  - Mitigation:
    - Enforce tenancy in the store: Postgres row-level security keyed by a session variable set from the authenticated actor, or a database per tenant. Add a contract test that runs every repository query with a wrong tenant.
    - Key every cache, image, mirror, index and vector store by tenant. Model-proxy accounts belong to exactly one tenant.

### A05 — Security Misconfiguration

- **Tailnet identity headers and the loopback listener** [MEDIUM] (round-1 MEDIUM, unaddressed)
  - Where: §17.12 ("tailnet identity **and** a passkey"), §17.19 Plan 6 amendment (lines 677–678)
  - Attack: "Tailnet identity" in practice means `Tailscale-User-Login` headers added by `tailscale serve`. A dashboard bound to loopback that trusts those headers can be spoofed by any local process that connects to loopback directly: the human's own agent sessions, Playwright MCP, or an escaped worker container in P2.
  - Mitigation: Ignore identity headers on the loopback TCP listener. Accept them only on a unix socket that only `tailscaled` can reach. Treat the passkey as the authenticator. `doctor` checks that the serve config isn't funnel and that the loopback port rejects header-only identity.

- **Lease store location and authentication** [MEDIUM]
  - Where: §17.19 Hosts and the lease (lines 653–656)
  - Attack:
    - The lease "lives on the always-on box", which is the personal Linux box. A ring-1 instance's active-host authority would then sit on a `personal` host, against the ring-1 table.
    - Any tailnet node that can write the lease can make itself active and gain outward-write authority.
  - Mitigation: Each instance (§17.20 P3) has its own lease store on a host of its own class. Lease writes are authenticated per host key and compare-and-swap on the epoch. Possible backings: a create-only ref under the remote's ruleset, or a store reachable only by enrolled hosts.

### A06 — Vulnerable & Outdated Components

- **Context-graph adapter pins are not placed in the capability/code split** [MEDIUM] (round-1 MEDIUM, unaddressed)
  - Where: §17.3 ("Each adapter pin is a registered artifact … evolve proposes upgrades and swaps")
  - Attack: §17.19 splits artifacts into prompt text and capability fields. An adapter pin is neither: it is third-party **code** that runs host-side with connector tokens and all ingested workplace data. Read as "registered artifact, self-adopt on backtest win", it installs a new dependency tree on the coordinator with no review.
  - Mitigation: Classify adapter pins and swaps as code tier (PR, human merge, lockfile review). Run adapters that parse untrusted content in the bounded-job container with egress limited to declared endpoints. Disable candidates' default telemetry and hosted calls, and probe that in `doctor`.

### A07 — Identification & Authentication Failures

- **H2. Passkey assertions aren't bound to the dashboard origin or a server-issued challenge** [HIGH]
  - Where: §17.19 Phone decisions (lines 671–681); §17.19 Hosts ("one stable tailnet name that follows the lease", lines 660–661)
  - Attack:
    1. **Same host, different port.** With one stable tailnet name, the practical "separate origin" for walkthroughs is another port on that name.
       - Cookies aren't port-scoped, and same-site includes other ports, so "no cookies" on the walkthrough server doesn't stop a script there from reading or setting the dashboard's non-HttpOnly cookies.
       - More importantly, the WebAuthn **RP ID is the hostname, with no port**. A script in a generated walkthrough (built from model-written and untrusted text) can call `navigator.credentials.get({rpId: <stable name>})` and get a valid user-verified assertion. The human sees a normal Face ID prompt.
    2. **Who picks the nonce.** The decision signs `(item id, item version, nonce, choice)`, and nothing says the server issues the nonce. The offline state ("queued locally, sent on reconnect") requires the client to produce signed decisions without a fresh server challenge. So the nonce is client-chosen and only checked for reuse. Combined with (1), a walkthrough script can mint an assertion for any item, version and choice. The only thing that stops it is the server checking `clientDataJSON.origin` exactly (including the port), which the spec doesn't require.
    3. **A stolen, unlocked phone.** Without `userVerification: "required"`, a stolen unlocked phone (tailnet identity and passkey on one device) holds both factors. With user verification, a biometric or PIN is still needed for each decision.
  - Mitigation:
    - Serve walkthroughs from a **different hostname** (a second tailnet name, not a port) and make them script-free: `Content-Security-Policy: default-src 'none'; img-src data:; style-src 'unsafe-inline'`, plus an `iframe sandbox` with no `allow-scripts`. Or render them from structured JSON with the dashboard's `textContent` renderer. CSP also stops a page that holds workplace code from beaconing it off the tailnet.
    - The server issues each challenge, single-use, with expiry ≤ 2 min: `H(kind, item id, item version or head SHA, choice, server nonce, expiry)`. The server verifies `clientDataJSON.origin` equals the exact dashboard origin and `rpIdHash` equals the stable name.
    - Offline queueing records only the *intent*. On reconnect the item is re-rendered and a fresh challenge is signed (one tap), so no signed decision is created offline.
    - Require `userVerification: "required"`, rate-limit per device, and alert on bursts.

- **Federation and the unified API need instance-to-instance authentication** [MEDIUM]
  - Where: §17.20 P3 (line 731); §17.21 API ("other instances all use", line 776); event consumers "other instances" (line 762)
  - Attack: If instances use each other's API or event stream, the work instance holds a credential to the personal instance (or the reverse). That conflicts with "never share state or credentials", and makes a durable cursor into another tenant's event log a data channel across the line.
  - Mitigation: Instances never call each other's API or read each other's event streams. Published packs move as signed bundles (source instance key) through a one-way drop the human carries over or approves. The API's "other instances" client class is removed, or limited to a pack-pull endpoint that serves only published packs.

### A08 — Software & Data Integrity Failures

- **H4. P3 federation ships code and capability-tier artifacts across the work/personal line as "lowest trust" packs** [HIGH]
  - Where: §17.20 P3 row (line 731: "published packs (prompt-text lenses, rules, evidence providers, workflow templates)"); this conflicts with §17.19 (lines 628–636: workflow templates and security rules are capability fields; imports drop capability fields).
  - Attack:
    - **Evidence providers are TypeScript or C# code** that runs host-side and in the verifier path (`verify()` decides verdicts, §17.11). **Workflow templates** are capability fields under §17.19.
    - "Lowest trust tier" is a concept for prompt text. It doesn't constrain code or capability fields.
    - The personal instance self-adopts model-written prompt text and reads untrusted personal sources. So a poisoned or compromised personal instance publishes an evidence provider whose `verify()` passes everything, or a workflow template with a wider tier. The work instance then runs it on workplace code.
    - P3 "starts when ring 1 begins", so this channel opens early.
  - Mitigation:
    - Federation carries **prompt text only**, signed by the source instance's key. Its capability fields are dropped on arrival, exactly as for community imports. Rules of `kind: security` don't travel.
    - Evidence providers, workflow templates and any code cross only as a PR to the receiving instance's code tier with human review (they are code-tier artifacts, not packs).
    - The first adoption of any federated pack in a ring-1 instance is approval-tier, with the text diff shown.

- **First import of community and federated prompt text can self-adopt** [MEDIUM] (round-1 residual)
  - Where: §17.19 ("Imported … packs enter as prompt text at the lowest trust tier"; "Prompt text may self-adopt"), §17.7, §17.18 seed idea
  - Attack:
    - An imported subagent definition *is* a system prompt, so it can't be wrapped as `<untrusted>` data. "Lowest trust" therefore has no effect on how it's used.
    - Upgrades never self-adopt, but the **first** import isn't excluded. A `build` swipe on a scouting card could bring one in.
  - Mitigation:
    - The first import and every upgrade of external or federated prompt text is approval-tier, at the desk, with the diff shown. It is pinned by commit and content hash.
    - Scan the text for outward URLs, install or network commands, and override phrases, and park on a hit.
    - The loader always prepends the session preamble's untrusted-data clause, which imported text can't override.

### A09 — Security Logging & Monitoring Failures

- **Hash-chained audit has no anchor; gate-input snapshots aren't required** [MEDIUM]
  - Where: §17.21 Audit (line 771); §17.10 auto-merge
  - Attack:
    - A hash chain written and stored by the coordinator proves internal consistency only. Whoever controls the store can rebuild the whole chain.
    - The round-1 request to snapshot gate inputs on each auto-merge, and to log pack graph-record ids with trust labels, isn't restated.
    - Audit isn't required to exist before P4, yet the epic merge (F2) and phone decisions (F4) come much earlier.
  - Mitigation:
    - Sign each audit checkpoint with the per-host or service key. Anchor the checkpoint hash periodically outside the store: a signed ref on the remote, or an export to the tenant's own storage.
    - Pull the outward-action and decision audit stream forward to F2 and F4. Don't wait for P4.
    - Every auto-merge records check ids and conclusions, thread resolvers, evidence hashes and SHAs. Every pack records graph record ids and trust labels.

- **Trace context and OTel export from containers** [LOW]
  - Where: §17.21 Observability ("W3C trace context passed into containers")
  - Attack: Session code controls any trace or baggage headers it sends back and any spans it exports. It can forge correlation into other items or tenants, inject into logs, or carry repo content out through span attributes.
  - Mitigation: The coordinator assigns trace ids and ignores incoming `tracestate` and baggage from containers. Container span export goes only through the egress proxy to the coordinator's collector, with the scrubber and a size cap. Session-originated spans are tagged untrusted.

### A10 — SSRF

- **Connector fetch on extracted URLs and endpoint ranges** [MEDIUM] (round-1 MEDIUM, partly addressed)
  - Where: §17.3 `Connector.fetch(ref)`, deterministic link extraction; §17.5 network row
  - Attack:
    - Session endpoints are now capped by the profile ceiling.
    - Host-side connectors are not. URLs extracted from chat or tickets can still become host-side fetches that reach loopback services (Prism, bridge, dashboard), the tailnet (lease store, ntfy) or, in P2, worker ports.
  - Mitigation:
    - Restate §8.3 for the graph: connectors fetch only typed ids for their own source system. Extracted URLs become `mentions` edges with no fetch.
    - The egress proxy denies loopback, link-local, RFC 1918 and `100.64.0.0/10` unless a profile endpoint names them exactly.

## STRIDE findings

### S — Spoofing
- H2: a walkthrough script on the same hostname mints passkey assertions; client-chosen nonces (HIGH).
- H3: an unenrolled tailnet node registers as a worker and pulls Steps (HIGH).
- Tailnet identity headers forged on the loopback listener (A05, MEDIUM).
- **ntfy publish is still unauthenticated** [MEDIUM] (round-1, unaddressed): any tailnet node, including a P2 worker, can publish a look-alike "approve now" push with a deep link. Mitigation: ntfy auth with a publish token only the coordinator holds; payloads `{kind, id}` only (§8.6); deep links resolve only to the dashboard origin.

### T — Tampering
- H1: a ticket author steers a micro-packet's write scope (HIGH).
- H4: federated evidence providers or workflow templates rewrite verdicts or tiers (HIGH).
- Outbox command injection and stale dispatch (A04, MEDIUM).
- Cross-tenant cache poisoning (A04, MEDIUM).
- Handover snapshot: secrets, mirrors and WIP [MEDIUM] (round-1 residual). §17.19 signs and verifies the snapshot, but doesn't restate:
  - Secrets never travel; each host runs `sindri auth`.
  - Mirrors are re-fetched from the remote, not copied.
  - Phase 1 is re-run on every transferred WIP bundle.
  - Profile approval hashes are re-checked against the git-tracked profile at the target.
- Test-path convention widens write scope to shared test setup files such as `vitest.setup.ts` and `conftest.py` [LOW] (round-1, unaddressed). Mitigation: add only per-task test files, and put shared setup behind the §8.5 path guard.

### R — Repudiation
- Unanchored audit chain; no gate-input snapshots before P4 (A09, MEDIUM).
- Phone undo exists (10 s), but the ledger record doesn't capture what was shown [LOW]. Mitigation: record the rendered card's hash and the short head SHA in the decision record.

### I — Information Disclosure
- H3: packs delivered to unenrolled or wrong-class workers (HIGH).
- H2: a walkthrough page holding workplace code, with scripts enabled, beacons it out from the phone (HIGH, covered by the CSP mitigation).
- Context-graph store retention [MEDIUM] (round-1, still unaddressed before P4): §17.21 data lifecycle is scheduled with P4, but the graph, vector and FTS stores and consolidated memory hold workplace content from F5 onward. Mitigation:
  - Per-store retention in the profile from F5.
  - Purge propagation from connector tombstones.
  - Stores at 0600 in sindri-private space.
  - `BehaviorSource` limited to aggregate counts.
- Account routing enforcement point [LOW]: §17.15 and §17.20 restate the work/personal rule but not the enforcement. Mitigation: bind the session proxy credential to the item's ring class at launch, and refuse untagged accounts.

### D — Denial of Service
- Mostly addressed:
  - P1 scheduler WIP limits and resource classes.
  - Human-minutes ceiling.
  - Notification budget per surface.
  - Per-tenant fair scheduling in P4.
- **Babysitter and incident loops** [LOW]: these still have no per-workflow spend budget, and the babysitter's `act` action set is still undefined. Mitigation:
  - One open investigation per incident key.
  - A per-PR daily cap on babysitter rounds.
  - A daily spend budget per workflow that parks the workflow when it runs out.
  - `act` = fix-round push through §8.5 plus structured-field comments only.
- A P2 worker that heartbeats but never finishes holds Steps [LOW]. Mitigation: a hard wall-time per Step regardless of heartbeats, and per-worker failure scoring that drains a misbehaving worker.

### E — Elevation of Privilege
- **H5. The unified API would replace the host-verb authority without a substitute** [HIGH]
  - Where: §17.21 API (line 776: "One versioned API … that the CLI, dashboard, phone and other instances all use … No client gets a private back door"); §8.7 ("approving needs an interactive terminal and a typed confirmation"); §17.12 ("Host CLI verbs … stay the authority")
  - Attack:
    - Today, approval-tier verbs (`profile approve`, capability approvals) are authenticated by the host OS user, and they also need an interactive TTY with typed confirmation. That TTY requirement is what stops the human's own unsandboxed host agent sessions from approving through Bash.
    - If the CLI becomes a client of the HTTP API, the TTY check runs client-side, where it can be skipped. Any local process with the API credential, or with loopback access to a header-trusting listener, can call the approve endpoint. That includes a host agent session, an MCP server, or the dashboard's own browser automation.
    - This would undo the boundary §17.12 says stays authoritative.
  - Mitigation:
    - Approval-tier decision kinds (capability fields, trust tiers, profile approval, role grants, packet freeze) require either a WebAuthn user-verified assertion over the server-issued challenge (H2), or a local channel that proves a human at a TTY: a unix socket with peer-credential check, plus a server-generated confirmation code typed back.
    - Host agent sessions get no API credential.
    - The API refuses approval-tier kinds from service identities, always.
    - Add a `doctor` probe: a non-TTY process on the host can't complete an approval-tier call.
- H1: a model-derived write scope frozen without the desk (HIGH).
- H4: federated code and capability fields (HIGH).
- Self-approval by a single owner (A01, MEDIUM).

## Verdict
- **Critical findings:** 0
- **High findings:** 5
  - H1: micro-packet auto-freeze with model-derived write scope (§17.19, contradicts invariant 13)
  - H2: passkey assertion not bound to the exact origin or a server challenge (walkthrough on the same hostname; offline queue)
  - H3: P2 worker enrollment, token placement, verifier placement and class matching
  - H4: P3 federation ships evidence providers (code) and workflow templates (capability fields)
  - H5: the unified API drops the TTY and typed-confirmation guard on approval-tier verbs
- **Recommendation:** FIX-CRITICAL-FIRST

All five are spec edits, and each comes before the step that builds it:
- H1 before F0 and F0.5 (micro-packets).
- H2 before F4 (phone).
- H3 before P2.
- H4 before P3, which starts with ring 1.
- H5 before the API ships. Note it in the C# port seams now, so the API isn't designed around client-side TTY checks.

The round-1 structural fixes (ceiling, capability split, boundary table, holdouts, lease, host tags, ring-1 table, lowest-trust consolidation) hold.

verdict: NEEDS_WORK
