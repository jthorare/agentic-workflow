# Threat Model: Sindri application design epic (PR #170), pass 2

**Mode:** plan
**Source:** `plans/2026-10-11-sindri-app-epic/plan.md`, which indexes `docs/sindri/README.md` (X1–X34), `2026-10-10-sindri-api-surface-design.md` (API), `2026-10-10-sindri-workflow-engine-design.md` (WF) and `2026-10-10-sindri-context-retrieval-design.md` (CTX). Read in the worktree on branch `docs/sindri-app-epic` at `1c328de9`. Background: dashboard plan decision 15 (`docs/superpowers/plans/2026-10-09-sindri-plan-6-dashboard.md`), which defines the Warp launcher that `ILauncher` inherits.
**Reviewed:** 2026-10-11
**Pass:** 2. Pass 1 is `pass1/security-review.md` (H1–H2, M1–M9, L1–L8) and `pass1/consolidated-review.md` (E1–E29). README X19–X34 record the fixes.
**Threat model:** gates guard against honest mistakes by agents, not hostile agents. This is a single-owner personal toolkit. Exploit-style concerns are judged in that frame. Two outside parties stay in scope: ticket and Slack authors, who can inject text, and other local software. One honest-mistake pattern gets weight here: agents routing around a guard that refused them by copying a documented recipe. They don't do it out of malice; they copy what the docs show.

## Summary

Pass 1's two HIGH findings are fixed in substance:
- `context.sessions.enabled` now gates every session context read on any transport, and repo scope is defined (X25, CTX §3).
- Codex and Cursor are never registered. `ILauncher` passes the endpoint at launch (X22, API §8).

The fail-closed `IClassifiedCommand` rule now names `grantTokens`. Map import and promote are `protected`, `sindri_*` text is fenced, and double opens are refused. Most of what's left over is stale text and partial fixes.

The new surfaces bring one HIGH and several MEDIUM findings:
- **N1 (HIGH).** The `step-report` credential is specified only as "a run-scoped token `ILauncher` hands the session". No spec says how it reaches the session, where it is stored, what header carries it, or when it dies. The run id it sits beside (`--run 1.1`) is a public counter printed by `status`. The launcher every spec inherits writes **one fixed, persistent** Warp config file (`~/.warp/launch_configurations/sindri-attach.yaml`). The obvious implementation puts the token there, where concurrent launches overwrite each other's token. That breaks the requirement "only the session launched for that run may report it", and the factory line exists to run concurrent launches.
- **N2–N8 (MEDIUM).**
  - N2: the per-start `human` token has no phase, header row, error code or prover support. It also contradicts "v0 has no tokens" in the same spec.
  - N3: the documented prover recipe gets past the protected tier, and only a wrapper script guards it.
  - N4: the agent runtime pushes the injection plan at launch, unfenced and persisted, and leaves the unattended permission mode unspecified.
  - N5: token use is self-reported, so the budget gate rests on the reporter.
  - N6: stale CTX and API text still tells implementers to register Codex and Cursor and to gate on `context.mcp.enabled`.
  - N7: the `issue` carve-outs have no row in the authorization table.
  - N8: there are no challenge subjects for the non-workflow protected actions, and map review's batch shape is still undefined.

Nothing is CRITICAL.

## Pass-1 findings: status

