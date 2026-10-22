# Spec: Setup (Avalonia build spec)

Screen `setup` (the rail's Settings). Inputs: `screens/setup.html`, `setup.notes.md`, `setup.questions.md` (all Recommended answers are decisions), `data/CURRENT-UI.md`, `SPEC-BRIEF.md`, and the setup-once spec (`docs/superpowers/specs/2026-10-19-sindri-setup-once-design.md`, rev 4, owner-approved 2026-10-20). Code read at origin/main (fetched 2026-10-20, tip `b6f564402`): `src/Sindri/Aw.Sindri.Ui/{Views/SettingsView.axaml,ViewModels/Settings,ViewModels/Approvals,Views/Approvals/ApproveAllDialog.axaml,Api/Workflow/{AppChallengeView,AppChallengeRequest,ApproveAll*}.cs,Api/ISindriApi.Approvals.cs,ViewModels/Labels.Approvals.cs,ViewModels/Components/ProtectedAction*}`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{workflow-recipes-approve-all,commands,profile,profile-approve}`.

**Merge status of the setup-once spec.** The spec file is NOT on main: it exists only on `origin/docs/sindri-setup-once` (rev 4). Built on main: the approve-all plan and job routes (#474: `GET` and `POST /sindri/workflow/recipes/approve-all`, goldens `workflow-recipes-approve-all/{plan,plan-first-approval,applied,refused,empty}.json`), the UI client for the app challenge and approve-all (`ISindriApi.Approvals.cs`), `ApproveAllViewModel` and `ApproveAllDialog`, error `SND-SETUP-007`. NOT built: `GET /setup`, `POST /setup/*`, `POST /onyx/*`, the app challenge route `POST /workflow/challenges/app`, the `AppConfirm` flag (so `ApproveProfile`, `ApproveRecipe` stay `cli-only:typed-confirmation` and `ApproveAllRecipes` is `pending:SO-E3` in `catalog.json`), `confirm` on catalog rows, the `setup:*` needs-you rows, `service:setup`. The ui lane builds against fixtures copied from that spec's section 8 (setup-once units SO-U1 to SO-U4).

## 1. Goal and the approved decisions

One Setup checklist of six steps (repo, settings, workflows, coding agents, map, Onyx), with a first-run prompt, the two app confirmations (typed and held) built as shared controls, the Onyx form, and a map that builds itself. The page changes no rule: it sends only `desk`, `job`, `decision` rows and the three `AppConfirm` rows; every other protected action stays terminal copy.

| Q | Decision (Recommended) | Effect |
|---|---|---|
| 1 | The six steps live on the Settings page, always there; Home links to it until all six are done; a new repo adds its rows again | Checklist is the first section of `SettingsView`; Home gets a one-line link (this replaces setup-once 5.7's three-row Home card: open item 1) |
| 2 | Finished rows stay, collapsed, with their detail line | Row keeps the `done` badge and shows `detail` instead of `why` |
| 3 | The whole workflow list is shown before the hold | Plain `ItemsControl`, no inner scroll box |
| 4 | The map step has no button; progress, and Retry on failure | Row shows a meter; action only when `failed` |
| 5 | The Onyx terminal alternative is one line under the list | Footnote with a copy chip, not inside the row |

Also approved in the rounds: typed confirm is the short phrase bound to the change's fingerprint (needs-you Q10), approve-all is a 1.5 s hold (Q11), status words are To do, Working, Done, Failed, Needs you; "Needs you" uses `Attention` only.

## 2. Regions and layout: shared controls first (defined once; `specs/needs-you.md` references them)

Both live in `Aw.Sindri.Ui/Controls/` with their own view models so any page can host them. They draw state only; the caller owns the challenge. Neither can send a protected action other than the three `AppConfirm` ones: the host passes an `AppConfirmAction` that is one of `ApproveProfile`, `ApproveRecipe`, `ApproveAllRecipes`, and the control refuses to construct for any other id (a unit test, plus the architecture rule that the app references only the app challenge route, setup-once SO-12).

### 2.1 `TypedConfirmBox` (ApproveProfile and ApproveRecipe)

| Part | Spec |
|---|---|
| Layout | one row: label "To approve, type" + `code` chip (mono 12, `Surface1`, `HairlineStrong`, `Radius.XS`) + `TextBox` (32 px high, max 200, mono 13, `Surface2`, ring `HairlineStrong`) + primary button; wraps under the preview below 700 px |
| Input | the phrase is `AppChallengeView.TypedAlternative`, else `Action` (the served text). Compared after trim, case sensitive, exact. Match turns the border `Success` plus a tick glyph (never colour alone) and enables the button |
| Button | "Approve settings" or "Approve workflow" (`Primary` fill); disabled until exact match; Enter in the field presses it when enabled |
| Flow | on open: `POST /workflow/challenges/app {action}` -> challenge (good 5 min). On press: the host calls the existing approve route with `{challenge, typed}` (profile: `POST /profile/approve {hash, answer}`; recipe: the recipe approve route). The challenge is bound to the fingerprint, so a changed list answers `SND-PROFILE-003` or `SND-RECIPE-009`: the control shows "This changed while you were reading it. Read it again, then approve." and asks for a fresh challenge |
| Guards | one press sends once; the field clears and disables while sending; expiry (5 min) shows "That took too long. Asking again." and re-issues; a `session`-caller refusal `SND-AUTH-002` shows `Labels.ApproveSessionRefused` |
| Fallback | catalog row not `ready` (today): the control is replaced by the existing `ProtectedActionButton` (`⌂ Approve settings · terminal`, copy of the served `cli`) |
| Hint | "The phrase is tied to exactly this list. If your settings change before you press the button, Sindri asks again." |
| Accessibility | field name "Type the phrase to approve" (existing `Labels.ApproveTypeName` pattern); mismatch is announced once after a pause, not per key; the button's name carries the subject |

### 2.2 `HoldButton` (ApproveAllRecipes)

Extracted from `ApproveAllDialog`/`ApproveAllViewModel.Answer.cs`; the dialog is rebuilt on it (its tests keep passing).

| Part | Spec |
|---|---|
| Layout | 36 px high, min width 260, `Radius.SM`, `Surface2` fill, `HairlineStrong` border, centred label with a hold glyph; the fill is a `Border` whose width is 0 to 100% of the button, colour `AttentionFill` (NEW token, below), clipped to the radius; no shadow |
| Hold | pointer down, or Space or Enter held, starts a `DispatcherTimer` (50 ms) over `minHoldMs` (served, 1500; never hard-coded); keyboard auto-repeat keydowns are ignored, only key-up resets. Pointer up, leave, `PointerCaptureLost` or key-up before the time cancels: fill resets, text "Not approved, hold again" (announced) |
| Complete | at `minHoldMs` the host calls approve-all with `{challenge, heldMs}`; the button shows "Approving..." and disables until the job answers |
| Text | idle "Hold to approve all {n}" (`Labels.ApproveHoldName` shows the full form to screen readers); holding "Keep holding, 0.8 of 1.5 seconds"; reduced motion: a stepped counter (tenths of a second) instead of a smooth fill |
| Announcements | at the start ("Holding. Keep holding to approve.") and at the end only, never per tick (existing `LiveAnnouncer`) |
| Typed alternative | always offered under the button: "Or type this text to approve:" + `TypedConfirmBox` bound to `TypedAlternative` (`approve-all <digest6>`); either redeems |
| Refusals | `SND-SETUP-003` (short hold or answer within 2 s): does not consume the challenge; the button resets with "Hold for the full time"; `SND-RECIPE-009` (plan changed): re-read the plan; `SND-JOB-409`: "Approve all is already running."; `SND-SETUP-007`: "Approve your settings first." |
| Fallback | catalog row not `ready` (today `pending:SO-E3`, then `cli-only`): the existing "copy `sindri recipe approve-all`" `ProtectedActionButton`; the plan list still shows |
| Expiry | after 5 min the control asks again with the same list ("That took too long. Asking again: hold once more.", existing `Labels.ApproveExpired`) |

### 2.3 Tokens for the controls
| Role | Token |
|---|---|
| hold fill | NEW `Color.AttentionFill` = `#624328` (`Attention` at 35% over `Surface2`, precomputed, opaque). Add to `Tokens.axaml` with its `Brush.AttentionFill` mirror; `TokenTests` (key set, mirror), `ContrastTests` (`Ink` on `AttentionFill` about 9:1; `AttentionFill`/`Surface2` as decoration). `FluentOverrideTests` unchanged |
| everything else | existing: `Surface1/2`, `Hairline`, `HairlineStrong`, `Success`, `Attention`, `AttentionSoft`, `Primary`, `OnPrimary`, `Ink*` |

### 2.4 Page regions

Replace the top of `SettingsView` (keep `TitleText`, `ReduceMotionSwitch`, `TextScaleSlider` and their `x:Name`s; the local preferences move into a "This app" section below the checklist). Content column max 760, centred, 24 px side padding, `StackPanel Spacing=16`. Entered from the rail Settings, `⌘,`, the chord `G U` (setup-once), the deep link `setup`, and the Home link line.

| Region | Size and tokens | Avalonia |
|---|---|---|
| Page title | "Settings" (`PageTitle`) | existing `TitleText` |
| Lead and sub | lead 20/28 semibold ("Set up Sindri", or "Sindri is set up"); one `Caption` sub line, hidden when done | `TextBlock`s |
| Progress strip | six 4 px segments, gap 4, `Radius.XS`: done `Success`, working `PrimaryHover`, else `Surface3`; plus "N of 6 done" (`Caption`, tabular) | `UniformGrid Columns=6` of `Border`, own `SegmentStrip` |
| Run-line | 40 px, `Surface1`, `Hairline`, `Radius.MD`: "You've run ./setup.sh --with-sindri" with the command in mono | `Border` |
| Checklist panel | `Surface1`, `Hairline`, `Radius.MD` (prototype 8), six rows separated by `Hairline` | `ItemsRepeater` of `SetupStepRow` (one panel, no nested cards) |
| Step row (collapsed) | min 64 px: 24 px number circle (`Surface3`; a check on `SuccessSoft`/`Success` when done), title `BodyStrong` 14, why line `Caption` `InkMuted` (done rows show `detail`), status badge, one primary action, chevron | `Grid ColumnDefinitions="24,*,Auto,16"`; the row is a focusable `Expander` header |
| Step row (open) | body indented 56 px, padding 12/16/16 | `Expander` content |
| Status badge | word plus glyph, 22 px, `Radius.Pill`: To do (`InkSubtle` on `Surface3`), Working (`PrimaryHover` on `PrimarySoft`, spinner `Motion.Spin`, static under reduced motion), Done (`Success` on `SuccessSoft`, tick), Failed (`Error` on `ErrorSoft`, cross), Needs you (`Attention` on `AttentionSoft`, person glyph) | `StateChipView` (existing) with these five |
| Primary action | 30 px `Size.Control`, `Primary` fill; one per row | `Button` |
| Plan list (workflows) | rows: index circle 16, name `BodyStrong`, capability sentences `Caption` `InkSubtle`, change word chip ("New", "Updated", "Needs a second look"); no inner scroll (page scrolls) | `ItemsControl` bound to `ApproveAllViewModel.Rows` |
| Hold row | `HoldButton` + note text right/below | shared control |
| Typed row | `TypedConfirmBox` | shared control |
| Per-agent list | three rows: badge, name, result text right | `ItemsControl` |
| Map meter | 6 px bar, `Radius.XS`, `Surface3` track; fill `PrimaryHover` (working) or `Error` (failed) + "1,240 of 3,100 files" `Caption` | own `ProgressBar` style |
| Error panel | `ErrorSoft` fill, `Error` word and glyph, plain sentence, mono code row in `InkSubtle` | `Border` |
| Onyx form | two labelled `TextBox`es (visible labels "Onyx email", "Onyx password"; password has a show/hide toggle), 32 px, `Surface2` | `StackPanel` |
| Grey code row | mono 11 `InkSubtle`: the step id and the event (e.g. `setup:repo  repo.added`) | `TextBlock` |
| Footnote | `Caption` `InkMuted`: "Prefer a terminal? Run `sindri-app onyx up`" + copy chip | under the panel |
| Done panel | `SuccessSoft` fill, `Success` tick + sentence + "Open Workflows" button | shown at six done |
| This app section | existing Reduce motion switch and Text size slider, untouched | existing controls |
| Home link line | on Home above Needs you while any step is not `done` or `skipped`: "Set up Sindri: 2 of 6 done" + the first not-done step's title + "Continue" (deep link `setup`); one line, 40 px, `Surface1` | `Border` in `HomePage`; added to `HomeFold` accounting (the fold test moves) |

Open rows: opening a row never closes another. A step in `needs-you` or `failed` opens by default; others collapsed. At 900 px the action wraps under the title and the plan rows stack their capability line.

## 3. States

| State | Prototype hash | How the VM reaches it |
|---|---|---|
| First run | `#fresh` | `GET /setup` first-run (`repo` needs-you or todo, others todo with `action: null` except `onyx`); row 1 open, first-run prompt: the lead "Set up Sindri" and sub "Six steps. Sindri does most of them for you." |
| Settings approval | `#profile` | `profile` step `needs-you`; row open; `TypedConfirmBox` bound to the live hash challenge; phrase shown `approve 3f9a1c` |
| Workflows approval | `#approveall` | `recipes` step `needs-you` (profile approved); plan read; `HoldButton`; while the job runs the step is `running` with `progress {done,total}` |
| Done | `#done` | all six `done` or `skipped`: lead "Sindri is set up", done panel, rows collapsed, `setup.done` read from the view |
| Failed | `#failed` | a step `failed` (the map after three failures, Onyx `needs-you`): error panel with the plain sentence and Retry or the Onyx form |
| Onyx waiting for login | in `#failed` | job parked on `onyx-account`: step `needs-you`, form open, focus in the first field |
| Docker missing | none | `onyx` `skipped` with reason "Docker isn't installed" and a link; counts as finished |
| Loading | none | six-row skeleton (`Motion.Pulse`, static under reduced motion) |
| Views rebuilding, stalled | none | existing `asOf` banners above the checklist |
| Sindri not running | none | `GET /setup` fails to connect: "Sindri isn't running", the launcher command `sindri-app` with a copy chip, "The app reconnects on its own when Sindri starts." No Start button |
| Not served yet | none | `GET /setup` answers 404 (this build): "Setup isn't available in this build yet." and the existing "not served yet" line; the three confirmations stay usable through their own routes |
| 900 px | `setup-900.png` | action wraps, plan stacks, content 24 px gutter, no horizontal scroll |
| Annotations | `?annot=1` | dev only, NOT built |

A step never moves backwards by itself except when its inputs change (a new repo, a new build): the row reopens and the Home link line returns.

## 4. Data

| Region | Route / fields | Status |
|---|---|---|
| Six steps | `GET /sindri/setup` -> `SetupView{steps[{id,title,state,why,detail,error{code,sentence},action,progress{done,total},items[]}], asOf}` (setup-once 7.2; goldens `setup/{first-run,all-done,running-progress,...}.json` on the spec branch) | PENDING: api lane (SO-A1). Build against fixtures copied from the spec's section 8; fallback "Setup isn't available in this build yet." |
| Titles and the one-line glosses | the UI owns them (spec: "a client may show its own"); keyed by step id (Copy) | local |
| Step `repo` | `POST /setup/repos {path,name?}` (`AddRepo`, desk); errors `SND-SETUP-001/002` | PENDING: api lane (SO-A3). Fallback: the row shows "Not available in the app yet" and no button (the terminal `sindri repo onboard <path>` is the only path today); no fake button |
| Folder picker | desktop-only `IStorageProvider` folder dialog; typed path field beside it | local (UI lane); other heads show the field only |
| Step `profile` preview in plain words | repos with resolved path and size, services, allowance, connections, human-only stops | PENDING: api lane (SO-A2/A3; `GET /profile` serves only changed key paths today, `Golden/Api/profile/*`). Fallback: the changed key paths as `+`/`-` lines |
| Typed challenge | `POST /workflow/challenges/app {action}` -> `{challenge, action, expiresAt, confirm:"typed", typedAlternative?}` | Client SERVED (`AppChallengeView`); server route PENDING: api lane (SO-A2). Fallback: terminal copy (`ProtectedActionButton`, served `cli`) |
| Profile approve | `POST /profile/approve {hash, answer}` (golden `profile-approve/{preview,approved,stale-hash,refused-session}.json`) | SERVED route; the app path for `human` is PENDING SO-A2 (`refused-no-terminal.json` is today's answer) |
| Workflow plan | `GET /workflow/recipes/approve-all` -> `ApproveAllPlan{recipes[{id,displayName,version,hash,previousApprovedHash,change,changeWord,capabilities[{id,sentence}],delta{added,removed},changedBlocks,children}], ownApproval[], digest, empty, asOf}` | SERVED (#474; `Golden/Api/workflow-recipes-approve-all/{plan,plan-first-approval,empty}.json`; UI type `Api/Workflow/ApproveAllPlan.cs`) |
| Per-workflow "passed N of N" | none: the plan has no `evals` (setup-once 7.2: shipped recipes pass their checks at publish) | NOT SERVED by design. Replace the prototype's per-row chip with one fixed line "Every workflow passed all of its own checks when Sindri was built." (open item 3) |
| Capability sentences | `capabilities[].sentence` | SERVED |
| "Needs its own look" | `ownApproval[{id,displayName,reason,sentence,added[]}]` | SERVED; each row opens `ApproveRecipe` via `TypedConfirmBox` (action `ApproveRecipe`, same flip as above) |
| Approve-all apply | `POST /workflow/recipes/approve-all {challenge, heldMs|typed}` -> 202 `ApproveAllStarted{accepted{commandId,sequence},jobId}`; job `GET /jobs/{id}` -> `ApproveAllResult{approved[],refused[{id,hash,code,sentence,cli}],skipped[]}`; golden `applied.json`, `refused.json`, UI golden `SpecShaped/recipes-approve-all-start/refused-short-hold.json` | SERVED (route), the app challenge to redeem it is PENDING SO-A2 |
| Step `mcp` per coding agent | `mcp.registered{providers[{provider,outcome}]}`; `POST /setup/mcp` (`RegisterMcp`, desk) | PENDING: api lane (SO-A4). Fallback: the row shows `why`/`detail` text only, no per-agent list; the fixed lines "Codex and Cursor will each ask you to trust the folder once." |
| Step `map` progress | `progress{done,total}` (the spec says repos; the prototype shows files: a files figure is a new field) | PENDING: context lane (SO-C1; files progress is extra). Fallback: a meter of `progress` as served (repos) or an indeterminate bar; no number invented |
| Map retry | `RefreshContextMap` (job; `POST /context/map/refresh`, "Retry now") | PENDING: context lane (catalog row owned by SO-C1). Fallback: no button; "Retrying in N minutes" from `detail` |
| Onyx start and login | `POST /onyx/setup` (`SetupOnyx`, desk), `POST /onyx/account {adminEmail,adminPassword}` (`ConnectOnyx`, desk), `POST /onyx/defer` ("Not now"); errors `SND-SETUP-005/006`, `SND-ONYX-001` | PENDING: api lane (SO-A5). Fallback: row shows the footnote command only |
| Docker state, "Not now" | `skipped` with `why`; `onyx.deferred` | PENDING SO-A5 |
| Errors in plain words | `error.sentence`; the UI keeps a code-to-sentence map for `SND-SETUP-001..007` as the safety net (prototype text: 001 "That folder isn't a git checkout, or can't be read", 002 "That repository is already added", 003 "Hold the button for the full time", 004 "Add the repository and approve your settings first", 005 "Enter your Onyx email and password") | sentences SERVED once the view is; map is local |
| Home link line | the same `SetupView` (first not-done step, count) | PENDING SO-A1; hidden when the view is not served |
| `setup:*` needs-you rows | `setup:repo`, `setup:recipes`, `setup:mcp` | PENDING SO-A1/A6; while the Home line shows (any step open), Home hides every `setup:*` row; they still appear in Needs you elsewhere (setup-once rev 5, #502) |

## 5. Actions and keys

| Control | Action id (class, today's `ui` status) | Rule |
|---|---|---|
| Choose folder / Add repo | `AddRepo` (desk). NEW action id needed: api lane (SO-A3, golden row and label; not in `catalog.json` today) | sends; a session is refused |
| Approve settings | `ApproveProfile` (protected, `cli-only:typed-confirmation` today) | `TypedConfirmBox`; terminal copy until the catalog row says `ready` and carries `confirm: "typed"` |
| Approve one workflow ("Needs its own look") | `ApproveRecipe` (protected, same) | `TypedConfirmBox` |
| Approve all workflows | `ApproveAllRecipes` (job; `pending:SO-E3` today) | `HoldButton` + typed alternative; terminal copy until `ready` |
| Register agents | `RegisterMcp` (desk). NEW action id needed: api lane (SO-A4) | "Connect" button on a failed provider; the automatic run needs no press |
| Retry the map | `RefreshContextMap` (job). NEW action id needed: context lane (SO-C1) | Retry on `failed` |
| Set up Onyx / Connect / Not now | `SetupOnyx`, `ConnectOnyx`, `DeferOnyx` (desk). NEW action ids needed: api lane (SO-A5) | form never sends a password to a log; one press sends once |
| Retry (generic) | the step's own `action` id | |
| Run the terminal alternative | local copy of `sindri-app onyx up` | no id |
| Copy the launcher command ("Sindri isn't running") | `Clipboard` | local |
| Open Workflows (done panel) | page navigation | local |
| `⌘,`, `G U` | open Settings, scroll to the checklist | local chord (`ChordKeysTests`) |

Keys (focus in the checklist; no `TextBox` focused): Up/Down move between rows, Enter or Space toggles a row, Tab goes row header to its action to its body. In the hold: Space or Enter held. Esc closes an open Onyx form and returns focus to the button. After a step completes focus stays where it was; one polite live region says "Workflows: done".

Protected-action rules applied: terminal copy everywhere except typed `ApproveProfile` and `ApproveRecipe` and held `ApproveAllRecipes`. The password field is excluded from logs, the clipboard and the accessibility tree's value dump (`PasswordChar`, `Revealed` toggle).

## 6. Copy (`Labels.Setup.cs`, new partial; no em dashes, no internal terms)

| Key | Text |
|---|---|
| `SetupLead` / `SetupLeadDone` | "Set up Sindri" / "Sindri is set up" |
| `SetupSub` | "Six steps. Sindri does most of them for you; you decide the ones that give it access." |
| `SetupCount(n)` / `SetupCountAll` | "{n} of 6 done" / "All 6 done" |
| `SetupRunLine` | "You've run ./setup.sh --with-sindri" |
| `SetupTitleRepo` / `Profile` / `Recipes` / `Agents` / `Map` / `Onyx` | "Add your repository" / "Approve your settings" / "Approve the workflows" / "Connect your coding agents" / "Build the product map" / "Connect Linear and Slack (Onyx)" |
| `SetupWhyRepo` | "Sindri works inside a repository you choose. Pick its top folder." |
| `SetupWhyProfileWait` / `Needs` / `Done` | "Waits for a repository." / "Your settings changed. Sindri works only inside what you approve here." / "Sindri works only inside what you approved." |
| `SetupWhyRecipes*` | "Waits for your settings." / "{n} workflows are loaded and waiting. Read what each may do, then approve them all at once." / "All {n} workflows can run." |
| `SetupWhyAgents*` | "Waits for your settings." / "Sindri is adding itself to this repository for Claude Code, Codex and Cursor." / "Claude Code, Codex and Cursor can reach Sindri in this repository." |
| `SetupWhyMap*` | "Waits for your settings." / "Sindri reads your code and builds a map of it. It builds itself and keeps up to date." / "The map is up to date with your code." / "The map could not be built." |
| `SetupWhyOnyx*` | "Onyx lets Sindri read your tickets and chats. Sign in once." / "Sign in to Onyx once. Your password is used for this step only." / "Sindri can read your Linear and Slack context." |
| `SetupStateTodo` / `Working` / `Done` / `Failed` / `NeedsYou` | "To do" / "Working" / "Done" / "Failed" / "Needs you" |
| `SetupActChoose` / `Review` / `ReviewAll` / `Retry` / `Connect` | "Choose folder" / "Review and approve" / "Review and approve all" / "Retry" / "Connect" |
| `SetupFolderLabel` / `Browse` | "Folder" / "Browse" |
| `SetupRepoBody` | "Choose the top folder of a git checkout. Adding it changes your settings, so you approve them next." |
| `SetupRepoNote` | "Linked worktrees resolve to their main checkout. If the folder is already added, or is inside Sindri's own folder, Sindri says so here and changes nothing." |
| `SetupProfileLead` | "This is what your settings let Sindri do. Nothing runs outside it." |
| `SetupSumRepos` / `Own` / `Allowance` / `Connections` / `Stops` | "Repositories" / "Runs on its own" / "Work allowance" / "Connections" / "Needs you for" |
| `SetupTypeAsk(phrase)` | "To approve, type {phrase}" |
| `SetupApproveSettings` | "Approve settings" |
| `SetupTypedNote` | "The phrase is tied to exactly this list. If your settings change before you press the button, Sindri asks again." |
| `SetupRecipesLead` | "Each workflow is listed with what it may do. The small ones come first; the bigger ones below use them." |
| `SetupChecksLine` | "Every workflow passed all of its own checks when Sindri was built." |
| `SetupHoldIdle(n)` / `Holding` | "Hold to approve all {n}" / "Keep holding" (the 1.5 s figure and counter come from `Labels.ApproveHold`, `ApproveHolding`) |
| `SetupHoldNote` | "Hold for 1.5 seconds. Let go to cancel. Sindri refuses a shorter hold." |
| `SetupAgentConnected` / `Connecting` / `NotStarted` | "Connected" / "Connecting" / "Not started" |
| `SetupTrustOnce` | "Codex and Cursor will each ask you to trust the folder once." |
| `SetupAgentsNote` | "Only this repository, never your whole account. Skipped automatically if a coding agent isn't installed." |
| `SetupMapProgress(done,total)` | "Building, {done} of {total}" |
| `SetupMapNoButton` | "No button on purpose: the map builds itself and updates whenever your code changes." |
| `SetupMapFailedAt(done,total,n)` | "Stopped at {done} of {total}. Failed {n} times in a row." |
| `SetupMapRetries` | "Sindri also retries on its own with longer waits." |
| `SetupOnyxIntro` | "Create your Onyx account in the browser first if you haven't, then sign in here." |
| `SetupOnyxEmail` / `Password` / `Show` / `Hide` | "Onyx email" / "Onyx password" / "Show password" / "Hide password" |
| `SetupOnyxNote` | "Used once to connect Linear and Slack. Never stored, never shown in any log." |
| `SetupOnyxFoot` | "Prefer a terminal? Run sindri-app onyx up" |
| `SetupDockerMissing` | "Docker isn't installed" with link "Install Docker" |
| `SetupDonePanel` / `OpenWorkflows` | "Sindri is set up. You can start work from Workflows." / "Open Workflows" |
| `SetupNotRunning` / `Reconnects` | "Sindri isn't running" / "The app reconnects on its own when Sindri starts." |
| `SetupNotServed` | "Setup isn't available in this build yet." |
| `SetupHomeLink(n)` / `Continue` | "Set up Sindri: {n} of 6 done" / "Continue" |
| `SetupChanged` | "This changed while you were reading it. Read it again, then approve." |
| `SetupErr001`...`005` | the five sentences in Data |
| `SetupAgainOnyx` | "Onyx added a section to your profile. Approve the profile once more." |

Unknown step ids, states or provider words render the served string raw; unknown state uses the To do chip with the raw word.

## 7. Tests the ui lane must add or change

| Test class | Change |
|---|---|
| `TokenTests`, `ContrastTests` | add `Color.AttentionFill` (key set, `Brush.*` mirror, `Ink`/`AttentionFill`, decoration pair) |
| `NamedControlsTests` | keep `TitleText`, `ReduceMotionSwitch`, `TextScaleSlider`; add `SetupChecklist`, `SetupProgress`, `SetupHomeLink`, `SetupHold`, `SetupTyped`, `SetupOnyxForm` |
| `SettingsViewModelTests` | extend: the checklist is the first section; local preferences unchanged |
| NEW `SetupViewModelTests` | state to badge word per fixture (`first-run`, `running-progress`, `all-done`, failed), progress, default-open rule, finished meaning (`done` or `skipped`), "Sindri isn't running", not-served fallback |
| NEW `TypedConfirmBoxTests` | exact match only, trim, case, tick glyph, Enter, one send, `SND-PROFILE-003`/`SND-RECIPE-009` re-ask, expiry re-issue, refuses to build for any id outside the three, terminal-copy fallback when the row is not `ready` |
| NEW `HoldButtonTests` | fake clock: fill reaches 100% at `minHoldMs` (served value), early release resets and announces, key-up and capture-lost cancel, auto-repeat ignored, `SND-SETUP-003` does not consume, announcements only at start and end, reduced-motion stepped counter |
| `ApproveAllDialogTests`, `ApproveAllViewModelTests`, `ShellApproveAllTests` | stay green after the dialog is rebuilt on `HoldButton` and `TypedConfirmBox` |
| NEW `SetupFramesTests` | headless frames at 1440 and 900 for first run, profile, approve-all (hold at 62%), done, failed; no horizontal scroll at 900 |
| `HomeFold`/`StripFoldTests` | move: the Home link line adds 40 px above Needs you at 1280 x 800; Needs you still starts above the fold |
| `ActionLabelsTests`, catalog golden | new ids `AddRepo`, `RegisterMcp`, `SetupOnyx`, `ConnectOnyx`, `DeferOnyx`, `RefreshContextMap` need golden rows and unique labels when the api and context lanes add them; this spec adds none |
| `LabelsTests` | every key above, no em dash, no internal term; the five error sentences carry no code |
| API goldens | when `GET /setup` lands copy `setup/*.json` into `Golden/Api/setup/` with `SetupView` in `SindriUiJsonContext` and `FakeSindriApi` |
| `SpecTextTests`, `docs/csharp/sindri-ui.md` | update the Settings and Setup section |

## 8. Build order (each slice red test first)

1. Shared controls: `TypedConfirmBox`, `HoldButton`, the `AttentionFill` token, and `ApproveAllDialog` rebuilt on them. Red first: `HoldButtonTests.A_hold_shorter_than_the_served_time_approves_nothing` and `TypedConfirmBoxTests.Only_the_exact_phrase_enables_the_button`. Proves: both lifecycles, token pins, no behaviour change in the dialog.
2. Checklist shell on fixtures: `SetupStepRow`, `SegmentStrip`, `StateChipView` words, Settings restructure, Home link line, the deep link `setup` and `G U`. Red first: `SetupViewModelTests.First_run_opens_the_first_step_and_shows_to_do_for_the_rest`, then `SetupFramesTests` at 1440 and 900. Proves: the six states, finished meaning, the not-served and not-running fallbacks.
3. The two confirmations in their rows: profile typed row (preview lines, challenge, stale-hash re-ask) and workflows row (plan list from `ApproveAllViewModel`, hold plus typed alternative, "Needs its own look"), behind catalog readiness with terminal-copy fallback. Red first: `SetupViewModelTests.Profile_row_shows_terminal_copy_until_the_catalog_is_ready`. Proves: no protected id is sent; plan golden renders `New`, `Updated`, `Needs a second look`.
4. Agents, map and Onyx rows: per-agent list, map meter with Retry, Onyx form (labels, show/hide, errors under the field, focus rules), folder picker and the repo row. Red first: `SetupViewModelTests.Map_row_has_no_button_unless_it_failed`. Proves: Q4, Q5, password handling, one press sends once.
5. Live wiring (separate PRs after other lanes land): `GET /setup` and `POST /setup/*` clients, `setup:*` rows, the plain-words profile preview, files progress, per-agent outcomes. Each swaps a fixture for the served field.

## 9. Open items for the ui lane
1. Home: Q1 puts the checklist under Settings with a Home link, but setup-once 5.7 draws a three-row Setup card on Home and counts it in the fold. This spec follows the approved question (link line only). The setup-once spec needs the same edit; ask the api lane to record it.
2. Typed phrase: the approved short phrase `approve <hash6>` is not the server's text (`approve profile <hash6>`); see `specs/needs-you.md` open item 1. Show the served `action` until the server adds a `phrase`.
3. Per-workflow check results (prototype `passed N of N`) have no field by design; confirm the single fixed line is acceptable, or ask the engine lane for an `evals` summary.
4. Prototype lists 11 workflows; the real plan is the shipped set (26 today). The list must stay a full, non-scrolling `ItemsControl` (Q3); check the page length at 26 rows and group by `change` word if it is too long.
5. The "files done of total" map figure is not what the spec serves (repos); ask the context lane or keep a bar without numbers.
6. Folder picker: Avalonia `StorageProvider` on desktop; confirm the head list (no picker on browser).
7. Titles: the setup-once titles ("Add your repo", "Approve the profile") differ from the prototype's; this spec uses the prototype's, owned by the UI.

## Settled by ideas (2026-10-20, after review)
- Typed phrase: show the server's challenge text verbatim (`approve profile <hash6>`); no `phrase` field needed. The owner's
  "short phrase" is satisfied by the served text.
- Location: the checklist lives under Settings, with one "Finish setting up Sindri" line on Home while any step is open
  (owner Q1). The setup-once spec's three-row Home card needs the same edit: ideas has asked api (spec owner).
- "passed N of N" per workflow: dropped as written here (the plan serves no evals by design).
- New action ids: `AddRepo`, `RegisterMcp`, `SetupOnyx` are already in the setup-once spec (SO-2, api). `ConnectOnyx`,
  `DeferOnyx` (api) and `RefreshContextMap` (context) are new asks; ideas has sent them.
- Map Retry: the existing catalog action `RefreshContextMap` (Job row, `POST /sindri/context/map/refresh`, label "Refresh
  map", from F1 #494), not `RunService` (SND-SVC-404 for built-ins). A 409 with `runningJob` = already running: show
  "Already building" and follow that job. Not a new id (context lane, 2026-10-20).
- Onyx ids (api, 2026-10-20): `SetupOnyx`, `ConnectOnyx` (the login form after signing up in the browser) and `DeferOnyx`
  ("Not now", records `onyx.deferred`) are three existing rows in the setup-once spec (SO-A5), not new asks.
- Setup location: api amends the setup-once spec in a rev-5 docs PR after #445 (checklist under Settings, route `setup`,
  one Home line, U1 reworded). `GET /sindri/setup` and its goldens do not change.

## Answered by ui (2026-10-20, ui digest ~/.agentic-workflow/digests/sindri-ui-f3-spec-setup.md)
- Checklist under Settings plus the Home line (setup slice 2 building); map Retry = `RefreshContextMap`.
- 26-row list, grouped by `change` word only if too long (open item 4); no map file numbers until served; desktop-only `StorageProvider` picker; prototype titles.
- Setup-once rev 5 (api, 2026-10-20): draft PR #502, waiting for the owner's read. It puts the checklist under Settings (route `setup`, `G U`) and adds the one Home line, which hides `setup:*` rows on Home only. The checklist shows all six steps in step order (the three-row fold is gone). `GET /sindri/setup` and its goldens are unchanged, so U1 builds on the same fixtures. Matches this spec.
