# Sindri setup once: the owner sets up Sindri and Onyx, and nothing else is typed (spec amendment)

**Status:** approved rev 4 (owner, 2026-10-20: approved with the recommended answer to each question in §14); rev 5
proposed, awaiting the owner's read. It is a docs-only amendment; the code lands in the phases of §13. Lanes: api (this
spec), engine, context, ui.

**Rev 5 (2026-10-20), owner Q1 of the F3 Setup spec:** setup under Settings, one Home line; the API is unchanged. The
six-step checklist is its own page under **Settings** (route `setup`, reached from Settings and by `G U`), and Home shows
one line, **"Finish setting up Sindri"**, while any step is open (it links to the checklist and is gone when every step is
`done` or `skipped`). That replaces the three-row Setup card on Home (§1, §2, §3 SO-D1, §5.7, §6, §7.2, §7.3, §7.5, §9, §11,
§13, §13.1). The steps, their states, the `why` sentences and the actions are unchanged. **The Home fold:** "three rows
filled by priority" had a role only on a card that had to fit above the fold; the checklist is a page, so it draws all six
steps in the spec's step order (repo, profile, recipes, mcp, map, onyx) with each state shown, and the priority fill and
"and N more" are dropped (R3-2's concern, a failed or needs-you step hidden behind "and N more", cannot arise: nothing is
hidden on the page). Home hides the `setup:*` needs-you rows while any step is open, because the Home line replaces them,
as before in kind (the Needs you page and the tray keep them all). **The API contract does not change:** §7 (including
`GET /sindri/setup`), the goldens of §8, the events, the codes and the action ids stay as in rev 4, because the API carries
no layout. The history in §12 keeps the wording of the time it was written.

**Rev 4 (2026-10-19), /autoplan round 3, the last (§12):** the first slice is shippable (SO-A7 is split: A7a keeps
today's Onyx default and the MCP hint, A7b drops them only once A4, A5 and U1 have merged, and the slice is recounted);
the Setup card filled its three rows by priority (dropped in rev 5); the default profile allows the effects the shipped recipes declare; every
C# command is written `sindri-app <verb>`; the manifest file is gone (the shipped set is the recipe files embedded from
`git archive HEAD`, hashed at serve start, with a new `sindri-app recipe manifest` verb); a recipe's capability set is its
block ids, effect kinds and capability literals (`RecipeCapabilitySet`); the app-confirm check is in the handlers, not in
`Decide`; the detached start has an exact line and a success check; "finished" has one meaning.

**Rev 3 (2026-10-19), /autoplan round 2 (§12):** recipes wait for the approved profile (the first-run golden no longer
offers a button that cannot work); the first approvals are defined (a never-approved recipe is "New" in the hold, with
its full capability set); approve-all skips a refused recipe and its dependents and carries on, and the shipped recipes'
evals run at publish; the app is let in to the three protected rows by a per-row `AppConfirm` flag; setup.sh publishes
first with `--publish-only` and starts the app detached; Onyx can be skipped, and its wait is a named signal; the
password has one in-memory slot; the `RegisterMcp` job key is valid; existing boxes consent once; Q11 is answered.

**Rev 2 (2026-10-19), /autoplan round 1 (§12):** one honest first-run order (setup.sh installs and opens the app; Onyx is
a step in the app; one table counts every prompt); approve-all is a job with a plan digest, an internal approval path and a
shipped-recipe manifest written from the committed tree; recipe versions bump with `displayName`; job kinds and keys match
the id types; Onyx setup is two calls and the password never reaches a job row; approve-all also takes a typed answer;
Setup is a Home card, not a first screen (rev 5: a Settings page and one Home line); the UI specs are amended; consent is in plain words; background failures are
visible and retryable.

**Decision it implements (owner, 2026-10-19, after the demo):** Sindri is set up **once**, then Onyx. Everything else
happens in the background or through the app, never through CLI commands: repo registration, the profile approval
prompt, MCP registration, map builds and recipe registration.

**Amends** (the full list, with the repo docs, is §11; each inline edit is marked **(SO-n)** in the amended text):
- the API surface spec (`2026-10-10-sindri-api-surface-design.md`): SO-1 to SO-9, SO-18 and SO-23 (the setup route, the setup
  commands, the `setup` needs-you kind, the app as a confirmation channel for three `protected` actions, error codes,
  events, `displayName` on the recipe views, the `sindri takeover` short form);
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): SO-10 to SO-13 and SO-17 (`displayName` and the
  version rule, registration at serve start, approve-all, `SND-RECIPE-011`, the `sindri takeover` short form);