| Pass 1 (consolidated) | Finding | Status | Evidence / what's left |
|---|---|---|---|
| H1 (E7) | Context reads bypass the egress switch and repo scope | **Resolved** | CTX §3 L106–111: `context.sessions.enabled` covers MCP, CLI and HTTP; repo scope covers files, implemented nodes and `context.repoTeams`; anything else answers `SND-CONTEXT-003`. Left over: stale wording in CTX §3 L83–84, CTX §8.6 `SND-CONTEXT-020` ("MCP is disabled") and API §8 L759 (`context.mcp.enabled`). The missing-header case for context is not stated. See N6 |
| H2 (E6) | Codex/Cursor registration leaks or gets committed | **Resolved** (owner decision X22) | API §8 L743–748: never registered, passed at launch, no Sindri MCP until P10 proves the form, doctor checks all three configs. Left over: CTX §3 L101–103 still says "Codex and Cursor get the equivalent entry" and that doctor checks only `~/.claude.json`. See N6 |
| M1 (E15) | `grant-tokens` could be classed `desk` | **Resolved** | API §2.1 L72–73 and WF §6.2 L535 name `grantTokens` / `answer-grant-tokens`. Left over: API §4.1's `protected` row and §3.5 still list only grant and accept (L-a) |
| M2 (E8) | Nonce and challenge POSTs unclassified | **Partly** | API §2.5 L248 lists both as `ARCH-CQRS-001` carve-outs with catalog rows of kind `issue`. `issue` appears in neither §4.1 nor §4.2, and it isn't said whether the routes run inside the chain. See N7. `recipe revoke` (protected) and `command-<verb>` (desk) are classed in WF §6.2 |
| M3 (E16) | Map review doesn't fit one challenge per action | **Partly** | `SND-CONTEXT-041` is retired, and review, import and promote are `protected` with the shared challenge (CTX §8.6 L464, API §3.7). There is still no batch challenge or subject binding, and the review subcommands (`--undo`, rename, merge, move, add, delete) have no class. See N8 |
| M4 (E17) | Import/promote make unreviewed content current from the UI | **Resolved** | API §3.7 L453: both `protected`. CTX Phase 4 L1031: trust is recomputed from provenance and never read from the file. Left over: a contradiction about an HTTP import path (L-f) |
| M5 (E18) | `sindri_*` text not fenced | **Resolved in text** | API §8 L761. Left over: no P8 test plants an injection in a `sindri_*` result (L-g) |
| M6 (E19) | Trusted `human` header guards every desk action | **Partly** | API §6 L696–702 defines `auth/human.token`. No phase owns it. See N2 |
| M7 (E20) | Routes outside the chain before API P1 | **Partly → LOW** | X17/X19 put the stacked PRs into waves after API P1. S1 still merges in wave 0 with C2 open until P1. The impact is low because nothing installs or points at the C# build before the switch (L-h) |
| M8 (E21) | `OpenItem` double launch | **Resolved** | API P10 L1130: a second open while one is live is refused. Left over: the key shape (L-b) |
| M9 (E22) | Provider keys have no pointer rule | **Partly** | X18 says "pointers only, never values". Nothing enforces it in `profile validate`, and the resolver lands only in CTX Phase 5 (L-e) |
| L1 | `X-Sindri-Repo` not validated | **Not addressed** | Still open (L1 below) |
| L2 | `human:ui` can't be told apart from forged callers | **Not addressed** | Folded into N2 (record whether the token was verified; audit the downgrade) |
| L3 | Session context reads not attributed | **Not addressed** | CTX §6.2 L309 `query_log` still has only `client` (L3 below) |
| L4 | Sindri should mark launched sessions | **Partly** | `AW_SINDRI_CHILD` is in the CLI's variable list (API §6 L676), but no phase makes `ILauncher` set it (L-c) |
| L5 | Cursor may send `Origin` | **Partly** | P10 records each target's form, but `/sindri/mcp` still refuses every `Origin` and nothing plans for an Electron client (L5 below) |
| L6 | Onyx update cadence | **Not addressed** | Still open (L6 below) |
| L7 | Session takeover has no limit | **Not addressed** | More relevant now that agent-runtime sessions exist (L7 below) |
| L8 | README vs P10 seeding | **Resolved** | README L66 now describes the pull model |

E2 (no agent runtime) is resolved by the owner decision X21 (WF §6.5, P8). Its new surface is reviewed below (N1, N4, N5).

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 4 (N1, N7, L-a, L7) |
| A02 Cryptographic Failures | findings: 1 (L-e) |
| A03 Injection | findings: 3 (N4, L-d, L-g) |
| A04 Insecure Design | findings: 3 (N5, L-b, L-h) |
| A05 Security Misconfiguration | findings: 3 (N6, L1, L5) |
| A06 Vulnerable & Outdated Components | findings: 1 (L6) |
| A07 Identification & Authentication Failures | findings: 3 (N2, N3, L-c) |
| A08 Software & Data Integrity Failures | findings: 2 (N8, L-f) |
| A09 Security Logging & Monitoring Failures | findings: 1 (L3) |
| A10 SSRF | No findings. |

