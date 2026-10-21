# Spec: Needs you (Avalonia build spec)

Screen `needs-you`. Inputs: `screens/needs-you.html`, `needs-you.notes.md`, `needs-you.questions.md` (all Recommended answers are decisions), `data/CURRENT-UI.md`, `SPEC-BRIEF.md`. Code read at origin/main (fetched 2026-10-20, tip `b6f564402`): `src/Sindri/Aw.Sindri.Ui/{ViewModels/NeedsYou,Views/NeedsYou,ViewModels/Components/ProtectedAction*,ViewModels/Approvals,Views/Approvals,Api/NeedsYou*,Api/Workflow,Navigation/PageId.cs,Theme}`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{needs-you,decisions,commands,workflow-recipes-approve-all,workflow-instance,workflow-status}`. The typed-confirm and hold-to-confirm controls are defined ONCE in `specs/setup.md` section 2 ("Shared controls"); this spec references them as `TypedConfirmBox` and `HoldButton`. The run canvas (nodes, wires, zoom pill) belongs to `specs/run-canvas.md`; this spec adds only the takeover inspector block, the clock and the nudge.

## 1. Goal and the approved decisions

Turn today's Needs-you panel and sheets into one inbox page: a grouped list on the left, one request card on the right, and a fixed action bar under it. Four request kinds stay distinct. The page changes no server behaviour: every send goes through an existing catalog action, and protected ones keep the terminal rule.

| Q | Decision (Recommended) | Effect |
|---|---|---|
| 1 | One list grouped by kind under sticky headers | `ItemsRepeater` with a sticky group header template; no tabs |
| 2 | Approve and Send back live in a fixed bar under the scroll area | `ActionBar` row, outside the card, always visible |
| 3 | A on an unread request shows "Approve?", a second A approves | `ReadToEnd` flag set by the scroll position; the bar's primary turns attention-coloured |
| 4 | Send back needs a reason; send button disabled until text; Ctrl+Enter sends, Esc cancels | `ReasonBox` (TextBox `AcceptsReturn`) in the bar |
| 5 | Edit before approve is "our addition", not built | Placeholder only (section 5). OWNER LATER: out of scope for the ui lane |
| 6 | Stop 1 shows answered questions with the chosen option marked | Q&A section renders `fact:questions` plus the chosen answer |
| 7 | The 2-hour hand-back nudge stays until dismissed | Takeover toast with no timeout; asks for an action |
| 8 | Takeover clock reads "2h 50m left" (from the served paused timeout) | Text built from `paused.timeoutLeftSeconds` |
| 9 | Done-criteria ids (AC-1) shown small and grey | mono `InkSubtle` prefix on each criterion |
| 10 | Typed confirm phrase is the short form `approve <hash6>` | `TypedConfirmBox`; see open item 1 (the server's text is longer today) |
| 11 | Approve-all is a 1.5 s hold, not a typed field | `HoldButton`, reuses `ApproveAllViewModel` |
| 12 | "Handed back" toast fades after 5 s | informational toast, `Motion` timer, announced once |

Approved words from the review rounds: one takeover word, "You took over"; "draft" not "candidate"; no em dashes; amber (`Attention`) for "needs you" and, per the owner 2026-10-20, for DIDN'T WORK OUT (own glyph, served `attn` tone).

## 2. Regions and layout

The page is a new `NeedsYouPage` (UserControl, VM `NeedsYouPageViewModel`) shown in the page region while `PageId.Home` stays active (no new nav entry; `ShellNavTests` unchanged). Entered from Home "all N" (existing `Labels.AllRows`), Enter on a Home row, and the deep links `needs-you` and `needs-you/<row>` (existing). Esc from the inbox, or the breadcrumb "Home", returns to Home. Home keeps its capped five-row panel (HomeFold pinned).

`Grid ColumnDefinitions="400,*"` (330 under 1100 px). Minimum window stays 900 x 600.

| Region | Size and tokens | Avalonia |
|---|---|---|
| Breadcrumb | 44 px row: "Home > Needs you"; `InkMuted`, current word `Ink` | `StackPanel`, existing `Size.TopBar` is 48: use 48 (the prototype's 44 is dropped) |
| Inbox column | `Surface1`, right `Hairline` border | `Grid RowDefinitions="Auto,Auto,*,Auto"` |
| Inbox header | padding 12/16/8; title "Needs you" `PageTitle` ramp 15/22 semibold per prototype, use `DetailTitle`; count `Caption` `InkMuted` tabular | `TextBlock`s |
| "N new" pill | 26 px, `Radius.Pill`, `AttentionSoft` fill, `Attention` 1 px border, text `Attention` plus the word; shown while the pointer is over the list and rows are held (existing `SeenTracker`) | `Button` |
| Group header | 32 px sticky; glyph (`Attention`) + bold name + count; `Surface2` fill; hairline top and bottom | header template of the repeater (own `StickyGroupList`, below) |
| Row | min 56 px, two lines (title 13 medium + item 12 `InkMuted` + waiting time mono right; question 12 `InkMuted` one line ellipsis + "pauses in ..." 11 `InkSubtle`); 24 px kind tile (`AttentionSoft`, `Attention` glyph, `Radius.SM`); bottom `Hairline`; no card border, no side stripe | `Grid` data template; extend `NeedsYouRow.axaml` (keep its `x:Name`s) |
| Selected row | `Surface2` fill + 1 px inset ring `HairlineStrong` | style `:selected` |
| New row | tint pulse `AttentionSoft` to `Surface1` over `Motion.Pulse` 1.6 s (existing `RowTint`; never opacity); reduced motion: steady `AttentionSoft` plus the word "new" | `Animation` on `Background` between two brushes |
| Key hints | `Caption` row: J K move, Enter open, A approve, D send back; `Kbd` ramp | `WrapPanel` |
| Detail column | `Canvas` background with the dot grid (reuse the context page's dot `DrawingBrush`, `HairlineStrong` dots; plain `Canvas` if that has not merged) | `Grid RowDefinitions="*,Auto"` |
| Request card | one card, max 760, centred, `Surface1`, `Radius.LG` (12; the prototype's 16 is open item 5), padding 0, no shadow; head, then sections separated by `Hairline` | `ScrollViewer` > `StackPanel` of section templates; the card is a `Border` |
| Card head | 28 px kind tile, title `DetailTitle` 15, item link (`InkMuted`, underline on hover), copyable grey reference (mono 11 `InkSubtle` + copy glyph), kind word right ("Approval 1 of 3", `Eyebrow`, `Attention`) | `Grid` |
| "Why this needs you" | first section, tinted `Surface2`; paragraph 13/20; two boxes "If you approve" / "If you send it back" (`Surface1`, `Radius.MD`, `Hairline`) | `Border` + `UniformGrid Columns=2` (one column under 1100) |
| Other sections | caption (`Eyebrow`, capitals written by the label) then body; stats chips (`Radius.Pill`, `Surface1`, `Hairline`, 12/18); list rows 6 px padding, `Hairline` between; Q&A blocks `Surface2` with option buttons (`Surface1`, `Radius.MD`, selected ring `PrimaryHover`) | `ItemsControl` templates |
| Status footer | 36 px: "Waiting for you" + clock glyph left; "waiting 5h 51m · pauses and asks again in 6d 18h" right, tabular; `AttentionSoft` fill, `Attention` text | `Grid` |
| Action bar | fixed strip under the scroll, `Surface1`, top `Hairline`, padding 10/24/12, content max 760; buttons 32 px (`Size.Control` is 30: use 32, one-off), `Radius.SM`, gaps 8; primary = `Primary` fill `OnPrimary` text with `Kbd` chip; "Approve?" state = `AttentionSoft` fill, `Attention` border and text; terminal button = `Surface2`, `HairlineStrong`, mono 12 (`ProtectedActionButton` look); hint line 12 `InkMuted` under it | own `ActionBar` control (below) |
| Takeover inspector | 480 px (46% under 1100), `Surface1`, left `Hairline`; head (badge, title, link, takeover time, hand-back box, Cancel run), then step list | right panel of the run view; hosted beside the run-canvas control |
| Toasts | 340 px (300 for "Handed back"), `Surface1`, `Hairline`, `Radius.LG`, bottom 56 right 16; title 13 semibold, body 12 `InkMuted` | `Border` in the run view's overlay layer; no shadow |

Own controls (`Aw.Sindri.Ui/Controls/`): `StickyGroupList` (an `ItemsRepeater` in a `ScrollViewer`; the group header sticks by measuring offsets, about 120 lines, no dependency) and `ActionBar` (hosts the mode templates below). `TypedConfirmBox` and `HoldButton` come from `specs/setup.md`.

### Tokens
No hex outside `Theme/Tokens.axaml`. Map by role:

| Prototype role | Token |
|---|---|
| `--bg`, canvas | `Brush.Canvas` |
| `--card`, inbox, bar | `Brush.Surface1` |
| `--field`, `--muted`, selected row, why tint | `Brush.Surface2` |
| pills, menus | `Brush.Surface3` |
| `--border` | `Brush.Hairline`; rings `Brush.HairlineStrong` |
| `--fg`, `--muted-fg`, `--subtle` | `Ink`, `InkMuted`, `InkSubtle` (`InkTertiary` stays disabled only) |
| `--attn*` (needs you, "Approve?", new row) | `Attention`, `AttentionSoft` |
| `--ok*` (Done, "You chose this") | `Success`, `SuccessSoft` |
| "You took over" violet (`--with*`) | `PrimaryHover` text, `PrimarySoft` fill (the app has no violet; the role maps to indigo) |
| running blue | `PrimaryHover` glyph, `PrimarySoft` fill |
| `--primary` button | `Brush.Primary` / `OnPrimary` |
| selected option ring | `Brush.PrimaryHover` (2 px, `Size.FocusRing`) |
| NEW `Color.AttentionFill` | `#624328` (Attention at 35% over `Surface2`, precomputed so the token stays opaque): the `HoldButton` fill. Pin `Ink`/`AttentionFill` (about 9:1) and `AttentionFill`/`Surface2` as decoration in `ContrastTests`; add the key to `TokenTests` (key set and `Brush.*` mirror). `FluentOverrideTests` unchanged. Defined once in `specs/setup.md`; this spec only uses it |

