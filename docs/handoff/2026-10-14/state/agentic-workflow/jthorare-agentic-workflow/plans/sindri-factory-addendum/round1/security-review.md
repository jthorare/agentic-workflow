# Threat Model — Sindri factory addendum (§17)

**Mode:** plan
**Source:** ~/repos/agentic-workflow/docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md (rev 2), against the main spec's safety boundary (`2026-10-07-sindri-design.md` §8) and the Plan 6 dashboard spec (§6 Security)
**Reviewed:** 2026-10-09

## Summary
The addendum keeps the §8 boundary intact for code execution: containers, the model proxy, sindri-mediated push and the two-phase verifier are all still in place. Where it falls short is authority. Four new things now decide what a session may do or what gets merged: lens definitions (including imported community ones), a model-proposed workflow, phone decisions, and an act-tier babysitter feeding an auto-merge gate. None of these is classified as a protected surface, and none is capped by the profile's approved allowlist. The most serious gaps are these:
- The compiled sandbox policy has no ceiling.
- Lens capability fields can change through import, self-adopt or a model pass.
- The context graph can launder untrusted conversation and telemetry text into "Sindri-owned" memory.
- The phone path doesn't say how enrollment works or how a passkey assertion is tied to the exact decision.
- Host handover moves the ledger and secrets between machines without stating integrity rules. It can also put ring-1 workplace data on the personal Linux box.

Nothing is exploitable as written, because each gap needs a compromised upstream, a malicious input author or an implementer filling the gap the wrong way. But the plan states several security requirements that its own mechanisms don't meet, so the HIGH findings should be settled in the spec before F2/F3/F4/F7.

## Coverage
| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 3 |
| A02 Cryptographic Failures | findings: 1 |
| A03 Injection | findings: 3 |
| A04 Insecure Design | findings: 3 |
| A05 Security Misconfiguration | findings: 2 |
| A06 Vulnerable & Outdated Components | findings: 1 |
| A07 Identification & Authentication Failures | findings: 2 |
| A08 Software & Data Integrity Failures | findings: 2 |
| A09 Security Logging & Monitoring Failures | findings: 1 |
| A10 SSRF | findings: 1 |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **Compiled sandbox policy has no ceiling; lens capability fields widen it without approval** [HIGH]
  - Where: §17.5 table (lines 202–213), §17.7 lens schema (lines 267–280), §7.4 adoption tiers (main spec)
  - Attack: The session's commands, MCP tools, skills/agents and network are compiled from the lens's `commands`, `mcp`, `skills`, `agents` and the packet's evidence endpoints. Lenses are "registered artifacts". In §7.4, registered artifacts other than the listed protected surfaces sit in the **self-adopt** tier. So an evolve proposal (or an imported pack upgrade, see A08) that adds `kubectl`, a write-capable MCP tool or a broad command glob to `lens.performance` can be adopted automatically. Every later session with that lens gets the wider policy. §8.1's "default deny / profile allowlist" isn't stated as the upper bound of the compiled policy.
  - Mitigation: State it as an invariant: **compiled policy = (lens ∪ packet requests) ∩ approved profile allowlist**. Anything outside the profile is dropped and logged, never granted. Add lens capability fields (`commands`, `mcp`, `agents`, network needs, `model`) to the §7.4 **approval tier** list. Keep prose fields (`questions`, rule selectors) self-adopt. The gate must also re-check every call against the profile allowlist directly, not only against the compiled policy. Add a contract test: a lens that names a command outside the profile produces a policy without it.

- **Phone can "raise a standing workflow's tier", which is a privilege escalation from the weakest surface** [HIGH]
  - Where: §17.12 swipe inbox row (line 433) vs §17.9 (lines 367–368) and §8.7 profile approval
  - Attack: §17.9 says a tier change is an approval-tier profile change. §8.7 requires an interactive terminal plus typed confirmation to approve a profile. §17.12 lists "raise a standing workflow's tier" as a binary phone swipe. Raising a workflow to `act` grants it outward write actions. A mis-swipe, a stolen unlocked phone, or a UI-redress attack (A03) would grant autonomous write authority. The swipe list also includes "approve … an evolve proposal" without excluding approval-tier proposals.
  - Mitigation: Take tier raises off the phone list (lowering a tier from the phone is fine, because it only removes authority). Enforce the phone list in code as an allowlist keyed by `(decision kind, artifact tier)`. Any approval-tier evolve proposal, rule of `kind: security`, scope expansion or tier raise is refused on the phone path with a "use the desk" response.