## OWASP Top 10 findings

### A01: Broken Access Control

- **N1. The `step-report` credential has no transport, storage, header or lifetime, and the inherited launcher shares one persistent config file** [HIGH]
  - Where:
    - API §4.1 L554 and §4.2 L568: `step-report` is allowed **only** for the session the agent runtime launched for that run, "proven by its run-scoped token (§6)".
    - API §6 L698–699 adds only one sentence: "The agent runtime gets a separate run-scoped token per launched session".
    - WF §6.5 L650–653: "a run-scoped token `ILauncher` hands the session".
    - WF §5.1 L313 and §5.4 L385: the run id is `{entry}.{n}` (`1.1`), passed as `--run`. `status` prints it (§6.4 L602), and `GET /workflow/instances` returns it (§6.3 L571) to any repo-scoped session.
    - API P10 L1130: `ILauncher` is "a Warp launch configuration as in the dashboard plan's decision 15". That plan (L100, L7634) writes **one fixed file**, `~/.warp/launch_configurations/sindri-attach.yaml`, then opens `warp://launch/sindri-attach.yaml`.
    - WF P8 L1291 tests only "the same `complete` from another session exits 2". That test passes whether the token travels safely or not.
  - Attack (honest mistakes and races):
    1. *Shared file race.* The agent runtime launches Steps for two items within a second. Launch B rewrites `sindri-attach.yaml` before Warp reads launch A's copy, so both tabs start from B's config: B's item, repo and run token. The session for A reports with B's token. The server checks the token against B's open run, finds it valid, and records A's work as B's Step. The gates still decide, but on the wrong evidence for the wrong item. Human `OpenItem` from the app races the same file.
    2. *Token at rest.* The token sits in a persistent YAML file, and on Codex's `-c` command line it is visible in `ps`. A later session, or the human's own Claude Code session reading the file "to see how Sindri launches", finds a valid token. "Only the launched session" then holds only by luck.
    3. *Run id taken for the credential.* Without a header defined in §6, an implementer reuses `--run 1.1` as the proof, since WF calls it "the run token" in five places. Any repo session that ran `workflow status` and saw the `next` line can then report. That is the exact case the class exists to stop.
    4. *Lifetime.* A takeover pushes the frame, and the pop resumes it "with the same run token" (WF §5.1 L321, P5 L1235). Nothing says whether the agent's token stays valid while the human holds the instance, after handback, after `session.ended`, or after the run's `timeout` fires. A stale agent process can report into a frame the human has since steered.
  - Mitigation:
    - Rename to keep the two apart: `run` (the public `{entry}.{n}` id) and `reportToken` (the secret).
    - Define in API §6:
      - `reportToken` is 128-bit random, minted per launch, and stored server-side hashed with (instance, entry, run, sessionId).
      - It is sent as `X-Sindri-Report-Token` (add the header row).
      - It is accepted only with caller `session`, and only on `CompleteStep` for that run.
      - It is never returned by any query, never logged, and kept out of `command.handled` (record a hash prefix).
    - Hand it over without persisting it. `ILauncher` puts it in the launched process's **environment** (`AW_SINDRI_REPORT_TOKEN`) or in a per-launch 0600 file under `$AW_SINDRI_HOME/runs/<launchId>/`, referenced by path. Never in argv, in a provider config or in the Warp YAML.
    - Replace decision 15's fixed filename with a **per-launch** name (`sindri-<launchId>.yaml`, 0600), deleted after Warp reads it (or on `session.launched`). P10's signal grows to cover this: "the launch config contains no item text **and no token**, and two concurrent launches each open their own item".
    - Lifetime: the token dies on report, `session.ended`, run timeout, cancel, or a `spec-conflict` replace. While the instance is `taken-over`, reports are refused (`SND-WF-003`). After handback, the same token is valid again only if the same session is still live.
    - WF P8 signal: add "two concurrent agent launches report to their own runs" and "a report with the public run id but no token gets `SND-AUTH-002`".

