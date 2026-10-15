# Threat Model: Sindri application design epic (PR #170)

**Mode:** plan
**Source:** `plans/2026-10-11-sindri-app-epic/plan.md`, which indexes `docs/sindri/README.md` (X1–X18), `2026-10-10-sindri-api-surface-design.md`, `2026-10-10-sindri-workflow-engine-design.md` (rev 5) and `2026-10-10-sindri-context-retrieval-design.md`, read in the worktree on branch `docs/sindri-app-epic` at `5a4f05b0`
**Reviewed:** 2026-10-11
**Scope:** coherence across the three specs after X1–X18. Each spec's settled internals are not re-reviewed.
**Threat model:** gates guard against honest mistakes by agents, not hostile agents. This is a single-owner personal toolkit. Exploit-style concerns are judged in that frame. Two outside parties are still in scope: ticket and Slack authors, who can inject text, and other local software such as browsers and editors.

## Summary

The shared authorization model is sound. One caller model, one action-class table and one command chain now cover the API, engine and context routes. MCP is query-plus-request only, and every `protected` action needs a server challenge typed at a TTY. The two HIGH findings are both about **work context reaching the wrong session**:
- **H1.** Context reads are "repo-scoped" only by name. The context spec never says what repo scope means for Linear, Onyx or PRD records. Its `context.mcp.enabled` egress switch covers only the MCP transport, so a session in any repo can run `sindri context query` through the CLI or curl and get work intent text.
- **H2.** "Codex and Cursor get the equivalent entry" is not specified. Codex has no per-project MCP scope, so the natural registration is user-wide with one fixed `X-Sindri-Repo`. That breaks the "never user-wide" rule. Codex and Cursor are now first-class launch targets (X2, X15), which makes this path real.

The MEDIUM findings are mostly coherence gaps that touch authorization:
- `grant-tokens` falls outside API's fail-closed rule for protected fields (M1).
- The nonce and challenge routes have no catalog class (M2).
- Map review's "protected" does not fit the one-challenge-per-action model (M3).
- `map import` and `map promote` are `desk`, so they can make unreviewed content `current` from the UI (M4).
- Tracker text on the `sindri_*` tools is not fenced (M5).
- The `human` header is on the honour system while the UI becomes a full desk client (M6).
- S1 and the stacked PRs open routes outside the chain before API P1 (M7).
- `OpenItem` has no protection against a double launch (M8).
- Provider API keys have no secret-pointer rule (M9).

Nothing is CRITICAL.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 4 (H1, M1, M2, M3) |
| A02 Cryptographic Failures | findings: 1 (M9) |
| A03 Injection | findings: 1 (M5) |
| A04 Insecure Design | findings: 2 (M7, M8) |
| A05 Security Misconfiguration | findings: 3 (H2, L1, L5) |
| A06 Vulnerable & Outdated Components | findings: 1 (L6) |
| A07 Identification & Authentication Failures | findings: 1 (M6) |
| A08 Software & Data Integrity Failures | findings: 1 (M4) |
| A09 Security Logging & Monitoring Failures | findings: 1 (L3) |
| A10 SSRF | No findings. |

## OWASP Top 10 findings

### A01: Broken Access Control