- **Epic auto-merge gate trusts inputs that agents control** [HIGH]
  - Where: §17.10 (lines 374–378), §17.9 PR babysitter `act` (line 355), §8.1 "always denied: undraft, merge"
  - Attack: The gate is "CI green, threads resolved, evidence satisfied, round cap not exceeded". The act-tier babysitter can resolve review threads and push fix rounds, so the one human-shaped signal can be cleared by an agent. Other gaps:
    - "CI green" can mean any status context unless the plan names required checks.
    - The merged SHA isn't bound to the SHA that passed phase 1 and phase 2.
    - GitHub can't merge a draft PR, so the gate needs an undraft, which §8.1 forbids without approval. Implementers will resolve this conflict ad hoc.
    - Nothing says the epic branch is protected, so a session-side push path or an unprotected ref could fill the epic.
    - The final epic → default PR then puts one large, already-merged diff in front of the human, which concentrates review fatigue.
  - Mitigation:
    - Compute the gate host-side from trusted signals only: required checks by app/workflow id; threads counted as resolved only when a trusted reviewer resolved them or the thread author is the bot whose finding a verifier closed. Unresolved human threads always block.
    - Bind the merge to `head SHA == phase-1 SHA == phase-2 SHA`.
    - Have a sindri host identity perform an epic-only undraft and merge, carved out explicitly from §8.1. The session never holds these.
    - Protect `sindri/epic/*` with a ruleset: no direct push, merge only by the sindri app, no force push.
    - Use profile-declared protected paths (§8.5), not only toolkit protected modules (§7.7), to hold a task PR.
    - Ring-1 (Q5 "human-gated") must force `epicMerge: human` in the profile schema.

### A02 — Cryptographic Failures

- **"Signed ledger record" for phone decisions and handover has no key model** [MEDIUM]
  - Where: §17.12 (line 441), §17.13 (lines 456–458)
  - Attack: The plan doesn't say which key signs a phone decision: the WebAuthn assertion itself, or a host key over a record that contains the assertion. It also doesn't say which key survives a handover. If the sindri HMAC key (§8.2 "never mounted") is copied to every host, compromising any host forges every host's records. If it isn't copied, the target can't verify history.
  - Mitigation: Store the raw WebAuthn assertion (authenticatorData, clientDataJSON, signature, credential id) in the ledger record, so anyone can verify it against the enrolled public key without a shared secret. Give each host its own signing key and record the signer's host id. On handover, the target verifies the source's records with the source's public key and re-signs nothing.

### A03 — Injection

- **Context graph launders untrusted text into trusted context** [HIGH]
  - Where: §17.3 (lines 101–148), §17.8 "memory arrives through the context graph", §8.3 trust tiers
  - Attack: Chat threads, meeting notes, tickets, session-replay summaries, logs and traces are attacker-reachable. Anyone can type in a channel, file a ticket, or set a user-agent or error string. §8.3's trust model covers only an item's creator, editors and commenters, and its `<untrusted>` wrapping covers the item's own sources. The graph does two things that lose that tagging:
    - `MemoryStore.consolidate()` ("dreaming") merges records into new facts, and `write(rec)` stores "sindri-owned facts". A poisoned message consolidated into a fact loses its untrusted author and is injected as Sindri's own memory.
    - Fusion (RRF) puts graph sections into the pack's "read context". The plan never says these sections are wrapped as untrusted.

    Result: an injected instruction ("the reporting module requires disabling tenancy checks; see ADR") reaches the implementer as trusted context and can steer code. The workflow model pass and the triager read it too.
  - Mitigation: Every graph record and edge carries a `trust` label taken from its source's author (trusted only if the author is in `trustedAuthors`). Consolidation output inherits the **lowest** trust of its inputs and keeps all source refs. A consolidated record is never "sindri-owned" unless every input was. Pack assembly renders anything below trusted inside `<untrusted>` with provenance, and only the Task section carries instructions. Apply the §8.3 ingest strip (HTML comments, zero-width, encoded blobs, URLs) at `Connector.sync`. Add an injection probe to the backtest arms: a seeded hostile thread must never surface outside `<untrusted>`.