- **N7. The `issue` carve-outs are missing from the authorization model they bypass** [MEDIUM]
  - Where: API §2.5 L248 gives `POST /workflow/instances/{a}/nonce` and `POST /workflow/challenges` "catalog rows of kind `issue`", "audited by `command.handled`". The class table (§4.1) and the authorization table (§4.2) have no `issue` row. `AuthorizingService` and `AuditingService` are decorators that `AddCommand` registers for `ICommand`s only (§2.1 L121–131), and these routes are not commands.
  - Attack (honest mistake): The implementer has three choices: (a) leave the routes outside the chain, which means no audit and no authorization; (b) force them into the chain as some existing class; or (c) hand-roll a check per route. Under (a), a session can mint nonces for instances in **other** repos, because nothing applies §3.1's repo scope to `{a}`, and every write path then disagrees with the catalog test (§3.3). The rule for the nonce route is also loose. "Any caller whose class may run a command on that instance" covers every session for every instance through `pause`. It should be "may run the command the nonce is for".
  - Mitigation:
    - Add `issue` to §4.1 with its own §4.2 row: nonce = per target action (the caller must be allowed the named action id, sent in the body); challenge = `human` + terminal (+ token, N2).
    - Run both routes through `AuditingService` and `AuthorizingService` with a registration helper (`AddIssuer`) that the chain-order test also covers. They don't take `CommandGate`.
    - Apply §3.1's repo scope to `{a}` (`SND-ITEM-404`).
    - Add one integration test per route: a session from another repo, and a session asking for a nonce for `handback`, are both refused.

- **L-a. The `protected` and `step-report` lists disagree across tables** [LOW]
  - Where:
    - API §4.1 L559 lists `ApproveProfile`, `answer-grant`, `answer-accept` and `recipe approve`. It omits `answer-grant-tokens`, `recipe revoke` and the context `import`/`promote`/`review` rows (§3.7).
    - §3.5 L432 still says "answer with `grant` or `accept`".
    - WF §6.2 L534 and §6.3 L563 class `CompleteStep` / `workflow complete` as `desk` only, with no `step-report` variant.
  - Mitigation: Make §4.1 the full list (or say "see the catalog"). Add the `step-report` variant to WF §6.2 and §6.3. The catalog unit test already enforces the real rows, so this is about the text.

- **L7. A session may pause any instance in its repo, including other agents' unattended runs** [LOW, carried from pass 1]
  - Where: API §4.2 L569 (`pause` open to sessions); WF §6.5 (unattended agent runs).
  - Attack: The attach hook misfires in agent session A, or an agent "pauses the stuck one". Session A takes over agent run B's instance, B's timers stop, and nothing surfaces until `takeoverIdle` (2 h).
  - Mitigation: A session may take over only the instance it was launched for or is attached to (`ItemView.session`). Any other takeover by a `session` becomes a needs-you row at once.

### A02: Cryptographic Failures