## 3. States

| State | Prototype hash | How the VM reaches it |
|---|---|---|
| Pending | `#pending` | `NeedsYouPageViewModel.Mode = Pending`; the selected row's card shows with the primary action ready |
| Approve? | `#confirm` | A pressed on a row whose `ReadToEnd` is false and whose kind needs reading (`desk:` rows only): `Mode = Confirming`; button reads "Approve?"; hint "You have not read to the end. Press A again to approve." Scrolling to the end, J/K, or Esc clears it. Other kinds approve on the first A |
| Send back | `#sendback` | D (or the button) sets `Mode = SendingBack`; the bar becomes `ReasonBox` + Send + Cancel; focus moves to the box; Send disabled until non-blank text |
| Approved | `#approved` | the action was accepted: the card is replaced by the done card (glyph, "Scope approved: <item>", one sentence), the row leaves the list, J selects the next; the bar reads "Approved. The row has left your inbox." |
| Typed confirm | `#set`, `#wf` | the selected row's primary is `ApproveProfile` or `ApproveRecipe` and the catalog says `ready`: bar shows `TypedConfirmBox` (shared control) |
| Hold | `#all` | the row is `setup:recipes` and `ApproveAllRecipes` is `ready`: bar shows `HoldButton` (shared control) |
| Terminal-only | `#s3`, `#s1` merge | the primary is protected: bar shows the terminal button (copy); after the copy: "Copied. Paste it in your terminal to approve." in `Success` for `Motion` 1.5 s |
| Paused run answer | `#answer`, `#park` | a `park:` row: card has the answer form (section 5); the bar's primary is "Send answer" (or the terminal button when the picked option is protected) |
| Takeover | `#takeover` | the run view of a taken-over instance (reached from the row's item link or `takeover:` row): canvas node badge "You took over", footer "clock paused, 2h 50m left", inspector with the hand-back box |
| 2-hour nudge | in `#takeover` | the served `takeover:` row (see Data) sets `NudgeVisible`; the toast has "Hand back" and "Not yet"; Not yet hides it until the next served row |
| Handed back | `#handback` | after `ApplyCommand:handback` is accepted: node badge "Running", footer "clock running, 2h 50m left", nudge gone, quiet "Handed back" toast for 5 s |
| Empty | none | `empty` sentence from `GET /needs-you` ("Nothing needs you") centred in the detail column, inbox shows no groups |
| Loading / rebuilding / stalled | none | existing banners and `NeedsYouList.AsOf`; skeleton rows while the first read is out |
| 900 px | `needs-you-900.png` | inbox 330 px; "Why" boxes stack; inspector 46vw; key-hint row wraps |
| Annotations, mock-state switcher | `#ann`, top bar `seg` | dev only, NOT built |

Row selection survives a re-read by id. A row that clears while its card is open keeps its text and disables the bar with the existing "cleared" line (`Labels.Cleared`); focus moves to the next row.

## 4. Data

Row facts come from `GET /sindri/needs-you` (`NeedsYouRowView`, `Api/NeedsYouRowView.cs`; goldens `Golden/Api/needs-you/{desk-task,park,one-request,idle-takeover,fixture,large}.json`). The four kinds are told apart by id prefix and `kind`: `desk:` (kind `park`, label "desk task") = your approvals; `park:` and `takeover:` (kind `park`) = paused runs; `decision:` = agent decisions; `profile:drift` and `setup:*` = workflow and settings approvals. `failure:service:*` rows keep their existing group "Something failed" after the four (not in the prototype, kept so no row is lost).

| Region | Route / fields | Status |
|---|---|---|
| Inbox rows, order, waiting time | `GET /needs-you` rows: `id, kind, label, title, reason, item, repo, urgency, since, primaryAction, actions, cli, chip` | SERVED (`Golden/Api/needs-you/*.json`); waiting time = now minus `since` (existing `Labels.RowAge`) |
| Group counts, "N waiting, M runs" | counted on the client from the rows | SERVED |
| Row "pauses in ..." | instance `status.timeout{kind, inSeconds, on}` | SERVED on the instance (`Golden/Api/workflow-status/desk-task.json`, `workflow-instance/dev-desk-round-trip.json`); NOT on the row. PENDING: api lane (add `timeout` to the row). Fallback: the client reads the instance for the selected `desk:` and `park:` row only; the list row omits the phrase until the row carries it |
| "Approval n of 3", plain stage name | none on the row (`label` is "desk task") | PENDING: api lane (item: stop number and plain title on desk rows). Fallback: kind word "Your approval", title = row `title` |
| "Why this needs you" (3 texts) | the step's `why` text | PENDING: engine lane (F5, recipe step `why`). Fallback: the card shows the row `reason` sentence under the caption; the two "If you approve / send back" boxes are hidden |
| Request body for stop 1 and 2 (summary, stats chips, what changed, done-criteria AC-n, Q&A with chosen answer, documents) | facts: `GET /workflow/instances/{id}/facts/{key}` (`fact:scope`, `fact:questions` per `schemas/scoping/questions.json`, `fact:answers`) | PARTIAL. Facts are served; the fact viewers (scope-map, packet, decision) are U13 and not built. PENDING: ui lane (a scope viewer in `FactsPanel`). Fallback until then: the section "The request" shows the existing facts panel with a link per document; Q&A shows `fact:questions` with the chosen answer from `fact:answers`; stats chips are omitted |
| Recommended mark on a stop option | `recommended` on stop options | PENDING: engine lane (not in real data; interview options have it). Fallback: no "Suggested" tag |
| Stop 3 (merge) request: PR number, checks, proof, review, docs | `step.babysit.watch` is a stub in this snapshot | PENDING: engine lane (babysitter facts). Fallback: row `reason` and a link to the item |
| Stop outcomes (approve, revise, drop) | `current.desk{run, outputs, checks, completeAction}` (`Golden/Api/workflow-status/desk-task.json`); outcomes from the step | SERVED (`desk`); the outcome ids per stop are the step's (stop 1: `approve|revise|drop`; stop 2: `approve|reviseDesign|revisePlan|drop`) |
| Agent decision | `GET /decisions` (`DecisionView`: `question, options[{id,label,rejectLike,cli}], recommended, filedBy, filedAt, recordedOnly, notice`; `Golden/Api/decisions/pending.json`) | SERVED. "Asked by an agent session at 15:48" from `filedBy`, `filedAt` |
| Paused run: why, "Used all 3 tries", try history | row `reason`; `answerKind`; instance `stepRuns` (#469, `Golden/Api/workflow-instance/*`) | `reason` and `answerKind` SERVED; per-try lines from `stepRuns` SERVED where present. Fallback: the reason sentence only |
| Takeover: badge, node footer, "took over at 14:05 (2h 35m ago)", clock "2h 50m left" | instance `paused{...timeoutLeftSeconds}` (field is `null` while not taken over, see `dev-desk-round-trip.json`); takeover time from the ledger event | PARTIAL. `timeoutLeftSeconds` SERVED per `data/sample.json`; the golden shows `paused: null` only. Takeover time and actor: PENDING api lane (per-step times and actors). Fallback: "You took over" with no time, clock line from `timeoutLeftSeconds` when present, else "clock paused" |
| 2-hour nudge | row `takeover:<instance>` (kind `park`, label "taken over", urgency `calm`, primary `ApplyCommand:handback`, actions `[handback, cancel]`, `Golden/Api/needs-you/idle-takeover.json`; engine fires it after 2 h, `docs/csharp/sindri-workflow.md` "takeoverIdle") | SERVED. The toast is a view of that row; the inbox also lists it under Paused runs |
| Step names ("Draft the phase plan"), "AI agent / Automatic" kind label | recipe `displayName` (#468) per recipe; per-step plain names | PARTIAL. Recipe `displayName` SERVED; per-step display names PENDING: engine lane. Fallback: the step key, humanised by the existing `Labels` word map |
| Settings approval (`profile:drift`) preview | `GET /profile` (changed key paths) and the typed challenge preview | PARTIAL. Changed key paths SERVED (`Golden/Api/profile/*`); the plain-words summary (repos, services, allowance, connections, human-only stops) is PENDING: api lane (setup-once SO-A2/A3). Fallback: the changed key paths as `+`/`-` lines in the diff box |
| Single workflow approval row ("Approve a changed workflow") | none: `GET /needs-you` emits no recipe row | PENDING: api lane (a `recipe` needs-you kind; also needed so the group is not empty). Fallback: the group shows only rows that exist; a changed workflow appears under Setup's "Needs its own look" (specs/setup.md) |
| Approve-all row ("Approve all new workflows") | `setup:recipes` row from setup-once SO-A6; plan from `GET /workflow/recipes/approve-all` | Plan SERVED (#474, `Golden/Api/workflow-recipes-approve-all/{plan,plan-first-approval,empty}.json`: `recipes[{id,displayName,version,change,changeWord,capabilities[],delta}], ownApproval[], digest, empty`). The `setup:*` row: PENDING api lane (SO-A1 `setup` kind, not on main). Fallback: no row; the Setup page is the only entry |
| App challenge (typed or held) | `POST /workflow/challenges/app` `{action}` -> `{challenge, action, expiresAt, confirm, minHoldMs, typedAlternative}` (`Api/Workflow/AppChallengeView.cs`) | Client SERVED (`ISindriApi.Approvals.cs`); server route PENDING: api lane (setup-once SO-A2, `AppConfirm`). Until it lands the catalog rows stay `cli-only` and the UI shows terminal copy |
| "Read to the end" | none; a client scroll flag | local (no API) |

The prototype's per-recipe "passed N of N" checks are NOT shown: the plan has no `evals` field by design (setup-once section 7.2). Use the fixed line in `specs/setup.md` Copy instead.

## 5. Actions and keys

Catalog ids (golden `Golden/Api/commands/catalog.json`, 54 rows). The UI sends only `desk`, `decision`, `pause` and `job` rows; `protected` rows render as terminal copy except the three `AppConfirm` rows once the server flips them.

| Control | Action id (class, today's `ui` status) | Rule |
|---|---|---|
| Approve scope or design (stops 1, 2) | `CompleteStep` (desk, ready) with outcome `approve` and the `run` token from `desk.run` | A, once read to the end; first A shows "Approve?" (Q3) |
| Send back (stops 1, 2) | `CompleteStep` with outcome `revise` (stop 2: choice `reviseDesign` or `revisePlan` by the Design/Plan toggle); the reason goes as `--output fact:answers=<text>` | D; reason required (Q4) |
| Drop (stops) | `CompleteStep` outcome `drop` | NOT in the prototype. Open item 2 |
| Approve the merge (stop 3) | the row's `CompleteStep ... --outcome merged` is class desk today, but the rule says merge is terminal only. NEW action id needed: `CompleteStep:merge` (protected, `cli-only:typed-confirmation`): api lane, with a catalog golden row and a label. Until then: `ProtectedActionButton` from the row's served `cli`, label `⌂ Approve merge · terminal`, chosen by a client rule on outcome `merged` | copies the command; no send |
| Send back (stop 3) | `CompleteStep` outcome `rework` | D; reason recorded |
| Accept / Decline (agent decision) | `Decide` (decision class, ready) with the option id (`accept`, `decline`) and the reason as `--note` | A runs the recommended, D the `rejectLike` option with the reason; button word "Decline" |
| Approve settings | `ApproveProfile` (protected today, `cli-only:typed-confirmation`; the typed app path is `AppConfirm`, PENDING api SO-A2) | `TypedConfirmBox`; until the flip, terminal copy |
| Approve a changed workflow | `ApproveRecipe` (protected, same flip) | `TypedConfirmBox` |
| Approve all new workflows | `ApproveAllRecipes` (job; catalog says `pending:SO-E3` on main, flips to ready with the app challenge) | `HoldButton` plus the typed alternative (`ApproveAllViewModel`, `ApproveAllDialog` exist; this card reuses the VM) |
| Send back a settings or workflow approval | none exists (there is no "decline" for an approval). The bar shows only the approve control; D is inert on these rows and the hint says "Leave it here to keep the current settings." NEW action id needed only if the owner wants a recorded decline: api lane | open item 3 |
| Paused run: Allow more tries | `ApplyCommand:answer-grant` is protected (`cli-only`). The prototype's "Send answer" for this option becomes the terminal button `⌂ Allow more tries · terminal`; the extra-tries stepper fills `<rounds>` in the served `cli` (existing `FillInHint`) | prototype is wrong here; contract wins |
| Paused run: Give a hint | `ApplyCommand:answer-text` (desk, ready), text from the box | Send answer, Ctrl+Enter |
| Paused run: Stop this run | `ApplyCommand:answer-reject` (desk, ready); "Cancel run" in the takeover inspector is `ApplyCommand:cancel` (desk, ready) | Send answer |
| Take over | `ApplyCommand:takeover` (pause, ready) | button on the run; clock pauses |
| Hand back (inspector box, toast) | `ApplyCommand:handback` (desk, ready) | clock resumes with the time left |
| Edit before approve (E) | none; OUT OF SCOPE. Placeholder: a dashed chip "Not available yet" in the bar, not focusable, no key. No catalog row is added | Q5 owner "later" |
| Open item / document / context link | local links: `workflow/<address>` deep link, `context?anchor=` deep link ("See its context"), document opens through the facts panel | no id |
| Copy reference | `Clipboard` (local) | no id |

Keys (active when the page has focus and no `TextBox` has focus; they do not clash with G-chords or F6; existing `NeedsYouKey`, `NeedsYouKeysTests`):

| Key | Effect |
|---|---|
| J / K | next / previous row (the card follows; scroll resets; `ReadToEnd` resets) |
| Enter | open the selected row (existing; on the list it focuses the card) |
| A | primary: approve, accept, copy the terminal command, or (typed and held rows) focus the control; never sends a protected action; second A confirms "Approve?" |
| D | send back / decline: opens the reason box (existing key was X; X stays as an alias on decision rows) |
| Ctrl+Enter | send the reason, or Send answer; Esc cancels the reason box or closes the card back to the list |
| Y | copy the row's command (existing) |
| E | inert (placeholder) |

Every control keeps an `AutomationProperties.Name` that carries the subject ("Approve Link expiry scope"); the "Approve?" state and the hold are announced through the existing `LiveAnnouncer`, once.

## 6. Copy (`Labels.NeedsYouPage.cs`, new partial; no em dashes, no internal terms)

| Key | Text |
|---|---|
| `NyCrumb` | "Home > Needs you" |
| `NyWaiting(n, runs)` | "{n} waiting, {runs} runs" |
| `NyGroupApprovals` / `Runs` / `Decisions` / `Settings` | "Your approvals" / "Paused runs" / "Agent decisions" / "Workflow and settings approvals" |
| `NyKindApproval(n, of)` | "Approval {n} of {of}" (fallback "Your approval") |
| `NyKindRun` / `Decision` / `Workflow` / `SettingsChange` | "Paused run" / "Agent decision" / "Workflow change" / "Settings change" |
| `NyNewPill(n)` | "{n} new request, held while you read" |
| `NyKeyHints` | "move", "open", "approve", "send back" |
| `NyWhyCaption` | "Why this needs you" |
| `NyIfApprove` / `NyIfSendBack` | "If you approve" / "If you send it back" |
| `NyRequestCaption` | "The request" |
| `NyChangedCaption` | "What changed since the last round" |
| `NyDoneCaption(shown, of)` | "What \"done\" means" with "{shown} of {of} shown" |
| `NyQuestionsCaption(open, answered)` | "Questions and your answers" with "No open questions, {answered} answered" |
| `NyYouChose` / `NySuggested` | "You chose this" / "Suggested" |
| `NyDocsCaption` | "Documents" |
| `NyFootWaiting` | "Waiting for you" |
| `NyFootRight(wait, pauses)` | "waiting {wait} · pauses and asks again in {pauses}" |
| `NyApprove` / `NyApproveQ` | "Approve" / "Approve?" |
| `NyAccept` / `NyDecline` / `NySendBack` | "Accept" / "Decline" / "Send back" |
| `NyEditLater` | "Edit before approve" with chip "Not available yet" |
| `NyConfirmHint` | "You have not read to the end. Press A again to approve." |
| `NyReasonLabel` | "Why are you sending it back?" |
| `NyReasonRequired(toAi)` | "Required. It goes to the AI as your notes." / "Required. The reason is recorded." |
| `NyReasonPlaceholder*` | "What should change in the scope? The AI uses this as its instructions." / "What should the builder fix?" / "Why decline? It is recorded with the item." / "What should the author change?" |
| `NyCancel` | "Cancel" |
| `NyApprovedHint` | "Approved. The row has left your inbox." |
| `NyApprovedTitle(what, item)` | "{what} approved: {item}" with body "It is locked in. The next stage has started from exactly this." |
| `NyCopied` | "Copied. Paste it in your terminal to approve." |
| `NyTerminalOnly` | "Only you can approve a merge, and only from your terminal. The button copies the command." |
| `NyTypedHint` | "Type the words shown to confirm. This guards against a slip; only you can do this in the app." |
| `NyHoldHint` | "Press and hold for 1.5 seconds. Let go early and nothing is approved." |
| `NyKeepSettings` | "Leave it here to keep the current settings." |
| `NyAnswerCaption` | "Your answer" |
| `NyMoreTries` / `NyHint` / `NyStop` | "Allow more tries" / "Give a hint first" / "Stop this run" with their one-line help ("The AI reviews again. Pick how many extra tries below.", "Tell the AI what to focus on, then it tries again.", "Ends the run. You can start it again later.") |
| `NyExtraTries` / `NySendAnswer` | "Extra tries" / "Send answer" |
| `NyHistoryCaption` | "What happened" |
| `NyTookOver` | "You took over" (node badge, footer, inspector badge, step row; person glyph each time) |
| `NyClockPaused(left)` / `NyClockRunning(left)` | "clock paused, {left} left" / "clock running, {left} left" |
| `NyTookOverAt(time, ago)` | "you took over at {time} ({ago} ago)" |
| `NyHandBackBox(left)` | "Hand back when you are done. The workflow carries on from here, and the clock restarts with the {left} that was left." |
| `NyHandBack` / `NyCancelRun` | "Hand back" / "Cancel run" with "Stops this step and the stages that wait on it." |
| `NyNudgeTitle(h)` | "You've had this for {h} hours: hand it back?" |
| `NyNudgeBody` | "The clock on this step stays paused until you do. The workflow carries on from here." |
| `NyNotYet` | "Not yet" |
| `NyHandedBackToast` | "Handed back" with "The workflow carries on from here." |
| `NyEmpty` | served `empty` sentence; fallback "Nothing needs you" |
| `NyClearedRow` | existing `Labels.Cleared(id)` |

Unknown group, kind or outcome words render the served string raw (the `Words.Known` pattern); kind tile falls back to a generic dot.

## 7. Tests the ui lane must add or change

| Test class | Change |
|---|---|
| `TokenTests`, `ContrastTests` | add `Color.AttentionFill` (once, in the setup spec's slice; whichever lands first) |
| `NamedControlsTests` | keep every current `x:Name` of `NeedsYouRow`, `NeedsYouSheet`, `DecisionSheet`; add `NeedsYouInbox`, `NeedsYouCard`, `NeedsYouActionBar`, `NeedsYouReason`, `NeedsYouWhy`, `NeedsYouToast` |
| `NeedsYouKeysTests` | update: D added, X kept as a decision-row alias, E inert, A twice on an unread `desk:` row, Ctrl+Enter, Esc order; keys ignored in a focused `TextBox` |
| `NeedsYouViewModelTests`, `NeedsYouRowViewModelTests`, `RowSheetViewModelTests` | extend: group assignment by id prefix and kind, `failure:` rows in the fifth group, sticky header counts, selection survives a re-read |
| `ActionCommandTests`, `NeedsYouActionCliTests` | extend: outcome-to-`CompleteStep` mapping per stop; merge row renders terminal copy; `answer-grant` renders terminal copy with the rounds filled |
| `ProtectedActionTests` | unchanged contract; add that no bar mode sends a `protected` id |
| NEW `NeedsYouPageViewModelTests` | modes (Pending, Confirming, SendingBack, Approved), `ReadToEnd`, reason required, one send per press, refusal shown as served |
| NEW `NeedsYouGroupingTests` | the four kinds and the failure group for the goldens `fixture`, `large`, `desk-task`, `park`, `idle-takeover` |
| NEW `StickyGroupListTests` | header sticks, count updates, no layout loop |
| NEW `TakeoverViewModelTests` | clock text from `timeoutLeftSeconds`, paused vs running footer, nudge from the `takeover:` row, Not yet, handback toast 5 s |
| NEW `NeedsYouFramesTests` | headless frames at 1440 and 900 for pending, confirm, send back, approved, answer, takeover, handback; no horizontal scroll at 900 |
| `BannerLayoutTests`, `HomeFold`/`StripFoldTests` | unchanged: Home keeps its panel; assert it |
| `LabelsTests` | every key above, no em dash, no internal term |
| `ActionLabelsTests`, catalog | unchanged unless `CompleteStep:merge` lands (then a golden row and a unique label in `Labels.Actions.cs`) |
| `SpecTextTests`, `docs/csharp/sindri-ui.md` | update the Needs-you section |

## 8. Build order (each slice red test first)

1. Inbox page and grouping: `NeedsYouPage`, `StickyGroupList`, the group and row templates on the existing rows, the card head and status footer, the entry points and Esc back. Red first: `NeedsYouGroupingTests` (goldens to groups), then `NeedsYouFramesTests` for pending at 1440 and 900. Proves: four kinds distinct, sticky headers, no change to Home or nav.
2. Action bar and keys: `ActionBar` with Pending, Approve?, Send back and Approved modes, `CompleteStep` and `Decide` mapping, terminal button for the merge and `answer-grant`, D and Ctrl+Enter keys, E placeholder chip. Red first: `NeedsYouPageViewModelTests.A_on_an_unread_desk_row_asks_before_approving` and `NeedsYouKeysTests`. Proves: Q3, Q4, protected rules, one send per press.
3. Confirm controls in the bar: `TypedConfirmBox` and `HoldButton` (from `specs/setup.md` slice 1) wired for `profile:drift`, single-recipe and approve-all rows, behind the catalog status (terminal copy until `ready`). Red first: `NeedsYouActionCliTests.Profile_row_shows_terminal_copy_until_the_catalog_is_ready`. Proves: no protected id is sent, reuse of `ApproveAllViewModel`.
4. Paused-run answer form and takeover: the answer options and stepper, the takeover inspector block, clock text, the nudge toast from the `takeover:` row, hand back and the "Handed back" toast. Red first: `TakeoverViewModelTests.Clock_reads_the_served_time_left`. Proves: Q7, Q8, Q12, hand back sends `ApplyCommand:handback` only.
5. Pending data (separate PRs after other lanes land): "Why this needs you" text and the approve/send-back outcome boxes (engine `why`), stop number and row timeout (api), per-step names and takeover times, the scope viewer for the request body (ui U13), the recipe needs-you kind and the `setup:recipes` row.

## 9. Open items for the ui lane

1. Typed phrase (Q10): the approved short phrase `approve <hash6>` is not what the server compares. The app challenge answers `action` as `approve profile <hash6>` or `approve <recipe> <version>` (setup-once section 4), and the typed answer is compared to that text. Either the server also accepts the short phrase (api lane, SO-A2: add a `phrase` field to `AppChallengeView`) or the field shows the served `action` text. Until the owner picks, show the served `action`.
2. Drop: stops 1 and 2 have a `drop` outcome the prototype does not draw. Ask the owner whether it is an overflow item on the bar.
3. Settings and workflow approvals have no decline: confirm D stays inert there, or add a recorded decline (new id, api lane).
4. Where a `desk:` row is "read": the scroll-to-end flag only; a card shorter than the viewport counts as read at once (the prototype does the same).
5. Card radius: prototype 16, app max `Radius.LG` 12; use 12 unless the token pass adds 16.
6. Which fact viewer shows stop 1's scope (counts, done-criteria, what changed): U13's scope viewer is not built; the fallback is the facts panel. Slice 5 waits on it.
7. Merge approval: the server serves `CompleteStep` as desk for stop 3; the terminal-only rule needs the NEW `CompleteStep:merge` row or a client rule. Ask the api lane which.
8. Run canvas details (node footer, zoom pill, step list) are owned by `specs/run-canvas.md`; confirm the inspector's hand-back box sits in that spec's inspector, not a second copy here.

## Settled by ideas (2026-10-20, after review)
- Approve the merge: no `CompleteStep:merge` row. As in run-canvas.md (ui's rule), the button renders the step's served
  `StatusAnswer` (ActionId, Class, Cli); with none served it shows terminal copy. Engine and api settle what the line's last
  step serves while the PR-watching stage is a stub.
- Drop (stops 1 and 2): a secondary "Drop this item" in the card's More menu, with a confirm, never on the bar or a key.
- D on settings and workflow approvals: shown disabled with the tooltip "These can only be approved. Change the settings
  or workflow file to undo."
- The canvas and inspector in the takeover state come from specs/run-canvas.md (now written; reuse its controls).

## Answered by ui (2026-10-20, ui digest ~/.agentic-workflow/digests/sindri-ui-f3-spec-needs-you.md)
- Read rule = scroll-to-end only; a card shorter than the viewport is read at once (open item 4).
- Card radius 12; slice 5 waits on the scope viewer.
- One hand-back box, in run-canvas's inspector; no second copy here (open item 8).

## Owner override (2026-10-20)
- OWNER 2026-10-20 (via meta): DIDN'T WORK OUT is AMBER, per the 2026-10-19 table; this overrides the earlier "neutral" settlement. FAILED stays red, CANCELLED stays neutral. ui uses the served `attn` tone (api #504 matches). It keeps its own glyph (minus in circle) and word, so it never reads as "needs you" by colour alone.

- OWNER 2026-10-20 (via meta, from ui): no in-app free-form input to an agent for now (tabled). On an agent Step, input goes through Attach ("Watch the agent": Warp + tmux, typing takes over); there is no "Reply here" text box. An agent Step's needs-you row offers Attach and Take over directly. Slice 4 order unchanged. This overrides any "Reply here" / free-text-to-agent element above.