- **H1. Session context reads bypass the egress switch and the "personal repos never see work context" rule** [HIGH]
  - Where:
    - API §3.7: context queries are "session callers repo-scoped, §3.1".
    - CTX §3: `context.mcp.enabled` covers "whether sessions may read intent text through MCP at all". It also says personal repos never see work context *because registration is per repo*.
    - CTX §8.5 offers the same query over CLI and HTTP.
    - CTX never defines repo scope for graph nodes or records. Linear issues, Slack hits and PRDs have no repo.
  - Attack (honest mistake): A session in a personal repo follows the agent-rules snippet, or simply tries the documented CLI, and runs `$SINDRI context query "<feature>"` through Bash. The CLI sends `session` with that repo's `X-Sindri-Repo`, or no header at all. Neither the MCP switch nor MCP registration applies. API §3.1's filter is defined for items only, and the context engine has no repo concept to filter by. Work intent text reaches a session whose provider and memory (for example Prism) are personal. It does so while `context.mcp.enabled: false` says egress is off.
  - Mitigation:
    - Gate on **caller class**, not transport. A `session` caller on any `/context/*` query is refused (`SND-CONTEXT-020`) unless `context.sessions.enabled` is true. Rename the switch so it is not MCP-specific.
    - Define context repo scope in CTX §8.5. A session's `X-Sindri-Repo` must be listed in `context.repos`, otherwise refuse. A missing header is refused, not answered unscoped.
    - Add the integration test to Phase 1: CLI as `session` from a non-`context.repos` repo, and with no header, gets the refusal.
    - Add a row to API §3.7 stating the rule.

- **M1. `grant-tokens` falls outside API's fail-closed rule for protected fields** [MEDIUM]
  - Where:
    - API §2.1: "any request carrying `grant` or `accept` is `protected`".
    - API §3.5: "answer with `grant` or `accept` is `protected`".
    - WF §6.2 and W7 have a separate variant, `ApplyCommand:answer-grant-tokens` (`--grant-tokens n`).
  - Attack (honest mistake): The implementer classifies by the API rule's field list. A body with `grantTokens` but no `grant` is classed as `answer`, which is `desk`. The app is `human` without a terminal, so it can raise a token budget, or a session's honest mistake can. If the engine runs the challenge check only for variants classed `protected`, nothing else stops it.
  - Mitigation: Write the rule as an allowlist in both specs: "`answer` is `desk` only when it carries nothing but `text`, `file`, `resume` or `reject`. Any other field is `protected`." List `grant`, `grantTokens` and `accept` explicitly. Add `answer-grant-tokens` to API §3.5 and the §4.1 table, and add a unit test for each field.

- **M2. Nonce and challenge issuance (and some engine commands) have no catalog class** [MEDIUM]
  - Where:
    - WF §5.4: `POST /workflow/instances/{a}/nonce` (`IssueNonce`). WF §7.1: `IssueChallenge`.
    - WF §6.3: `recipe register` (desk), `recipe revoke` and `ApplyCommand:command-<verb>`.
    - API §4.1's class table and §3.5 list none of these.
    - API ARCH-CQRS-001 says every POST is a command with a catalog row.
  - Attack (honest mistake): Under the fail-closed rule, an unclassified POST is either `protected`, which breaks every desk write from the app because the app needs a nonce first, or is left outside the chain as a "helper". The second option is an unaudited write path that inserts `workflow_nonces` rows. Either way the catalog test (API §3.3) and the UI's catalog-driven forms disagree with the engine.
  - Mitigation: Give each route a catalog row in WF §6.2:
    - `IssueNonce`: allowed for every caller that may run at least one desk or pause command on that instance. Audited, or explicitly flagged `NotAudited` with a reason.
    - `IssueChallenge`: `human` with a terminal only. The WF §4.2 architecture test already keeps it out of the UI and MCP.
    - `recipe revoke`: `protected`.
    - `command-<verb>`: `desk`.
    - Mirror all of these in API §4.1.