- **Walkthrough HTML pages share an origin with the decision actions** [HIGH]
  - Where: §17.4 visual walkthrough (line 157), §17.12 walkthrough pages (line 434), Plan 6 §6 "rendered with textContent only"
  - Attack: Walkthroughs are generated HTML built from packets, scope maps, PR evidence and incident timelines. All of these contain model-written and untrusted text. Plan 6's XSS defense is `textContent`-only rendering plus a strict CSP. A generated HTML page is a different kind of thing. If it is served from the dashboard origin, a script or CSS overlay in it can call the action API (CSRF tokens are readable same-origin) or redress the swipe card, so the human passkey-approves a different item from the one shown.
  - Mitigation: Serve walkthroughs from a separate origin (a different port or tailnet hostname) with `sandbox` (no `allow-scripts`, no `allow-same-origin`), CSP `default-src 'none'; img-src data:`. Or render them from structured JSON with the same `textContent` renderer. The decision card must render the decision from server data only, and the WebAuthn challenge must commit to that data (A07).

- **Incident and telemetry text flows into reports and comms drafts** [LOW]
  - Where: §17.9 incident investigator and comms manager (lines 358–360)
  - Attack: Log lines and error messages are attacker-controlled. They land in "cited timelines" and in drafted external updates the human sends.
  - Mitigation: Wrap telemetry excerpts as untrusted. Run the outward-write scrubber and URL allowlist (§8.5) on comms drafts, and mark prod-derived content sensitive per §8.5 "Prod data".

### A04 — Insecure Design

- **PR babysitter starts at `act` without the promotion bar, and its action set is undefined** [MEDIUM]
  - Where: §17.9 table (line 355) vs lines 367–368
  - Attack: Every other workflow earns a tier through measured `report` precision. The babysitter defaults to `act`. Its triggers include review comments and CI logs, which are untrusted unless filtered. "Fix rounds within the ship contract" doesn't list which outward actions are allowed (push, resolve thread, re-request review, comment, undraft). Combined with epic auto-merge, it closes the loop with no human. It also conflicts with the ring-1 human-gated policy.
  - Mitigation: Define the `act` allowlist explicitly: fix-round push through the §8.5 pipeline, and comments composed from structured fields. Never resolve threads, undraft or merge. Restate §8.3: only trusted reviewers' comments trigger rounds. Default it to `propose` in ring 1.

- **Workflow model pass can add lenses, and added lenses add capabilities** [MEDIUM]
  - Where: §17.7 step 3 (lines 306–308)
  - Attack: The model pass reads the packet and the context graph, both partly untrusted (A03). It "may add lenses, never remove a required one". Adding the data or performance lens adds migration or load-test commands to that Step's sandbox. Invariant 13 says agents don't decide their scope, but a model-chosen lens set is exactly that.
  - Mitigation: A lens added by the model and not by a deterministic trigger runs **review-only** (read tools, no lens commands), unless the human accepts it at freeze. `packet freeze` shows a **capability delta** (commands, MCP tools and endpoints beyond the deterministic baseline) and requires the human to accept it. Combined with the A01 ceiling, the worst case is bounded.

- **Read scope is claimed as enforced by mounts, but the mounts expose the whole repo** [MEDIUM]
  - Where: §17.5 table, row "File reads … Mounts (§8.2) and the gate" (line 208); §17.4/§17.7 holdout barrier
  - Attack: §8.2 mounts the full mirror and base worktree read-only, and `/workspace/src` is a full clone. A Bash `git show`, `cat` or `rg` reads anything in the repo. A PreToolUse gate can't reliably constrain reads through a shell. Reads outside scope are a context-discipline issue for repo code. But for the **held-out acceptance examples and evaluator notes** (the "information barrier"), any copy in the repo, the packet dir or `/sindri` is readable, and the implementer can overfit to the verdict.
  - Mitigation: Say plainly that read scope is a context control, not a confidentiality boundary. Holdouts and evaluator notes live only in sindri-private storage, are never committed to the repo or the packet directory inside the repo, and are never mounted into implementer containers. They are passed only to the evaluator container. Add a `packet validate` check that no holdout content hash appears in any path under the repo.