- **L-e. "Provider keys are pointers only" is stated but not enforced, and the resolver lands late** [LOW, M9 partly]
  - Where: README X18 L118 states the rule. CTX §4 L148 and Phase 5 L1062: Sindri Host resolves pointers starting at CTX Phase 5. Model calls arrive earlier: CTX Phase 4 labeling, engine rung-2 gates, Judge (#156) and engine P8 usage reads.
  - Mitigation: `profile validate` refuses a literal at any `*key*`, `*token*` or `*secret*` path, and runs `Aw.Redaction` detectors over the profile. The pointer resolver lands with the first model call through the Host's `IModelPort` adapter, not with Onyx.

### A03: Injection

- **N4. The agent runtime pushes the injection plan at launch, and leaves the unattended permission mode and checkout unspecified** [MEDIUM]
  - Where:
    - WF §6.5 L646–648: `ILauncher` is called "with the run token, the target …, the repo and the Step's injection plan".
    - API P10 L1130: `OpenItem` "passes only the item id and the repo path …; the session pulls its context through the MCP endpoint", and "the launch config contains no item text".
    - CTX D15 and §7: untrusted text goes out only fenced.
    - README L61: containing an adversarial agent is "the sandbox's job". No spec names that sandbox, or the permission flags, for a session launched unattended.
  - Attack (outside author / honest mistake):
    - The injection plan holds ticket and Slack text (Phase 10 `input.context.retrieve`). Passed as the session's opening prompt, it lands in argv (`claude "<prompt>"`, `codex … "<prompt>"`) and in the persistent Warp YAML. It is also unfenced: it arrives as the user turn, so an injected "ignore the Step, push to main" reads as the owner's instruction. This contradicts the pull model P10 just adopted.
    - Separately, an unattended session either blocks on its first permission prompt (the run times out) or is launched with broad permissions in the owner's real checkout. Two concurrent Steps on one repo then edit one working tree.
  - Mitigation:
    - The agent runtime uses the same pull model as `OpenItem`. The launch prompt is a fixed template naming only the address and run ("You are running `<address>` run `<run>`; fetch your task with `sindri workflow status <address> --json` and the `sindri_*` tools"). The plan is served fenced, through a query.
    - Specify per target in WF §6.5 (or the UI design spec's launch matrix):
      - the permission mode and allowed-tools set for unattended runs, never bypass mode (`.claude/settings.json` already disables it);
      - the sandbox, or "none in v0, ring 0 only";
      - a **per-run git worktree**, so concurrent Steps never share a tree.

- **L-d. `ILauncher` builds shell text from profile paths and ids** [LOW]
  - Where: API P10 (a Warp launch config runs a command in the repo path from the profile); dashboard plan `warpYaml(cwd, command)`.
  - Attack (honest mistake): A repo path with a space, a `'` or a `$(` in the profile breaks the launch, or runs part of the path as a command.
  - Mitigation: Build argv as an array, quote it for the YAML with one escaper, and validate typed ids before use. Add a unit test with a path containing `'`, `$(…)` and a newline.

- **L-g. `sindri_*` fencing has no test** [LOW, M5 partly]
  - Where: API §8 L761 states the rule. P8's signal L1129 doesn't test it.
  - Mitigation: Add to P8 an injection-shaped item title, `FileRequest` text and decision question, and assert the `trust` field and the fence in `sindri_item`, `sindri_queue` and `sindri_pending_decisions`. Share the escaper with CTX (Kernel or `Aw.Redaction`).

### A04: Insecure Design

- **N5. Token use is self-reported in the step report, so the budget gate rests on the reporter** [MEDIUM]
  - Where: WF §6.5 L654–656: "The report carries token usage, read by the target's provider adapter … from the session's own usage output. Budgets count it; an over-budget report parks with `budget`."
  - Attack (honest mistake): The usage arrives **in the report body**, which the session writes. A session that omits the field, reports only its last turn, or reports before a long final turn under-counts. `gate.budget.*` passes, and the grant-tokens protection (a `protected` action) is quietly sidestepped. Codex's and Cursor's usage formats differ, and the session is the party least likely to get them right.
  - Mitigation: Sindri reads usage server-side, from the provider's transcript or usage API for the recorded `sessionId` (the scorer already parses all three providers' transcripts). It records `usage.source: transcript | reported`. A report whose reported usage is missing or lower than the transcript's uses the transcript's number. If no reading can be made, the Step parks with `budget`, reason `usage unknown`, and does not count zero.

- **L-b. `OpenItem`'s key is "per item and session", which doesn't dedupe** [LOW, M8 partly]
  - Where: API P10 L1130: "a second open of the same item while one is live is refused (fact key per item and session)".
  - Attack: Two opens fired together (a double-click, or the app and the CLI) each get a fresh session id. Their keys differ, and both launch before either `session.launched` is projected.
  - Mitigation: Key the launch on the item's live slot: `session.launched:<itemId>:s<openCount+1>`, with `openCount` read inside the gate. Or decide on `ItemView.session` inside `CommandGate`, with `ReadsViews` set. Add a concurrency test like the `Decide` one.

- **L-h. S1 merges in wave 0 with C2 open** [LOW, M7 partly]
  - Where: README build order L83 (wave 0 = S1), API §10 C2 (fixed in P1, wave 1).
  - Impact is low, because nothing installs, registers or points at the C# build before the switch, and its home is separate from the TS Sindri's.
  - Mitigation: One line in X17: "Between S1 and API P1, run the C# host only through `sindri-prove.sh` homes." Optionally, make `serve` refuse a non-temp `AW_SINDRI_HOME` until the catalog reports P1.

### A05: Security Misconfiguration

- **N6. Stale text still tells implementers to register Codex and Cursor, and to gate on MCP only** [MEDIUM]
  - Where:
    - CTX §3 L98–103: "(Codex and Cursor get the equivalent entry)" and "Doctor check `context:mcp-scope` warns when a user-scope `sindri` server exists in `~/.claude.json`". This contradicts X22 / API §8 L743–748.
    - CTX §3 L83–84: `context.sessions.enabled` is "whether sessions may read intent text **through MCP at all**". This contradicts L106–107 in the same section.
    - CTX §8.6 L461: `SND-CONTEXT-020` "MCP is disabled". API §8 L759: the `context_*` tools are "off while `context.mcp.enabled` is false", a key that no longer exists.
    - CTX §3 L108–111 defines repo scope but not the **missing `X-Sindri-Repo`** case. API §3.1 L300 answers "only items that have no repo"; the context equivalent is undefined.
  - Attack (honest mistake): The context-stage implementer works from CTX, which the plan says wins for its part. They write the Codex entry into `~/.codex/config.toml`, and every personal Codex session now carries one work repo header: pass 1's H2, brought back. Or they gate only the MCP tools on the flag, which brings back H1 over the CLI.
  - Mitigation:
    - Replace CTX §3 L98–103 with a pointer to API §8. Fix L83–84, `SND-CONTEXT-020`'s text and API §8 L759 to say `context.sessions.enabled`, any transport.
    - Add: "A `session` context read without `X-Sindri-Repo` answers `SND-CONTEXT-020`-style refusal (nothing is reachable)."
    - Add a Phase 1 test: a CLI session with no header, and one from a repo not in the profile, are both refused.

- **L1. An unknown `X-Sindri-Repo` silently scopes to nothing** [LOW, carried]
  - Mitigation: A name not in the approved profile gets a 400 with a code whose fix is "re-register / relaunch". Doctor compares the Claude local registrations with the profile.

- **L5. `/sindri/mcp` refuses every `Origin`; an Electron client may send one** [LOW, carried, partly]
  - Mitigation: P10 records each target's MCP request headers. If one sends a fixed origin, allowlist that exact value on `/sindri/mcp` only.

### A06: Vulnerable & Outdated Components

- **L6. Onyx is pinned with no update cadence** [LOW, carried]
  - Mitigation: `docs/sindri/onyx.md` names a review cadence. Doctor warns when the pinned tag is more than N days old, and §8.8's manual checks re-run on each bump.

### A07: Identification & Authentication Failures

- **N2. The per-start `human` token has no phase, header row, code, prover support or audit, and the same spec still says v0 has no tokens** [MEDIUM, M6/L2 partly]
  - Where:
    - API §6 L696–702 defines `auth/human.token`, `X-Sindri-Token`, and the rule that a `human` header without the token "counts as `session`". It lands "before the app's first command (its first write phase in the UI design spec)", a spec not yet written.
    - API §1 D4 L55 and §6 L657–658 still say "There are **no tokens**". The §6 header table L665–670 has no `X-Sindri-Token` row.
    - No API phase (P1–P11) lists the token. §9 reserves `SND-AUTH-001` only for "the token phase (§11)".
    - §12 L931–932: `sindri-prove.sh curl` adds the human headers but no token.
  - Attack (honest mistake):
    - Since no phase owns the token, the UI's write phase ships against API P3/P4 with the header alone. That is pass 1's M6.
    - When the token does land, every `sindri-prove.sh curl` human proof silently becomes `session` and gets 403. The quickest "fix" an agent reaches for is to read the token file into its curl.
    - The silent downgrade also hides the cause. The refusal says "a human decision", not "token missing".
  - Mitigation:
    - Give the token an API phase: P1 (the chain) or a new P3a before the UI's first write. Add the header row, and fix D4 and §6's opening line.
    - When `human` arrives without a valid token, answer with `SND-AUTH-001` ("token missing or stale; restart the app") instead of downgrading silently, and audit it as `caller: human-unverified`. Record `tokenVerified` in `command.handled` (pass 1 L2).
    - `sindri-prove.sh curl` reads the token from its guarded temp home.
    - The CLI reads the token only after it has classified itself `human`, never in session mode.

- **N3. The prover recipe is a working route past the protected tier, guarded only by a wrapper script** [MEDIUM]
  - Where:
    - API §12 L927–939 and WF Phases L1074–1076: `sindri-prove.sh human <verb>` runs the CLI "under `script -q /dev/null` with the agent variables unset", and protected confirmations are piped (`printf '<first 6>\n' | … profile approve`). The temp-home refusal lives in the wrapper.
    - API §6 L681 and WF §6.2 L522: "a parent process that is an agent harness also counts". Its depth isn't specified. If it covers the full ancestry, it classes the prover itself as `session`, so `human` steps can't work. If it covers only the immediate parent, `script` defeats it.
    - The server accepts `X-Sindri-Client: prover` (`human:prover`) on any home.
  - Attack (honest mistake, copied recipe): An agent is asked to "get the profile approved" or to "grant the tokens so the run continues" against the owner's running Sindri. The wrapper refuses, because it isn't a temp home. The agent reproduces what the wrapper does, which every spec documents: `env -u CLAUDECODE script -q /dev/null "$SINDRI" workflow answer … --grant-tokens 100k`, with the challenge text piped in. Once N2 lands, the CLI reads the owner's `auth/human.token`. The result is a `protected` action recorded as `human:cli`, with no human at the keyboard. No hostility is needed: it is the documented recipe with the guard removed.
  - Mitigation (keeps the honest-mistake grade; makes copying the recipe fail):
    - The parent-process check walks the **full ancestry**, so `script` and `env -u` inside a harness still read as `session`.
    - The prover's way past the check is an explicit `AW_SINDRI_PROVER=1`, honoured by the CLI only when `control.json` carries `prover: true`. `serve` sets that only when its home is under `$TMPDIR` or a proof folder. The real `serve` refuses `X-Sindri-Client: prover` with `SND-AUTH-002`.
    - The protected-tier refusal names the fix: "a human must type this in their own terminal".
    - Add one integration test: the copied recipe against a non-temp home is refused.

- **L-c. Nothing makes `ILauncher` set `AW_SINDRI_CHILD`** [LOW, L4 partly]
  - Where: API §6 L676 lists the variable. P10 and WF §6.5 don't say `ILauncher` sets it. Agent-runtime tabs are real TTYs, and Codex in full-access mode sets no `CODEX_SANDBOX`.
  - Mitigation: `ILauncher` always sets `AW_SINDRI_CHILD=1` (and the report token, N1) in the launched environment, with a P10 unit test per target.

### A08: Software & Data Integrity Failures

- **N8. Challenge subjects for profile and context protected actions, and map review's batch shape, are undefined** [MEDIUM, M3 partly]
  - Where:
    - WF §4.2 L260–263: subjects are "the recipe hash and version, or the instance version". The route is `POST /workflow/challenges`.
    - API §3.2 L337–339: `ApproveProfile` adopts the challenge. CTX §8.6 L464 and API §3.7: map review, import and promote adopt it too.
    - CTX §8.7 L483–502: the interactive review writes "one `map_override` row" per action, and quitting keeps the decisions made so far. `--undo`, rename, merge, move, add and delete have no class.
  - Attack (honest mistake): Two failure modes, as in pass 1. The implementer issues one challenge per proposal: 46 typed confirmations, after which the owner asks for a bulk shortcut that skips the challenge. Or the subcommands land as unclassed or `desk` POSTs, which the fail-closed rule only half covers, because they are separate routes. An import bound to nothing can be confirmed with one file and then applied with another.
  - Mitigation:
    - List the subject per protected action in WF §4.2: profile = live hash; import = SHA-256 of the file; promote = draft version; review = hash of (draft version, decision set).
    - The interactive review collects decisions locally and confirms once per batch (on quit or `--apply`). Every subcommand is a `protected` batch entry, written into `map_override` only when the batch is confirmed.

- **L-f. The HTTP import path contradicts its `protected` class** [LOW]
  - Where: CTX Phase 4 L1031: "the HTTP route imports them as `model` provenance (untrusted, unconfirmed)". This implies an import open to non-terminal callers, while API §3.7 L453 makes `POST /context/map/import` `protected`.
  - Mitigation: Pick one. Either import is always `protected` (the app shows the CLI to copy), or a separate `desk` "import as draft" route never touches `current`, and its rows enter the review queue.

### A09: Security Logging & Monitoring Failures

- **L3. Session context reads still aren't attributed to a caller or repo** [LOW, carried]
  - Where: CTX §6.2 L309 `query_log` (`client` only); CTX §8.5 L442 counts only MCP queries.
  - Mitigation: Add `callerClass` and `repo` to `query_log`. `context sources status` shows session reads per repo and transport, so a scope leak shows up.

### A10: SSRF

No findings:
- The MCP URL passed at launch is the loopback URL from `control.json`.
- Onyx must be on loopback (`SND-CONTEXT-055`), and permalinks are allowlisted.
- Webhooks are cut.
- `OpenItem` passes an id and a profile path.
- Provider endpoints come from the approved profile.

## STRIDE findings

### S: Spoofing
- **N1.** Any holder of the shared launch file, or of the public run id, can report as the run's session.
- **N2.** `human` remains a header until a phase owns the token, and the downgrade is silent.
- **N3.** The prover recipe makes an agent read as a human at a terminal.
- **L-c.** Launched sessions are not marked by Sindri.

### T: Tampering
- **N1.** A launch race binds one session's work to another item's run.
- **N8.** Protected confirmations are not bound to the content they approve.
- **L-f.** Import has two classes.

### R: Repudiation
- **N2 (L2).** `command.handled` can't show whether a `human` row was token-verified.
- **N1.** `step-report` rows need the launch id and a token hash prefix, so "which session reported" can be answered later.
- **N5.** Usage doesn't record whether it was read from the transcript or reported.

### I: Information Disclosure
- **N4.** Untrusted ticket text in argv and a persistent launch file.
- **N6.** Stale CTX text re-opens user-wide Codex registration and MCP-only gating.
- **L3.** Unattributed session reads.

### D: Denial of Service
- **L7.** A session can pause other agents' runs.
- **L-b.** Double launches still race.
- **N4.** Unattended sessions blocking on permission prompts burn the Step timeout.

### E: Elevation of Privilege
- **N3.** The copied prover recipe reaches the protected tier.
- **N7.** The `issue` routes sit outside the authorization table.
- **N5.** Self-reported usage gets past the budget gate, which a `protected` grant would otherwise be needed to raise.

## What the UI design spec must settle (security, updated)

1. The app's `human` credential (N2): read `auth/human.token` from the home; on `SND-AUTH-001`, show "restart the app".
2. The launch matrix per target (Warp plus Claude Code, Codex or Cursor), for both the human handoff and the agent runtime:
   - per-launch config files with no item text and no token (N1, N4);
   - how the MCP endpoint and repo header reach each target (X22);
   - `AW_SINDRI_CHILD` (L-c);
   - the permission mode for unattended runs (N4);
   - each target's failure states.
3. Debounced create-type buttons, and "already open: attach?" driven by `ItemView.session` (L-b).
4. A confirmation step for irreversible desk actions with no undo: cancel instance, close item, stop process, cancel job.
5. `cli-only` rows render the content-bound command (hash or subject) to copy, never a button. Map import, promote and review are `cli-only` (N8, L-f).
6. Untrusted fields are visibly marked in item, decision and needs-you views, matching the `trust` fence (L-g).

## Verdict
- **Critical findings:** 0
- **High findings:** 1 (N1)
- **Medium findings:** 7 (N2, N3, N4, N5, N6, N7, N8)
- **Low findings:** 13 (L-a, L-b, L-c, L-d, L-e, L-f, L-g, L-h, L1, L3, L5, L6, L7)
- **Pass-1 status:**
  - resolved: H1, H2, M1, M4, M5 (in text), M8, L8;
  - partly: M2, M3, M6, M7, M9, L4, L5;
  - not addressed: L1, L2, L3, L6, L7.
- **Recommendation:** FIX-CRITICAL-FIRST.
  - Fix N1 in API §6, WF §6.5 and P10 before API P10's contract unit; the launcher's file and token handling is set there.
  - Fix N2 and N3 before the UI's first write phase.
  - Fix N6 (a text sweep) before context S-1A / S-1M.
  - Fold N7 and N8 into API P1's and engine P2's contract units.
  - Fix N4 and N5 before engine P8.

verdict: NEEDS_WORK