- **M3. Map review's "protected" does not fit the shared challenge model** [MEDIUM]
  - Where:
    - CTX §8.7 has an interactive review with dozens of accept and reject choices, plus `--undo`, rename, merge, move, add and delete subcommands.
    - API §3.7 and X9 say only `POST /context/map/review` is `protected`.
    - WF §4.2 says one typed challenge per action, bound to the subject.
    - CTX keeps `SND-CONTEXT-041` ("needs an interactive terminal"), although WF retired its own codes of that kind in favour of `SND-AUTH-002`.
  - Attack (honest mistake): One of two things happens:
    - The implementer issues a challenge per proposal, which makes review unusable and leads to bulk shortcuts.
    - The implementer issues none for the interactive form, so the subcommands (`--undo`, rename, merge) land as unclassified or `desk` POSTs that the UI or a misclassified CLI can drive.
  - Mitigation:
    - Define a **batch challenge**: the CLI collects the decisions locally, then asks for one challenge bound to the hash of the draft version and the decision set, then applies.
    - Class every review subcommand. Undo, rename, merge, move, add and delete are `protected` batch entries.
    - Retire `SND-CONTEXT-041` in favour of `SND-AUTH-002`, as WF did with `SND-WF-007` and `SND-RECIPE-004`.

### A02: Cryptographic Failures

- **M9. Provider API keys have no secret-pointer rule** [MEDIUM]
  - Where:
    - README X18: "Switching provider, an API key included, is a profile choice."
    - CTX §4 resolves secret pointers (`keychain:`, `env:`, `op:`, `file:`) in Sindri Host for *context sources*, and only from Phase 5.
    - No spec says how Judge's `IModelProvider` gets its key inside Sindri, or that the profile may hold only a pointer.
    - The profile can be a git repo (`$AW_STATE_DIR/profile` may link to a private profile repo).
  - Attack (honest mistake): The owner or an agent editing the profile pastes an API key into `provider: { apiKey: sk-… }`. It gets committed to the profile repo and copied into `profile-approved/<hash>/` snapshots. It may also show up in the `profile approve` diff preview and in `profile.checked` changed paths.
  - Mitigation:
    - One rule in API §6 or README X18: provider credentials are pointer-only, resolved by the same Host `Composition` resolver, and that resolver lands with the first model call (CTX Phase 4 or WF rung-2 gates), not Phase 5.
    - `profile validate` refuses a literal value at any `*key*`, `*token*` or `*secret*` path, and runs `Aw.Redaction` detectors over the profile.
    - The approve preview names the egress destination whenever the provider changes.

### A03: Injection

- **M5. Tracker and agent text on the `sindri_*` tools isn't fenced; the `context_*` tools on the same endpoint are** [MEDIUM]
  - Where:
    - API §3.6 "Agent text": scrubbed and sanitized (control, invisible, ANSI and OSC characters removed) and rendered as plain text in the UI. That protects the UI and the terminal, not the model.
    - CTX D15 and §7: untrusted text is wrapped in `<untrusted source=… author=…>` with one escaper, "otherwise a prompt injection in a ticket returns to a session in Sindri's own voice".
    - Both tool sets are served from `/sindri/mcp` (X8).
  - Attack (outside author): A Linear title or description, or a `FileRequest` text one session wrote, reaches another session through `sindri_item`, `sindri_queue` or `sindri_pending_decisions` as plain JSON with no fence. Instructions in it read as Sindri's own output. `OpenItem` (P10) has the launched session pull its context through exactly these tools.
  - Mitigation: Apply CTX's trust marking to every `sindri_*` tool response. Fields that come from trackers, agents or requests are returned fenced, with the same escaper, sitting in the shared Kernel or `Aw.Redaction` layer so both packages use one implementation. Add a P8 integration test that plants an injection-shaped title and asserts the fence.

### A04: Insecure Design