### A05 — Security Misconfiguration

- **Per-session settings overlay and MCP "read-only" rely on writable or self-declared config** [MEDIUM]
  - Where: §17.8 mechanism (lines 339–341), §17.3 Connector row (line 117), §17.5 MCP row (line 211)
  - Attack: "Sindri writes a per-session settings overlay that enables only the planned skills, agents, MCP servers **and hooks**." If the overlay lands in a user-level settings file, the unprivileged session user can edit it to enable more MCP servers or skills. If it can toggle hooks, a bad overlay could drop the gate. "Read-only by default" MCP servers and the connector's "read-only MCP client" break if the read-only property comes from server tool annotations (`readOnlyHint`), because servers declare those themselves.
  - Mitigation: Write the overlay into the root-owned managed-settings layer via the read-only `/sindri` mount. The overlay schema has no field that can disable or replace hooks in managed settings (the gate and safety hooks are image-fixed). MCP and connector tools are allowed by **exact tool name** from an approval-tier list, as in §8.1. Annotations are never trusted. `doctor` probes that a session can't modify its overlay.

- **Phone surface conflicts with the Plan 6 binding and Host model** [MEDIUM]
  - Where: §17.12 network (lines 426–427) vs Plan 6 §6 (127.0.0.1 only, `Host` must be `127.0.0.1`/`localhost`, CSRF/Origin)
  - Attack: `tailscale serve` proxies to loopback with `Host: <machine>.<tailnet>.ts.net`, and WebAuthn needs that hostname as the RP ID over HTTPS. The plan doesn't amend Plan 6's Host/Origin checks. The likely ad hoc fix is to drop the Host check, which reopens DNS rebinding. Separately, a server that trusts Tailscale identity headers (`Tailscale-User-Login`) is spoofable by any local process (including the human's own agent sessions, such as Playwright MCP) that connects to loopback directly.
  - Mitigation: Specify an exact Host and Origin allowlist (`localhost:<port>` for desk use, `https://<host>.<tailnet>.ts.net` for phone use). Set the RP ID to the tailnet name. Either ignore Tailscale identity headers or accept them only on a dedicated listener socket that only `tailscaled` can reach (a unix socket with restricted permissions). Treat the passkey as the actual authenticator, with the tailnet as network reachability. Never use `funnel`, and have `doctor` check that serve config isn't public.

### A06 — Vulnerable & Outdated Components

- **Context-graph adapter swaps bring third-party code onto the host process that holds workplace data, at an unspecified tier** [MEDIUM]
  - Where: §17.3 "Each adapter pin is a registered artifact … evolve proposes upgrades and swaps" (lines 98–99, 141–142); Q2 candidates (Mem0, Zep/Graphiti, Letta, Cognee, LanceDB)
  - Attack: Adapters run host-side, outside the session container, with connector read tokens and all ingested data. If adapter pins are self-adopt like "vendored-pack upgrades" (§7.4), a backtest win can install a new dependency tree on the host with no human review. Several candidates also make network calls (hosted LLM extraction, telemetry) by default.
  - Mitigation: Classify adapter pins and swaps as **code tier** (PR, human merge, lockfile review). Run adapters that parse or consolidate untrusted content in a container with egress limited to their declared endpoints. A candidate's default outbound calls (telemetry, hosted extraction) must be disabled and probed in `doctor`.

### A07 — Identification & Authentication Failures

- **Phone decision authentication is underspecified: enrollment, decision binding, revocation** [HIGH]
  - Where: §17.12 authentication (lines 438–447)
  - Attack:
    1. **Enrollment:** the plan doesn't say how a passkey becomes "bound to an enrolled device". If enrollment is a web flow on the tailnet surface, any tailnet node, or a local process that reaches loopback, can register its own (even virtual) authenticator.
    2. **Binding:** if the WebAuthn challenge is a generic session nonce, one assertion can be replayed or applied to whatever the server currently shows. An item whose head SHA or evidence changed between render and swipe gets approved in its new state (TOCTOU).
    3. **Single device:** the tailnet identity and the passkey are on the same phone, so an unlocked stolen phone holds both factors.
  - Mitigation:
    - Enrollment only via a host CLI verb (`sindri device enroll`) with typed confirmation that prints a one-time code, and a ledger record for it. Use `sindri device revoke` for a lost phone, plus a host-side "phone decisions off" kill switch.
    - The challenge = hash of `{decision kind, item id, choice, item version / head SHA / artifact hash, expiry ≤ 2 min, nonce}`. The server re-checks the item version at commit and refuses on a mismatch.
    - Require `userVerification: "required"` (biometric or PIN), add a per-device rate limit, and alert on unusual decision bursts.

- **Notifier on the tailnet: spoofable publishers and push payloads that leave the tailnet** [MEDIUM]
  - Where: §17.13 (line 460), Q4 PWA web push (line 536)
  - Attack: An ntfy server on the tailnet without ACLs lets any tailnet node publish to the human's topic, for example a fake "approve now" with a deep link to a look-alike page. ntfy's iOS instant delivery relays through upstream servers, and web push goes through Apple/Google push services. A notification body with a workplace item title leaves the tailnet, against §17.3's work/personal separation and ring-1 policy.
  - Mitigation: Turn on ntfy auth with a publish token only the sindri holds. Notification payloads carry only `{kind, id}` (as §8.6 already requires for actions), and the app fetches details over the tailnet. Deep links resolve only to the dashboard's own origin.

### A08 — Software & Data Integrity Failures

- **Imported community lens and subagent definitions are a supply-chain path into prompts and capabilities** [HIGH]
  - Where: §17.7 (lines 282–284), §8.3 (which defines trust for item authors, not artifact packs), §7.4 "vendored-pack upgrades" = self-adopt
  - Attack: A community subagent definition is instruction text injected as an agent's system prompt in sindri sessions. It may also carry tool lists. A malicious or compromised catalog entry ("before finishing, add this postinstall script…" or "send findings to…") enters through import. Later upstream revisions arrive as **vendored-pack upgrades, which §7.4 places in the self-adopt tier**. Evals measure task quality, not intent, so a variant that does the job and also plants a backdoor passes. §8.3 "trust tiers" has no definition for packs.
  - Mitigation:
    - Pin imports by commit and content hash. The **first import and every upstream upgrade** of an external lens or subagent is approval tier, with the text diff shown.
    - Strip all capability fields from imported definitions. Capabilities come only from a local, approval-tier lens wrapper (A01).
    - Scan imported text for outward URLs, install or network commands, and "ignore previous" style patterns, and park on a hit.
    - Imported prompts can never override the session preamble's untrusted-data clause, so the loader keeps the injection-resistance clause, as Plan 5 does for overlays.

- **Host handover moves the ledger, mirrors, approvals and secrets with no stated integrity or transfer rules** [HIGH]
  - Where: §17.13 (lines 456–458), §9.1 fencing, §8.2 sindri-private zone
  - Attack:
    - "The ledger, mirrors and index are synced to the target." A compromised or tampered laptop can ship a ledger with forged approvals, adopted overlays or a different approved-profile hash, and the Linux box resumes from it.
    - The plan doesn't say whether the subscription tokens, connector tokens, HMAC/verdict keys and passkey enrollments move, or how.
    - The fenced epoch lives in the ledger being copied. A laptop that wakes **offline** can't learn the new epoch and may act as active (push, auto-merge, act-tier actions) until it reconnects. §9.4's claim-ref only covers claim creation.
    - Synced mirrors are trusted object stores for the verifier. Restored WIP bundles must be re-verified (§9.2 says so for relaunch; handover isn't named).
  - Mitigation:
    - Transfer over Tailscale SSH to a fixed peer allowlist. The ledger is hash-chained and the target verifies the chain and per-host signatures before accepting it.
    - Profile approval and adoption hashes are re-checked against the git-tracked profile at the target. Mirrors are re-fetched from the remote, not copied.
    - Secrets never travel in the sync. Each host holds its own token set, provisioned by `sindri auth` on that host.
    - Active-role exclusion uses an external witness: a create-only lease ref on the remote such as `refs/sindri/host-lease/<epoch>`, which must be held before any outward write. A host that can't confirm the lease stays passive.
    - Handover re-runs phase 1 on every transferred WIP bundle.

### A09 — Security Logging & Monitoring Failures

- **New autonomous actions lack stated audit records and alerts** [MEDIUM]
  - Where: §17.9 `act` tier, §17.10 auto-merge, §17.12 phone decisions, §17.3 graph ingest, §17.15 routing
  - Attack: §8.7's audit log covers gated tool calls in sessions. The new host-side actors aren't named: epic auto-merge, act-tier workflows, phone decisions, connector syncs, account routing. After an incident, there's no record of which gate inputs justified a merge, which graph records went into a pack, or which account served a ring-1 Step.
  - Mitigation: Every auto-merge records a snapshot of its gate inputs (check ids and conclusions, thread resolvers, evidence artifact hashes, SHAs). Every pack records the graph record ids and their trust labels. Every proxy request records the account id and class. Phone decisions record device id and assertion. Alert on: auto-merges per day above a threshold, a phone decision on an item whose version changed, any ring-1 request on a personal account (should be impossible, see I), and phone decisions from a newly enrolled device within 24 h.

### A10 — SSRF

- **Evidence-plan endpoints and connector refs can widen host or container egress** [MEDIUM]
  - Where: §17.5 network row ("the environment endpoints the evidence plan needs", line 210); §17.3 `Connector.fetch(ref)` and deterministic link extraction (lines 117, 130)
  - Attack: Packets are drafted with a model at the desk and can contain URLs from untrusted sources. If evidence endpoints are taken from the packet, a session gets egress to an arbitrary host, including internal services or tailnet `100.64.0.0/10` addresses and MagicDNS names. Host-side connectors that `fetch(ref)` on URLs extracted from chat or tickets turn untrusted text into requests from the host, which can reach loopback services (Prism, bridge, dashboard) and the tailnet. §8.3's "by id, never by arbitrary URL" isn't restated for the graph.
  - Mitigation: Compiled endpoints = packet request ∩ the profile's environment allowlist. The egress proxy always denies loopback, link-local, RFC1918 and the tailnet CGNAT range unless a profile endpoint names them exactly. Connectors fetch only typed ids for their own source system. Extracted URLs become `mentions` edges with no fetch, unless their host matches a connector's base URL.

## STRIDE findings

### S — Spoofing
- Phone enrollment and decision binding (A07, HIGH): an attacker-registered authenticator, or a replayed or retargeted assertion, impersonates the human.
- Tailscale identity headers trusted on a loopback listener (A05, MEDIUM): local processes forge tailnet identity.
- Unauthenticated ntfy publish (A07, MEDIUM): any tailnet node sends look-alike approval prompts.
- Context-graph author spoofing [MEDIUM]: chat and meeting-note records often carry display names, not immutable ids. Trust must come from immutable account ids in `trustedAuthors` (§8.3). A display-name match never makes a record trusted.

### T — Tampering
- Lens capability fields modified via self-adopt or import (A01/A08, HIGH).
- Handover ledger and mirror tampering (A08, HIGH).
- Session editing its own settings overlay (A05, MEDIUM).
- Test-path convention widens write scope to CI-executed setup files [LOW]: §17.5 `write` adds "its tests by the repo's test-path convention". Globs like `tests/**` include shared setup, fixtures and config (`vitest.setup.ts`, `conftest.py`) that run for every suite and in CI. Mitigation: add only test files that match a per-task pattern (`<module>.test.*`). Shared test setup and config files sit behind the §8.5 path guard.

### R — Repudiation
- Auto-merges, act-tier actions and account routing without gate-input snapshots (A09, MEDIUM).
- Swipe misapproval with no undo [LOW]: binary swipes on a phone invite mistaken approvals, and nothing records what the human saw. Mitigation: the card shows the item id and short head SHA and records the rendered card hash in the ledger record. Reversible decisions (accept evidence, approve a proposed rule) get an undo window before taking effect.

### I — Information Disclosure
- **Ring-1 workplace data can land on the personal Linux box** [HIGH]
  - Where: §17.13 (lines 454–461), §17.15 work/personal account tags, §17.17 Q5 ("work accounts only"), §17.3 separation
  - Attack: Accounts are tagged `work` or `personal`, but **hosts aren't**. Handover syncs ledger, mirrors and index (workplace source code and context-graph stores) to the always-on Linux box. That box is personal hardware, which conflicts with a workplace source-of-record and human-gated policy. The same goes for walkthroughs and notifications reaching personal devices.
  - Mitigation: Tag hosts `work` or `personal` in the profile. A ring-1 profile lists only work hosts, and `host handover` refuses a target whose class doesn't match every active profile's ring. Mirrors and graph stores are partitioned per ring and synced only to hosts of the matching class. `doctor` fails if ring-1 data exists on a personal-class host.
- Multi-account routing enforcement [MEDIUM]: §17.15 states the work/personal rule but not where it's enforced. Mitigation: at launch, the session's proxy credential is bound to an account class taken from the item's profile ring. The proxy refuses any upstream account of another class, and untagged accounts are unusable (fail closed). A checkpoint relaunch can move only to an account of the same class, and the checkpoint's pack is never sent through another class.
- Context-graph persistence and retention [MEDIUM]: §8.4's 14-day TTL covers packs, not the graph, vector and FTS stores, consolidated memory, or behavior and telemetry summaries. These hold scrubbed but sensitive workplace content indefinitely, and embeddings of sensitive text can leak content. Mitigation: per-store retention in the profile, purge propagation when a source deletes a record (from the connector cursor's tombstones), stores at 0600 in sindri-private space, and `BehaviorSource` limited to aggregate counts (no per-user ids).
- Read scope isn't a confidentiality boundary, which affects holdouts (A04, MEDIUM).
- Notification payloads leaving the tailnet through push relays (A07, MEDIUM).

### D — Denial of Service
- **Event-fired workflows, quota and load tests** [MEDIUM]
  - Attack: An error spike or alert storm fires many incident investigators, and the babysitter can loop on flaky CI. Each consumes model quota, and multi-account routing spreads the drain across every account, which can push the human's own accounts toward `reserveForHuman`. The performance lens's load tests, if pointed at a shared preview or staging environment, can degrade it for other people.
  - Mitigation: Per-workflow concurrency caps and debouncing (one open investigation per incident key, babysitter capped per PR per day). Count each workflow's spend against a daily budget that parks the workflow when exhausted. Keep `reserveForHuman` per account, not pooled. Load-test targets only ephemeral per-PR environments named in the profile, with a request-rate cap enforced by the egress proxy.
- Host-side parsing of ingested documents [LOW]: connectors that parse design files, HTML and PDFs on the host widen the host attack surface §8.2 tries to keep small. Mitigation: run parsing and extraction in the short-lived bounded-job container (§8.2 "Bounded jobs").

### E — Elevation of Privilege
- Compiled policy without a profile ceiling, plus lens capability self-adopt (A01, HIGH).
- Phone tier raise to `act` (A01, HIGH).
- Epic auto-merge gate cleared by an act-tier agent (A01, HIGH).
- Imported subagent definitions carrying capabilities (A08, HIGH).
- Model-added lenses (A04, MEDIUM).
- Babysitter `act` by default (A04, MEDIUM).

## Verdict
- **Critical findings:** 0
- **High findings:** 9
  - compiled-policy ceiling and lens capability tier
  - phone tier raise
  - epic merge gate inputs
  - context-graph taint laundering
  - walkthrough origin
  - phone enrollment and binding
  - imported lens supply chain
  - handover integrity
  - ring-1 data on the personal host
- **Recommendation:** FIX-CRITICAL-FIRST

Most of these are spec edits, not new machinery:
- one ceiling invariant;
- three items added to the §7.4 approval-tier list;
- trust labels carried through the graph;
- an exact WebAuthn challenge format and an enrollment CLI verb;
- a separate origin for walkthroughs;
- host class tags;
- a remote lease ref for handover.

Settle each in the spec before the rollout step that builds it: F2 (policy, epics), F3 (lenses, import), F4 (phone, workflows), F5 (graph), F7 (handover, routing).

verdict: NEEDS_WORK