- the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`): SO-14 (the map has no manual promote
  step; the `context-map-refresh` service promotes);
- the Onyx spec (`2026-10-16-sindri-onyx-managed-design.md`): SO-15 (the owner's Onyx login may be given in the app);
- the UI specs (`2026-10-11-sindri-ui-design.md`, `2026-10-13-sindri-ui-screens-design.md`): SO-19 to SO-21 (Setup under
  Settings with one Home line, three protected actions become app confirmations);
- `.agents/rules/mcp-servers.md`: SO-22 (Sindri may register its own MCP after the profile is approved);
- `docs/sindri/README.md`: SO-16 (X65).

**Command names.** Until the switch, the C# Sindri's command is `sindri-app`; `sindri` is the TypeScript build. Every C#
command in this spec, its goldens and its headless forms is written `sindri-app <verb>`. (The API and engine specs write
the post-switch name `sindri`; their rows quoted here keep their text, and the setup-once rows in them use `sindri-app`.)

**Relation to PR #433 (CI apply spec, draft):** §10 folds it in clause by clause. This text wins where they differ, and
only for the clauses §10 marks "supersedes" or "conflicts".

## 1. Goal and the owner's rule

**The rule (owner, 2026-10-19):** the owner sets up **Sindri** once, then **Onyx**. Sindri setup is one command in a
terminal. Onyx setup is a step in the app (with `sindri-app onyx up` kept as the terminal fallback). A thing that is not
one of those is a background job or a button in the app. A new repo is a button. An approval is a typed or held
confirmation in the app. A map builds itself.

**What stays human.** Approvals stay human. The rule removes the *terminal*, not the *person*: the profile approval and
the recipe approvals keep a confirmation bound to the content's hash, now given in the app (§4). A session is still
refused `SND-AUTH-002`. Nothing in this spec lets an agent approve anything.

**Success.** A new owner runs `./setup.sh --with-sindri`. It installs, publishes the app and opens it. Home shows one
line, "Finish setting up Sindri", that opens the Setup checklist under Settings, six steps (repo, profile, recipes, MCP, map, Onyx). They pick a repo, confirm one profile approval (two
when Onyx is set up after the profile is approved), hold one "approve all recipes", sign up to Onyx in the browser and enter their Onyx login in a form (or press Not now), and
watch the rest finish.
The second repo they add is one pick and one typed confirmation. Success is measured two ways: the prompts in the table
of §2, and **time to first value**, the time from the app's first open to all six steps `done` or `skipped` (`setup.done`;
"finished" means exactly that, here and in §5.7, §6 and §7.5), and the time from there to the first work item's first transition (`setup.first-item`), both recorded from the ledger (§7.5). When the six are done, the Home line is gone and the checklist's last line says what to do next ("Open a repo's map" or
"Start a work item").

## 2. Today, and after: every prompt a new owner meets

The C# Sindri app, from nothing to a working Sindri with one repo. A numbered row is one command the owner types. A
"typed" row also asks for a typed confirmation. Counts: 26 shipped recipes (`recipes/sindri/*.yaml`). Until the switch the
C# Sindri's command is `sindri-app`; `sindri` is the TypeScript build.

| # | Step | Exact command today | Typed confirmation |
|---|---|---|---|
| 1 | Install Sindri (builds the TS `sindri`, the deps, the Serena image), publish `sindri-app`, then stand up Onyx at the terminal: your Onyx account in the browser, then your Onyx admin login and password (prompts, not commands) | `./setup.sh --providers claude --with-sindri` (it runs `sindri-app onyx up` itself unless `--no-onyx`) | the profile approval, inline |
| 2 | Start the control plane and open the app | `sindri-app` | no |
| 3 | Register a repo: there is no C# verb, so the owner edits `profile.yaml` (`$AW_SINDRI_HOME/profile/profile.yaml`) and adds a `repos:` entry. The TS build has `sindri repo onboard <path>` (add, approve, pre-commit hook, first index) | hand edit; or TS `sindri repo onboard .` | its profile approval |
| 4 | Look at the profile change | `sindri-app profile approve` | no |
| 5 | Approve the profile | `sindri-app profile approve <hash>` | typed: `approve profile <hash6>` |
| 6 | Register Sindri's MCP in the repo (per repo, from inside it) | `sindri-app mcp register --apply --providers all` | no |
| 7-32 | Register each shipped recipe, children first (26 commands) | `sindri-app recipe register recipes/sindri/<id>.yaml` | no |
| 33-58 | Approve each shipped recipe, children first (26 commands) | `sindri-app recipe approve <id>` | typed: `approve <recipe> <version>`, 26 times |
| 59 | Build the map | `sindri-app context map build` | no |
| 60 | Accept what is certain | `sindri-app context map review --accept-deterministic` (and `review accept\|reject …` for the rest) | no |
| 61 | Make the map current | `sindri-app context map promote` | typed: `promote map v<n>` |
| 62 | Check | `sindri-app doctor` | no |

Today is **62 commands and 28 typed confirmations** for the first repo (1 setup.sh, which also runs Onyx, 1 start, 1 repo
registration, 2 profile, 1 MCP, 52 recipe, 3 map, 1 doctor), plus **7 commands and 2 typed confirmations for each further
repo** (registration, two profile commands, MCP, three map commands). Rev 1 counted the Onyx command separately (63); it is
part of setup.sh, so 62 is the honest figure.

**The new first run, in order** (one sequence, no step twice):

1. `./setup.sh --providers … --with-sindri` installs the dependencies and tools, **publishes `sindri-app` right after the
   provider installs, as its own named step** (`sindri-app --publish-only`, §9), **starts the app detached** when a desktop
   session exists (or, with none, prints `sindri-app --headless` with the launcher's absolute path), then does the slow
   installs (the Serena image, the TypeScript build). **From A7b on** it no longer runs `onyx up` or prints the MCP register
   hint; `--with-onyx` keeps the old terminal behaviour (Q9). **In the first slice (A7a, §13.1) both stay**, because the app
   steps that replace them (A4, A5, U1) have not merged yet; the table below is the count from A7b on.
2. In the app, Home shows the "Finish setting up Sindri" line, which opens the Setup checklist. The shipped recipes register at start. Only the folder pick and Set up Onyx
   (any time Docker is ready) can start. The profile step is `todo` ("Waits for your first repo") until a repo exists
   (§5.2), and `recipes` is `todo` ("After the profile is approved") until the profile is approved (§5.3).
3. The owner picks a repo and confirms the profile once. Then the hold is offered, and the owner holds approve-all. They
   sign up in the browser for Onyx and enter the login in the form (or press Not now). MCP and the map finish by
   themselves.

| | Today | After (rev 2) |
|---|---|---|
| Commands typed in a terminal | 62 | **1** (`./setup.sh --with-sindri`) on a box with a desktop; a daily start is `sindri-app`, not setup. **With no desktop session: 6** (setup.sh, `sindri-app --headless`, `sindri-app repo add`, `sindri-app profile approve`, `sindri-app recipe approve-all`, `sindri-app onyx up`); the start stays in the foreground, so it takes a second terminal, and `onyx up` runs in the same terminal as setup |
| Typed confirmations at a terminal | 28 | **0** (2 with no desktop session: the profile and approve-all) |
| Typed confirmations in the app | 0 | **1** (the profile, bound to its hash), **or 2 when Onyx is set up after the profile was approved** (its section is one more profile change; Q10) |
| Held confirmations in the app | 0 | **1** (approve-all; a typed answer is accepted instead) |
| Browser steps | 1 (Onyx sign-up) | 1 (Onyx sign-up) |
| Forms | 2 prompts at the terminal (Onyx email, password) | 1 (Onyx login) |
| Folder picks | 0 | 1 |
| Per further repo | 7 commands, 2 typed | 0 commands, 1 pick, **1** typed |

The terminal-only fallback finishes the same setup with the CLI forms (§7.4, §9), which ship with their units (A3, A5, E3):
a box with no desktop can complete it, and the app is still the owner's rule, not a requirement.

**Time.** The app opens after about 10 to 15 minutes (the build tools, then the publish of `sindri-app`, 5 to 10 minutes; it
may download the .NET SDK), because setup.sh publishes it before the slow installs. The rest of the 45 to 75 minutes (the Serena image, the TS `sindri` build
that setup.sh still does until the switch) runs unattended while the owner is already in the app; about 5 minutes of the
owner's time. Each long stretch is a named setup.sh step with a time hint (§9).

**A box that already has Sindri.** The checklist reads what is there: `repo` and `profile` are `done` for repos already in
the approved profile; `recipes` is `needs-you` once (the version bump of §5.3 means one approve-all); `mcp` and `map` are
`needs-you` once with the action "Turn on for your repos": that profile was approved before Sindri could write MCP
settings or run cleanup calls on its own, so **nothing is written or started until the owner presses it**
(`setup.consent.given`, §5.4); entries made by hand are `unchanged`, never overwritten, and doctor still warns on a
user-wide one; `onyx` is `done` or `needs-you`. The checklist says why `recipes` came back ("Sindri updated 26 recipes").

## 3. Decisions

| # | Decision | Why |
|---|---|---|
| SO-D1 | The setup is **one visible list of six steps**, served by `GET /sindri/setup`, in the order repo, profile, recipes, mcp, map, onyx. The app draws it as a **Setup checklist under Settings, with one "Finish setting up Sindri" line on Home while any step is open** (§5.7); nothing in it is the app's own logic | The UI is a pure client (UI spec §17.27.1); the owner sees one place that says what is done and what needs them, Home stays the default page and carries one line, not a card |
| SO-D2 | Long work is a **background job** returning 202 and a job id, never a blocking POST; a step that fails or waits on the owner is also a **needs-you row**. This includes approve-all | The existing job and needs-you machinery (API §3.6, P6) already gives progress, cancel and events |
| SO-D3 | The app becomes a **second confirmation channel for exactly three `protected` actions**: `ApproveProfile`, `ApproveRecipe`, `ApproveAllRecipes`, each marked by a per-row `AppConfirm` flag (§4 item 7). Everything else `protected` (grants, accept, revoke, map promote by hand, review-apply, merge, add, delete) stays terminal-only | The owner's rule needs the profile and the recipes in the app. Widening further is a separate decision |
| SO-D4 | The app confirms by **typing** (the profile, a recipe) or by **holding or typing** (approve-all), against a server challenge bound to the content's hash. Same challenge store, same 5 minutes, same drift refusal as the terminal | The profile widens what Sindri may do (repos, services, effects), so it is typed. Approve-all covers only recipes whose `id@hash` is in the installed build's embedded set (§5.3), so a deliberate hold after reading the list is enough, and the typed answer keeps it reachable by keyboard and assistive tools |
| SO-D5 | Shipped recipes come from the build: one set per box. `serve` registers every shipped recipe at start, idempotently, from the build's embedded copies, and never approves. The registry and the approvals live in each Sindri home's `ledger.db` | A shipped recipe is part of the install; registering it is not a decision |
| SO-D6 | **Approve-all** is one human confirmation applied as one approval per recipe, children first, run as a job; a refusal skips that recipe and the recipes that wait on it, and the rest carry on. A previously approved recipe that gains a capability is never in it; a never-approved shipped recipe is in it, with its full capability set shown | Keeps `recipe.approved` per recipe, so pins, drift and revoke work unchanged; a gain is a decision of its own |
| SO-D7 | **MCP registration is done by Sindri** for every repo of the **approved profile snapshot**, local scope only, never user-wide. Approving a profile that adds a repo is the consent | §5.4 says why this is safe and why it is not an agent-triggerable action |
| SO-D8 | The map has **no manual step**: the `context-map-refresh` service builds, cleans up and promotes. The protected manual promote stays | Owner rule, 2026-10-19 (source: this decision, relayed with #433): map promote has no manual step, so the service promote bypasses the protected command by design |
| SO-D9 | Onyx setup is the **second setup**, and it is a **step in the app**. `sindri-app onyx up` stays as the terminal fallback; setup.sh runs it only with `--with-onyx` | The owner said "Sindri once, then Onyx"; the Onyx login is the one thing only the owner can supply. Running it at the terminal first made the first run contradict itself (rev 1) |
| SO-D10 | The setup step ids are fixed words (`repo`, `profile`, `recipes`, `mcp`, `map`, `onyx`) and appear in the route, the event and the needs-you id | One vocabulary across lanes |
| SO-D11 | The `setup` view is a **read model over stored state** (the ledger, the views, the live and approved profile, git heads, provider config, `control.json`), computed on read; the service only emits the change events (§6) | Rev 1 said "derived from the ledger"; several steps need more than the ledger |

## 4. The app as a confirmation channel (SO-D3, SO-D4)

Today a `protected` action is refused to the app (API §4.2) and the app shows the CLI to copy. This spec changes that
for three actions, and only them. The change is made by **adding a path, not by loosening the old one**: the existing
challenge route keeps its `NeedsTerminal` rule, so a session still cannot issue a challenge for any protected action.

1. **Issue.** The app asks `POST /sindri/workflow/challenges/app` with `{action}`. It is a separate route that accepts the
   `human` token only (a `session` is `SND-AUTH-002`) and mints a challenge only for `approve profile <hash6>`,
   `approve <recipe> <version>` and `approve-all <digest6>`; any other text is
   `SND-AUTH-002`. The challenge records its mode, its channel and the time it was issued (`IssuedChallenge` gains `Mode`
   and `Channel`; SO-A2). Approve-all's challenge is refused
   `SND-SETUP-007` before a profile is approved.
2. The answer carries the preview as today, plus `confirm`:
   `{challenge, action, expiresAt, confirm: "typed"|"held", minHoldMs?: 1500, typedAlternative?, approve?|approveAll?}`.
   For approve-all, `confirm` is `held` and `typedAlternative` is the text (`approve-all c41e07`), so either answer redeems.
3. **Typed:** the app shows the preview, then a field that asks for the action text (`approve profile 3f0c9a`). The
   answer is `{challenge, typed}`, compared as the terminal's is. **Held:** the app shows the list and a button that
   fills while pressed (or Space or Enter held); the answer is `{challenge, heldMs}`. A typed challenge cannot be redeemed
   by `heldMs`, and a held one is redeemed by `heldMs` or by its `typedAlternative` only; the held answer has its own redeem
   path in `ProcessChallenges`. The server refuses `heldMs` under `minHoldMs`, and any answer less than 2 seconds after the
   challenge was issued, with `SND-SETUP-003` ("confirmation not given"); that refusal does **not** consume the challenge,
   so the owner can hold again (any other redeem does, as today). The hold is a slip guard, not proof of reading;
   that is the threat model (honest mistakes, not a deliberate agent).
4. **The hold, in full.** It fills over 1.5 seconds with a text count ("Hold to approve, 1.5 seconds"). Releasing early
   cancels, resets the fill and announces "Not approved, hold again". After the 5 minutes the app asks again with the same
   list. With reduced motion the fill is a stepped counter. By keyboard, Space or Enter held for the same time works (auto-repeat
   keydowns are ignored and only keyup resets; the fill is announced at its start and its end, not at every tick), and the
   typed alternative is always offered under the button.
5. Everything else is the terminal's rule: one-time, 5 minutes, bound to the subject, refused when the subject changed
   (`SND-RECIPE-009` for recipes, `SND-PROFILE-003` for a stale profile hash). A second open challenge for the same action
   replaces the first one issued **on the same channel** (replacement is keyed by action and channel): an open terminal
   challenge for the same text survives an app one.
6. The terminal path stays and writes the same events; the actor tells them apart (`human:cli`, `human:ui`). The prover
   (`human:prover`) is accepted for these actions only in a scratch home: `AW_SINDRI_HOME` under a temp root the prover
   itself created, holding the marker file `.prover-scratch` that only the prover writes. The default home never carries
   it, and a test shows the prover refused there (`SND-AUTH-002`).
7. **The `AppConfirm` flag, and which layer does what.** Today `Authorization` gives every `protected` row `Protected =
   Who.Terminal` and forces `NeedsTerminal`, so `POST /profile/approve` from the app would still be `SND-AUTH-002`.
   `Authorization.Decide` is pure and runs before the handler, so it cannot know whether a request holds a valid answer.
   The table therefore gains a per-row `AppConfirm` flag, set on exactly three rows: `ApproveProfile`, `ApproveRecipe` and
   `ApproveAllRecipes`.
   - **`Decide`** lets `Who.App` through on an `AppConfirm` row: the class check passes, and the row's `NeedsTerminal`
     clause does not apply to it (every other `protected` row keeps its terminal gate). A session (`Who.Session`), a service
     and a caller with no token stay `SND-AUTH-002` on every row, before the handler runs.
   - **The handler** redeems the challenge through the single `IChallenges.Redeem` **before its first write of any kind**.
     The codes stay as today: a missing or wrong answer is `SND-PROFILE-004` for the profile and `SND-RECIPE-009` for a
     recipe or approve-all, and `SND-SETUP-003` covers a short hold, an early answer or the wrong kind (item 3). The job
     row and the rate-limit counter of approve-all are written only after redemption; a failed insert after redemption is a
     clean refusal that burns the challenge.
   - **Three architecture tests.** (a) Each `AppConfirm` row's handler, driven with a refusing fake `IChallenges`, leaves the
     ledger, the jobs table, the rate-limit counter, the profile and the approvals unchanged (zero writes of any kind). (b) A
     `Who.Session` caller and a caller with no token get `SND-AUTH-002` on all three rows before the handler runs. (c) A
     table test that exactly three rows carry the flag.
   - The changes to `IChallenges`, `IssuedChallenge` (`Mode`, `Channel`) and `ChallengeAnswer` (`heldMs`) are SO-A2's.

**Catalog.** `GET /commands` rows gain `confirm` (`typed`, `held` or `null`). The three rows' UI status flips from
`cli-only:typed-confirmation` to `ready` (SO-3). The architecture test that says the app and the MCP server reference no
challenge route (engine §4.2 last line) keeps its MCP half and changes its app half to "the app references only the app
challenge route, and only for the three allowed actions" (SO-12).

**What stops a session.** A `session` caller is refused `SND-AUTH-002` for the three actions as for any `protected`
action. The app sends `human` with the per-start token (API §6); the MCP endpoint stamps `session` whatever it is
sent. Reading the owner's token file stays out of scope for v0 (API §6, §11): **the token file is a v0 trust boundary**
(Q1 says so), and doctor's `setup` check warns when it is group- or world-readable. This channel is desktop-only: a
browser head cannot hold the token.

## 5. The six steps

Each step: **trigger**, **job or route**, **events**, **needs-you row on failure**, **human-only part**,
**idempotent on re-run**. `service:setup` is a new **built-in service** (listed in `sindri-app services`, **not pausable**, no
profile entry). It runs the in-process parts below as caller `service:setup`, is woken by events (`profile.approved`,
`recipe.approved`, `repo.added`, `mcp.registered`, `job.finished`) and has a 60-second backstop tick, so a step changes
within a second or two of what caused it. The grants it needs are the existing `ServiceGrants` field on the catalog rows
(`RegisterRecipe`, `RegisterMcp`), as #433 uses them for `ci-apply`. It is unpausable already (`PauseServiceService`
answers `SND-SVC-404` for a service the approved profile does not name), and a test says so. Its automatic jobs
(`RegisterMcp` runs and the follow-ups to `AddRepo`) are a **single-slot job class**: when the 8-job limit is reached they
queue and run when a slot frees; they do not fail (the queue is built in A1, with a test). **Every write to `profile.yaml`** (the repo appender, the Onyx section,
the first file) goes through the existing versioned write (`if-version`); on a conflict it re-reads and retries once, and
a second conflict leaves the step `failed` with "The profile changed while Sindri was writing it. Try again.".

### 5.1 Step `repo`: add a repo from the app

| | |
|---|---|
| Trigger | The owner picks a folder in the app and presses **Add repo** (`AddRepo`, `desk`), or types a path beside the picker (the picker is desktop-only; other heads show only the field). The app sends the absolute path |
| Route and job | `POST /sindri/setup/repos` `{path, name?}`. `name` defaults to the folder name **normalised to the `RepoName` rules** (lowercase, `[a-z0-9-]`, at most 100 characters; each run of other characters becomes `-`); the app's confirm shows the name and lets the owner edit it, and a name that is still invalid is `SND-SETUP-002` with its fix line. The cheap checks run **before** the job is claimed, so their codes are real HTTP errors: the path exists, is a git checkout, resolves to its **main checkout** (a linked worktree resolves as API §3.1 says), is readable, is not under `$AW_SINDRI_HOME`, is **not `/`, not the user's home folder and not a parent of `$AW_SINDRI_HOME`** (`SND-SETUP-001`); the repo is not already in the profile and its name does not collide (`SND-SETUP-002`). Then **202** `JobStarted(accepted, JobId, links)`. Job kind **`AddRepo`**, key = **the first 16 hex of sha256 of the resolved path**. The path is never in a job event; events carry the repo name only. A second add of the same path while one runs is `SND-JOB-409` |
| What the job does | 1. Runs `git` with `-c core.fsmonitor= -c core.hooksPath=/dev/null` and `GIT_CONFIG_NOSYSTEM=1`, arguments as an argv, never a shell string. 2. Appends a `repos[]` entry (`name` as above, `path` = the real path) to the **live profile file** through the versioned write (§5), keeping every other key and comment (a general `repos[]` appender on the profile writer the Onyx step uses). The first repo **creates** the file, with the defaults and any Onyx section that was waiting (§5.6). **The default profile's `workflow.effects` is the union of the effect kinds the shipped recipes declare**, read from the embedded set (§5.3), so the first work item's effects are not refused (`SND-WF-011`) after approve-all. A CI check fails if a shipped recipe declares an effect the default doesn't allow, and a unit test over the embedded set checks that every shipped recipe passes `ApproveShippedRecipe`'s checks (allowlist and capability ceiling) under the default profile. That change is a profile drift, so the profile waits for approval (§5.2). 3. Appends `repo.added`. Progress lines: `checking`, `added to the profile`. **The job ends there**; waiting for the profile is the `profile` step, not a job line, so the job key is free again |
| After approval | The `setup` service sees `profile.approved` name a repo it has not onboarded and runs, as `service:setup`: the pre-commit scrub hook (when the C# build has an installer; until then the repo step shows `done` **with the detail "No secret-check hook yet: add it when the installer ships"**, also a doctor line, never a silent green), then step `mcp` (§5.4). It appends `repo.onboarded {repo, approvedHash}`. The first map is the `context-map-refresh` service's own trigger (§5.5) |
| Events | `repo.added {repo, commandId}` (public; the path is in the non-public payload), `repo.onboarded`, `job.*`, `setup.step.changed` |
| Failure | Checks: `SND-SETUP-001/002` (400/409). Job: the profile's `SND-PROFILE-001/002` as the job's failure code. Needs-you `setup:repo` with the sentence and the action `AddRepo` to try another folder |
| Human-only part | The path is the owner's pick, so `AddRepo` is `desk`: a session is refused `SND-AUTH-002`. Approving the entry is the profile approval |
| Re-run | Adding an added path is `SND-SETUP-002` and changes nothing, except that a profile entry whose `repo.added` is missing (a crash between the two) gets the event appended. Onboarding after approval is keyed by `(repo, approved hash)` through `repo.onboarded` |
| Git facts | The step's git facts (default branch, HEAD, tracked MCP files) are cached per repo for 30 seconds and refreshed on `repo.*` events, so `GET /setup` runs no `git` per request |

### 5.2 Step `profile`: the approval prompt

| | |
|---|---|
| States | **`todo`**, "Waits for your first repo", until the profile names a repo: on a fresh box there is nothing to approve, so there is no `needs-you` row and no preview. **No profile file is written before a repo exists** (the first `AddRepo` creates it, §5.1), so Set up Onyx before a repo leaves this step `todo` and raises no `profile:drift` row. It becomes `needs-you` when the live profile differs from the approved one (`profile.checked drifted:true`, every 30 s) |
| Route | The existing `GET /profile` and the app challenge of §4 (`approve profile <hash6>`, **typed**), then `POST /profile/approve` `{hash, answer}` (unchanged body) |
| Needs-you | The existing `profile:drift` row (kind `profile`); its `primaryAction` becomes `ApproveProfile`, `ready` in the app (SO-5). **There is no `setup:profile` row**: the Setup checklist links the same row, so the checklist and Needs you show one approval, not two |
| Preview | In plain words, first: for each added repo, **its resolved real path and its size (files and megabytes)** and the sentences the approval carries: "Sindri will read `<path>` and send what it reads to your Onyx and to the map cleanup agent", "Sindri will write its MCP settings into `<repo>` for Claude Code, Codex and Cursor" (§5.4) and "Sindri will keep `<repo>`'s map up to date on its own". Then widened services and effects in words (for the default profile, the effects it allows: reserve a work item, put a question or notice in Needs you, write progress lines, open or update a pull request), then the changed key paths. The typed text is a slip guard; the sentences are the consent. Every pending change (the repo and, if Onyx ran first, the Onyx section) is in this one preview |
| Events | `profile.checked`, `profile.approved` (unchanged), `setup.step.changed` |
| Failure | A stale hash is `SND-PROFILE-003` (the app re-reads and asks again); an invalid profile file is `SND-PROFILE-001/002` shown on the step as `failed` with the sentence |
| Human-only part | The whole step. The typed confirmation is bound to the live hash |
| Re-run | Approving an approved hash is a no-op (as today) |

### 5.3 Step `recipes`: registration at start, approve-all in the app (engine lane)

**The shipped set.** At **publish time** (`sindri-app --publish-only`, §9) the script runs `git archive HEAD recipes/sindri`
**in the checkout** (the build copy has no `.git`; a toolkit tree that is not a git checkout fails the publish with "Sindri can
only be published from a git checkout"), **deletes the build copy's `recipes/sindri`, then extracts the archive into it**, so
only `HEAD`'s files are there and an untracked or edited working-tree recipe is never embedded. An MSBuild target in
`Aw.Sindri.Host` embeds that staged directory, and only it, as resources, and `recipes/sindri` joins the build-input
pathspecs (`SINDRI_BUILD_PATHSPECS`), so a recipe-only commit republishes. A publish test has an untracked
`recipes/sindri/x.yaml` and a dirty tracked one and asserts that neither is in the binary.

**There is no manifest file.** The embedded copies are the shipped set. At `serve` start the registry hashes each embedded
file with its own hash function, **once**, and registers only those; "shipped" means `id@hash` is in that immutable set.
Nothing reads `recipes/sindri/` or any manifest on disk at run time, and there is no disk fallback. Dev and `dotnet run`
builds embed the same `HEAD` archive (or an empty set), never the working tree; a test edits a working-tree recipe and shows
the `recipe manifest` output unchanged. Publishing from a dirty tree is allowed and says so; the embedded set is still
`HEAD`'s.

The new engine verb **`sindri-app recipe manifest <dir> [--json]`** prints `id@version → hash` (and the eval-case count) for
the recipe files in `<dir>`, using the registry's own hash function. After the build the publish runs **`sindri-app recipe
eval --shipped`** with the freshly published binary against the embedded set, and a failing eval fails the publish (exit 1). It
runs them **only when the embedded set changed since the last publish**, by comparing `recipe manifest` output with the
previous output kept in the publish cache (`$AW_STATE_DIR/cache/sindri-app-build/`); that comparison is made at publish time,
only decides whether to run the evals, and is never read by the running app. **CI runs every shipped recipe's evals**, and
runs `recipe manifest` over `HEAD` and over `git archive origin/main` (the same archive command, so `export-ignore` applies
equally) and fails when a hash changed at the same version (the bump rule below). A dirty tree builds a binary that may differ
from `HEAD`, so "every shipped recipe passed" holds for the binary that ran them; the publish stamp includes the commit and
the git tree hash of `recipes/sindri/` at `HEAD`, and doctor's `setup` check warns when the embedded set's tree hash differs
from `HEAD`'s.
| | |
|---|---|
| Trigger | Registration: every `serve` start, after the control plane listens. Approval: the owner presses **Approve all**, offered only once a profile is approved |
| Order | `recipes` waits for the approved profile. The shipped recipes register at start, but the step is `todo`, `action: null`, "After the profile is approved" until then, because every approval check uses the approved profile's allowlist. Before a profile is approved the plan and its challenge answer `SND-SETUP-007` |
| Registration | `service:setup` registers each recipe of the embedded set (26 today), children first, through the `RegisterRecipe` slice. Idempotent by hash: an already-registered hash is `unchanged`. It **never approves**. Each recipe registers on its own: **one that fails shows on the `recipes` step with its sentence and the other 25 still register** (a recipe whose child failed waits, with `why` naming the child). It appends `recipe.registration.completed {registered, unchanged, failed}` (counts) once per start, and the `recipe.registered` event per new hash as today. A different hash at a version already taken is refused by the registry (`RecipeVersionTaken`) for that recipe only: the backstop for a missed version bump |
| What counts as a gain | A recipe's **capability set** is the sorted set of its block descriptor ids, the effect kinds it emits and the literal values of its capability fields. One pure Core type, `RecipeCapabilitySet`, computes it; the plan, the digest and the preview all use that one function, and it also computes `old` for the last approved hash from that hash's stored recipe (so a later engine version that adds a dimension can only make more recipes look like gains, the safe direction). The six `Capability` flags are not used: they cannot see a newly added `step.ship.*` block. A recipe **gains a capability** when `newSet \ oldSet ≠ ∅`, where `oldSet` is the set of the **last approved hash** and **the empty set for a recipe that was never approved**. Tests: a recipe that already has `step.checks` and adds a `step.ship.*` block is a gain; a reorder or a version bump is not; a changed literal under an unchanged id is a gain. A never-approved embedded recipe therefore has no earlier approval to gain from: it is `New` in the hold, and its **full capability set is shown in the preview and covered by the digest**. A shipped recipe with a `step.ship.*` capability is in the hold on a fresh box with its sentence ("Opens or updates a pull request and reads its checks") |
| Approve-all: the plan | `GET /sindri/workflow/recipes/approve-all` is the **plan** (a query): every shipped recipe that is registered and not approved at its current hash, or whose approval is stale (an approved recipe whose child's approved hash moved), children first. Each row has its plain-words capabilities (§5.3.1), the hash last approved (`null` when never approved) and what changed since (a capability delta in words and the changed blocks). A **previously approved** recipe that gains a capability, or that waits on one that does, is **not in the plan's list**: it is in `ownApproval` and is approved one at a time, typed, with `ApproveRecipe`. The plan runs no evals and carries no eval result: the shipped recipes passed theirs at publish, and approval itself only checks that evals are declared (`SND-RECIPE-008` when none are) |
| The digest | `digest` = sha256 over the ordered list of `id@hash` plus each recipe's capability set (sorted ids). `approve-all <digest6>` is its first six hex characters. The challenge is bound to it |
| Approve-all: apply | `POST /sindri/workflow/recipes/approve-all` (`protected` with `AppConfirm`, app challenge `approve-all <digest6>`, held or typed) answers **202 `JobStarted`**: job kind **`ApproveAllRecipes`**, key the fixed `approve-all` (one at a time: a second POST while one runs answers 409 `SND-JOB-409` with `runningJob`). The challenge is **redeemed last**, after the cheap prechecks (the profile is approved, the digest matches) and the job-limit and rate-limit checks (reads only; the job row and the counter are written after redemption), so a `SND-JOB-409` or a rate-limit refusal does not burn the owner's hold. **One challenge covers the plan.** Once it is redeemed, the job approves each recipe through an **internal approval path** (`ApproveShippedRecipe`) that takes the redeemed plan and runs the same checks as `ApproveRecipe` (pins, evals declared, the capability ceiling, the approved profile's allowlist) without minting a challenge per recipe, because each recipe's own challenge binds a pins digest that changes as its children are approved. For each row it first checks that `id@hash` equals the plan row and is in the embedded set; a mismatch is `SND-RECIPE-009` for that recipe, and its result row carries the `cli` of the one-recipe approval (`sindri-app recipe approve <id>`) when the check has no CLI form of its own. `ApproveShippedRecipe` has no catalog row, no route, no MCP tool and no CLI verb: it is constructible only from a redeemed plan (a private constructor on the plan token), only the `ApproveAllRecipes` job calls it (an architecture test asserts it has no `Authorization` row and no endpoint, and that the job is its only caller), it writes the same `recipe.approved` row and the same drift wake as `ApproveRecipe` (a stale approval whose child moved wakes the parked instances), and each approval carries the job's actor and `batch`. One `recipe.approved` each, with `batch: <commandId>`, the plan digest and the hold time. **A refusal skips that recipe and every recipe that waits on it, and the job carries on with the rest.** **Progress** is the job's `{done, total}` and its lines (the `displayName` of the recipe in hand); the **result** is on the job: `{approved, refused, skipped}` |
| Drift between preview and apply | If the plan changed after the challenge was issued and before the first approval (a recipe re-registered, a pin moved), the POST is `SND-RECIPE-009` and nothing is applied; the app re-reads the plan. A recipe that changes between rows is skipped (above) |
| Only shipped recipes | A recipe whose `id@hash` is not in the embedded set (an owner-written or generated recipe) is never in the plan; naming one is `SND-RECIPE-011`. Those are approved one at a time (`ApproveRecipe`, typed in the app, §4) |
| Refusals | A refused recipe shows on the step under `items` with its sentence (the sentence carries no command; the fix is in the row's `cli` field), and the step reads "N approved, 1 needs a look, 2 waiting on it". A recipe refused last time stays in the plan marked "refused last time: <sentence>", and its row opens the one-recipe `ApproveRecipe` or the doctor line, so repeating the same hold is never the only way out. Evals are not re-run at approval; `SND-RECIPE-008` ("declares no checks") cannot happen to a shipped recipe after publish, so it shows only for an owner-written recipe or a damaged build |
| `displayName` and versions | `displayName` is a new **required** recipe field (SO-10): the name the app shows instead of `workflow.step`. It changes every hash, and the registry refuses a different hash at the same version (`RecipeVersionTaken`), so **the same change bumps every shipped recipe's version**. The rule is general: **a content change bumps the version**, and CI runs `recipe manifest` over `HEAD` and over `git archive origin/main` and fails when a hash changed at the same version; `recipe validate --against <the origin/main output>` is the comparison (no manifest is committed anywhere). **Instances already started keep the version they pinned**: approving v2 never repins a running v1 instance; only new starts take v2. Every shipped recipe is re-approved once, which approve-all makes one hold. Views that name a recipe carry `displayName` beside the id (SO-9) |
| Step states | `needs-you` while the plan's `recipes` or `ownApproval` is not empty; `running` during the job; `done` only when the plan and `ownApproval` are both empty. The `ownApproval` recipes are `items` under "Needs its own look", each opening `ApproveRecipe` (typed, §4) |
| Needs-you | `setup:recipes` once a profile is approved and recipes are registered and unapproved ("26 recipes wait for your approval"), on a failed registration or a refusal, or while `ownApproval` is not empty; primary action `ApproveAllRecipes` (or `ApproveRecipe` when only `ownApproval` remains). Before the profile is approved there is no row: the step is `todo` |
| Events | `recipe.registration.completed`, `recipe.registered`, `recipe.approved`, `job.*`, `setup.step.changed` |
| Human-only part | Approve-all. A session is refused `SND-AUTH-002`; registration is not a decision |
| Re-run | Registration of known hashes changes nothing. Approve-all on an empty plan answers 200 `{approved: [], refused: [], skipped: []}` without a job and the plan's `empty` sentence says "Every recipe is approved." |
| After a Sindri update | A new build's changed recipes are registered at the next start and appear in the plan as `Updated`; the step returns to `needs-you` with the reason, and one more hold approves them (Q5) |

**5.3.1 Capabilities in plain words.** The consent surface never shows a machine name first. Each block descriptor carries a
plain sentence (a descriptor without one fails the block catalog test, so **every capability a shipped recipe carries has
words**), and the plan groups the recipes by capability. The machine id sits in a small secondary line.

| Capability (family) | Sentence shown |
|---|---|
| `effect.claim.ref` | Reserves a work item so no other run starts on it |
| `effect.notify.desk` | Puts a question or notice in your Needs you list |
| `effect.line.report` | Writes progress lines into the workflow log |
| a step that starts an agent (`step.produce`, `step.review.*`, `step.rootcause.*` and the like) | Starts an AI agent session in a repo you added |
| `step.checks`, `step.verify*`, `step.reproduce.*` | Runs your repo's tests and build commands |
| `step.ship.*` | Opens or updates a pull request and reads its checks |
| anything the table has no words for (an **owner-written** recipe only) | Does something this list has no words for yet: `<id>`. Never part of approve-all |

The change words are **New** (never approved), **Updated** and **Needs a second look** (a part it uses changed). A capability
that was added to a previously approved recipe is written "Newly allowed: …" and puts the recipe in `ownApproval`.

### 5.4 Step `mcp`: Sindri registers itself in each repo

Today `sindri-app mcp register --apply` is the human's verb, and `scripts/sindri-app.sh` only prints the command. The owner's
rule moves the doing to Sindri. The mechanism, and why it is safe:

| | |
|---|---|
| Trigger | (a) A repo appears in the **approved profile snapshot** with no registration recorded for the current port; (b) every `serve` start, for each approved repo (so a changed port is repaired); (c) the owner presses **Register MCP** (`RegisterMcp`, `desk`). (a) and (b) run only for a profile that carries consent: a profile approved on this build records `setup.consent.given` when it is approved (its preview showed the sentences of §5.2). On a box whose profile was approved before this build, the step is `needs-you` once ("Turn on for your repos", action `RegisterMcp`), pressing it records `setup.consent.given` (for the `mcp` and `map` steps), and **nothing is written before that press** |
| Which profile | The service reads **the approved snapshot by hash** (the content of the ledger's `profile.approved`), never the live file. A repo only in a drifted live file is not registered (`SND-SETUP-004`). A test starts `serve` with a drifted live profile and checks that nothing is written |
| Route and job | (c) `POST /sindri/setup/mcp` `{repo?}`; the cheap check (the repo is named by the approved snapshot, `SND-SETUP-004`) runs first, then **202** `JobStarted`. Job kind **`RegisterMcp`**, key = **`mcp-` plus the first 16 hex of sha256 of the repo name** (`RepoName` allows capitals and 100 characters, `JobKey` is lowercase and at most 80; the name is never in the key and shows through the job's repo field). (a) and (b) run the same slice in-process as `service:setup` and show as the same job kind in `GET /jobs`. The automatic runs use a request that names the repo, sets `WriteCodexTrust = false` and does not read `Environment.CurrentDirectory` (the existing request is `(Directory, Repo, Apply, Providers)`; the terminal verb keeps its behaviour; A4) |
| What it writes | Claude Code's **local** scope (`claude mcp add --scope local`, run in the repo's main checkout), the checkout's `.cursor/mcp.json` and `.codex/config.toml`. Providers are those found on the box; with **no provider CLI found the step is `todo`** with "Install Claude Code, Codex or Cursor", never `done`. `claude` runs with an argv only, an explicit environment and no repo-provided hook (a test with a project hook file in the repo checks that it does not run; the same is verified against the installed `claude`). It writes only when the content differs, never touches a key that is not Sindri's, and adds the two files to `.git/info/exclude` (not `.gitignore`). **It does not write Codex's project-trust row**: Codex asks the owner once, and the detail says "Codex will ask you to trust this folder once". The terminal verb `mcp register --apply` is unchanged and still writes the row |
| Never | User-wide scope, any repo not in the approved snapshot (`SND-SETUP-004`), any content other than Sindri's loopback `/sindri/mcp` URL and the `X-Sindri-Repo` header. Doctor's `context:mcp-scope` still warns on a user-wide entry (`.agents/rules/mcp-servers.md`, SO-22). A checkout that **tracks** `.cursor/mcp.json` or `.codex/config.toml` in git is left alone, and the step says which file and why (`needs-you`, `RegisterMcp` after the owner decides). Only the main checkout is registered; a lane's linked worktree is not |
| Why a background process may do it | The inputs are fixed and come from the approved profile, so the profile approval (typed, human) is the consent and the preview says so (§5.2). The action is idempotent (provider outcome `unchanged`) and affects one repo's local config. No session can start it: `RegisterMcp` is `desk`, and the automatic runs are triggered by `profile.approved`, a human event. The server runs as the owner's OS user, so it writes only what the owner could |
| Needs-you | `setup:mcp` on a failed provider ("Couldn't register with Claude Code: `claude` isn't installed" with the provider's fix line), primary action `RegisterMcp` |
| Events | `mcp.registered {repo, port, providers: [{provider, outcome}]}` (public), `setup.consent.given`, `job.*`, `setup.step.changed` |
| Re-run | Unchanged providers write nothing |
| Off switches | The profile key `setup.mcp: off` and `AW_SINDRI_APP_NO_MCP=1` turn the automatic runs off; the step's detail then reads "Automatic registration is off" (the button still works). `scripts/sindri-app.sh` stops printing the register command from A7b on (SO-16) |

### 5.5 Step `map`: the auto map (context lane)

The context lane's final names are used as stated. The trigger and the cleanup agent are the context lane's; this spec
adds only the setup step that reports them.

| | |
|---|---|
| Trigger | The built-in service **`context-map-refresh`** ticks every minute with no profile entry. For each repo of the approved profile (on a box whose profile pre-dates this build, only after `setup.consent.given`, §5.4) it starts a job when the repo's HEAD moved and is reachable from the default branch (`origin/HEAD`, `main`, `master`; no fetch). A repo with none of those refs makes the step `todo` with "Waits for a default branch". Backoff 5, 10, 20, 40, 60 minutes after a failure |
| Job | Kind **`RefreshContextMap`** (key `refresh`, one at a time): a draft under the heavy lock, then a cleanup agent, then the promote. The cleanup agent: deterministic proposals are accepted; orphaned overrides are dropped; model proposals at or above 0.75 are accepted; the rest go to the model as keep or remove in batches; if the model is unavailable or **over its daily cap** they are rejected. The service then **promotes through the context engine directly**, `Who = service:context-map-refresh` |
| The cleanup agent's limits | It runs **read-only with no tools**; its input is scrubbed by `Aw.Redaction` and its output is validated against a schema. A **per-run removal ceiling** (more than 30% of the map's entries removed, the context lane may tune the number) parks the run and raises the failure row instead of promoting. The previous map version stays one click away (a promote is a pointer move). `context.map.promoted` carries `cleanup` and the counts too |
| Cost | The cleanup's model calls have a **cap per repo per day (40)** and **no separate machine cap** (the ceiling is 40 times the number of repos), so a repo added later is never starved by an earlier one (the owner chose 40, Q3). The step's detail shows the day's use ("cleanup: 12 of 40 calls today"), and `items` say "cleanup unavailable today" for a repo over its cap; over the cap the agent is `unavailable` for that repo and rejects the guesses |
| Manual | `sindri-app context map refresh` and `POST /sindri/context/map/refresh` (class `job`; **action `RefreshContextMap`, "Retry now"**); the context lane owns the catalog row and its goldens |
| The owner rule | **Map promote has no manual step.** The service promote bypasses the protected `context map promote` command by design. The manual protected promote stays for a person who wants it (source: owner decision, 2026-10-19, stated with #433). Every accept the agent makes is a reversible `map_override` row with actor `service:context-map-refresh` and the Undo of API X63; the Context screen shows "changed by the agent" next to them |
| Events | `context.map.built` (existing), **`context.map.refreshed {version, heads{repo: sha}, accepted, removed, dropped, cleanup: "agent"\|"unavailable"}`** (new; the context lane registers it), `context.map.promoted` (existing). There is no repo-registered event: the service reads the approved profile's repos each tick |
| Step | `running` while a `RefreshContextMap` job runs (progress = repos done of repos total); **after the first failure it stays `running` and the detail reads "Retrying in N minutes: `<sentence>`"**; `failed` after 3 failures in a row, with the action `RefreshContextMap`; `done` when every approved repo has a current map at its head; `todo` with "Waits for a repo" when there is none |
| Needs-you | **`failure:service:context-map-refresh`** after 3 failures in a row, cleared on the next success. The service counts them by reading the last `RefreshContextMap` jobs and returning an error on the third; it is the existing `failure` kind with the service's id, not a `setup` row (so `setup:map` is never emitted) |
| Human-only part | None. A map that needs a decision the agent cannot make is rejected by the rules above, not parked |
| Re-run | A tick with HEAD unchanged starts nothing |

### 5.6 Step `onyx`: a step in the app, in two calls

| | |
|---|---|
| Trigger | The owner presses **Set up Onyx** in the app (any time Docker is ready), or runs `sindri-app onyx up` at a terminal (or `setup.sh --with-onyx`) |
| Docker and "Not now" | Docker missing: the step is **`skipped`** with the reason "Docker isn't installed" and a link to the install doc (`SND-ONYX-001`), `action: null`; installing Docker moves it back to `todo` and reopens the checklist and the Home line. A Docker that is present but fails the memory, disk or port preflight (`sindri-app onyx status`) is shown the same way, with that reason. A **not-started Onyx is `todo`** with the action `SetupOnyx`, never `needs-you`: nothing blocks the owner. **Not now** (`DeferOnyx`, `desk`, `POST /sindri/onyx/defer {deferred: true}`) sets the step to `skipped` with "You chose to set up Onyx later"; Set up Onyx in the checklist's menu reverses it (`{deferred: false}`). Both append `onyx.deferred` |
| Call 1: start | `POST /sindri/onyx/setup` (`SetupOnyx`, `desk`, no body) answers **202** `JobStarted`; job kind **`SetupOnyx`**, key `onyx`. It runs the Onyx spec's §4.1 steps 1 to 6 (the stack up) and then **parks in `waiting` on the named signal `onyx-account`** (`JobState.Waiting` is new: it holds no job slot while it waits, shows in `GET /jobs` as `waiting`, and a restart turns it into `failed`; added to API §3.5 and unit A5): the step is `needs-you`, why "Create your Onyx account in the browser, then enter your login here", and the detail always holds the sign-up address (`http://127.0.0.1:3000/auth/signup`) so a box with no browser opener still works. The job opens the page when it can. A wait of **30 minutes** fails the job with the `SND-SETUP-006` sentence; the wait can also be cancelled (`CancelJob`); either way the step goes back to `todo` |
| Call 2: the login | `POST /sindri/onyx/account` `{adminEmail, adminPassword}` (`ConnectOnyx`, `desk`) is **refused unless the job is waiting on `onyx-account`** (`SND-SETUP-006`); otherwise it answers **202** with the same job id and signals it (steps 7 to 10). Without both fields it is `SND-SETUP-005` |
| The password | The endpoint reads the body with its own reader straight into a **dedicated secret type**: no `ToString`, not serializable, excluded from `AwJson` and from `Aw.Redaction`'s output by construction. It puts the secret in a **single-use in-memory slot keyed by the job id**; the command sent to the job carries **only the job id**, so the password is not a field of the command record or the job's stored request. The slot is cleared on use, on cancel, on job failure and on restart. A binding error on this route returns a fixed message and never echoes the body. The password is **never** in the job row, the ledger, `command.handled`, a job line, a log, a span, a response or the UI's own state. A job does not survive a restart with a login pending: on restart the step goes back to `needs-you` and asks again. The password reaches Onyx in a request body only, never an argument or an environment variable. The sentinel test sends a password and greps logs, the ledger, jobs and `command.handled` for it on **every path: success, a malformed body, a wrong password** (`SND-ONYX-007`), `ConnectOnyx` while not waiting (`SND-SETUP-006`), a missing field (`SND-SETUP-005`), the 30-minute timeout, and the Onyx login call itself |
| The profile section | Onyx writes its section into the profile, a change like a repo (Q10). **No profile file is written before a repo exists**: if Onyx runs first, the job keeps the section in `control.json`, the step says "Connected; the profile section is added with your first repo", the profile step stays `todo`, and the first `AddRepo` write includes the section, so one approval covers both. If the profile has already been approved, the section is a second typed approval and the checklist says so (§5.7) |
| Needs-you | The existing **`failure:service:onyx`** row (`NeedsYouRows.ForOnyx`), whose reason carries the wait or failure sentence (`SND-ONYX-0nn`) and whose primary action is `SetupOnyx` (or `ConnectOnyx` once waiting for the login); **no `setup:onyx` row**. `cli` = `sindri-app onyx up`. A `todo` Onyx has no row and is checklist-only |
| Events | `job.*`, `setup.step.changed`, `onyx.deferred`; the existing Onyx state in `control.json` |
| Human-only part | The sign-up and the owner's login. Docker is a prerequisite (`SND-ONYX-001`), shown on the step on first open when it is missing |
| Re-run | Onyx's own idempotence (Onyx spec §4.1) |
| Terminal | Unchanged: `sindri-app onyx up` stays (Onyx spec P5). `setup.sh` runs it only with `--with-onyx`; its profile confirmation is then the typed challenge at the terminal, as today |

### 5.7 The Setup checklist under Settings, the Home line, and their states

The app shows the six steps as a **Setup checklist**, its own page under **Settings** (in-app route `setup`, UI §10),
reached from Settings and by the chord **`G U`**. There is no OS URL scheme for it, because `sindri://` is later (UI
§12.2). **Home stays the default page** (screens spec R8-1; rev 7 §3.3) and shows **one line, "Finish setting up Sindri",
while any step is not `done` or `skipped`**; the line links to the checklist, and it is gone when every step is `done` or
`skipped`. A step that reopens (a new repo, a new build) brings the line back. Rev 5 replaces the rev 4 three-row Setup
card on Home (owner Q1, F3 Setup spec).

- **Finished:** one meaning. Setup is finished when every step is `done` or `skipped`; `setup.done` is emitted at that
  moment and the Home line goes at the same moment. The checklist page stays reachable from Settings.
- **Order, no fold:** the checklist draws all six steps in the spec's step order (repo, profile, recipes, mcp, map, onyx),
  each with its state, so a failed or needs-you step is never hidden. Rev 4's "three rows filled by priority" and "and N
  more" are dropped: they existed to keep a Home card above the 1280 by 800 fold, and Home now carries one line. The UI
  spec's Home fold rule is amended (SO-19): Home's Setup line is one row, so Needs you starts above the fold. A UI fixture
  has three `running` steps and a failed Onyx, and checks that all six rows are drawn on the page and that Home shows only
  the line.
- **One approval, one row:** the server always emits the `setup:*` needs-you rows (it cannot know the client's layout,
  §7.3). **While any step is open, the Home view hides every `setup:*` row**, because the Home line replaces them; the
  Needs you page and the tray keep them all, and the tray badge counts each once. When no step is open there is no `setup:*`
  row to show. The checklist links the `profile:drift` row rather than copying it.
- **Needs its own look:** a previously approved recipe that gained a capability (§5.3) is listed under that heading, one
  line each, each opening the existing recipe approve flow (`ApproveRecipe`, typed, §4).
- **Onyx:** a not-started Onyx is `todo` with Set up Onyx, and its line says "Do this before you approve the profile to
  save one approval". `skipped` reads "Onyx isn't set up" with Set up Onyx in the checklist's menu. Docker missing is `skipped`
  with the reason "Docker isn't installed" and a link to the install doc (`SND-ONYX-001`); it is finished like any `skipped`
  step, and installing Docker reopens it as `todo`. When Onyx changed the profile after it was approved, the checklist says "Onyx added a section to your profile. Approve the profile once more."
- **The Onyx form:** each field has a visible label ("Onyx email", "Onyx password"); autofill and paste are allowed; the
  password has a show/hide toggle; focus goes to the first field when the form opens and back to the button when it
  closes; `SND-SETUP-005` and `SND-SETUP-006` show under the field and are announced.
- **Loading:** a six-row skeleton on the checklist page; Home shows no line until the answer arrives. **Rebuilding or stalled views:** the existing `asOf` banners. **Sindri is
  restarting:** "Sindri is starting" until the next answer.
- **Sindri isn't running:** derived client-side when `GET /setup` fails to connect: the checklist says "Sindri isn't
  running", shows the launcher command (`sindri-app`, the daily path after a reboot) with a **copy** action, and says "The app reconnects on its own
  when Sindri starts." There is **no Start button**: the app never starts the control plane from inside the window (UI
  §6.5). No server means no `setup` events, so nothing else is shown.
- **Several repos:** a step that counts repos carries `items` (one line per repo with its own state), so "which repo
  failed" is answered; an `action` on a `done` step is labelled "again" or "another".
- **Accessibility:** UI §11 applies. One polite live region announces a change ("Recipes: done"); after a step
  completes, focus stays where it was; the hold and the forms follow §4 and UI §11. A `setup` row raises no OS
  notification, on purpose (only `notify.desk` does, X50); the tray shows a count badge only.

## 6. Fixed words: the step states

| State | Meaning (plain) |
|---|---|
| `todo` | Not started, or waiting on an earlier step (the `why` says which). A step that waits has `action: null`, so no button fails when pressed; a not-started Onyx is `todo` too, but waits for nothing and keeps its action |
| `running` | Sindri is working; `progress {done, total}` says how far |
| `needs-you` | Only the owner can go on; `action` is the button |
| `done` | Finished; `detail` says what it did |
| `failed` | It stopped; `error {code, sentence}` and the fix are in plain words |
| `skipped` | Only `onyx` has it: the owner chose "Not now", or Docker isn't installed (the reason is in `why`, the link in `detail`). It counts as finished, and `action` (`SetupOnyx`) stays available from the checklist's menu when the owner chose it |

A step never moves backwards by itself except when its inputs change (a new repo, a new build).

**The inputs of each step** (the view reads them on request; the service only watches them and emits `setup.step.changed`):

| Step | Reads |
|---|---|
| `repo` | the approved profile snapshot and the live profile file; `repo.added`, `repo.onboarded` |
| `profile` | the live and approved profile hashes; `profile.checked` |
| `recipes` | the build's embedded set, the registry, the approvals (`recipe.registered`, `recipe.approved`), the approved profile, the running `ApproveAllRecipes` job |
| `mcp` | `mcp.registered` (with its port), `setup.consent.given`, the running port, the approved snapshot |
| `map` | `context.map.*`, `setup.consent.given`, the `RefreshContextMap` jobs, each repo's git heads |
| `onyx` | the Docker probe and preflight, `control.json`, `onyx.deferred`, the running `SetupOnyx` job, the Onyx service state |

`views rebuild` therefore rebuilds the event-fed parts; the rest is read live, so a rebuild cannot make a step flicker.
Dependencies, stated once: `repo` has none; `profile` waits for a repo (no profile file is written before one exists, so
there is no drift row either); **`recipes` waits for the approved profile**: the shipped recipes still register at start,
but the step is `todo` with `action: null` and "After the profile is approved" until then, because every approval check
uses the approved profile's allowlist; `mcp` and `map` wait for an approved profile that names the repo, and on a box
whose profile pre-dates this build also for `setup.consent.given` (§5.4); `onyx` waits for nothing but Docker, and
pressing it before a repo exists leaves the profile `todo`.

## 7. The contract

**Rev 5: this section is unchanged.** Moving the checklist under Settings and reducing Home to one line is layout, and the API carries no layout: `GET /sindri/setup`, the routes, the needs-you rows the server emits, the events, the error codes, the action ids and the goldens of §8 are as in rev 4.

### 7.1 Profile

One new optional key, left out of the canonical JSON when absent (existing approvals keep their hash):
`setup: { mcp: auto | off }` (default `auto`). `off` stops the automatic MCP registration of §5.4; the button still
works. `docs/csharp/sindri-profile.md` documents it (`check-profile-doc.mjs` enforces that).

### 7.2 Routes (SO-1, SO-2)

| Route | Slice | Class | Answer |
|---|---|---|---|
| `GET /setup` | `GetSetup` | query | `SetupView` (below) |
| `POST /setup/repos` | `AddRepo` | desk | checks 400/409, then 202 `JobStarted(accepted, JobId, links)` |
| `POST /setup/mcp` | `RegisterMcp` | desk | checks 409, then 202 `JobStarted` |
| `POST /onyx/setup` | `SetupOnyx` | desk | 202 `JobStarted` |
| `POST /onyx/account` | `ConnectOnyx` | desk | 202 (the same job); the password is a secret type held in a single-use slot (§5.6) |
| `POST /onyx/defer` | `DeferOnyx` | desk | 200 `{deferred}`; the step becomes `skipped` or back |
| `POST /workflow/challenges/app` | `IssueAppChallenge` (api) | desk, `human` token only | the challenge of §4 |
| `GET /workflow/recipes/approve-all` | `GetApproveAllPlan` (engine) | query | `ApproveAllPlan` |
| `POST /workflow/recipes/approve-all` | `ApproveAllRecipes` (engine) | **protected** (app: held or typed) | 202 `JobStarted`; the job's result is `ApproveAllResult` |

`approve-all` has no `@`, so it cannot be read as a `{id}@{hash}` route; a route test pins that. All routes sit under
`/sindri`. `JobStarted` is the existing answer (`links {job, cancel, events}`). A code raised inside a job is the job's
failure code, not an HTTP status; only the checks that run before the job is claimed answer 400/409.

**`SetupView`:**

```json
{
  "steps": [
    {
      "id": "repo",
      "title": "Add your repo",
      "state": "todo|running|done|failed|needs-you|skipped",
      "why": "plain sentence: why this step is in this state",
      "detail": "plain sentence: what it did or is doing, or null",
      "error": { "code": "SND-SETUP-001", "sentence": "plain sentence with the fix" },
      "action": "AddRepo",
      "progress": { "done": 1, "total": 3 },
      "items": [ { "repo": "agentic-workflow", "state": "done", "detail": "Claude Code, Codex" } ]
    }
  ],
  "asOf": { }
}
```

- `steps` is always six entries in the order `repo`, `profile`, `recipes`, `mcp`, `map`, `onyx`.
- `error` is `null` unless `state` is `failed`, or `needs-you` after a failure. `action` is a catalog action id or
  `null` (it is `null` while the step waits on another). `progress` is `null` unless the step counts something. `items` is
  `[]` unless the step counts repos (`{repo, state, detail}`) or lists recipes that need their own look (`{recipe, state:
  "needs-you", detail}`). `skipped` is only ever `onyx`'s.
- `asOf` is the views' `asOf` object (API §2.3), as every view carries.
- Titles are fixed: Add your repo · Approve the profile · Approve the recipes · Connect your agents · Build the map ·
  Set up Onyx. A client may show them or its own. Each has a one-line gloss in the checklist: "Set up your repos", "Allow
  Sindri's workflows" and so on (the glosses are the app's text, UI §12.4); paths go in a secondary line and commands only in
  `cli`.

**`ApproveAllPlan`:** `{recipes: [{id, displayName, version, hash, previousApprovedHash, change: "new"|"updated"|"second-look",
changeWord, capabilities: [{id, sentence}], delta: {added: [{id, sentence}], removed: [{id, sentence}]}, changedBlocks: [id],
children: [id]}], ownApproval: [{id, displayName, reason: "gains-capability"|"waits-for-child", sentence, added:
[{id, sentence}]}], digest, empty, asOf}`, in the order approval will happen (children first). `previousApprovedHash` is `null`
for a never-approved recipe, which is `new` and carries its full capability set. There is no `evals` field: the shipped
recipes passed theirs at publish (§5.3). `ownApproval` holds only a previously approved recipe whose new hash adds a
capability, or one that waits on such a recipe. `capabilities`, `delta` and `added` are computed from `RecipeCapabilitySet` (§5.3), so an `id` is a block descriptor id, an effect kind or a capability literal. `digest` is the value of §5.3; the challenge is `approve-all <digest6>`.
`empty` is the sentence when `recipes` and `ownApproval` are both empty, else `null`. Before a profile is approved the
plan answers `SND-SETUP-007`. **`ApproveAllResult`** (the job's result): `{approved: [{id, displayName, hash}], refused:
[{id, hash, code, sentence, cli}], skipped: [{id, displayName, because}]}`. `refused` holds each recipe the checks
refused; `skipped` holds each recipe left because a recipe it waits on (`because`) was refused or skipped. The job
carries on with every other recipe. The app re-reads the plan afterwards.

### 7.3 Needs-you (SO-5)

A new kind **`setup`**. Its row id is **`setup:<step>`**. `NeedsYouRowId` accepts `setup:repo`, `setup:recipes` and
`setup:mcp` only: the profile uses `profile:drift`, the map uses `failure:service:context-map-refresh`, and Onyx uses the
existing `failure:service:onyx`.

| Field | Value |
|---|---|
| `kind` | `setup` |
| `subject` | `{setup, <step>}` |
| `title`, `reason` | the step's `title` and its `error.sentence` or `why` |
| `urgency` | `pulse` when `needs-you`, `colour` when `failed` |
| `primaryAction` | the step's `action` (`AddRepo`, `ApproveAllRecipes`, `RegisterMcp`) |
| `cli` | the terminal fallback: the CLI form of the action |
| Cleared when | the step is `done` or `running` |

The server always emits these rows; the Home view hides them all while any step is open, because the Home line replaces them (§5.7). Onyx is the one step that is checklist-only until it fails or waits for the login (it has no `setup:onyx` row; the existing `failure:service:onyx` row carries it). Rows order by urgency, then oldest first (API §3.6). A `setup` row raises no OS notification (notifications come only
from `notify.desk`, X50).

### 7.4 Catalog action ids (SO-3)

| Action id | Class | Callers | Confirm | Route |
|---|---|---|---|---|
| `AddRepo` | desk | human (cli, app) | — | `POST /setup/repos` |
| `ApproveProfile` | protected | human + terminal, **human app** | typed | `POST /profile/approve` |
| `ApproveRecipe` | protected | human + terminal, **human app** | typed | `POST /workflow/recipes/{id}@{hash}/approve` |
| `ApproveAllRecipes` | protected | human + terminal, **human app** | **held** (typed accepted) | `POST /workflow/recipes/approve-all` |
| `RegisterMcp` | desk | human; `service:setup` through `ServiceGrants` | — | `POST /setup/mcp` |
| `SetupOnyx` | desk | human (cli, app) | — | `POST /onyx/setup` |
| `ConnectOnyx` | desk | human (cli, app) | — | `POST /onyx/account` |
| `DeferOnyx` | desk | human (app) | — | `POST /onyx/defer` |
| `RefreshContextMap` | job | human; `service:context-map-refresh` | — | `POST /context/map/refresh` (context lane) |

Job kinds are the action ids (`AddRepo`, `RegisterMcp`, `SetupOnyx`, `ApproveAllRecipes`, `RefreshContextMap`), PascalCase
as `JobKind` requires. Job keys are valid `JobKey`s (lowercase, at most 80): `AddRepo` and `RegisterMcp` use a 16-hex
sha256 prefix (`mcp-` before the second), never a path or a repo name; `ApproveAllRecipes` uses the fixed `approve-all`. The three `protected` rows are the only rows with
the `AppConfirm` flag (§4 item 7).

CLI forms (the fallback; they ship with their units, A3, A4, A5, E3): `sindri-app repo add <path>`,
`sindri-app profile approve <hash>`, `sindri-app recipe approve-all`, `sindri-app mcp register --apply`, `sindri-app onyx up`. They
call the same routes. `sindri-app recipe approve-all` asks the typed `approve-all <digest6>` at the terminal.

### 7.5 Events (SO-4)

| Type | Payload (besides `commandId`) | Key | Public | Writer |
|---|---|---|---|---|
| `setup.step.changed` | `id`, `state`, `code?` | `setup.step.changed:<id>:<state>:<n>`, n = the step's count of earlier change events | yes | `service:setup` (only when the derived state differs from the last event of that step) |
| `repo.added` | `repo` (the path is non-public) | `repo.added:<commandId>` | yes | `AddRepo` |
| `repo.onboarded` | `repo`, `approvedHash` | `repo.onboarded:<repo>:<hash>` | yes | `service:setup` |
| `recipe.registration.completed` | `registered`, `unchanged`, `failed` (counts) | `recipe.registration.completed:<epoch>` | yes | `service:setup` |
| `recipe.approved` (existing) | gains `batch?` (the approve-all `commandId`), `planDigest?`, `heldMs?` | as today | yes | `ApproveRecipe`, `ApproveAllRecipes` |
| `mcp.registered` | `repo`, `port`, `providers: [{provider, outcome}]` | `mcp.registered:<commandId>` | yes | `RegisterMcp` |
| `setup.consent.given` | `steps: ["mcp","map"]`, `profileHash` | `setup.consent.given:<profileHash>` | yes | `ApproveProfile` (this build) or the "Turn on for your repos" press (`RegisterMcp`) |
| `onyx.deferred` | `deferred` (bool) | `onyx.deferred:<commandId>` | yes | `DeferOnyx` |
| `context.map.refreshed` | `version`, `heads{repo: sha}`, `accepted`, `removed`, `dropped`, `cleanup` | context lane's | yes | `context-map-refresh` |

The event catalog's `invalidates` lists (UI E-15) add: `setup.step.changed`, `repo.added` and `mcp.registered` re-query
`setup`; `recipe.registration.completed` re-queries `setup` and the recipe list. Step durations come from
`setup.step.changed`. Two metrics (`Aw.Kernel.Telemetry`, no new ledger event) are computed by `service:setup` from the
ledger: `setup.done` (the app's first open to the moment every step is `done` or `skipped`, the same moment the Home line goes) and `setup.first-item` (recorded on the first `workflow.instance.started` after `setup.done`, carrying the minutes between them), so the proof and the checklist can report time to done and time to first value.

### 7.6 Errors (SO-8)

Checked against `main` and PR #433's branch on 2026-10-19 (`SND-SETUP-001…007`): `SND-SETUP-*` is a new family; `SND-RECIPE-011` is the next free
recipe code (both have `SND-RECIPE-010` as the last); #433 owns `SND-CI-001…010` and `SND-PROFILE-017…019`, which this spec
does not use. The phase that reserves a code re-greps before merging.

| Code | Kind (status) | When | Fix line |
|---|---|---|---|
| `SND-SETUP-001` | Invalid (400) | The folder is missing, is not a git checkout, is unreadable, is inside the Sindri home, is `/`, is your home folder, or contains the Sindri home | Pick the repo's top folder |
| `SND-SETUP-002` | Conflict (409) | The repo is already in the profile, or its name collides with another repo's | Pick another folder, or give a different name |
| `SND-SETUP-003` | Forbidden (403) | The app's confirmation was not given: a hold shorter than the minimum, an answer less than 2 seconds after the challenge, or the wrong kind of answer | For a held challenge: hold the button for the full time, or type the text. For a typed one: type the text exactly (the line follows the challenge's `confirm`) |
| `SND-SETUP-004` | Conflict (409) | `RegisterMcp` for a repo the approved profile does not name | Add the repo, approve the profile, then try again |
| `SND-SETUP-005` | Invalid (400) | `ConnectOnyx` without the Onyx admin email and password | Enter both; sign up in Onyx first if you have no account |
| `SND-SETUP-006` | Conflict (409) | `ConnectOnyx` while no Onyx setup is waiting for the login, or the 30-minute wait ran out (the job then fails with this sentence) | Press Set up Onyx first |
| `SND-SETUP-007` | Conflict (409) | The approve-all plan or its challenge is asked for before a profile is approved | Approve the profile first |
| `SND-RECIPE-011` | Conflict (409) | A recipe is not one this build ships (its `id@hash` is not in the embedded set): approve-all names it | Approve it on its own, after reading its preview |

`SND-RECIPE-009` is reused for a plan that changed between the preview and the approval (engine lane, agreed).
`SND-AUTH-002` is the refusal for a session on every action above.

### 7.7 Views that name a recipe (SO-9)

`displayName` is on `RecipeView`, each `RecipeSummary` row, and wherever an instance or item view names its recipe
(`recipe: {id, version, hash, displayName}`). It is required on a recipe file (`RCP-DISPLAY-001`, rule id chosen
because `RCP-NAME-002…006` already name the instance-name template). It is plain text, at most 60 characters, no
control characters, and is scrubbed like other agent-visible text.

## 8. Golden files and examples

Each is `Golden/Api/<path>`; one body each. Hashes and ids are shortened. **Thirteen bodies:** eight `setup/…` and five
`recipes-approve-all/…`.

**`setup/all-done.json`**
```json
{"steps":[
 {"id":"repo","title":"Add your repo","state":"done","why":"A repo is in your approved profile.","detail":"agentic-workflow","error":null,"action":"AddRepo","progress":null,"items":[{"repo":"agentic-workflow","state":"done","detail":"Claude Code, Codex and Cursor"}]},
 {"id":"profile","title":"Approve the profile","state":"done","why":"Your profile is approved.","detail":"Approved 3f0c9a.","error":null,"action":null,"progress":null,"items":[]},
 {"id":"recipes","title":"Approve the recipes","state":"done","why":"Every recipe that ships with Sindri is approved.","detail":"26 recipes.","error":null,"action":null,"progress":{"done":26,"total":26},"items":[]},
 {"id":"mcp","title":"Connect your agents","state":"done","why":"Sindri is registered in your repos.","detail":"Claude Code, Codex and Cursor in agentic-workflow. Codex will ask you to trust this folder once.","error":null,"action":null,"progress":null,"items":[]},
 {"id":"map","title":"Build the map","state":"done","why":"Every repo has a current map.","detail":"Map v3, built at 8597ecc.","error":null,"action":null,"progress":{"done":1,"total":1},"items":[]},
 {"id":"onyx","title":"Set up Onyx","state":"done","why":"Onyx is running and Sindri can search it.","detail":"Onyx v4.9.0 on 127.0.0.1:3000.","error":null,"action":null,"progress":null,"items":[]}],
 "asOf":{"projectedAt":"2026-10-19T09:12:00Z","cursor":5120,"ledgerHead":5120,"rebuilding":false,"stalled":false,"stalledAt":null,"stalledType":null,"text":null}}
```

**`setup/first-run.json`** (a fresh box: the app has just opened; `recipes` is `todo` with `action: null` until a profile is approved; `onyx` is `todo`, not started, and keeps its action)
```json
{"steps":[
 {"id":"repo","title":"Add your repo","state":"needs-you","why":"Pick the folder of a repo to start.","detail":null,"error":null,"action":"AddRepo","progress":null,"items":[]},
 {"id":"profile","title":"Approve the profile","state":"todo","why":"Waits for your first repo.","detail":null,"error":null,"action":null,"progress":null,"items":[]},
 {"id":"recipes","title":"Approve the recipes","state":"todo","why":"After the profile is approved.","detail":"Registered 26 of 26.","error":null,"action":null,"progress":null,"items":[]},
 {"id":"mcp","title":"Connect your agents","state":"todo","why":"Waits for an approved profile with a repo.","detail":null,"error":null,"action":null,"progress":null,"items":[]},
 {"id":"map","title":"Build the map","state":"todo","why":"Waits for a repo.","detail":null,"error":null,"action":null,"progress":null,"items":[]},
 {"id":"onyx","title":"Set up Onyx","state":"todo","why":"Docker is ready. Set up Onyx when you want search. Do this before you approve the profile to save one approval.","detail":null,"error":null,"action":"SetupOnyx","progress":null,"items":[]}],
 "asOf":{"...":"..."}}
```

**`setup/running-progress.json`** (the recipes step while the `ApproveAllRecipes` job runs; the others shortened with `...` here; the file holds all six)
```json
{"steps":[
 {"id":"repo","state":"done","...":"..."},
 {"id":"profile","state":"done","...":"..."},
 {"id":"recipes","title":"Approve the recipes","state":"running","why":"Sindri is approving your recipes.","detail":"Approving Review loop.","error":null,"action":null,"progress":{"done":14,"total":26},"items":[]},
 {"id":"mcp","title":"Connect your agents","state":"running","why":"Sindri is registering itself in agentic-workflow.","detail":null,"error":null,"action":null,"progress":{"done":0,"total":1},"items":[]},
 {"id":"map","title":"Build the map","state":"running","why":"Sindri is mapping agentic-workflow.","detail":"Reading the code.","error":null,"action":null,"progress":{"done":0,"total":1},"items":[]},
 {"id":"onyx","state":"todo","...":"..."}],
 "asOf":{"...":"..."}}
```

**`setup/map-retrying.json`** (the `map` step after its first failure)
```json
{"steps":[{"id":"repo","state":"done","...":"..."},{"id":"profile","state":"done","...":"..."},{"id":"recipes","state":"done","...":"..."},{"id":"mcp","state":"done","...":"..."},
 {"id":"map","title":"Build the map","state":"running","why":"Sindri is mapping agentic-workflow.","detail":"Retrying in 5 minutes: the code index was busy.","error":null,"action":"RefreshContextMap","progress":{"done":0,"total":1},"items":[]},
 {"id":"onyx","state":"todo","...":"..."}],"asOf":{"...":"..."}}
```

**`setup/failed.json`** (the `repo` step; the other five follow it)
```json
{"steps":[{"id":"repo","title":"Add your repo","state":"failed","why":"The last folder you picked could not be added.","detail":null,
  "error":{"code":"SND-SETUP-001","sentence":"That folder is not a git repo. Pick the top folder of a repo."},
  "action":"AddRepo","progress":null,"items":[]},{"id":"profile","...":"..."}],"asOf":{"...":"..."}}
```

**`setup/profile-needs-approval.json`** (the `profile` step)
```json
{"steps":[{"id":"repo","state":"done","...":"..."},
 {"id":"profile","title":"Approve the profile","state":"needs-you","why":"You added agentic-workflow. Sindri waits for you to approve the profile before it uses it.","detail":"1 change: repos[agentic-workflow] added.","error":null,"action":"ApproveProfile","progress":null,"items":[]},
 {"id":"recipes","state":"todo","...":"..."}],"asOf":{"...":"..."}}
```

**`setup/onyx-skipped.json`** (everything else done; the owner chose Not now, so the Home line goes and `setup.done` is emitted)
```json
{"steps":[{"id":"repo","state":"done","...":"..."},{"id":"profile","state":"done","...":"..."},{"id":"recipes","state":"done","...":"..."},{"id":"mcp","state":"done","...":"..."},{"id":"map","state":"done","...":"..."},
 {"id":"onyx","title":"Set up Onyx","state":"skipped","why":"You chose to set up Onyx later.","detail":"Onyx isn't set up. Use Set up Onyx in the checklist's menu when you want search.","error":null,"action":"SetupOnyx","progress":null,"items":[]}],
 "asOf":{"...":"..."}}
```

**`setup/onyx-no-docker.json`** (everything else done; Docker isn't installed, so Onyx is `skipped` with the reason and a link, which finishes setup)
```json
{"steps":[{"id":"repo","state":"done","...":"..."},{"id":"profile","state":"done","...":"..."},{"id":"recipes","state":"done","...":"..."},{"id":"mcp","state":"done","...":"..."},{"id":"map","state":"done","...":"..."},
 {"id":"onyx","title":"Set up Onyx","state":"skipped","why":"Docker isn't installed.","detail":"Install Docker to set up Onyx: https://docs.docker.com/get-docker/","error":null,"action":null,"progress":null,"items":[]}],
 "asOf":{"...":"..."}}
```

**`recipes-approve-all/plan.json`** (`workflow.ship` is in `ownApproval` because its new hash adds the descriptor id `step.ship.open` to a previously approved recipe's `RecipeCapabilitySet`, although the six `Capability` flags did not change)
```json
{"recipes":[
 {"id":"workflow.step","displayName":"One step","version":2,"hash":"1a2b3c4d5e6f","previousApprovedHash":"0f1e2d3c4b5a","change":"updated","changeWord":"Updated","capabilities":[{"id":"step.produce","sentence":"Starts an AI agent session in a repo you added"},{"id":"effect.notify.desk","sentence":"Puts a question or notice in your Needs you list"}],"delta":{"added":[],"removed":[]},"changedBlocks":[],"children":[]},
 {"id":"workflow.item","displayName":"Work item","version":3,"hash":"9f8e7d6c5b4a","previousApprovedHash":null,"change":"new","changeWord":"New","capabilities":[],"delta":{"added":[],"removed":[]},"changedBlocks":[],"children":["workflow.step"]}],
 "ownApproval":[{"id":"workflow.ship","displayName":"Ship a change","reason":"gains-capability","sentence":"Ship a change can now open a pull request. Approve it on its own after reading it.","added":[{"id":"step.ship.open","sentence":"Opens or updates a pull request and reads its checks"}]}],
 "digest":"c41e07","empty":null,"asOf":{"...":"..."}}
```

**`recipes-approve-all/plan-first-approval.json`** (a fresh box, a profile just approved: a never-approved recipe with a `step.ship.*` capability is `New` in the hold with its full capability set; `ownApproval` is empty because it has no earlier approval to gain from)
```json
{"recipes":[
 {"id":"workflow.ship","displayName":"Ship a change","version":2,"hash":"5d4c3b2a1f0e","previousApprovedHash":null,"change":"new","changeWord":"New","capabilities":[{"id":"step.ship.open","sentence":"Opens or updates a pull request and reads its checks"},{"id":"effect.notify.desk","sentence":"Puts a question or notice in your Needs you list"}],"delta":{"added":[],"removed":[]},"changedBlocks":[],"children":[]}],
 "ownApproval":[],"digest":"7b20de","empty":null,"asOf":{"...":"..."}}
```

**`recipes-approve-all/applied.json`** (the finished job's result)
```json
{"approved":[{"id":"workflow.step","displayName":"One step","hash":"1a2b3c4d5e6f"},{"id":"workflow.item","displayName":"Work item","hash":"9f8e7d6c5b4a"}],"refused":[],"skipped":[]}
```

**`recipes-approve-all/refused.json`** (one refusal: its dependent is skipped, the rest are approved)
```json
{"approved":[{"id":"workflow.step","displayName":"One step","hash":"1a2b3c4d5e6f"},{"id":"workflow.note","displayName":"Note","hash":"2c3d4e5f6a7b"}],
 "refused":[{"id":"workflow.item","hash":"9f8e7d6c5b4a","code":"SND-RECIPE-009","sentence":"Work item changed while Sindri was approving. Read the new plan.","cli":"sindri-app recipe approve <id>"}],
 "skipped":[{"id":"workflow.review","displayName":"Review loop","because":"workflow.item"}]}
```

**`recipes-approve-all/empty.json`** (the plan when nothing waits)
```json
{"recipes":[],"ownApproval":[],"digest":"000000","empty":"Every recipe is approved.","asOf":{"...":"..."}}
```

Each golden is a test input for the API (serialization) and the UI (fixtures); the UI's contract tests read them, and
`Aw.Sindri.Ui.Tests` pins the catalog (the api lane's gate for P1 and P2 runs it, as the UI lane's note requires).

## 9. The setup commands

| Command | What it does | Changes in this spec |
|---|---|---|
| `./setup.sh --providers … --with-sindri` | Installs Sindri's dependencies and tools, publishes `sindri-app`, then opens the app | **Order:** `setup.sh` runs the `sindri-app` publish **right after the provider installs and `install-deps.sh`** (build tools, and the NativeAOT prerequisites, clang and the zlib development headers, which `scripts/install-deps.sh` already installs) and **before the scorer, judge, Serena image and TypeScript builds**, so the app can open while the slow steps finish. It uses `sindri-app --publish-only` (SO-A7a). It is reported on its own line with a time hint ("Publishing the Sindri app, about 5 to 10 minutes; it may download the .NET SDK") and its own failure sentence ("The Sindri app could not be published; see `$AW_STATE_DIR/cache/sindri-app-build/publish.log`"). **A failed publish stops setup.sh with exit 1.** `--dry-run` prints "would run sindri-app --publish-only". Then setup.sh **starts the app detached with this exact line:** `AW_SINDRI_APP_NO_MCP=1 nohup sindri-app </dev/null >>"$AW_STATE_DIR/logs/sindri-app.log" 2>&1 &` (no terminal, so no Onyx offer and no register hint; the log is created 0600 and scrubbed by `Aw.Redaction`), **only when a desktop session exists**: on Linux, `DISPLAY` or `WAYLAND_DISPLAY` set and `SSH_CONNECTION` unset; always on macOS. It then polls `sindri-app status --json` for up to 60 seconds (any answer, exit 0 or 1, counts; exit 2 or no answer does not) and prints "Sindri is running" or "Sindri didn't start, see `<log path>`"; **a failed start makes setup.sh exit 1.** `serve` outlives the terminal; `sindri-app stop` stops it. On a re-run with an older build running, setup.sh prints "Sindri is running an older build: run `sindri-app stop`, then `sindri-app`" and does not replace it. With no desktop session setup.sh prints `sindri-app --headless` with the launcher's absolute path (not a bare `sindri-app` that may not be on `PATH`), and §2 counts that as a second typed command. **From A7b on** it no longer runs Onyx by default or prints the MCP register command; `--with-onyx` restores the Onyx run (Q9); `--no-onyx` is accepted, prints one line ("Onyx is a step in the app now; this flag does nothing") and changes nothing; it ends with "Still to do in the app: Set up Onyx". **In A7a** it still runs `sindri-app onyx up` (against the detached control plane) and prints the MCP register command (`sindri-app mcp register --apply --providers all`, run inside a repo) in its closing summary, because A4, A5 and U1 have not merged. It still builds the TypeScript `sindri` until the switch; the time estimate says so |
| `sindri-app --publish-only` | Publishes `sindri` and `sindri-ui` into `$AW_STATE_DIR/bin/sindri-app/<stamp>/` when the source changed, stages `git archive HEAD recipes/sindri` into the build copy and embeds it (§5.3), runs `sindri-app recipe eval --shipped` with the published binary when the embedded set changed since the last publish, then returns | New (SO-A7a). `--publish-only` is a leading flag (it may be followed by `--rebuild`); with it any other argument is exit 2, and without it the script's other arguments go to sindri as today. It never execs the control plane and never prints the register hint. Exit 0 published or current; exit 1 a publish or an eval failed. The stamp includes the commit and the git tree hash of `recipes/sindri/` at `HEAD`, so a recipe-only commit republishes |
| `sindri-app onyx up` | Onyx setup at a terminal: the stack, your account, Sindri's key, the profile section | Unchanged: the terminal fallback. Not at a terminal, it prints the command instead of asking; the app's **Set up Onyx** does the same job |

**Exit codes of setup.sh.** 0 when every install step ran, even when the summary lists "Still to do" items or a step the
owner skipped (printed as skipped); 1 when a publish, the app's start or an install step failed (the summary names it).

**With no desktop session** the same setup finishes with the CLI forms (§7.4): `sindri-app --headless` (start, in a second
terminal because it stays in the foreground), `sindri-app repo add <path>`, `sindri-app profile approve <hash>` (typed),
`sindri-app recipe approve-all` (typed) and `sindri-app onyx up` (in the setup terminal). MCP and the
map still run by themselves. The forms ship with their units (A3, A5, E3); until then a box with no desktop is not
covered, and the app is still the owner's rule, not a requirement.

**Re-running `setup.sh` is safe.** It re-checks the installs (the npm builds and the Serena image no-op when current),
re-publishes `sindri-app` only when the source changed (the stamp check), never replaces a running `serve` (it declines and prints the older-build line)
and runs Onyx only with `--with-onyx`. Its summary lists what is still to do and what failed, separately.

Daily start (`sindri-app`, bare) is not setup. On a box where Onyx is not set up it shows the Onyx step on the Setup checklist
and the existing `failure:service:onyx` row. **From A7b on**, bare `sindri-app` prints no MCP register hint and skips its
Onyx offer when the Setup checklist owns that step (`docs/csharp/sindri.md` is amended with A7b, §11).

## 10. Folding in PR #433 (CI apply spec): clause by clause

#433 lets a merged, checked PR approve recipes and promote maps from CI/CD (`ci-apply`, class `ci`). Setup once removes
most of what #433 needed a CI for on a fresh box.

| #433 clause | Verdict | Why |
|---|---|---|
| §1 and §3.2 `ci-apply` service, mirror, cursor, candidates, checks, events (`ci.*`) | **Keeps**, unchanged, for recipes only | They are about recipe files merged in a repo; nothing here touches them. It is not part of the setup: it needs a forge token, mergers and a workflow, which is more setup, not less |
| §3.1 profile `ci:` block, `SND-PROFILE-017…019` | **Keeps** | No collision: this spec's profile key is `setup:` and its codes are `SND-SETUP-*` |
| §3.4 recipe approve with the capability ceiling | **Keeps** for owner-written recipes in a repo. **Conflicts** only where it says a recipe id's first approval is at the terminal: it becomes "in the app (`ApproveRecipe`, typed) or at the terminal". Shipped recipes are not CI's: they come through approve-all (§5.3) | The shipped recipes are the installed build's, approved by the owner's hold, not by a merged PR of this repo's `recipes/sindri/` |
| §3.5 map promote from CI (`promote` entries, build at a commit over the mirror, `SND-CI-008`, CI-P5) | **Supersedes** | `RefreshContextMap` builds and promotes on a default-branch move with no forge, no checks and no mergers (§5.5). CI-P5 is dropped; `promote:` entries are removed from the `ci:` block's schema, and `SND-CI-008` is retired |
| §3.5 "model proposals are left out of a CI promote" (O4) | **Conflicts** | The auto map's cleanup agent decides model proposals (accept at 0.75, ask the model below, reject if it cannot), so a promoted map can hold agent decisions. The agent runs read-only with a removal ceiling (§5.5). The owner decided: the agent decides (Q3) |
| §3.6 map import from CI (CI-P8) | **Keeps**, deferred, outside setup | An authored map file is not what an auto map is. Still waits on its own questions (#433 O2) and the switch |
| §4 class `ci`, `ServiceGrants`, separate request types, no route | **Keeps** | `service:setup` uses `ServiceGrants` on `RegisterRecipe` and `RegisterMcp` the same way, with no new class; the auto map's promote is the owner rule of §5.5, not a `ci` command |
| §6.2 needs-you kind `failure` with `failure:ci:*`, `RetryCiApply`, the optional `link` field | **Keeps** | Different ids from `setup:<step>`; the `link` field is also available to setup rows |
| §6.2 `cli` per code (`sindri recipe approve <id@hash>` for `SND-CI-007`) | **Conflicts, narrowly** | A row's `primaryAction` for `007` becomes `ApproveRecipe` (ready in the app); the `cli` stays as the fallback |
| §6.1 and §6.2 System `ci:apply` node and `GET /ci` | **Keeps** | Independent |
| §7 credential | **Keeps** | Independent |
| §8 "what stays human" table | **Amended** | Profile approve and recipe approvals stay human but are now app-confirmable (§4); revoke, grants, accept and manual map promote stay terminal |
| §9 self-hosted runner alternative | **Keeps** (an alternative, unchanged) | |
| §10 edits to API §3.7, §4.1, §4.2, §6 (CI-1…CI-4) | **Keeps; textual overlap** | The two PRs both edit API §4.1/§4.2/§6 and §3.6. Whichever merges second rebases; this PR's edits are additive and do not alter CI-1…CI-4 text |
| §11 O1 to O7 | **O4 interacts** (above); the rest unchanged | |
| §13 CI-P1…CI-P8 | CI-P5 **dropped**; CI-P1…P4, P6, P7 and P8 unchanged | |

#433's own §12 findings (rounds 1 to 3) are about the pull model, the ceiling and the cursor; none is reopened. This PR
should add one line to #433's body: "its map-promote half is replaced by the setup-once spec."

## 11. Edits this amendment makes

| File | Section | Change | Mark |
|---|---|---|---|
| API surface spec | revision note, §3.1 | `GET /setup` row, with `items` in the step shape | SO-1 |
| API surface spec | §3.2 | `AddRepo`, `RegisterMcp`, `SetupOnyx`, `ConnectOnyx`, `DeferOnyx`, `ApproveAllRecipes` rows and the app challenge route; the `RegisterMcp` job key | SO-2 |
| API surface spec | §3.3 | the `confirm` column and the three rows' status | SO-3 |
| API surface spec | §3.1, §3.4 | the `skipped` state; the events of §7.5 (with `setup.consent.given`, `onyx.deferred`) | SO-4 |
| API surface spec | §3.6 | kind `setup`, id `setup:<step>`, the `profile` row's `primaryAction` | SO-5 |
| API surface spec | §4.1, §4.2 | `protected` has a second channel for three actions, by a per-row `AppConfirm` flag (`Decide` lets `Who.App` through; the handler redeems before any write); `service:setup` and the map service | SO-6 |
| API surface spec | §6 "Protected actions in the UI" | rewritten for §4 | SO-7 |
| API surface spec | §9 | the codes of §7.6 (`SND-SETUP-001…007`) | SO-8 |
| API surface spec | §3.8 | `displayName` on the recipe and instance views | SO-9 |
| API surface spec | §3.5 (job view) | the job state `waiting` (the Onyx wait on the signal `onyx-account`; no job slot while it waits) | SO-23 |
| Engine spec | §3.2 | `displayName` field, `RCP-DISPLAY-001` and the version rule | SO-10 |
| Engine spec | §4.1, §4.2 | registration at start from the embedded set (no manifest file), `recipe manifest <dir>` and `recipe eval --shipped`, `RecipeCapabilitySet`, approve-all as a job that skips and carries on with the fixed key `approve-all`, `ApproveShippedRecipe` reachable only from the job, the app channel | SO-11 |
| Engine spec | §6.2 | class table: `protected` rows in the app | SO-12 |
| Engine spec | §8 | `SND-RECIPE-011` | SO-13 |
| Context spec | §3 "Decisions stay human" | the service promote | SO-14 |
| Onyx spec | O3, §4.1 step 7 | the owner's login may be given in the app, in two calls | SO-15 |
| `docs/sindri/README.md` | X65 | the decision | SO-16 |
| Engine spec | §6.3 (the `recipe`/`workflow` CLI table) | the short form `sindri takeover <item-or-address>` beside `sindri handback`, as added by #448 (open at rev 2; whichever merges second keeps the line) | SO-17 |
| API surface spec | §3.2 and the engine-owned list | the same short form beside `sindri handback` | SO-18 |
| UI screens spec (rev 8) | §3 (R8-1, the page table), §4.1 Home | Home stays the default and shows one "Finish setting up Sindri" line while any step is open; the checklist is a page under Settings (route `setup`, `G U`; no OS scheme). Rev 7 §3.3 gets the same one-line note | SO-19 |
| UI spec (rev 7) | §6.5 (what the app never does), §7 (the protected rows and the closing paragraph), §18.1 and the E-9/E-13 row | `ApproveProfile`, `ApproveRecipe` and `ApproveAllRecipes` become app confirmations (typed or held); the rest stay `cli-only`; the line-15 list of protected rows drops `ApproveRecipe` | SO-20 |
| UI spec (rev 7) | §3.2 (the Home fold rule) | Home's Setup entry is one line, so Needs you starts above the 1280 by 800 fold | SO-19 |
| UI screens spec (rev 8) | the recipes page ("Re-approval is `protected`: the CLI to copy") | re-approval of one recipe is typed in the app | SO-21 |
| `.agents/rules/mcp-servers.md` | per-repo servers, the `sindri` row | the owner runs `sindri mcp register`, or Sindri registers itself after the owner approves a profile naming the repo; never user-wide, never by `setup.sh` or a provider installer | SO-22 |
| `docs/csharp/sindri-profile.md` | `setup` | the key | SO-P1 |
| `docs/csharp/sindri.md` | commands, errors, events | as phases land; with A7b: bare `sindri-app` prints no register hint and skips its Onyx offer when the Setup checklist owns that step (line 19); with E1: `recipe manifest`, `recipe eval --shipped` | each phase; SO-P4 for A7b |
| `scripts/sindri-app.sh`, `scripts/lib/sindri-build-inputs.sh`, `setup.sh` | headers and output, A7a | A7a: `--publish-only`, `recipes/sindri` in `SINDRI_BUILD_PATHSPECS`, the `git archive` staging, the publish step right after the provider installs, the detached start line and its success check, the exit codes, the log path; the headers (sindri-app.sh's "the one caller in setup.sh is the Onyx step") | SO-P4 (A7a) |
| `scripts/sindri-app.sh`, `setup.sh` (header and `usage()`), `config/lib/setup-onyx-step.sh`, `scripts/install-onyx.sh`, `scripts/tests/install-onyx.test.sh` | headers and output, A7b | A7b: no register-command print, `--with-onyx`, `--no-onyx` is a no-op, the closing line | SO-P4 (A7b) |
| `AGENTS.md` (and so `CLAUDE.md`) | the sindri-app and Onyx lines | the `sindri-app` line and the C# build paragraph's "setup.sh builds and links it only through the Onyx step of `--with-sindri` (skip with `--no-onyx`)": setup.sh now publishes and starts it, Onyx is `--with-onyx`; the `install-onyx.test.sh` line | SO-P6 (A7b) |
| `deps.yaml`, `.env.example` | as phases land | the new cli edges (`claude`, `codex`, `git`) and `AW_SINDRI_APP_NO_MCP` (`check-deps.mjs`, `check-env-example.sh`) | SO-P5 |

(The review's "SO-U*" UI edits are numbered SO-19 to SO-21 here, because SO-U1 to SO-U4 are the UI lane's phase units.)

## 12. Review findings

Rows below keep the wording of the round that wrote them. Where they say "the card" or "Home card", read the rev 5 layout (§5.7): the Setup checklist under Settings and one line on Home.

/autoplan round 1 (2026-10-19): five lenses, all NEEDS_WORK, **19 HIGH** (product 1, arch 6, design 7, devex 4, security 1).
Resolutions are rev 2. "Open" means the owner decides (a question in §14) or a later round re-checks it.

| Lens | Verdict | HIGH |
|---|---|---|
| product | NEEDS_WORK | 1 |
| arch | NEEDS_WORK | 6 |
| design | NEEDS_WORK | 7 |
| devex | NEEDS_WORK | 4 |
| security | NEEDS_WORK | 1 |

| id | lens | severity | finding | resolution (rev 2) |
|---|---|---|---|---|
| R1-1 (C1, H1, H2, D4) | product, devex, design | HIGH | The first run contradicted itself: setup.sh ran `onyx up` and approved the profile in the terminal before the app existed; "2 commands, 0 typed" was not true; the profile step read needs-you before any repo existed | One order: setup.sh installs and opens the app; Onyx is a step in the app (`--with-onyx` keeps the terminal run); the profile step is `todo` until a repo exists; one table counts every prompt (§1, §2, §5.2, §5.6, §9); Q9, Q10 |
| R1-2 (F3, D2) | arch, design | HIGH | Approve-all was a blocking POST yet showed 14/26 | 202 + job `ApproveAllRecipes`, progress and result on the job (§5.3, §7.2, §8) |
| R1-3 (F4, F5, SS-1) | arch, security | HIGH | No plan digest; per-recipe challenges; no source for "shipped"; no capability delta | Digest defined; one challenge and an internal approval path; manifest from git HEAD embedded with the build; previous hash and capability delta per row; a recipe that gains a capability is left out (§5.3) |
| R1-4 (F2) | arch | HIGH | `displayName` re-hashes at the same version: `RecipeVersionTaken` on every existing box | The same change bumps every version; "a content change bumps the version", checked by `recipe validate` (§5.3, SO-E1) |
| R1-5 (F1) | arch | HIGH | Job kinds and keys failed `JobKind` and `JobKey`; the path leaked into events | PascalCase kinds; key = first 16 hex of sha256 of the path; the path is never in events (§5.1, §7.4) |
| R1-6 (F6, D3, SS-2) | arch, design, security | HIGH | One POST with the password could not wait for the browser sign-up; the password lived in the job record | Two calls, `POST /onyx/setup` then `POST /onyx/account`; a secret type, never stored (§5.6) |
| R1-7 (D1) | design | HIGH | Approve-all had no keyboard or assistive path | Typed `approve-all <digest6>` accepted; the key-hold specified (§4) |
| R1-8 (D5) | design | HIGH | "First screen" broke "Home is always the default"; no nav, key or deep link | A Setup card on Home; nav entry, `G U`, `sindri://setup` (§5.7, SO-19) |
| R1-9 (D6) | design | HIGH | The UI specs still said protected actions never become app buttons | UI spec edits SO-19 to SO-21 (§11) |
| R1-10 (D7) | design | HIGH | The consent list showed machine words | Plain sentences per capability, change words, grouping (§5.3.1) |
| R1-11 (H3) | devex | HIGH | The first `sindri-app` publish was unreported and hidden in the Onyx step | Its own setup.sh step with a time hint and failure sentence (§9) |
| R1-12 (H4) | devex | HIGH | Background failures silent up to ~35 minutes; no server-down state; recipe registration failure unspecified; no map retry | "Retrying in N minutes" after the first failure, `failed` after 3, `RefreshContextMap`; "Sindri isn't running" with a start action; per-recipe registration with a `failed` count (§5.3, §5.5, §5.7) |
| C2 | product | MEDIUM | Auto MCP changes `.agents/rules/mcp-servers.md`, not in the edit list | SO-22; excluded files; the off switch shown in the detail (§5.4, §11) |
| C3 | product | MEDIUM | The hold is a slip guard; the token file is a trust boundary | Capability sentences on the hold screen; Q1 says so; doctor warns on a readable token file (§4) |
| C4 | product | MEDIUM | Success was commands, not first value | Time to first value from `setup.step.changed`; a "what now" last line (§1, §7.5) |
| C5 | product | MEDIUM | A typed profile approval for something the owner just picked | Open, Q10 (recommended: keep it typed) |
| C6 | product | MEDIUM | Auto map cost uncapped | A daily cap on cleanup calls, shown in the detail; Q3 asks the number (§5.5) |
| C7 | product | MEDIUM | `displayName` forces a full re-approval | Kept: one hold, versions bumped, the card says why (§5.3); Q5 |
| C8, C9 | product | LOW | Headless boxes; the missing hook hidden behind a green step | CLI forms ship with their units (§2); a visible detail and a doctor line (§5.1) |
| F7 | arch | MEDIUM | "Derived from the ledger" was not true | Read model over named inputs; `port` on `mcp.registered`; `repo.onboarded` (SO-D11, §6) |
| F8 | arch | MEDIUM | The confirmation channel changed more than said | A separate app challenge route, `NeedsTerminal` unchanged, held answers redeem separately (§4) |
| F9 | arch | MEDIUM | Two needs-you rows for Onyx | Reuse `failure:service:onyx`, no `setup:onyx` (§5.6, §7.3) |
| F10 | arch | MEDIUM | Job-time codes listed as HTTP statuses | Cheap checks before the job; job codes are job failure codes (§5.1, §7.2) |
| F11 | arch | MEDIUM | MCP auto-registration vs the rule; tracked files; worktrees | SO-22; tracked files and worktrees stated (§5.4) |
| F12 | arch | MEDIUM | Tick-only derivation; pausable setup service; failure counting | Event-driven plus a 60 s backstop; not pausable; the service counts the last jobs (§5, §5.5) |
| F13, F14, F15, F16 | arch | LOW | Golden inconsistencies; missing edits; unattended map decisions; client-asserted hold | Dependencies stated in §6 and goldens fixed; §11 completed (deps, env); the agent's limits (§5.5); §4 keeps the honest language |
| D8 | design | MEDIUM | The hold's lifecycle | Specified (§4 item 4) |
| D9 | design | MEDIUM | One failing recipe blocks all later ones; a CLI string in `sentence` | The sentence carries no command (`cli` field); continuing past a refusal is answered in rev 3 (§5.3) |
| D10, D11, D12 | design | MEDIUM | One error for many repos; `todo` with a failing button; no checklist states | `items`; `action: null` while waiting; loading, rebuilding, restarting and server-down states (§5.7, §6, §7.2) |
| D13, D14, D15 | design | MEDIUM | No typed-path fallback; the profile preview as a bare name; undefined nouns | A typed path field; the preview in plain words with path and size; glosses under the titles (§5.1, §5.2, §7.2) |
| D16, D17 | design | LOW | Announcements and focus; no OS notification | Stated (§5.7) |
| dx-M1 | devex | MEDIUM | The link to `sindri-app` may not be on `PATH` | setup.sh prints the absolute launcher path (§9) |
| dx-M2 | devex | MEDIUM | setup.sh still builds the TS `sindri` | Said in the time note; skipping it is out of scope until the switch (§2) |
| dx-M3 | devex | MEDIUM | Pre-app exit codes; exit 3 treated as success | The summary lists "Still to do"; the Onyx step shows `SND-ONYX-001` on first open (§9, §5.6) |
| dx-M4 | devex | MEDIUM | Headless sign-up; the AddRepo job waiting on a human | The address is always in `detail`; the job ends at "added to the profile" (§5.6, §5.1) |
| dx-M5 | devex | MEDIUM | Boxes that already have Sindri; re-running setup.sh | "A box that already has Sindri" and the re-run paragraph (§2, §9) |
| dx-L1..L3 | devex | LOW | Approve-all slip guard; no quickstart; Q6 as a safety regression | Capability sentences above the hold; a quickstart is a doc task in SO-P1's PR; Q6 shows a visible detail |
| SS-2 | security | MEDIUM | The password's 202 lifetime | Secret type, no restart persistence, a sentinel test (§5.6) |
| SS-3 | security | LOW | `git` in an owner-picked folder | Fixed git flags, argv only (§5.1) |
| SS-4 | security | MEDIUM | The preview hides the path; broad folders | Real path and size in the preview; `/`, home and a parent of the Sindri home refused (§5.1, §5.2); Q8 |
| SS-5 | security | MEDIUM | The cleanup agent's authority and input | Read-only, no tools, scrubbed input, schema output, a removal ceiling (§5.5) |
| SS-6 | security | MEDIUM | The Codex trust row contradicted "Codex will ask you" | Sindri does not write the row (§5.4) |
| SS-7 | security | LOW | MCP files committed or churned | `.git/info/exclude`; write only when different (§5.4) |
| SS-8 | security | MEDIUM | The hold is client-asserted | Residual accepted and written (§4); the prover only in a scratch home; one open challenge per action; the `challenge.shown` ack is open for the owner's later round |
| SS-9 | security | MEDIUM | Which profile the MCP service reads | The approved snapshot by hash; a test with a drifted file (§5.4) |
| SS-10 | security | LOW | Approve-all audit and the service promote's attribution | `planDigest` and `heldMs` on `recipe.approved`; the Context screen shows "changed by the agent" (§7.5, §5.5) |
| X-1 | #448 review | LOW | `sindri takeover` short form missing in the specs | SO-17 and SO-18 (§11) |

### /autoplan round 2 (2026-10-19)

Five lenses on rev 2: product NEEDS_WORK (2 HIGH), design NEEDS_WORK (3), devex NEEDS_WORK (1), security PASS (0 HIGH, 2
MEDIUM), arch NEEDS_WORK (3 HIGH, 12 MEDIUM; rerun after the first run died). Nine HIGH findings merged into R2-1 to R2-9
(two MEDIUMs raised by two lenses are R2-6 and R2-7). Resolutions are rev 3.

| id | lens | severity | finding | resolution (rev 3) |
|---|---|---|---|---|
| R2-1 | product H1, design R2-D1 | HIGH | The `first-run` golden showed `recipes` as `needs-you` with Approve all on a fresh box, but §5.3 said the hold is not offered before a profile is approved | `recipes` is `todo`, `action: null`, "After the profile is approved" until a profile is approved; golden and §6 fixed; `SND-SETUP-007` if asked early (§5.3, §6, §8) |
| R2-2 | design R2-D2, security SO2-1 (M), arch M12 | HIGH | "Gains a capability" was undefined for a never-approved recipe | Gain = `new & ~old != 0` over the `Capability` flags; old = the empty set when never approved; a never-approved recipe is `New` in the hold with its full capability set in the preview and the digest; `ownApproval` only for a previously approved recipe whose new hash adds one; golden `plan-first-approval.json` (§5.3, §8) |
| R2-3 | product H2, design R2-D10, arch H3 | HIGH | Stop-at-first-refusal dead-ended every attempt; no eval result is recorded per recipe hash, so a plan-time `evals` field had no source | `evals` field and eval-driven `blocked` list dropped; evals run at publish (and in CI) and a failure fails the publish; the manifest records `evalCases`; a refusal skips the recipe and its dependents and carries on; result `{approved, refused, skipped}`; Q11 answered and removed (§5.3, §7.2, §8) |
| R2-4 | design R2-D3 | HIGH | The card's Start Sindri button broke UI §6.5 | No button: the card shows the launcher command (`sindri-app`) with a copy action and says the app reconnects on its own (§5.7) |
| R2-5 | devex HIGH-1 | HIGH | "Publish, then open the app" had no mechanism (`sindri-app.sh` execs the control plane) | `sindri-app --publish-only` (SO-A7); setup.sh starts `sindri-app` detached when a desktop session exists, else prints the launcher path (a second typed command); `sindri stop` stops it; the A6 proof checks setup.sh exits 0 with the app alive (§2, §9, §13) |
| R2-6 | product M5, design R2-D7 | MEDIUM | Without Docker, or if Onyx is deferred, the card never collapsed | New state `skipped` ("Not now", reversible, `DeferOnyx`); Docker missing is `todo` with the reason and a link and counts as finished; the card collapses when every step is `done` or `skipped` (§5.6, §5.7, §6) |
| R2-7 | security SO2-2 | MEDIUM | The password's path from endpoint to job was unspecified | A single-use in-memory slot keyed by job id; the command carries only the job id; binding errors never echo the body; the sentinel test covers a malformed body and a wrong password (§5.6) |
| R2-8 | arch H1 | HIGH | `Authorization` kept `Protected = Who.Terminal`, so the app was still `SND-AUTH-002` on the three rows | A per-row `AppConfirm` flag on exactly `ApproveProfile`, `ApproveRecipe`, `ApproveAllRecipes`; `Decide` lets `Who.App` through only when the request redeems a challenge; a session stays `SND-AUTH-002`; an architecture test (§4 item 7) |
| R2-9 | arch H2 | HIGH | The `RegisterMcp` job key (the repo name) is not a valid `JobKey` | Key = `mcp-` + first 16 hex of sha256 of the repo name; the name is never in the key (§5.4, §7.4) |

| id | lens | severity | finding | resolution (rev 3) |
|---|---|---|---|---|
| P-M1 | product M1 | MEDIUM | 45 to 75 minutes before the app opens | setup.sh publishes `sindri-app` before the Serena image and the TypeScript build; the app opens in about 5 to 10 minutes (§2, §9) |
| P-M2 | product M2 | MEDIUM | No timing past "setup done" | `setup.first-item` beside `setup.done` (§7.5, §13.1) |
| P-M3 | product M3 | MEDIUM | One global cleanup cap starves later repos | 40 calls per repo per day, a machine ceiling of 120 (§5.5, Q3) |
| P-M4 | product M4 | MEDIUM | No release cut line | §13.1 First shippable slice: A1 + A7 + E1 + E2 + E3 |
| P-M6 | product M6 | MEDIUM | Consent grandfathered on existing boxes | On a box whose profile pre-dates this build, MCP and map are `needs-you` once ("Turn on for your repos"); `setup.consent.given`; nothing is written before the press (§2, §5.4, §5.5) |
| P-M7 | product M7 | MEDIUM | A second person on the box | One paragraph in §15 |
| D-M4 | design R2-D4 | MEDIUM | `sindri://setup` contradicts UI §10 | The in-app route name `setup` only; no OS scheme; `G U` defined (§5.7, SO-19) |
| D-M5 | design R2-D5 | MEDIUM | `ownApproval` had no UI | Listed in the card under "Needs its own look", each opening the existing recipe approve flow; the step is `done` only when plan and `ownApproval` are empty (§5.3, §5.7) |
| D-M6 | design R2-D6, arch | MEDIUM | Duplicate needs-you rows | The server always emits `setup:*` rows; the Home view hides them while the card shows; the tray counts once (§5.7, §7.3) |
| D-M8 | design R2-D8 | MEDIUM | Onyx form had no accessibility statement | Labels, autofill and paste allowed, show/hide, focus on open, errors under the field; the second-approval sentence (§5.7) |
| D-M9 | design R2-D9 | MEDIUM | The card pushes Needs you off the fold | Three rows (the first unfinished) and "and N more" (§5.7) |
| X-M1 | devex | MEDIUM | Onyx before a repo contradicts the profile step | The profile file is not written until a repo exists, so the step stays `todo`; the Onyx section waits and is written with the first repo (§5.2, §5.6) |
| X-M2 | devex | MEDIUM | Headless boxes count 62-ish | §2 lists the headless CLI forms and counts them (6 commands, 2 typed) |
| X-M3 | devex | MEDIUM | setup.sh exit codes undefined | Exit 1 on a failed publish or install, 0 when a step is skipped or "Still to do" lists items (§9) |
| A-M1 | arch M1 | MEDIUM | The version-bump check had no previous manifest | `recipe validate --against <previous manifest>`; CI uses origin/main's; `RecipeVersionTaken` is the backstop (§5.3) |
| A-M2 | arch M2 | MEDIUM | Manifest producer; "embeds" vs "beside the binary" | `sindri-app.sh` writes the manifest from `HEAD` at publish and embeds it as a resource; "beside the binary" removed (§5.3, engine §4.1) |
| A-M3 | arch M3 | MEDIUM | Live instances during an upgrade | Running instances keep their pinned version; approving v2 never repins a running v1; new starts take v2 (§5.3) |
| A-M4 | arch M4 | MEDIUM | MCP `done` with no provider CLI | With none found the step is `todo` ("Install Claude Code, Codex or Cursor"), never `done` (§5.4) |
| A-M5 | arch M5 | MEDIUM | The Onyx wait primitive | The job parks in `waiting` on the signal `onyx-account`; 30 minutes fails it with `SND-SETUP-006`; the account call is refused unless the job is waiting (§5.6) |
| A-M6 | arch M6 | MEDIUM | The 8-job limit | Setup's automatic jobs are a single-slot class that queues, not fails (§5) |
| A-M7 | arch M7 | MEDIUM | `profile.yaml` lost updates | Every write uses the versioned `if-version` write; a conflict is retried once from a fresh read (§5) |
| A-M8 | arch M8 | MEDIUM | Git on every `GET /setup` | Git facts cached per repo for 30 s and refreshed on `repo.*` events (§5.1) |
| A-M9 | arch M9 | MEDIUM | Profile `todo` beside a `profile:drift` row | Same as X-M1: no file, no drift row |
| A-M10 | arch M10 | MEDIUM | Invalid default repo name | Normalised to the `RepoName` rules; the confirm shows it and lets the owner edit it (§5.1) |
| A-M11 | arch M11 | MEDIUM | The first slice omitted A1 | The first slice includes A1 with the `service:setup` shell (§13.1) |
| A-M12 | arch M12 | MEDIUM | "Gains" against the `Capability` flags | `new & ~old != 0`, old empty for a never-approved recipe (R2-2) |
| L-R2 | all | LOW | One-sentence fixes | Applied inline: the fix line of `SND-SETUP-003` follows `confirm`; one shipped recipe must have words for each capability (fallback for owner-written only); `--no-onyx` prints a line; A5 no longer waits for A2; the prover's scratch home is a marker file; a replaced challenge is per channel; approve-all redeems last; each approve-all row is rechecked against the plan and manifest; `claude mcp add` runs with argv only and no repo hook; the publish stamp includes the recipes tree hash; `service:setup` is unpausable already (`SND-SVC-404`) with a test |

### /autoplan round 3 (2026-10-19), the last

Five lenses on rev 3: product NEEDS_WORK (1 HIGH), design NEEDS_WORK (2), devex NEEDS_WORK (3), arch NEEDS_WORK (3 HIGH, 9
MEDIUM), security PASS (0 HIGH, 4 MEDIUM, 4 LOW). Resolutions are rev 4. Rows of rounds 1 and 2 above are history: where
they name a manifest file, R3-6 replaced it with the embedded set.

| id | lens | severity | finding | resolution (rev 4) |
|---|---|---|---|---|
| R3-1 | product C1 | HIGH | The first slice dropped setup.sh's `onyx up` and the MCP hint before the app steps that replace them (A4, A5, U1) existed; the count was 12, not 11 | SO-A7 is split. A7a (first slice) adds `--publish-only` and the detached start and keeps the Onyx default and the MCP hint; A7b drops them once A4, A5 and U1 have merged. The slice is recounted with `sindri-app onyx up`: 12 commands (§2, §13.1) |
| R3-2 | design H1 | HIGH | A failed or needs-you step could hide behind "and N more" while its Home row was also hidden | The card fills its three rows by priority (needs-you, failed, running, todo); Home hides a `setup:*` row only for a step the card is drawing; a fixture of three running steps and a failed Onyx (§5.7, U1) |
| R3-3 | design H2, product C2 | HIGH | The first profile had no `workflow.effects`, so every effect was refused (`SND-WF-011`) and the first work item parked | The default profile's `workflow.effects` is the union of the effect kinds the shipped recipes declare; the preview lists them in words; a CI check fails if a shipped recipe declares an effect the default doesn't allow; a unit test that every embedded recipe passes the approval checks under the default; the A6 proof starts a work item and checks its first effect runs (§5.1, §5.2, §13.1) |
| R3-4 | devex H1, arch M6 | HIGH | C# commands were written `sindri <verb>`, which is the TypeScript binary until the switch | Every C# command is `sindri-app <verb>`, with one sentence saying why (header, §2, §7.4, §9, goldens, §13.1) |
| R3-5 | devex H2 | HIGH | The detached start had no argv, stdin or success check; a bare start asks the old Onyx prompt and prints the register hint; a failed start was silent; the no-desktop launcher was wrong | The exact line in §9, a 60 second poll of `sindri-app status --json`, "Sindri is running" or "Sindri didn't start, see <log>", exit 1 on a failed start, `sindri-app --headless` with no desktop; A7b removes the hint and the Onyx offer from bare `sindri-app` (docs/csharp/sindri.md); desktop detection: `DISPLAY`/`WAYLAND_DISPLAY` set and `SSH_CONNECTION` unset on Linux, always on macOS |
| R3-6 | devex H3, arch H3 | HIGH | The publish-time manifest could not be produced (hashes and evals need the built engine, the copy has no `.git`, `recipes/sindri` is not a build input, CI's "manifest on origin/main" was never committed) | No manifest file. `git archive HEAD recipes/sindri` is staged into the build copy and embedded; the registry hashes the embedded files at serve start; `sindri-app recipe manifest <dir> [--json]`; CI runs it over `HEAD` and `git archive origin/main` and fails on a changed hash at the same version; `recipe eval --shipped` runs with the published binary and fails the publish (§5.3, §9, E1, E2, A7a) |
| R3-7 | arch H1 | HIGH | The gain rule over six `Capability` flags could not see a new `step.ship.*` block | A recipe's capability set is its block descriptor ids, effect kinds and capability literals (`RecipeCapabilitySet`, one pure Core type for the plan, the digest and the preview); gain = new − old ≠ ∅; the plan golden and the proof follow (§5.3, §7.2, §8) |
| R3-8 | arch H2 | HIGH | `Decide` is pure and runs before the handler, so it cannot redeem a challenge; a missing answer is `SND-PROFILE-004` or `SND-RECIPE-009`, and `NeedsTerminal` is a second gate | `Decide` lets `Who.App` through on the three rows; the handler redeems before any write; the codes stay as today; architecture tests with a refusing `IChallenges` (§4 item 7, A2) |
| R3-M1 | design M1, arch M2 | MEDIUM | The approve-all job key varied with the digest, so two plans could run at once | The fixed key `approve-all`; a second POST answers 409 with `runningJob` (§5.3, §7.4) |
| R3-M2 | product C5, design M3 | MEDIUM | "Finished" was defined three ways | One meaning: every step `done` or `skipped`; missing Docker makes Onyx `skipped` with a reason and a link; a not-started Onyx is `todo`; `setup.done` and the collapse happen at the same moment (§1, §5.6, §5.7, §6, §7.5, §8) |
| R3S-1 | security | MEDIUM | An overlay of the archive on the working-tree copy could embed an untracked or dirty recipe | The publish deletes the staged `recipes/sindri` and extracts the archive; only that directory is embedded; a test with an untracked and a dirty file (§5.3) |
| R3S-2 | security | MEDIUM | Stale text still described a manifest file and a disk copy | "Shipped" means `id@hash` in the embedded set, computed once at serve start, no disk fallback; `SND-RECIPE-011` keeps only "not a recipe this build ships" (§5.3, §7.6, A7a, E2) |
| R3S-5 | security | MEDIUM | `ApproveShippedRecipe` skips the per-recipe challenge and must not be reachable | No catalog row, route, MCP tool or CLI verb; constructible only from a redeemed plan; only the job calls it; an architecture test (§5.3) |
| R3S-6 | security | MEDIUM | "No write before redemption" must include the job row and the rate-limit counter | The R3-8 test uses a refusing `IChallenges` on all three rows and asserts zero writes of any kind; a session or no-token caller gets `SND-AUTH-002`; exactly three rows carry the flag (§4 item 7) |
| R3-O1 | product C3 | MEDIUM | `setup.first-item` had no trigger | Recorded on the first `workflow.instance.started` after `setup.done`, with the minutes between (§7.5) |
| R3-O2 | product C4 | MEDIUM | A fourth repo was starved by the 120 machine ceiling | The ceiling is 40 times the repo count; the machine cap is dropped (§5.5, Q3) |
| R3-O3 | product C6 | MEDIUM | The second-person claim was unverified | Checked: the registry and approvals live in each home's `ledger.db`, so a second home approves once (one approve-all, one typed profile confirm); §15 and the A6 proof say so |
| R3-O4 | product C7 | MEDIUM | NativeAOT prerequisites unstated | §9 names clang and the zlib development headers; `scripts/install-deps.sh` already installs them |
| R3-O5 | design M2 | MEDIUM | The Home fold rule was not amended | The UI spec's fold rule is amended (SO-19, §11) |
| R3-O6 | design M4 | MEDIUM | "One typed confirm" is the best case | §1 and §2 say one, or two when Onyx is set up after the profile is approved; the Onyx row says to do it first |
| R3-O7 | design M5 | MEDIUM | Evals ran on every publish | Only when the embedded set changed since the last publish, compared at publish time from `recipe manifest` output (§5.3, §9) |
| R3-O8 | arch M1 | MEDIUM | The Onyx wait needs a job state nobody owns | `JobState.Waiting` with the signal `onyx-account`, holding no slot; SO-23 and unit A5 (§5.6, §11) |
| R3-O9 | arch M7 | MEDIUM | AGENTS.md and the install-onyx files contradict the new setup.sh | Listed in §11 as A7b edits (SO-P6) |
| R3-O10 | devex | MEDIUM | Publish placement and failure, the A6 proof in CI, `--publish-only` parsing, a stale running build, the log path | The publish runs right after the provider installs and a failed publish exits 1; A6 tests setup.sh's tail through a stub `sindri-app` in CI and the real run is the owner check; `--publish-only` is a leading flag; the older-build line; the real log path (§9, §13.1) |
| R3-O11 | arch M3, M4, M5, M8, M9 | MEDIUM | `ApproveShippedRecipe` guards; the single-slot queue had no unit; the `RegisterMcp` request differs; a short hold burned the challenge and channels were not modelled; evals need their inputs and the right binary | Reachability in §5.3; the queue is in A1 with a test; A4 writes a repo-named request with `WriteCodexTrust = false`; `SND-SETUP-003` does not consume and `IssuedChallenge` gains `Mode` and `Channel` (A2); evals run against the embedded set with the published binary and the stamp includes the commit |
| R3-O12 | product C2b, C8, C9 | MEDIUM | A refused recipe looped; the slice had no proof; Onyx `needs-you` had no row | "Refused last time" marker and a one-recipe way out; a scripted slice proof in A7a and E3; Onyx is card-only while `todo` (§5.3, §7.3, §13.1) |
| R3-L | design L1 to L4, arch L1 to L3, devex LOWs, security R3S-3, 4a, 7, 8 | LOW | One-sentence items | Applied inline: held Space ignores auto-repeat and announces at the start and end; the launcher command is the daily path after a reboot; a failed Onyx preflight reuses the Docker-missing reason; the `approve <recipe>@<hash>` challenge variant is dropped; `items` in the API `GET /setup` row; a lost `setup.consent.given` falls back to the one-time press; dev builds embed the `HEAD` archive or nothing; the eval-skip comparison is publish-time only; one `RecipeCapabilitySet` function for the digest and for `old`; the sentinel test covers every refusal path; the password goes in a request body only; the start log is scrubbed and created 0600; the setup.sh header and `usage()` are updated with A7b |

## 13. Phases, with a parallel plan

Lanes: **api**, **engine**, **context**, **ui**. Up to four builders per lane. A unit is a PR. `->` is "waits for".

| Unit | Lane | What | Waits for |
|---|---|---|---|
| SO-A1 | api | **The contract:** `SetupView` and `ApproveAllPlan` types, the thirteen goldens, `GetSetup` with the derived projection (all steps `todo` from an empty ledger), **the `service:setup` built-in shell** (listed in `sindri-app services`, not pausable, no steps yet) **and the queue for its automatic jobs, with a test**, the `setup` kind and `NeedsYouRowId` (the test names its regex: `setup:repo`, `setup:recipes`, `setup:mcp`), the `skipped` state, the events in `EventCatalog` (with `setup.consent.given` and `onyx.deferred`), `SND-SETUP-001…007`, `setup:` profile key, catalog rows (stubs), docs | — |
| SO-A2 | api | App confirmation: `IssueAppChallenge`, `confirm`, `minHoldMs` and `typedAlternative`, the held redeem, `IChallenges`, `IssuedChallenge` (`Mode`, `Channel`) and `ChallengeAnswer` (`heldMs`) changes, `SND-SETUP-003` not consuming the challenge, replacement keyed by action and channel, **the per-row `AppConfirm` flag in `Authorization` (`Decide` passes `Who.App`, the handlers redeem before any write) and its three architecture tests**, the prover's scratch-home marker, the catalog flip and its architecture-test change, doctor's token-file check | A1 |
| SO-A3 | api | `AddRepo`: the checks, route, job, the `repos[]` appender on the versioned profile write, **the default profile with `workflow.effects` set to the union of the shipped recipes' effect kinds (from the embedded set) and the CI check for it**, the normalised default name, `repo.added`, `repo.onboarded`, onboarding after approval (hook slot), the 30 s git-facts cache, `GetSetup` repo and profile steps; the CLI form `sindri-app repo add` | A1 (E2 for the effects union) |
| SO-A4 | api | MCP: `RegisterMcp` route and job (key `mcp-` + 16 hex), a repo-named request with `WriteCodexTrust = false` for the automatic runs, the automatic runs from `profile.approved` (snapshot) and serve start, `setup.consent.given` and the one-time "Turn on for your repos" row on an existing box, the `claude` argv hardening, `mcp.registered`; `sindri-app mcp register` keeps its flags | A1, A3 |
| SO-A5 | api | `SetupOnyx`, `ConnectOnyx` and `DeferOnyx`: three routes, **`JobState.Waiting`** and the job's wait on the signal `onyx-account` with its 30-minute timeout, the single-use password slot, the secret type and its tests (every sentinel path of §5.6), the Onyx section held until the first repo; `sindri-app onyx up` unchanged | A1 |
| SO-A6 | api | Setup service: the read model, `setup.step.changed`, the needs-you rows, doctor `setup`; the first-run proof in a scratch home (§13.1) | A1, A3, E2, C1 |
| SO-A7a | api | Publish (**in the first slice**): `sindri-app --publish-only` (leading flag, publish steps only, no exec, no register hint); `scripts/sindri-app.sh` stages `git archive HEAD recipes/sindri` into the build copy (deleting the copy's directory first) and embeds it as resources, with `recipes/sindri` in the build inputs, the commit and the recipes tree hash in the stamp, and runs `recipe eval --shipped` with the published binary when the embedded set changed; `setup.sh` publishes right after the provider installs, reports it as its own step, starts the app detached with the exact line of §9 when a desktop session exists and checks that it answers, exits 1 on a failed publish or start and 0 on a skipped step; **it keeps today's `onyx up` default and the MCP register hint**; the scripted slice proof (`scripts/tests/setup-sindri-app.test.sh` through a stub `sindri-app`, a faked desktop, both branches) | E1 (for the eval step) |
| SO-A7b | api | Publish (**after A4, A5 and U1 have merged**): `setup.sh` stops running `onyx up` and printing the register hint, adds `--with-onyx` and the no-op `--no-onyx`, the closing line; bare `sindri-app` prints no register hint and skips its Onyx offer when the Setup checklist owns the step; `docs/csharp/sindri.md`, `AGENTS.md`/`CLAUDE.md`, `scripts/install-onyx.sh` and its test, `.agents/rules/mcp-servers.md`; deps and env entries | A4, A5, U1, A7a |
| SO-E1 | engine | `displayName`: the field, `RCP-DISPLAY-001`, the version bump of all 26 recipes, the rule "a content change bumps the version" (CI: `recipe manifest` over `HEAD` and `git archive origin/main`, `recipe validate --against` that output), **the verbs `recipe manifest <dir> [--json]` and `recipe eval --shipped`**, evals updated, views carry it, a plain sentence on every block descriptor | — |
| SO-E2 | engine | Registration at serve start from the embedded set (hashed once, no disk fallback) and `recipe.registration.completed {…, failed}`; `RecipeVersionTaken` as the backstop; the test that a working-tree edit never changes the embedded set; the CI check that the default profile allows every effect the embedded recipes declare | E1, A1, A7a |
| SO-E3 | engine | Approve-all: **`RecipeCapabilitySet`** and the plan (digest, delta, `ownApproval`, the gain rule), the protected POST as a job with the fixed key `approve-all` that redeems last, `ApproveShippedRecipe` (no route, verb or catalog row; the job its only caller) with its per-row recheck, skip-and-continue with `{approved, refused, skipped}`, `batch`, `SND-RECIPE-011`, `SND-RECIPE-009` on drift, `SND-SETUP-007`; terminal verb `sindri-app recipe approve-all`; the `sindri takeover` short form lines | E1, A1 (and A2 for the app channel) |
| SO-C1 | context | The `map` step's inputs: `context.map.refreshed` and the failure row as the context lane specified, the retry action, the cleanup agent's limits and the per-repo daily cap (no separate machine cap), the consent gate, the manual `context map refresh` row | A1 |
| SO-U1 | ui | The Setup checklist page under Settings (route `setup`, reached from Settings and `G U`) and the one "Finish setting up Sindri" line on Home while any step is open: six steps from `GET /setup` in step order, states (with `skipped`), progress, items, plain words, a fixture of three running steps and a failed Onyx (all six drawn), loading, rebuilding and "Sindri isn't running" with the launcher command and a copy action (no Start button); hides `setup:*` rows on Home while any step is open; fixtures are the goldens | goldens in A1 |
| SO-U2 | ui | The confirmations: typed (profile, one recipe) and held-or-typed (approve-all), the preview sheet in plain words, the hold's lifecycle, `SND-SETUP-003` handling | goldens in A1; A2 to run live |
| SO-U3 | ui | Add repo (folder picker and typed path, then the job's progress), Set up Onyx (the wait, the login form with its accessibility rules, Not now), Register MCP and "Turn on for your repos" | A3, A5 to run live |
| SO-U4 | ui | `setup` needs-you rows, `displayName` on recipes and instances, the approve-all list and its plain capabilities, "Needs its own look" | A1, E1 |

**Waves.** Wave 0: A1, A7a and E1 start together (A7a's eval step lands after E1's verbs), and U1, U2 and U4 can start from the goldens in this spec before A1
merges (the UI lane builds against fixtures). Wave 1, once A1 merges: A2, A3, A5, E2, E3 (terminal path), C1 and the UI
units against live data run in parallel. Wave 2: A4, U3 against live data. Wave 3: A6, A7b (once A4, A5 and U1 have merged) and the proof.

**Contract each unit waits on.** The goldens and types are A1's; no other unit changes them. The challenge shape and the
`AppConfirm` flag are A2's; U2 and E3's app path depend on them, E3's terminal path and its tests do not. `displayName`
and the version bump are E1's; every recipe hash changes there, so E1 must merge before any shipped recipe is approved in
daily use (the engine spec's P2 note says the same of every hash-changing field). The embedded set is A7a's (the staging and the embed target); E2 and E3 read it, and `recipe manifest` is E1's.

### 13.1 First shippable slice

**A1 + A7a + E1 + E2 + E3 (the terminal approve-all path).** After it: `setup.sh` publishes the app first and starts it
(the app opens on the existing shell, with no Setup checklist or Home line, until U1; `GET /setup` answers with every step `todo`); the
shipped recipes register on their own at start; `sindri-app recipe approve-all` (typed `approve-all <digest6>`) replaces the 52
register and approve commands. `setup.sh` **still runs `sindri-app onyx up` and prints the MCP register command**, because
A4, A5 and U1 are not in the slice. A first run is **12 commands and 3 typed confirmations** (setup.sh, start, `sindri-app
onyx up`, repo by hand, profile 2, MCP, approve-all, map 3, doctor; typed: profile, approve-all, map promote) against 62 and
28 today. The slice has its own scripted proof (A7a): `setup.sh` in a scratch home through a stub launcher, then the CLI
sequence. The app units (A2 to A6, A7b, C1, U1 to U4) follow.

**Proof (A6):** on a scratch `AW_SINDRI_HOME` and `AW_STATE_DIR` (never a temp `HOME`), through `setup.sh`'s tail with a stub
`sindri-app` in CI (`scripts/tests/setup-sindri-app.test.sh`, `AW_SINDRI_APP=PATH`, a faked desktop; the real run, with the
real publish, is the owner check, `scripts/owner-check.sh`): `setup.sh` publishes with `--publish-only` and **exits 0 with
the app alive**, meaning `sindri-app status --json` answers (with no desktop session it prints `sindri-app --headless` and
exits 0; a start that does not answer exits 1); from a fresh home the six steps show (`setup/first-run`); `profile` is
`todo` until a repo is added, and `recipes` is `todo` with `action: null` until the profile is approved; add a repo
through the route as the human; the profile step asks; a session's attempt at the same approval is `SND-AUTH-002`, and so
is a session's attempt at each `AppConfirm` row; a typed confirmation approves; the MCP step writes the providers'
files in the scratch repo and not the Codex trust row, and on a box whose profile pre-dates this build writes nothing until
"Turn on for your repos" is pressed; recipes are registered (26, `unchanged` on the second start); a recipe file edited on
disk, or an untracked one, is not registered, not embedded and not in the plan; a held approve-all runs as a job and approves them in order (the typed answer
works too); a never-approved recipe with a `step.ship.*` capability is in the plan, with its sentence, not in
`ownApproval`; a previously approved recipe that gains one is in `ownApproval`; a recipe edited between the plan and the
POST gives `SND-RECIPE-009` and nothing applied; a refused recipe skips only the recipes that wait on it and the rest are
approved; **a work item started after approve-all runs its first effect with no `outbox.refused`** (the default profile
allows the effects the shipped recipes declare); a second scratch home on the same box shows `recipes` as needing one
approve-all; the map step runs the refresh job and shows "Retrying" after a forced failure; Onyx `skipped` (Not now, or Docker missing) lets the Home line go and emits `setup.done`, and a not-started Onyx is `todo`; the Onyx password sentinel appears nowhere (every path of §5.6). **Measure:** commands
typed from install to a working Sindri: 1 (target) on a box with a desktop, against 62 today; the time from the first app
open to all six `done` or `skipped` (`setup.done`); and the time from setup done to the first work item's first
transition (`setup.first-item`).

## 14. Questions for the owner (decided)

**Decided (owner, 2026-10-20): the recommended answer, for every question below.** Each was a real decision; the
question and its recommendation are kept as the record.

- **Q1. Approving in the app instead of the terminal.** The profile and the recipes are approved by typing or holding in
  the app, for those three actions only; revoke, grants, accept and a manual map promote stay terminal. The app holds the
  per-start token in a file that any program running as you can read, so this is a trust boundary for now. Is that the
  line? *Recommended: yes, exactly these three.*
- **Q2. Registering Sindri in your repos by itself.** When you approve a profile that adds a repo, Sindri registers its
  MCP server in that repo for Claude Code, Codex and Cursor on its own, and repairs it when the port changes. It does not
  trust the folder for Codex; Codex asks you once. Or should that be a button you press? *Recommended: automatic, with the
  off switch in the profile, because approving the profile already said which repos.*
- **Q3. The map without a person.** The cleanup agent decides what stays in the map (including a model's keep or
  remove), and the result becomes the current map with no review. It works read-only, never removes more than a third of
  the map in one run, and every choice can be undone in the Context screen. #433 said model guesses should wait for a person.
  Which? And what daily limit on its model calls (the step shows the day's use)? *Recommended: the agent decides, with Undo,
  the removal limit and the "cleanup: unavailable" case rejecting the guesses, as the context lane built it; it is what "no
  manual step" means. Limit: 40 calls per repo per day to start, with no separate machine cap.*
- **Q4. Your Onyx password in the app.** The app's form takes your Onyx admin login after you sign up in the browser, uses
  it once and forgets it; it is never written anywhere, and a restart asks again. The terminal command stays. Or terminal
  only? *Recommended: allow the form.*
- **Q5. After a Sindri update changes recipes.** You hold the approve-all button once more (the first time after this
  change re-approves all 26, because every recipe gets a display name and a new version). Or should an update auto-approve
  shipped recipes whose capabilities did not change? A recipe that gains a capability always waits for you on its own.
  *Recommended: hold again; a recipe can run commands, and one hold is cheap.*
- **Q6. The secret-check hook on a new repo.** The TypeScript build installs a pre-commit hook that refuses
  secret-shaped text. The C# build has no installer yet. Add the repo now without it and say so on the step (and in the
  health check), or hold the repo step until the installer exists? *Recommended: add now, say so, and install the hook when
  the installer ships.*
- **Q7. What happens to #433.** Keep its recipe half as an optional extra for recipes you write in your own repos, and
  drop its map-promote half as replaced here? *Recommended: yes.*
- **Q8. Which folders can be added.** Any git checkout your user can read, except `/`, your home folder itself, and the
  Sindri home and its parents; the app shows the real folder and its size before you approve. Or only folders under your
  home? *Recommended: any readable git checkout with those exceptions.*
- **Q9. Does setup.sh still set up Onyx at the terminal?** It does not: it installs, publishes the app and opens
  it, and Onyx is a step in the app. A flag (`--with-onyx`) keeps the old terminal run for a box with no desktop. (The first slice, A7a, keeps the old default until the app steps exist.) Or keep
  the old default and run Onyx at the terminal first? *Recommended: Onyx in the app by default, the flag for terminal-only
  boxes. The old default made you type a login and a profile approval before the app existed.*
- **Q10. Approving a profile change you just made yourself.** Picking a folder, or setting up Onyx, changes the profile,
  and you then type the approval. That is one typed approval if Onyx runs before you approve (the preview holds both), two
  if after (the Onyx row says to do it first). Should a change you made by your own pick or button be approved by that act, or by the hold, instead of typing?
  *Recommended: keep it typed; the profile is what widens what Sindri may do, and one typed line per new repo is cheap.*
Dropped: none. Q4, Q5, Q8 are reworded for the changes above; Q3 loses the machine cap in rev 4. Q11 (rev 2) is answered in §5.3: approve-all carries on past a refusal and skips only the recipes that wait on it.

## 15. Not in this spec

Multi-user setup (X61), a Windows setup, installing Docker (Onyx O5), the shipped recipes' own content, importing an
authored map (#433's deferred part), changing the sandbox (SB specs), the Context screen's design beyond the Undo and the
"changed by the agent" label it already has, a `challenge.shown` acknowledgement before a held answer (a later round), and
a browser head for approvals (this channel is desktop-only).
**Another person on the same box.** The profile, the MCP registration, the recipe registry and the recipe approvals are all
per `$AW_SINDRI_HOME` (they live in that home's `ledger.db`); only the shipped set, which comes from the build, is shared. A
second person with their own home sees all six steps as new: `recipes` needs one approve-all there, and the profile one
typed confirm.