- **M7. Routes outside the chain before API P1 (S1 C2, and the stacked PRs)** [MEDIUM]
  - Where:
    - API §10 C2: S1's `ApproveProfile` trusts `confirmed` from any loopback caller. It is fixed only in P1.
    - X17: S2 (#167), S3 (#166, which maps `POST /sindri/context/query`), S4a (#169) and S1b (#168) "bring their own surfaces outside" the chain.
  - Attack (honest mistake): S1 merges first, and someone runs the C# Sindri against the owner's real profile home, for example while building S1b's UI or proving S3. An agent that hits a profile-validation failure "fixes" it with `curl -X POST …/profile/approve -d '{"hash":…,"confirmed":true}'`. The approval is recorded with no human. The same applies to any S3 POST route before it is rebased onto the chain.
  - Mitigation: Make one of these an explicit merge rule in X17 and API Phases:
    - (a) S1 does not merge with C2 open. A minimal TTY and caller check on `ApproveProfile` lands in S1.
    - (b) Until API P1 merges, the C# host refuses to start unless `AW_SINDRI_HOME` is a temp directory, using the same guard as `sindri-prove.sh`.

    Also record that #166 drops `POST /context/query` (CTX §8.5 is now `GET`) in its rebase, and that S1b stays a draft until the UI spec and API P1 both exist.

- **M8. `OpenItem` has no protection against a double launch** [MEDIUM]
  - Where:
    - API §2.1: no `Idempotency-Key` in v0, and "commands that create things aren't deduplicated".
    - API P10 `OpenItem`: `mode: open | attach`.
    - The app is now the main launch surface (X2, X15).
    - `StartInstance` is idempotent per (recipe, item, attempt) (WF §6.1). `OpenItem` is not.
  - Attack (honest mistake): A double-click, or a retry after a slow Warp start, opens two agent sessions on the same item and repo checkout. Both edit the same worktree, and both may take the item over.
  - Mitigation:
    - `OpenItem` is keyed `item.opened:<itemId>:v<itemVersion>`, so a repeat returns `SND-ITEM-409` with the first launch.
    - When the item already has a live session or takeover, `open` turns into `attach` unless the human picks "new session".
    - The UI spec debounces launch buttons.

### A05: Security Misconfiguration

- **H2. Codex and Cursor MCP registration isn't specified and cannot easily be per-repo** [HIGH]
  - Where:
    - API §8 and CTX §3: "Codex and Cursor get the equivalent entry". Registration is "never user-wide".
    - Doctor `context:mcp-scope` checks only `~/.claude.json`.
    - X15 and P10 make Codex and Cursor launch targets.
  - Attack (honest mistake): Codex's MCP config is user-level (`~/.codex/config.toml`, the same file `codex mcp add` writes for this toolkit). Registering "the equivalent entry" puts one `sindri` server with one fixed `X-Sindri-Repo: work-app` into **every** Codex session, personal repos included. The server trusts that header (X8) and returns work items and context there. For Cursor, a project-level `.cursor/mcp.json` lives in the repo's working tree and can be committed.
  - Mitigation: Specify registration per provider in API §8:
    - **Codex:** no persistent entry. `ILauncher` passes the server per launch (`codex -c 'mcp_servers.sindri.url=…' -c 'mcp_servers.sindri.http_headers.X-Sindri-Repo=…'` or the current equivalent), so only Sindri-launched Codex sessions see it.
    - **Cursor:** a project `.cursor/mcp.json` that `sindri mcp register` also adds to `.git/info/exclude`.
    - Doctor `context:mcp-scope` checks all three providers' user-level configs.
    - P8 proves at least one non-Claude client.

- **L1. `X-Sindri-Repo` isn't validated** [LOW]
  - Where: API §3.1 and §6 say only how the header scopes; nothing about unknown values.
  - Attack: A stale registration after a repo rename, or a typo, silently scopes the session to nothing. "Empty queue" looks like a healthy answer.
  - Mitigation: An unknown repo name (not in the approved profile) is a 400 with a new `SND-API-0xx` that names the fix (`sindri mcp register` again). Add a doctor check comparing the registrations with the profile.

- **L5. Non-Claude MCP clients may send `Origin`** [LOW]
  - Where: `/sindri/mcp` refuses any `Origin` (API §6 and §8). The P8 proof uses only `claude -p`.
  - Attack: Cursor is Electron. If its HTTP MCP client sends an `Origin`, every call gets 403 `SND-API-008`. It fails closed, but the cause is not obvious.
  - Mitigation: Prove each target client in P8 or P10. If one sends a fixed origin (`vscode-file://…`), allowlist only that exact value on `/sindri/mcp`, and only for its stateless MCP POSTs.

### A06: Vulnerable & Outdated Components

- **L6. Onyx is pinned with no update cadence** [LOW]
  - Where: CTX §8.8 pins an Onyx tag and doctor checks it. Nothing says when it is updated. Onyx is a large stack (Postgres, a search index, a web UI) holding work Slack and Linear data on loopback.
  - Mitigation: `docs/sindri/onyx.md` names a review cadence. Doctor warns when the pinned tag is older than N days, re-runs §8.8's manual checks on change, and checks the MCP C# SDK and Avalonia pins under the existing ≥14-day rule.

### A07: Identification & Authentication Failures

- **M6. The `human` header is on the honour system while the UI becomes a full desk client** [MEDIUM]
  - Where:
    - API §6: any local process sending `X-Sindri-Caller: human` is human. A forged `human` is out of scope (§11).
    - X2 widens `human`-without-terminal to starting workflows, answering parks, handback, cancel, `RunService` and `OpenItem`. `OpenItem` launches new agent sessions.
  - Attack (honest mistake, not hostile): The agents that **build and prove the UI** write exactly that header in the Avalonia `HttpClient` and in `sindri-prove.sh human …`. An agent debugging a UI form against the owner's running Sindri copies the header into a curl to "check the endpoint". That answers a park or cancels an instance as `human:ui`. Under the old read-only UI this pattern could reach only decisions. Now it reaches every desk action.
  - Mitigation: Bring §11's `human` token forward to the phase that first lets the UI write. That is the UI spec's first write phase, or API P3.
    - Mint `auth/human.token` (0600) on each `serve` start. The app and the CLI-at-a-terminal read it. A `human` header without it is downgraded to `session` and logged.
    - It is still honest-mistake grade, because a session could read the file. But copying a header is no longer enough, and the tables and tests do not change, as §6 already promises.
    - `sindri-prove.sh` keeps its temp-home guard.

### A08: Software & Data Integrity Failures

- **M4. `map import` and `map promote` are `desk`, so unreviewed content can become `current` from the UI** [MEDIUM]
  - Where:
    - API §3.7 and X9: `import` and `promote` are `desk`.
    - CTX §8.7: "`map import` writes `current` directly", and promote makes the draft current.
    - CTX D15: model-written content stays untrusted until a human accepts it. Human overrides are `trusted`.
  - Attack (honest mistake): An agent asked to "draft the product map" writes `map.yaml` outside the repo. The owner clicks **Import** in the app, or promotes a draft with model labels still in the queue. The imported nodes, aliases and links become the confirmed map, possibly as `trusted` human overrides, without the `protected` review. Later context answers cite them in Sindri's own voice.
  - Mitigation: State in CTX §8.7 that import and promote **never change trust**:
    - Imported entries carry `provenance: import` and stay `untrusted` unless the file is a hash-verified `map export` of a trusted map.
    - Promote leaves unreviewed model proposals untrusted.
    - Alternatively, class `import` as `protected` like `review --apply`.

### A09: Security Logging & Monitoring Failures

- **L3. Session reads of work context aren't attributed to a repo** [LOW]
  - Where: CTX §6.2 `query_log` records `client` (`cli`, `http`, `mcp`) but not the caller class or `X-Sindri-Repo`. API audits only commands.
  - Mitigation: Add `callerClass` and `repo` to `query_log`. `context sources status` reports session reads per repo, so H1-style leakage shows up.

### A10: SSRF

No findings. Onyx must be on loopback (`SND-CONTEXT-055`), permalinks are allowlisted by scheme and host, webhooks are cut (D7), `OpenItem` passes only an id and a profile path, and provider endpoints come from the approved profile.

## STRIDE findings

### S: Spoofing
- **M6.** `human` is asserted by header, and the UI's desk surface makes copying it consequential.
- **H2.** One fixed `X-Sindri-Repo` in a user-wide Codex entry claims the wrong repo for every session.
- **L4. Launched sessions should be marked by Sindri, not only by harness variables** [LOW]
  - Where: API §6 relies on `CLAUDECODE`, `CODEX_SANDBOX` and `CURSOR_AGENT` plus a parent-process check. `CODEX_SANDBOX` is unset when Codex runs without a sandbox.
  - Mitigation: `ILauncher` always sets `AW_SINDRI_CHILD=1` in the launched session's environment, whatever the target, so a Sindri-launched session is always classed `session`. The human still uses a separate terminal for protected verbs, as today.

### T: Tampering
- **M4.** Import and promote can make unreviewed content the confirmed map.

### R: Repudiation
- **L2. `human:ui` can't be told apart from any local caller using the human header** [LOW]
  - Where: API §2.1 composes the actor from `X-Sindri-Client`, which the caller asserts.
  - Mitigation: Accept for v0 (§11). Once M6's token exists, record `tokenVerified` in `command.handled`. Until then, log a warning when `human` arrives without `X-Sindri-Client`.

### I: Information Disclosure
- **H1.** Work context reaches sessions through the CLI and HTTP, outside the egress switch and repo scope.
- **H2.** A user-wide registration hands work context to personal-repo Codex sessions.
- **M5.** Unfenced tracker text reaches sessions through the `sindri_*` tools.
- **L8. README and P10 disagree on how a launched session is seeded** [LOW]
  - Where: README "How you interject" says the session is "seeded with the item's state, packet, evidence and context". API P10 says it gets only the item id and pulls its context through MCP, and "the launch config contains no item text".
  - Mitigation: Change the README to P10's pull model. The pull model is the safer one: fenced and repo-scoped, with no text in launch files.

### D: Denial of Service
- **M8.** Double launches create competing sessions on one item.
- **L7. Session takeover (`pause`) has no limit within a repo** [LOW]
  - Where: API §4.2 opens `pause` to sessions (X3). Handback is `desk`, and `takeoverIdle` notifies only after 2 h.
  - Mitigation: A session may take over only the item it is attached to (the hook knows it), or at most N instances per session. Takeovers made by a `session` appear at once as needs-you rows, not after 2 h.

### E: Elevation of Privilege
- **M1.** `grant-tokens` can be classed `desk`.
- **M2.** Unclassified engine POST routes.
- **M3.** Map review subcommands with no class or challenge shape.
- **M7.** `ApproveProfile` and the stacked PRs' routes before the chain exists.

## What the UI design spec must settle (security)

The reserved slot (`<date>-sindri-ui-design.md`) must decide:
1. Where the app's `human` credential comes from (M6).
2. Debouncing and "already open, attach?" for `OpenItem` and every create-type button (M8).
3. A confirmation step for irreversible desk actions with no undo (cancel instance, close item, stop process, import map), because the app has no terminal and no passkeys.
4. The per-target launch and registration matrix: Warp plus Claude Code, Codex or Cursor, how each gets the MCP endpoint and repo header, and each one's failure states (H2, L4, L5).
5. That `cli-only` rows render the exact content-bound command (hash or challenge subject) to copy, and never a button.
6. That untrusted fields are visibly marked in item and decision views, matching M5's fence.

## Verdict
- **Critical findings:** 0
- **High findings:** 2 (H1, H2)
- **Medium findings:** 9 (M1–M9)
- **Low findings:** 8 (L1–L8)
- **Recommendation:** FIX-CRITICAL-FIRST. Resolve H1 and H2 in the API, CTX and README text before `/specToProvenPR` starts API P8 and CTX Phase 1's MCP and CLI query surfaces. Fold M1–M4 into the catalog and class tables before API P1's contract unit.

verdict: NEEDS_WORK
