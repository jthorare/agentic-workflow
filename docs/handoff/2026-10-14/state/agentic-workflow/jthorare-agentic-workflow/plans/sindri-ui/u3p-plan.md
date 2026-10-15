# U3' plan — rev 7 U3 plus the Workflows strip: Needs you, decisions, requests, Home with the strip, the pulse, Mark all seen, badges and the tray menu (screens spec §8 U3', §4.1; UI spec rev 7 §18 U3)

Planned 2026-10-14 by the UI lane's U3 planner. Plan only: no code, no commits.
Updated 2026-10-14T06:53:06Z: the API lane answered U3-API-1..8 and pinned P4
(`plans/sindri-api-lane/u3-api-answers.md`, `p4-plan.md`). Fixture names are now P4's golden names; the wire types
are as pinned; a UI-spec-edit task (T14) is added.
**Updated 2026-10-14T23:43:38Z (U3'):** UI revision 8 (`docs/superpowers/specs/2026-10-13-sindri-ui-screens-design.md`,
"screens spec") is approved and merged (origin/main 4a2389bd, #210). U3 becomes **U3'** (screens §8): every U3 task and
owner answer below is kept; the empty strip slot is replaced by real tasks **T15–T20** against E-20's root list
(`GET /workflow/instances?root=true…`, API §3.8, lands in API P11); "Rev 8 deltas" (new section) amends the U3 tasks
that rev 8 changed (the attention band, side stripes, the Decisions page, `G D`, chips, the empty sentence). The strip's
goldens are the API lane's rev-8 contract unit's, pinned 2026-10-14 (`plans/sindri-api-lane/rev8-goldens-for-ui.md`:
`workflow-instances/{root, recipe-filter, empty}` and the `workflow-instance/*`, `workflow-status/*`,
`workflow-recipe/*` sets); anything more the strip needs is an `extra-` fixture or an API edit. U8 (`u8-plan.md`) runs in parallel; the shared files go through the lead's rev-8 contract unit
(u8-plan.md "Worktrees and branches").

Branch `feat/sindri-ui-u3`, worktree `.claude/worktrees/sindri-ui-u3` (the slot freed by #184, see u8-plan.md), from
the lead's rev-8 contract branch `feat/sindri-ui-r8-contract`, itself on `origin/feat/sindri-ui-u2` (merge, not rebase,
when either moves). Draft PR base `feat/sindri-ui-r8-contract` until it merges into U2's line; stack U3' → contract →
U2 → U1 (#193) → U0 (#184).
**API P4 isn't built yet** (P3 is at `origin/feat/sindri-api-p3-items`). Until P4 records its goldens under
`tests/Aw.Sindri.Tests/Golden/Api/` (linked into the UI tests as `Golden/Api`), U3 writes spec-shaped copies **under
the same slug and scenario names** in `tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/`, built from the pinned shapes
below, and deletes each copy when P4's golden lands (the goldens decide; a diff is a contract-unit fix, not a test
edit). `Decide` and `FileRequest` stay `pending:P4` in the real catalog until P4 re-records `commands/catalog`; until
then a spec-shaped `commands/catalog` copy with both `ready` and the three P4 query rows stands in.

Read first: UI spec rev 7 §1 (U1–U3, U8, U10, U12), §2 (the five-second test, rules), §3.2 (window, attention band,
caps, fold rule), §3.3 (`G N`, `G D`, deep links `home/needs-you`, `needs-you/<rowId>`, `decision/<DecisionId>`,
stable selection), §4.1 (states, command lifecycle, cleared-while-open, busy and read-your-writes), §4.2, §4.3, §4.7,
§4.9 (Needs you, Home, Decisions/Requests rows), §5 (urgency, seen, Mark all seen, reduced motion, the live region,
tray table and menu), §7 (`Decide`, `FileRequest`, `ApproveProfile`, `RunService` rows, the copy block), §8 (`A`,
`Shift+A`, `X`, `Y`, `R`, `J/K`, the key guard and hold), §9 (headers, `X-Sindri-After`, `SND-ITEM-409`, re-query),
§11.2–§11.4, §12.3 (seen ids in preferences), §12.4 (copy), §14 (`Attention`, `AttentionSoft`, `Motion.Pulse`,
`Motion.Base`, `Size.AttentionBand`), §16 (`mockups/home.html`, `needs-you.html`, `decisions.html`), §18 U3, §18.1,
§18.2, §19. API spec §3.1, §3.2, §3.4, §3.6, §4.2, §5, §9, P4's row. The API lane's `u3-api-answers.md` and
`p4-plan.md` (§1.3, §1.5–§1.7, §4, §6). README X5, X21, X25, X26, X52, X61 (an optional `owner`, shown nowhere).
**Rev 8 (U3'):** screens spec status block (what it supersedes), §1 R8-1, R8-9, R8-13, §3 (navigation, chords, deep
links), §4.1 (Home and the strip), §4.8 (not served yet), §6 E-20, E-21, §8 U3' row, §8.2, §9 (waves, contract unit,
hotspots); `fixture.md` (the human queue, the strip's rows and order, the chip table, the protected-action label, the
sentences that read the same everywhere); `fixes.md` waves 4–6; `mockups/home.html`; API §3.8 (`InstanceList`,
`InstanceSummary`, `StateCell`) and P11's row; WF §3.2 `urgency` (E-21); README X62.

Rules carried from U0–U2: TDD (red view-model or client test first, `NotSupportedException` stubs only where analyzers
need them; then green; then the view; then a headless view test with key frames). View models hold no logic: API
fields to words and visibility; order, urgency, kind label, allowed actions, empty sentences, the `notice` and
`cleared` lines all from the API; the app's only state is presentation (seen ids, expanded, selection, motion).
Compiled bindings, no hex outside `Theme/`, every control named "<label> <subject id>". All timing on the injected
`TimeProvider`. **No OS notifications** (U8, X5): nothing in U3 calls a notification API. All dotnet through
`AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy dotnet`.

## Goal

The app answers spec §2's question 1 ("what needs me") on Home and makes the human's three non-protected answers
one click: decide a decision (or a filed request), run the row's ready action, copy a protected row's CLI. Needs-you
rows pulse until seen, the window title, dock or taskbar badge and tray carry the count, the tray menu lists the rows
and navigates only, and everything re-reads on stream events. Decisions and requests get their page, and the item
sheet gains its pending decisions and filed requests. With U2's panels, Home now passes the five-second test on all
four questions, on the default and `--large` fixtures.
**U3' adds** the Workflows strip between Needs you and the two columns (screens §4.1, R8-9): one line per open root
instance across recipes, in the API's order (incident, then triage, then work, then oldest; E-20, E-21), capped at 5
with "… N more" opening Workflows, each row with its chip, subject, recipe, stage tag, NOW sentence, age and one
inline action (the desk answer when there is one). The fold rule now counts the strip (the strip gives rows back to
Needs you first). Rev 8's shell changes that touch U3's surfaces land too (see "Rev 8 deltas").

## Observable signals (spec §18 U3), each mapped to a journey in the verification plan

| # | Signal | VP journey |
|---|---|---|
| S1 | A session's `curl` request appears as **one pulsing row** (no click) | J1 steps 2–3 |
| S2 | The window title reads `(1) Needs you · Sindri` | J1 step 4 |
| S3 | The tray shows the attention colour (and its menu lists the row; a click navigates only) | J1 steps 5–6 |
| S4 | **Accept** clears it everywhere (row, title, badge, tray, Decisions, item sheet) | J1 step 7 |
| S5 | A CLI race shows "decided elsewhere" (`SND-ITEM-409`'s `detail`) | J2 |
| S6 | Reduced motion shows the static outline and the word `new` | J3 |
| S7 | The five-second test passes on `--large` (and the default fixture) | J5 |
| **S8** | **The strip shows a running incident first** (screens §8 U3'): with an incident, a triage and two work instances open, the incident's row is the strip's first row whatever their ages, and it moves there without a click when the incident starts | J6 |
| **S9** | **The five-second test passes with the strip on the `--large` fixture** (screens §8 U3'): the strip's ≤ 3 rows (Needs you at 5) sit above the fold at 1280 × 800 and the fresh subagent answers the four questions plus "which workflow is most urgent and what is it waiting on?" | J7 |

Rev 8 reads "the pulse" and "tray" as before; the attention band and the side bars are gone (R8-13), so S1–S3's
assertions use the row's attention treatment of `mockups/home.html` (tint and chip) instead of a bar and a band.

## Wire types (pinned by P4; the goldens decide)

- `NeedsYouList{rows, empty, asOf}`; `NeedsYouRow{id, kind, label, subject:{kind, id}, item?, title, reason, repo?,
  urgency, since, recordedOnly, notice?, answerKind?, primaryAction, actions, cli}`.
  - `decision`: id `decision:DEC-…`, label `decision · re-scope|add task|note|hand back`, subject `{decision, DEC-…}`,
    `item` when the request named one, `pulse`, primary `Decide`, actions `[Decide]`, cli `sindri decide DEC-… <choice>`.
  - `failure`: id `failure:service:<name>`, label `failure · <name>`, subject `{service, <name>}`, `colour`, primary
    `RunService`, actions `[RunService, PauseService]` (both `pending:P6` in the catalog), cli `sindri services run <name>`.
  - `profile`: id `profile:drift`, label `profile · drifted`, subject `{profile, <live hash>}` **or null** (owner
    decision pending, G8), `calm`, primary `ApproveProfile` (catalog `cli-only:typed-confirmation`),
    cli `sindri profile approve <hash>`.
  - `primaryAction` and every `actions[]` entry are catalog ids, never `copy:`; ready/pending/cli-only comes from the
    catalog. `cli` is the one copy string. `answerKind` is null on every P4 row. `notice` is the served
    `recordedOnly` sentence. Order: urgency, then `since`, then `id` (the app never sorts).
- `DecisionList{decisions, empty, asOf}`; `DecisionView{id, kind:"accept-request", class:"decision", label, item?,
  itemVersion, repo?, question, options:[{id, label, rejectLike, cli}], recommended, state:pending|decided|voided,
  filedBy, filedAt, decidedBy?, decidedAt?, choice?, note?, recordedOnly, notice?, actions, cli, cleared?}`.
  Options `accept` ("Accept", recommended) and `decline` ("Decline", `rejectLike: true`); `actions` `[Decide]` while
  pending, `[]` after; `cleared` (the timeline line) on decided and voided decisions. No `GET /decisions/{id}`.
- `RequestList{requests, empty, asOf}`; `RequestView{id, kind, item?, repo?, text, actor,
  state:open|accepted|declined|voided, decisionId, filedAt}`.
- `FileRequestRequest{kind, itemId?, text}` → `RequestFiled{accepted:{commandId, sequence}, requestId, decisionId}`.
- `DecideRequest{choice, note?}` (the id in the route) → `CommandAccepted{commandId, sequence}`.
- Routes: `GET /sindri/needs-you`, `GET /sindri/decisions?pending=&item=`, `GET /sindri/requests?state=&item=`,
  `POST /sindri/requests`, `POST /sindri/decisions/{decisionId}`.
- Read-your-writes: after an accepted `Decide` or `FileRequest`, the next query sends `X-Sindri-After: <sequence>`;
  the timeout is **`SND-API-011`** (503), which marks the view stale and keeps the "done" confirmation (§4.1).

**The strip's wire subset (API §3.8, E-20; pinned by API P11's contract unit, the goldens decide).** The strip reads
`GET /sindri/workflow/instances?root=true` and uses only:
- `InstanceList{instances, empty, asOf}` (+ `counts` if U3'-API-10 is taken).
- `InstanceSummary{address, itemId?, recipe:{id, hash, version, urgency}, state, since, engineOwned?:{kind, reason},
  subject:{kind, key, label, href?}, inputs[], states[], now, actions}`. The strip shows `subject.label`, `recipe.id`,
  a stage tag built from the current `StateCell` (`state` id, `round` → `r2/3`, `desk` → `· desk`, `children` →
  `4/9`, `child` → `child`, `badge` replaces the glyph), `now` as served, the age from `since`, and one inline action
  from `actions` ∩ catalog. `inputs[]` and the full `states[]` are U8's; the strip ignores them.
- Order is the API's (urgency `incident`, `triage`, `work`, then oldest `since`): the app never sorts. The strip shows
  the first five rows whose current `StateCell.terminal` is false **only until U3'-API-10 serves `open=true`**; that
  filter is the one piece of selection logic the strip would carry, and it goes away with the edit.
- The record types are U8's (`Api/InstanceList.cs`, `InstanceSummaryView.cs`, `StateCellView.cs`, …), declared once
  in the lead's rev-8 contract unit; U3' adds no instance record of its own.

## Re-query map (until P5's `invalidates`, every event re-reads every visible view, plus needs-you always)

| Event | Re-reads |
|---|---|
| `request.filed`, `decision.recorded`, `item.state-changed` (a close voids that item's decisions) | needs-you, decisions, requests, queue, items, the open item and its timeline |
| `service.ran` | needs-you, services |
| `profile.checked` | needs-you, profile |
| `workflow.*` (P11: state entered, parked, escalated, answered, taken over, handed back, completed, cancelled) | needs-you (desk tasks and parks are `park` rows), **the strip's `workflow/instances?root=true`**, queue, the open item or instance |

U3 encodes this table nowhere in the app until E-15 serves it (P5); it is the test oracle for which views the
"re-read every visible view" rule must at least cover, and needs-you is re-read on every event whatever page is
visible, because the badge and tray show with the window closed.

## Files to touch

New (`src/Sindri/Aw.Sindri.Ui/`):
- `Api/NeedsYouList.cs`, `Api/NeedsYouRowView.cs`, `Api/SubjectRefView.cs` (nullable on the row), `Api/DecisionList.cs`,
  `Api/DecisionView.cs`, `Api/DecisionOptionView.cs`, `Api/RequestList.cs`, `Api/RequestView.cs`,
  `Api/DecideRequest.cs`, `Api/FileRequestRequest.cs`, `Api/RequestFiledView.cs`, `Api/SindriApiClient.NeedsYou.cs`.
- `Platform/IDockBadge.cs` (port, §18.2 platform notes).
- `ViewModels/NeedsYou/NeedsYouViewModel.cs`, `NeedsYouViewModel.Keys.cs` (J/K, `A`, `X`, `Y`, `Shift+A`, the key
  guard and the "N new" hold, as `QueueViewModel.Keys.cs`), `NeedsYouRowViewModel.cs`, `SeenTracker.cs`,
  `AttentionSummary.cs` (title text, badge count, tray view, the summary line; counts only).
- `ViewModels/Decisions/DecisionsViewModel.cs` (tabs Pending, Decided, Requests), `DecisionRowViewModel.cs`,
  `RequestRowViewModel.cs`, `DecisionSheetViewModel.cs`, `FileRequestViewModel.cs`.
- `ViewModels/NeedsYou/FailureSheetViewModel.cs`, `ProfileSheetViewModel.cs` (§4.3's sheet per kind; `park` rows render
  generically from `actions` ∩ catalog until U5).
- `ViewModels/Labels.NeedsYou.cs` (§12.4 additions: urgency words, `new`, tray tooltips, announcements, Mark all seen,
  the title pattern; **not** the `recordedOnly` sentence, which is served as `notice`).
- `Views/NeedsYou/NeedsYouPanel.axaml(.cs)`, `NeedsYouRow.axaml(.cs)`, `NeedsYouSheet.axaml(.cs)`,
  `NeedsYouStyles.axaml` (pulse on `Motion.Pulse`, static outline, slide-in on `Motion.Base`; zero under reduced motion).
- `Views/Decisions/DecisionsPage.axaml(.cs)`, `DecisionSheet.axaml(.cs)`, `FileRequestDialog.axaml(.cs)`.
- ~~`Views/Shell/AttentionBand.axaml(.cs)`~~ (U3': not created; the band is withdrawn, R8-13).
- **U3' strip:** `ViewModels/Home/WorkflowsStripViewModel.cs` (rows, cap, "… N more", fold give-back),
  `ViewModels/Home/StripRowViewModel.cs` (chip, subject, recipe, stage tag, now, age, one action),
  `ViewModels/Home/StageTag.cs` (the `StateCell` → `r2/3` / `· desk` / `4/9` / `child` words; presentation only),
  `ViewModels/Labels.Strip.cs` (heading "Workflows", "N open", "… N more", the empty line, tooltips),
  `Views/Home/WorkflowsStrip.axaml(.cs)`, `Views/Home/StripRow.axaml(.cs)`; it uses the contract unit's
  `Views/Controls/StateChip.axaml` and `ProtectedActionButton.axaml` (one chip table, the lock popover).

New (`src/Sindri/Aw.Sindri.Ui.Desktop/`): `MacDockBadge.cs` (`NSDockTile.badgeLabel` through `LibraryImport`
`objc_msgSend`), `WindowsDockBadge.cs` (`ITaskbarList3::SetOverlayIcon` through a source-generated COM interface),
`NullDockBadge.cs`.

Changed:
- `Api/ISindriApi.cs` (+ `NeedsYou`, `Decisions(pending, item)`, `Requests(state, item)`, `Decide`, `FileRequest`),
  `Api/SindriUiJsonContext.cs` (hotspot), `Api/SindriApiClient.Calls.cs` (`X-Sindri-After` from
  `RequestFiled.accepted.sequence` too).
- `Platform/ITray.cs`, `TrayState.cs` (+ `Attention`, `Calm`), `TrayView.cs` (+ `Count`, `Rows` up to five,
  `RunningLabel`), `SindriPlatform.cs` (+ `IDockBadge DockBadge`); Desktop `AvaloniaTray.cs`, `TrayMarks.cs`,
  `Composition.cs`, `Program.cs`.
- `ViewModels/Shell/ShellViewModel*.cs` (`WindowTitle`, `IsAttentionBandVisible`, `G N`, `G D`, `Shift+A`, deep links,
  sheet subjects for rows and decisions, announcements), `ShellText.cs`, `NavEntry` counts (Decisions N),
  `HealthViewModel.Refresh.cs` (register the needs-you, decisions and requests re-reads; needs-you always),
  `ControlPlaneFollower.cs` (no logic change).
- `ViewModels/Controls/ControlViewModel.cs` and `CatalogViewModel.cs`: a control for one **option** of `Decide` (label =
  the option's served `label`, the send carries the option `id`, the copy text is the option's `cli`; the catalog row
  still decides ready/pending); `X` binds to the option whose `rejectLike` is true (not the catalog `shortcut`, which
  is null for `Decide`); a copy block fed by a served `cli`.
- `ViewModels/Item/ItemSheetViewModel*.cs` (+ pending decisions via `GET /decisions?pending=true&item=`, requests via
  `GET /requests?item=`, §4.6).
- `ViewModels/Palette/CommandPaletteViewModel.cs`, `Labels.Palette.cs` (`Shift+A`, `A`, `X`, `Y`, `R`).
- `Views/Shell/HomePage.axaml(.cs)` (Needs you panel; **`WorkflowsStripSlot` now hosts `WorkflowsStrip`**, visible), `ShellWindow.axaml` (title binding,
  attention band, the Decisions page replaces its placeholder, sheet subjects), `ShellWindow.Keys.cs`, `TopBar.axaml`,
  `Views/AppTrayIcons.cs`, `Views/SindriApp.axaml` (hotspot).
- `Theme/Tokens.axaml` only if a key is missing (the contract unit checks; never a hex outside `Theme/`).
- `tests/Aw.Sindri.Ui.Testing/`: `FakeSindriApi.NeedsYou.cs`, `FakeSindriApi.Routes.cs`, `FakeSindriHandler.cs`,
  `SindriApiContract.cs`, `FakeTray.cs`, `TrayContract.cs`, `FakeDockBadge.cs`, `DockBadgeContract.cs`, `A.cs`
  (`A.NeedsYouRow`, `A.Decision`).
- `tests/Aw.Sindri.Ui.Tests/`: `Api/GoldenContractTests.cs` (place `needs-you`, `decisions`, `decisions-id`,
  `requests`; the `requests` slug reads `list-*` scenarios as `RequestList` and the rest as `RequestFiled`),
  `NeedsYou/*`, `Decisions/*`, platform and desktop badge contract tests, `Views/NeedsYouFramesTests.cs`,
  `Views/DecisionsFramesTests.cs`, `Views/TrayViewsTests.cs`, `Shell/AttentionTests.cs`, the spec-shaped copies.
- `scripts/sindri-ui-fixture.sh` (+ `request`, `decide`, `fail`, `unfail`; `items --large` adds 12 requests; **U3':**
  `workflows [--large]` and `incident start|stop`, under the U3' anchor) and `scripts/tests/sindri-ui-fixture.test.sh`.
- **U3' tests:** `tests/Aw.Sindri.Ui.Tests/Home/WorkflowsStripViewModelTests.cs`, `StripRowViewModelTests.cs`,
  `StageTagTests.cs`, `Views/WorkflowsStripFramesTests.cs`; `FakeSindriApi.Workflows.cs` is the contract unit's
  (U3' only adds scenarios to it at its anchor).
- `docs/csharp/sindri-ui.md` (U3), `PublicAPI.Unshipped.txt` (both projects).
- `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` — only through T14, only with the owner's OK (011 is
  confirmed).
- `deps.yaml`: none expected.

## Ordered TDD tasks (test names first; one commit red, one green per slice)

**T1 — Contract unit (runs alone, §19).** Records, JSON context, `ISindriApi` additions, client, fake routes,
spec-shaped copies under the golden names.
- Red: `GoldenContractTests.Every_golden_reads_through_the_apps_json_context_or_is_a_refusal` (fails until
  `needs-you`, `decisions`, `decisions-id`, `requests` are placed); `Requests_slug_reads_list_scenarios_as_RequestList_and_others_as_RequestFiled`;
  `SindriApiClientTests.NeedsYou_reads_rows_in_the_apis_order_with_every_pinned_field`;
  `NeedsYou_reads_a_profile_row_with_a_null_subject`; `NeedsYou_reads_a_profile_row_with_a_profile_subject`;
  `Decisions_sends_pending_and_item_as_query_parameters`; `Requests_sends_state_and_item_as_query_parameters`;
  `Decide_posts_choice_and_note_once_and_never_retries`; `Decide_maps_409_decided_elsewhere_and_voided_with_their_details`;
  `Decide_maps_SND_DEC_404_SND_DEC_001_and_SND_VIEW_001`; `FileRequest_returns_the_request_and_decision_ids`;
  `FileRequest_maps_SND_REQ_001_SND_REQ_002_and_item_404_409`; `The_query_after_an_accepted_Decide_sends_X_Sindri_After`;
  `A_read_your_writes_timeout_SND_API_011_keeps_done_and_marks_stale`;
  `SindriApiClientHeaderTests.New_calls_send_human_ui_and_never_terminal_or_origin`.
- Contract base facts (fake and client): `NeedsYou_empty_returns_the_served_sentence`,
  `NeedsYou_stalled_keeps_rows_and_flags_stalled`, `Decide_conflict_is_a_problem_not_an_exception`.
- `CliText.Refuses_a_cli_with_a_control_or_bidi_character` (§7 backstop; extend U1's if it exists).

**T2 — Platform ports: dock badge and tray.** (Parallel with T3 after T1.)
- `DockBadgeContract`: `Shows_a_count`, `Zero_clears_the_badge`, `Last_write_wins`; against `FakeDockBadge`,
  `NullDockBadge` and, macOS only in the integration lane, `MacDockBadge`.
- `TrayContract`: `Attention_shows_the_count`, `Calm_only_shows_a_small_count_and_stays_monochrome`,
  `Menu_lists_at_most_five_rows_then_open_running_quit`, `Picking_a_row_raises_RowRequested_with_its_id_and_runs_nothing`,
  `Row_labels_escape_underscores`.
- `AvaloniaTrayTests.Attention_mark_uses_the_attention_token`.

**T3 — Needs-you list view model.**
- `NeedsYouViewModelTests.Rows_keep_the_apis_order`; `Row_shows_label_urgency_word_id_title_repo_reason_and_age`;
  `Urgency_words_are_needs_you_now_failed_to_look_at`; `Primary_control_first_then_the_rows_other_actions`;
  `Actions_render_ready_pending_or_cli_only_from_the_catalog`;
  `Failure_row_shows_RunService_and_PauseService_pending_P6_with_the_served_cli`;
  `Profile_row_shows_a_lock_button_and_the_served_cli_with_copy_on_a_second_line`;
  `Profile_row_with_a_null_subject_keys_on_its_id_and_opens_its_sheet`;
  `Decision_row_offers_Open_only_when_item_is_served`;
  `Home_caps_at_five_with_pulse_rows_first_as_served_then_Show_all_N`; `Show_all_expands_in_place`;
  `Text_scale_1_3_drops_the_cap_to_four`; `Empty_shows_the_served_sentence`;
  `Stalled_keeps_rows_marked_stale_with_the_banner`; `Rebuilding_disables_reads_views_actions_with_retry_after_rebuild`;
  `Selection_is_kept_by_row_id_across_refreshes`; `A_cleared_selected_row_moves_to_the_next_and_announces_id_cleared`;
  `Pointer_over_the_panel_holds_new_rows_behind_N_new`; `Keys_are_ignored_for_500_ms_after_an_automatic_move`;
  `Recorded_only_row_shows_the_served_notice`.
- `NeedsYouKeysTests.A_runs_the_single_ready_primary_and_never_a_protected_one`;
  `A_on_a_decision_row_does_nothing_unless_its_sheet_is_open`;
  `X_on_a_decision_picks_the_rejectLike_option`; `X_does_nothing_on_a_row_without_a_rejectLike_option`;
  `Y_copies_the_rows_cli`; `Enter_opens_the_sheet_and_Esc_returns_focus_to_the_row`.

**T4 — Seen, pulse, Mark all seen, reduced motion.**
- `SeenTrackerTests.A_row_is_seen_after_1_s_on_screen_and_focused`; `A_row_shown_but_not_focused_stays_unseen`;
  `Acting_on_a_row_marks_it_seen`; `Seen_ids_persist_in_preferences`; `Seen_ids_are_pruned_when_the_row_leaves_the_list`;
  `Mark_all_seen_marks_every_current_row_and_none_that_arrive_later`.
- `NeedsYouRowViewModelTests.Pulse_row_pulses_until_seen_then_steady`; `Colour_row_has_a_steady_bar_and_no_motion`;
  `Calm_row_has_a_subtle_bar`; `Reduced_motion_replaces_the_pulse_with_an_outline_and_the_word_new`;
  `The_in_app_switch_forces_reduced_motion_whatever_the_OS_says`; `New_rows_slide_in_unless_reduced`.
- `AttentionTests.Band_shows_while_any_pulse_row_is_unseen`; `Band_is_static_under_reduced_motion`;
  `Panel_header_pulses_once_when_rows_arrive`.

**T5 — Attention summary: title, badge, tray, summary line, live region, re-reads.**
- `AttentionSummaryTests.Title_is_N_Needs_you_Sindri_and_plain_Sindri_at_zero`;
  `Badge_counts_every_row_including_calm`; `Tray_is_attention_when_any_pulse_or_colour_row_exists`;
  `Tray_is_calm_with_a_small_count_when_only_calm_rows_exist`; `Tray_tooltips_match_spec_5_word_for_word`;
  `Tray_menu_lists_the_first_five_rows_kind_id_title_age`; `Tray_stale_and_not_running_win_over_counts`;
  `Summary_line_reads_N_need_you_N_running_N_with_you_N_queued`; `Announces_N_now_need_you_once_per_change`.
- `ShellAttentionTests.Tray_row_click_opens_needs_you_rowId_and_runs_no_action`;
  `Badge_and_tray_update_while_the_window_is_closed`;
  `RequeryTests.Every_event_rereads_needs_you_whatever_page_is_visible`;
  `Request_filed_decision_recorded_and_item_state_changed_reread_needs_you_decisions_requests_queue_and_the_open_item`;
  `Service_ran_rereads_needs_you_and_services`; `Profile_checked_rereads_needs_you_and_profile`
  (all pass under the pre-P5 "every visible view" rule, so they keep passing when E-15 arrives).

**T6 — Decide from the row and the decision sheet.**
- `DecisionSheetViewModelTests.Shows_question_options_recommended_filed_by_and_at_item_and_notice`;
  `One_button_per_option_labelled_as_served`; `Option_press_sends_Decide_with_the_option_id_and_the_note`;
  `Second_press_while_sending_does_nothing`; `Accepted_shows_the_outcome_for_2_s_then_the_row_clears_on_refresh`;
  `Conflict_409_rereads_and_shows_detail_inline_with_focus_on_the_control` (decided elsewhere and voided);
  `Cleared_while_open_shows_Cleared_and_the_served_cleared_line_from_the_decided_list`;
  `Cleared_while_open_disables_the_form_and_keeps_its_text`;
  `A_voided_decision_reads_as_served_with_its_cleared_line`;
  `A_applies_the_recommended_option_only_while_the_sheet_is_open`.
  The sheet finds a decided or voided decision in `GET /decisions?pending=false` (by id in the served list); no route
  by id exists.
- `FailureSheetViewModelTests.Shows_service_last_run_and_RunService_pending_P6_with_its_cli`.
- `ProfileSheetViewModelTests.Shows_changed_paths_and_hashes_and_only_copies_approve`.

**T7 — Decisions page and File a request.**
- `DecisionsViewModelTests.Pending_tab_lists_decisions_in_the_apis_order_with_option_buttons`;
  `Decided_tab_shows_decided_by_decided_at_choice_and_cleared`; `Voided_decisions_show_their_served_state_word`;
  `Requests_tab_shows_kind_item_text_actor_state_and_decision`; `Each_tab_shows_its_served_empty_sentence`;
  `Nav_count_is_the_pending_count`.
- `FileRequestViewModelTests.Kinds_are_rescope_add_task_note_handback`; `Text_over_2000_characters_cannot_be_sent`
  (the API still decides; `SND-REQ-001` shows when it refuses); `Accepted_closes_and_names_the_new_decision`;
  `Rate_limited_shows_SND_REQ_002_detail`; `Closed_item_shows_SND_ITEM_409_detail`; `R_from_an_item_prefills_the_item`.

**T8 — Item sheet: decisions and requests for the item.**
- `ItemSheetViewModelTests.Lists_pending_decisions_for_the_item_with_option_buttons`;
  `Lists_requests_filed_against_the_item`; `Shows_the_served_empty_sentences_for_the_item`.

**T9 — Home and the shell.**
- `ShellViewModelTests.Home_panels_order_is_needs_you_strip_running_queued_health` (U3': the strip, not a slot);
  ~~`Workflows_strip_slot_exists_and_is_never_visible_in_U3`~~ (retired by T16's
  `Strip_is_visible_between_needs_you_and_the_columns`); `G_N_opens_home_with_needs_you_expanded`;
  `G_D_opens_home_filtered_to_decision_rows` (U3': rev 8 §3, was `G_D_opens_decisions`); `Shift_A_marks_all_seen_from_anywhere_but_a_text_field`;
  `Deep_link_needs_you_rowId_selects_and_opens_its_sheet`; `Deep_link_decision_id_opens_the_decision_sheet`;
  `J_K_move_across_panels_needs_you_then_running_then_queued`.

**T10 — Views and key frames (integration lane, one frame per state; full sets in heavy).**
- `NeedsYouFramesTests`: `u3-needs-you-fixture`, `u3-needs-you-empty`, `u3-needs-you-stalled`,
  `u3-needs-you-rebuilding`, `u3-needs-you-pulse-unseen`, `u3-needs-you-seen`, `u3-needs-you-reduced-motion`,
  `u3-needs-you-show-all`, `u3-needs-you-held`, `u3-home-five-second`, `u3-home-large`.
- `DecisionsFramesTests`: `u3-decisions-pending`, `u3-decisions-decided`, `u3-decisions-voided`, `u3-requests`,
  `u3-decision-sheet`, `u3-decision-409`, `u3-decision-cleared`, `u3-file-request`, `u3-failure-sheet`,
  `u3-profile-sheet`.
- `NamedControlsTests.Every_needs_you_and_decision_control_is_named_label_plus_subject`; the §11.2 order test
  `Needs_you_rows_come_after_the_summary_and_before_running`; `ContrastTests` extended if a new pair appears.

**T11 — Head adapters and composition.** `MacDockBadge`, `WindowsDockBadge`, `NullDockBadge`; `AvaloniaTray` rows and
marks; `Composition` passes the badge; the AOT smoke still publishes and starts.
`StartupTests.Composition_builds_with_a_dock_badge`.

**T12 — Fixture script.** Test first in `scripts/tests/sindri-ui-fixture.test.sh`:
`request_sends_a_session_curl_with_no_caller_header`; `decide_runs_sindri_decide_as_the_owner`;
`fail_adds_a_service_the_build_cannot_run_and_approves`; `unfail_removes_it_and_approves`;
`items_large_adds_30_items_and_12_requests_across_two_repos`; `every_verb_refuses_an_unguarded_home`.
Verbs: `request [--item ID] [--repo R] <text>`, `decide <DEC-…> <choice>`, `fail` / `unfail`, `items --large`.

**T13 — Docs and gate.** `docs/csharp/sindri-ui.md` U3 section; `PublicAPI.Unshipped.txt`; the merge gate pasted in
the PR.

**T14 — UI-spec edits (test first; waits on the owner's OK, except the 011 fix, which is confirmed).**
Red first: `SpecTextTests` in the UI tests' `Docs/` folder (unit lane, reads the spec file like U0's `UiSource`):
`Spec_names_SND_API_011_for_read_your_writes` (confirmed: runs red, then the edit lands in U3's PR);
`Spec_says_X_picks_the_rejectLike_option`, `Spec_says_recordedOnly_is_served_as_notice`,
`Spec_says_the_failure_row_primary_is_RunService_pending_P6` (each committed as a skipped test with
`Skip = "owner OK pending (U3-API-6/7/2)"` until the owner answers; on yes, un-skip, see red, edit, green).
Edits:
- §4.1: "read-your-writes timeouts (`SND-API-004`)" → `SND-API-011` (confirmed by the API lane, D3).
- §8: `X` "from the catalog's `shortcut`" → "the option whose `rejectLike` is true on a decision; elsewhere the
  catalog's `shortcut`" (U3-API-6).
- §4.3: the `recordedOnly` line is the served `notice`, not app copy (U3-API-7); no §12.4 entry.
- §4.3: the `failure` row's primary is `RunService` (`pending:P6`) with `PauseService`, its CLI the served `cli`
  (U3-API-2; aligns API §3.6's former `copy:sindri status`).
- Raised with the owner, not edited: §9.2's `SND-API-005` (429) vs API's `SND-API-012`; §4.7 has no word for a
  `voided` decision or request.

### U3' — the Workflows strip (replaces "The rev-8 strip slot"; screens §4.1, E-20, E-21)

The strip's records, client call, fake route and the pinned goldens' spec-shaped copies are declared in the **lead's rev-8 contract unit**
(u8-plan.md T0), so U3' and U8 build against one `Instances(…)` method and one fixture table. U3' starts its strip
tasks once that unit is merged into its branch.

**T15 — Strip contract check (red first, against the API lane's pinned goldens (`workflow-instances/root`, `empty`) and the extras).**
- `GoldenContractTests.Workflow_instances_root_goldens_read_through_the_apps_json_context` (places the
  `workflow-instances` slug; fails until the contract unit lands it).
- `SindriApiClientTests.Instances_sends_root_true_and_no_other_filter_for_the_strip`;
  `Instances_reads_rows_in_the_apis_order_with_recipe_urgency_and_the_current_state_cell`;
  `Instances_maps_SND_VIEW_001_stalled_and_rebuilding_like_every_list` (the shared banners).
- Contract base facts (fake and client): `Instances_root_empty_returns_the_served_sentence`,
  `Instances_root_serves_INC_14_first`.

**T16 — Strip view model.**
- `WorkflowsStripViewModelTests.Rows_keep_the_apis_order` (incident, then triage, then work, then oldest, as served);
  `Strip_is_visible_between_needs_you_and_the_columns`; `Caps_at_five_then_shows_N_more`;
  `N_more_opens_Workflows` (`G W`'s page; before U8 merges it is the contract unit's "arrives with API P11"
  placeholder); `Heading_reads_Workflows_N_open_from_the_served_count`
  (`counts.open` when U3'-API-10 lands; until then the count of non-terminal rows, test-named so the switch is one
  edit: `Open_count_falls_back_to_non_terminal_rows_until_counts_is_served`);
  `Fold_gives_rows_back_to_needs_you_first` (needs you 5 → strip 3; needs you 2 → strip 5; screens §4.1);
  `Text_scale_1_3_drops_the_strip_cap_to_two` (rev 7 §3.2's scale rule applied to the strip, proposed; owner OK in T14b);
  `Empty_strip_shows_the_served_sentence_and_no_N_more`; `Stalled_keeps_rows_marked_stale`;
  `Not_served_yet_shows_arrives_with_API_P11_when_the_route_is_absent` (screens §4.8; a 404 on
  `/workflow/instances` before P11 marks only the strip, never Home).
- `StripRowViewModelTests.Shows_chip_subject_recipe_stage_tag_now_and_age`;
  `Chip_word_comes_from_the_one_chip_table` (`RUNNING`, `DECISION`, `ESCALATED`, `PARKED`, `WITH YOU`, `DONE`; fixture.md;
  from the served `chip` once U3'-API-9 lands, from `engineOwned` + `desk` + `actions` until then, in one function
  shared with U8's list: `StateChip.For(InstanceSummaryView)` in the contract unit);
  `Now_is_the_served_sentence_unchanged` (INC-14's `agent rolling back in ITM-01JG2 · comms posting`);
  `Engine_badge_replaces_the_glyph_in_the_stage_tag` (`▲ escalated: <reason>`);
  `Desk_state_with_an_answer_action_shows_Answer`; `Protected_answer_shows_the_lock_button_with_its_popover`
  (`⌂ Accept · terminal`, the popover holds the served `cli`, Copy, Open in an agent pending P10a);
  `Row_without_a_ready_action_shows_none`; `Row_id_and_name_are_label_plus_address`.
- `StageTagTests.Round_reads_r_n_slash_max`; `Desk_reads_dot_desk`; `Children_read_done_slash_total`;
  `Child_reads_child`; `Loop_pair_reads_state_arrows_with` (`mitigate r1/3 ⇄ verify`).

**T17 — Strip keys, links and the inline answer.**
- `StripKeysTests.J_K_move_needs_you_then_strip_then_running_then_queued` (extends T9's J/K order);
  `Enter_opens_workflow_address` (deep link `workflow/<address>`; before U8 merges, the contract unit's placeholder
  sheet naming the address); `A_on_a_strip_row_runs_its_single_ready_desk_answer_and_never_a_protected_one`;
  `Answer_opens_the_park_rows_sheet_for_the_same_instance` (P11 serves a desk task as a needs-you row of kind `park`
  with the engine's action id; the strip's [Answer] selects that row's sheet, so the answer form is one component,
  U3's generic `park` rendering, until U5' replaces it);
  `Selection_is_kept_by_address_across_refreshes`; `A_row_that_turns_terminal_leaves_and_announces_address_done`.

**T18 — Re-reads and the fold.**
- `RequeryTests.Workflow_events_reread_the_strip_and_needs_you_whatever_page_is_visible`;
  `A_started_incident_moves_to_the_top_without_a_click` (S8; the fake pushes `workflow.started` and re-serves
  `extra-root-incident-first`); `Strip_refresh_never_moves_focus_scroll_or_input`.
- `FoldTests.At_1280x800_summary_needs_you_5_strip_3_and_column_tops_are_above_the_fold` (headless, measured by
  layout bounds at text scale 1.0); `At_scale_1_3_needs_you_4_and_strip_2_fit`.

**T19 — Strip views and key frames (integration lane).** `WorkflowsStripFramesTests`: `u3p-strip-fixture` (the five
fixture rows, "… 7 more"), `u3p-strip-incident-first`, `u3p-strip-empty`, `u3p-strip-stalled`,
`u3p-strip-not-served`, `u3p-strip-protected-popover`, `u3p-home-five-second-strip`, `u3p-home-large-strip`;
`NamedControlsTests.Every_strip_control_is_named_label_plus_address`; the §11.2 order test
`Strip_rows_come_after_needs_you_and_before_running`; `ContrastTests` for the chip pairs if the contract unit adds any.

**T20 — Fixture script and proof hooks (prover; test first in `scripts/tests/sindri-ui-fixture.test.sh`).**
- `workflows_starts_one_triage_and_two_work_instances_through_the_cli` (`sindri workflow start` on the dev recipes,
  `recipes/dev/triage-three-state.yaml` and `desk-round-trip.yaml`, screens §8.2);
  `incident_start_starts_an_incident_urgency_instance_and_stop_cancels_it` (needs a dev recipe with
  `urgency: incident`, ask **U3'-ENG-1**); `workflows_large_starts_twelve_roots_across_three_recipes`;
  `every_workflow_verb_refuses_an_unguarded_home`.
- Verbs: `workflows [--large]`, `incident start|stop`.

**T14b — Rev 8 spec text (lead, with T14).** Red first in `SpecTextTests`: `Screens_spec_strip_drawing_matches_the_fixture`
(§4.1's drawing says "WORKFLOWS 7 open … 2 more"; fixture.md says 12 open and "… 7 more"; the fixture wins on data,
§4.9); `Screens_spec_states_the_strip_cap_at_scale_1_3` (skipped until the owner OKs T16's proposal).

Units for the team (§19): T1 alone (contract); then **A** = T3+T4+T9, **B** = T6+T7+T8, **C** = T2+T5+T11; views (T10)
per unit; T12 by the prover; T13 by the integrator; T14 by the lead after the owner answers. **U3':** **D** = T15–T19
(after the lead's rev-8 contract unit), T20 by the prover, T14b by the lead. Hotspots appended at
anchors only: `SindriUiJsonContext.cs`, `ISindriApi.cs` and its fake, `SindriApp.axaml`, the shell's navigation table,
the key map, the palette registration, `Labels.*`, the head's composition, `sindri-ui-fixture.sh`,
`PublicAPI.Unshipped.txt`.

## Fixtures (P4's golden names; spec-shaped copies under the same names until P4 records them)

Path while spec-shaped: `tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/<slug>/<scenario>.json`; P4's own:
`tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json` (linked as `Golden/Api`). U3 reads these:

| Golden | Route | Used for |
|---|---|---|
| `needs-you/empty` | GET /needs-you | empty sentence ("Nothing is waiting on you. N running, M queued.") |
| `needs-you/fixture` | GET /needs-you | decision + failure + profile, in order |
| `needs-you/one-request` | GET /needs-you | S1's single pulsing row |
| `needs-you/stalled`, `needs-you/rebuilding` | GET /needs-you | banners |
| `needs-you/large` | GET /needs-you | 12 decisions in 2 repos + failure + profile (fold, cap, Show all) |
| `needs-you/after-decide` | GET /needs-you | the row gone after Accept (S4) |
| `needs-you/session-repo` | GET /needs-you | not read by the app (a session view); placed as a readable golden only |
| `decisions/pending`, `decided`, `empty`, `empty-decided`, `voided`, `by-item`, `stalled` | GET /decisions | Pending/Decided tabs, voided word, the item sheet block, cleared lines, banners |
| `decisions/session-other-repo` | GET /decisions | readable only |
| `decisions-id/accepted`, `declined` | POST /decisions/{id} | accepted lifecycle |
| `decisions-id/decided-elsewhere`, `voided` | POST /decisions/{id} | 409 inline details (S5) |
| `decisions-id/not-found`, `bad-choice`, `bad-id`, `refused-session`, `while-stalled` | POST /decisions/{id} | refusals inline |
| `requests/list-fixture`, `list-empty`, `list-state-accepted`, `list-by-item` | GET /requests | Requests tab, item sheet block |
| `requests/list-session-other-repo`, `list-bad-state` | GET /requests | readable / refusal only |
| `requests/filed`, `filed-item` | POST /requests | File a request accepted |
| `requests/invalid`, `invalid-kind`, `too-long`, `too-many`, `item-not-found`, `item-closed`, `refused-origin` | POST /requests | refusals inline |
| `queue/fixture` (re-recorded), `items-id/needs-decision`, `items-id/after-decide` | GET /queue, /items/{id} | Home's panels with the `needs-decision` row; the item after its decision |
| `commands/catalog` (re-recorded) | GET /commands | `Decide`, `FileRequest` ready; three query rows |

Retired from U2/U3's provisional set (covered by goldens): `requests-post/*` (→ `requests/*`),
`commands/extra-catalog-p4` (→ `commands/catalog`), `needs-you/extra-after-decide` (→ `needs-you/after-decide`),
`items-id/extra-p4-decided` (→ `items-id/after-decide`), U2's `items-id/extra-p4-needs-decision` (→
`items-id/needs-decision`) and `queue/extra-p4-waiting` (→ `queue/fixture`).

**Extras kept (no golden covers them; `extra-` prefix, SpecShaped only):**

| Extra | Why |
|---|---|
| `needs-you/extra-profile-null-subject` | the profile row with `subject: null`, in case the owner declines G8 (the app handles both) |
| `needs-you/extra-p11-park` | a `park` row with a protected primary and a desk secondary (generic render; U5 owns parks) |
| `queue/extra-p4-after-decide` | the queue with the item back in `queued` after Accept (P4 re-records only `queue/fixture`) |

**U3' strip goldens — pinned by the API lane's rev-8 contract unit** (`plans/sindri-api-lane/rev8-goldens-for-ui.md`,
2026-10-14; written by the API contract unit from the screens fixture into `tests/Aw.Sindri.Tests/Golden/Api/`,
linked as `Golden/Api`). Use these names and fields exactly; field names are fixed (API §3.8), record names may still
change. Routes are served under `/sindri` (`GET /sindri/workflow/instances…`). Until the files are on a branch U3' can
merge, the lead's contract unit writes byte-identical spec-shaped copies under **the same slug and scenario** in
`Golden/SpecShaped/` and deletes each when the real file lands (a diff is a contract-unit fix).

| Golden (API, pinned) | Route | Used for |
|---|---|---|
| `workflow-instances/root` | GET /workflow/instances?root=true | fixture.md's 12 roots in urgency order (INC-14 first); the five-row strip and "… 7 more" |
| `workflow-instances/empty` | same | the served empty sentence |
| `workflow-instances/recipe-filter` | GET …?root=true&recipe= | not read by the strip (U8's `[Recipe ▾]`); placed as readable |
| `workflow-instance/triage-desk` | GET /workflow/instances/{address} | not read by the strip; the target its [Answer] and `Enter` open (U8 renders it) |

**Extras U3' still needs (SpecShaped only, `extra-` prefix, built from the same fixture table; each is a candidate for
the API lane to record later):**

| Extra | Why |
|---|---|
| `workflow-instances/extra-root-incident-first` | S8: one incident running, one triage at a desk, two older work instances (fixture `root` has INC-14 first but every row's age fixed; this one changes the incident's `since` to be the youngest) |
| `workflow-instances/extra-root-large` | S9: the fold and cap with Needs you at 5 (twelve open roots across three recipes, no done rows) |
| `workflow-instances/extra-root-stalled`, `extra-root-rebuilding` | the shared banners on this list (the API's list rule, P3) |
| `workflow-instances/extra-root-terminal-mixed` | done rows among open ones, for the fallback filter until U3'-API-10 |
| `workflow-instances/extra-not-served` | a 404 standing for "no route before P11" (§4.8 not served yet) |
| `needs-you/extra-p11-desk-park` | P11's `park` rows (the triage desk task, an engine park) beside P4's kinds: the strip's [Answer] target; replaces U3's `needs-you/extra-p11-park` |
| `commands/extra-catalog-p11` | `ApplyCommand:*` and `CompleteStep` rows with their classes, until P11 re-records `commands/catalog` |

## API edits — answered by the API lane (2026-10-14, `u3-api-answers.md`)

| # | Asked | Answer | Status |
|---|---|---|---|
| **U3-API-1** | `NeedsYouRow` wire shape; `subject {kind, id}`; `label` for every kind | Taken, plus `item?` and `notice?`; `label` on every kind (`failure · <name>`, `profile · drifted`). The `profile` subject kind needs the owner (G8); if declined, `subject` is null | **answered**; owner pending on G8 (handled both ways) |
| **U3-API-2** | `primaryAction` catalog ids only | Taken: decision → `Decide`, failure → `RunService` (`pending:P6`, plus `PauseService`), profile → `ApproveProfile` (cli-only); `cli` is the one copy string; API §3.6 alignment edit | **answered** |
| **U3-API-3** | Decision, request, `RequestFiled` fields | Taken as pinned above (`options[{id,label,rejectLike,cli}]`, `recommended`, `filedBy`, `filedAt`, `decidedBy?`, `decidedAt?`, `choice?`, `note?`, `state` incl. `voided`) | **answered** |
| **U3-API-4** | `GET /requests?item=` | Taken: `?state=&item=`, CLI `--item` | **answered** |
| **U3-API-5** | Golden slug for GET + POST `/requests` | One slug `requests`; GET scenarios `list-*` | **answered** |
| **U3-API-6** | Reject-like marker per option | `options[].rejectLike`; `Decide`'s `shortcut` stays null; no `Decide:*` variants (lead) | **answered**; UI §8 wording → T14 (owner) |
| **U3-API-7** | Served `recordedOnly` sentence | `notice` on the row and the decision | **answered**; UI §4.3 wording → T14 (owner) |
| **U3-API-8** | Clearing line for itemless subjects | `cleared` on decided and voided `DecisionView`s from `GET /decisions?pending=false`; no `GET /decisions/{id}` (lead) | **answered** |

**U3' asks (new; to the API lane's P11 contract unit unless noted).** U8-API-1…7 (u8-plan.md) are shared; the strip
needs U8-API-1 and U8-API-2 directly, listed here under U3' numbers so the API lane can answer them once.

| # | Ask | Why (section) | Until answered |
|---|---|---|---|
| **U3'-API-9** (= U8-API-1) | A served `chip: {word, tone}` on `InstanceSummary`, `ChildGroup` rows and `NeedsYouRow`, from fixture.md's one chip table | Screens R8-13, fixture.md "State chips: one mapping"; the strip, the Workflows list and Home's needs-you must say the same word for the same instance, and `DECISION` (a desk answer) vs `WITH YOU` (a claimed desk step, PRJ-exports) can't be told apart from `desk` alone | `StateChip.For(…)` in the contract unit, one function for both lanes, test-pinned to the fixture's rows |
| **U3'-API-10** (= U8-API-2) | `GET /workflow/instances?open=true` (non-terminal only) and `InstanceList.counts {open, needsYou, withYou, running, done}` | Screens §3 ("open root instances" is the tab count), §4.1 ("N open"), fixture.md ("every count in a page head is derived from the rows under it"); API §3.8 already says the strip is "the first five rows with `state` not terminal", which puts a filter in the app; owner question Q2 (u8-plan.md) | the fallback filter on `StateCell.terminal`, one test-named function |
| **U3'-API-11** | The needs-you `empty` sentence drops "waiting on you": "Nothing needs you. N running, M queued." (or the mockup's "Nothing needs you. Last cleared HH:MM.") | fixture.md "waiting on you is retired: no page, heading, tooltip or table cell uses it" (R8-13); P4 pins "Nothing is waiting on you. N running, M queued." | the served sentence renders as served (the app never writes its own); the five-second shot shows the old wording |
| **U3'-API-12** | `GET /status` serves the health sentence (`health.sentence`), generated once from the pill's reasons | fixture.md "The health sentence (generated from the pill's reasons, verbatim on Home and System)"; R8-13 wave 6. If the app built it from `problems[]`, Home and System (U12) would each own a copy | Home's health strip keeps U1/U2's pill words; the sentence waits |
| **U3'-ENG-1** (engine lane) | A dev recipe with `urgency: incident` (e.g. `recipes/dev/incident-hold.yaml`: `detected → mitigate (desk) → closed`) with its eval, so the prover can start "a running incident" | S8 needs an incident-urgency root; screens §8.2 lists only the triage and desk dev recipes | S8 proven in the headless lane only (`extra-root-incident-first`); the real-app step waits |

## What is real-app-provable before P4, and what waits

Before P4 (against P3's control plane under `sindri-prove.sh`, once P3 merges; the fake in the headless lane until
then):
- U0–U2 regressions in the published app: Queue, item sheet, Create/Close, the stalled banner, Health.
- The reduced-motion switch and its persistence; the attention band's absence with no rows.
- The tray's neutral, stale and not-running states and the dock badge cleared at zero (macOS), OS screenshots.
- The published app starts with the new ports (AOT smoke); the Needs you panel, finding no `/needs-you` route, shows
  its refusal inline for that panel only rather than inventing rows.
- Everything else in the headless integration lane against the spec-shaped copies: every key frame in T10, every key
  in T3/T9, every lifecycle phase, the re-read tests in T5.

Waits for P4: S1–S7 entirely (the verification plan's real-control-plane journeys), the Decisions and Requests pages on
real data, the item sheet's decisions and requests, the five-second test on `--large`. `invalidates` (E-15) waits for
P5. T14's three owner edits wait on the owner.

**U3' — what is provable before P11, and what waits.**
- Before P11 (real app under P3/P4's serve): the strip's **not-served** state (`/workflow/instances` absent: the strip
  frame says "arrives with API P11", Home is untouched), the fold with Needs you only, the seven-tab shell from the
  contract unit, U3's P4 journeys with the rev-8 deltas (no band, no side bars, chips, `G D` filter).
- Headless before P11: every T15–T19 test and key frame against the pinned `workflow-instances/*` copies and extras.
- Waits for API P11 + WF P4 (instances exist) + WF P2b (`urgency`) + U3'-ENG-1: S8 and S9 in the real app (J6, J7).

## Rev 8 deltas to the U3 tasks (replaces "The rev-8 strip slot"; the screens spec wins where rev 7 differs, X52)

The owner's earlier "Rev 8: not approved; WorkflowsStripSlot stays empty and hidden" is superseded by the merge of #210
(4a2389bd) and the orchestrator's rev-8 assignments (2026-10-14). Every other owner answer stands. Each delta below is
a test change made red first in the U3' PR; the lead confirms the four marked **(confirm)** with the orchestrator
before T4/T7 start, since they remove things the U3 plan built for.

| # | U3 had | Rev 8 says | U3' does |
|---|---|---|---|
| D1 **(confirm)** | `AttentionBand` under the top bar while a pulse row is unseen (rev 7 §3.2; T4 `AttentionTests.*`, J1.3, J3) | R8-13: "the attention band is withdrawn (wave 6: the health pill carries the state …)" | No band. T4's band tests become `No_attention_band_on_any_page`; `Panel_header_pulses_once_when_rows_arrive` stays (the mockup's pill/header pulse once on a count change) |
| D2 **(confirm)** | A 3 px urgency **bar** on each row that pulses, a steady bar, a subtle bar (T4) | R8-13 "no side stripes"; `home.html`: unseen rows are tinted, "after Mark all seen: the tint clears" | The pulse and seen state move to the row tint (and the chip); T4's `*_bar_*` tests are renamed to `*_tint_*`; reduced motion keeps the static outline and `new` |
| D3 **(confirm)** | A **Decisions page** (Pending, Decided, Requests tabs; T7) and `G D` opening it | R8-1 "Decisions folds into Home's Needs you"; §3 `G D` = "Home filtered to decision rows"; per-instance Decisions and Requests are frame tabs (§4.2, U8) | T7 becomes `HomeDecisionFilter` (`G D` filters Needs you to `decision` rows; Esc clears) and keeps `FileRequestViewModel`; the Decided/voided/requests lists are built as standalone VMs (`DecisionListViewModel`, `RequestListViewModel`) that U8's frame tabs and the item sheet host. No `PageId.Decisions`; the nav shows rev 8's seven tabs (contract unit) |
| D4 **(confirm)** | T8 edits `ItemSheetViewModel` for decisions and requests | §4.7: the item sheet is re-based on U8's frame, whose tabs are Timeline, Decisions, Requests, Context | T8 builds `ItemDecisionsViewModel` (no edit to `ItemSheetViewModel*`); U8's frame tab hosts it. Avoids two lanes editing the item sheet |
| D5 | Row label `decision · re-scope` + urgency word | fixture.md: one chip per row (`PARKED`, `ESCALATED`, `DECISION`, `PROFILE`, `FAILED`); the label becomes the row's secondary line | Rows show the chip from `StateChip` (contract unit) and keep the served `label` as the reason line; named "<chip> <id>" for screen readers |
| D6 | The protected row's lock button with the CLI on a second line | fixture.md "The protected-action label": `⌂ <Verb> · terminal`, title "Needs a typed confirmation at a terminal", and the critique's "lock button opens a popover with the command, Copy and Open in an agent" | Uses the contract unit's `ProtectedActionButton` (popover); T3's `Profile_row_shows_a_lock_button…` asserts the popover |
| D7 | Needs-you `empty` "Nothing is waiting on you. N running, M queued." (served) | "waiting on you" is retired | Rendered as served; U3'-API-11 asks for the new sentence |
| D8 | Nav `Queue`, `G R`/`G Q` for Queue (rev 7) | §3: Work = `G Q`, Recipes = `G R`, Workflows = `G W`, System = `G .`/`G S` | Contract unit; U3' only adjusts "Show all N in Queue (`G R`)" to "all N →" opening Work |
| D9 | Mark all seen: immediate | `home.html`: "5 marked seen · Undo" for 10 s (a mockup comment; the text is silent) | `Mark_all_seen_offers_Undo_for_10_s` added to T4, flagged in the PR as mockup-sourced |

T14's §4.3 and §4.7 edits now target rev-7 sections the screens spec supersedes (status block: "Supersedes … §4.2–§4.8").
§4.3's text is still imported by reference (screens §4.1: "Rev 7 §4.2 as approved"), so T14 keeps the §4.3 edits and
drops the §4.7 edit (the page no longer exists); §4.1 and §8 are not superseded and keep their edits.

## Risks

| Risk | Mitigation |
|---|---|
| P4's recorded goldens differ from the pinned shapes | Spec-shaped copies carry the same names; when P4 records, `GoldenContractTests` reads both and the copies are deleted; a field change is a contract-unit fix |
| The owner declines `profile` as a subject kind (G8) | Rows key on `id` (`profile:drift`), never on `subject`; both shapes have fixtures and tests |
| A decided or voided itemless decision needed for "Cleared" falls off the decided list (P4 Q6: newest first, maybe capped at 200) | The sheet looks up its own id in `GET /decisions?pending=false`; if absent, it shows "Cleared" without a line and asks the API lane; tested |
| `MacDockBadge` through `objc_msgSend` under NativeAOT; `ITaskbarList3` COM under AOT | `LibraryImport` and source-generated COM only; the AOT smoke publishes and starts; `NullDockBadge` fallback; proof on macOS only |
| Tray icons with a drawn count per platform; GNOME needs AppIndicator (§18.2 accepts) | Marks rendered from tokens once per count (1–9, 9+); Linux best effort |
| The pulse animation costs CPU while idle | One shared animation on the bar; paused when the window is hidden; zero under reduced motion |
| "Seen" needs on-screen-and-focused for 1 s; headless tests can't see the screen | `SeenTracker` driven by visibility and focus events with `FakeTimeProvider`; the OS proof checks it (J3) |
| Re-reading needs-you on every event until P5, window closed included | U1's 250 ms debounce; accepted cost |
| A session request needs `X-Sindri-Repo` naming a profile repo | T12 adds two repos to the fixture profile before `up` |
| The failing service: a profile naming a service the build lacks may be refused by `profile validate` | T12's test tries it first; fallback chosen in the test |
| Request caps during `--large` | 12 requests across two repos (P4's `large` golden matches); the fixture test asserts no `SND-REQ-002` |
| The strip's chip word or open count disagree with U8's list (two lanes, one instance table) | One `StateChip.For` and one fixture table in the lead's contract unit; a test asserts the nav count, the strip head and the Workflows head equal on `workflow-instances/root` |
| P11 serves the triage desk task as a needs-you `park` row, so Home counts 6 where the fixture says 5 | Owner question Q1 (u8-plan.md); the app renders what is served and never filters rows |
| The incident signal (S8) has no dev recipe with `urgency: incident` | U3'-ENG-1; headless proof on `extra-root-incident-first` meanwhile |
| Stack drift: U2 in review; P3 goldens not recorded | Merge U2's tip into U3 when it moves; U3 swaps copies for goldens as P3 and P4 record them |

## Still conflicting with UI spec rev 7 after the answers (in T14 or raised)
- §8 `X` "from the catalog's `shortcut`" vs `options[].rejectLike` with `Decide`'s `shortcut` null (T14, owner).
- §4.3 shows the `recordedOnly` line as app text vs the served `notice` (T14, owner).
- §4.3/§7's failure row (`RunService`) now matches the API; §4.3's wording gains `PauseService` and the served `cli` (T14, owner).
- §4.1 `SND-API-004` vs `SND-API-011` (T14, confirmed).
- §9.2 `SND-API-005` (429) vs API `SND-API-012` (raised).
- §4.7 has only Pending and Decided; P4 serves `voided` decisions and requests with no UI word for them (raised; the
  app shows the served `state` and `cleared` line as is).
- §3.3 "Queue (all five groups)" vs §4.5's four groups (U2's; noted).

## Rev 8 vs the U3 owner answers and plan (U3'; see "Rev 8 deltas")
- Owner answer "Rev 8: not approved; WorkflowsStripSlot stays empty and hidden" vs #210 merged and approved: superseded.
- U3's attention band (rev 7 §3.2) vs screens R8-13 "the attention band is withdrawn": D1.
- U3's row bars vs R8-13 "no side stripes": D2.
- U3's Decisions page and `G D` vs screens R8-1 and §3 (`G D` = Home filtered to decision rows): D3.
- Owner answer (c)+(e) edits rev 7 §4.7, a section the screens spec supersedes: dropped from T14.
- P4's served empty sentence "Nothing is waiting on you…" vs fixture.md's retired "waiting on you": U3'-API-11.
- Screens §4.1's drawing ("WORKFLOWS 7 open", "… 2 more") vs fixture.md ("12 open", "… 7 more"): the fixture wins on data (§4.9); T14b.

## Owner answers (2026-10-14, via orchestrator) — T14 unblocked
- (a) §4.1 SND-API-004 → SND-API-011: go.
- (b) §8: X = the option with `rejectLike: true`; X hidden when no option is marked. Reword the "catalog's shortcut" line.
- (c)+(e) recordedOnly and voided sentences are served by the API; drop them from UI §4.3/§4.7 copy (render the served text).
- (d) §4.3 failure row stands (RunService, pending:P6); API §3.6 changes on its side (U3-API-2).
- G8: profile is a needs-you subject kind: `subject: {kind: "profile", id: <live hash>}` on the profile-drift row (keep the null-subject extra as a defensive test only).
- Rev 8: not approved; WorkflowsStripSlot stays empty and hidden. **(Superseded 2026-10-14: #210 merged and approved; see "Rev 8 deltas".)**
All spec edits test-first in the U3 PR.

## Owner answers (2026-10-14, via orchestrator a6): rev 8 readings CONFIRMED
- Q1 Home counts all six chips (desk tasks included) → shows 6. Q2 "open" = not terminal → fixture reads 8, with a collapsed "N done today" line per group. Write Q1+Q2 into the rev-8 spec in the U3'/U8 PRs, test-first.
- Q3 ⌘K protected confirm: not now; the lock popover is the in-app path.
- D1 attention band withdrawn; D2 no urgency stripes; D3 Decisions folds into Needs you, G D filters Home; D4 U3' builds the standalone item view model, U8's tab hosts it. The (c)+(e) rev-7 §4.7 edit is dropped.
- U8 builds the desk Complete form; U5' owns Complete for engine parks.
- Worktree plan approved: after #184 → lead contract unit, then U3'; after #193 → U8. U3'-ENG-1 sent to engine.

## API answers U8-API-1..9, U3'-API-11/12 (API lane c3, 2026-10-14)
Full text: plans/sindri-api-lane/u8-u3p-answers.md (citations rev8-contract-plan.md §8). Apply when building:
- chip {word, tone: attn|fail|ok|neutral} on InstanceSummary, ChildRow, NeedsYouRow; started un-taken-over desk step = DECISION, WITH YOU = taken over only.
- ?root=true&open=true + InstanceList.counts {open, needsYou, withYou, running, done} (root set); golden workflow-instances/open.
- InstanceSummary.answer? = status.answer record.
- No address routes: post to engine id routes /sindri/workflow/instances/{id}/nonce, …/commands, …/runs/{run}/complete using view's id + current.desk.run.
- Timeline: GET …/instances/{id}/events?after= (engine #192); children=true engine's; ?instance= on /decisions,/requests in API P11 (fallback until then).
- parent?: {address, title, task?, workstream?} (task/workstream null until engine P7).
- GET /items?parent= in API P11; until then open Work unfiltered.
- FactView {key, type, body, truncated}; goldens workflow-instance-fact/{scope-map, packet, payload, text, truncated, not-found}.
- commands[].confirm? null until owner approves templates → show "Cancel <address>?" meanwhile.
- U3'-11 empty: "Nothing needs you. N running, M queued." ("Last cleared" pending owner).
- U3'-12 health.sentence on /status; UI appends "Updated Ns ago" from asOf.
- New contract-unit goldens 24–30 being built; API will send the green sha → re-point fakes then.

## Owner wording + engine dev recipes (orchestrator 2c, 2026-10-14)
- Cancel confirm (served in commands[].confirm): "Cancel <address>? <N> running steps stop; <M> children are cancelled." Render the served text; no UI fallback once it ships.
- Needs-you empty sentence stays "Nothing needs you. N running, M queued."
- Engine dev recipes for fixtures/proofs: workflow.dev-triage-three-state, workflow.dev-desk-round-trip, workflow.dev-incident-hold (the strip's "incident first" proof).

## Engine answer U3'-ENG-1 (engine lane 9e, via 2c, 2026-10-14)
- Branch feat/sindri-wf-p2b-recipe-rev8 (e7595e7d, NOT on main yet), recipes unapproved: recipes/dev/incident-hold.yaml (workflow.dev-incident-hold, urgency incident, holds at desk state `hold` until answered, no inputs, `file` output on the desk state) — the strip's "incident first" proof; triage-three-state.yaml (urgency triage); desk-round-trip.yaml (urgency work).
- `recipe list --json` rows now carry urgency, inputs[]{key,label,type,kind}, outputs[]{key,label,type,from}. The real-app strip proof needs this branch on main (or the prover to approve the dev recipe in its temp home).

## API rev-8 contract unit GREEN (API lane f9, 2026-10-14)
- Branch feat/sindri-api-rev8-contract @ 249aec3a (off main 4a2389bd); PR + review next. Records in src/Sindri/Aw.Sindri.Core/Workflow/Views/ (§3.8/§8 names; instance view record = InstanceDetail, wire names unchanged; chip {word,tone}; Cancel commands[].confirm = approved sentence).
- 30 goldens under tests/Aw.Sindri.Tests/Golden/Api/: workflow-instances/{root,recipe-filter,open,empty}; workflow-instance/{dev-desk-round-trip,dev-triage-three-state,done,escalated-gate,incident-loop,not-found,project-build,transition-list,triage-desk}; workflow-status/{desk-task,done,escalated-gate,needs-answer-wait,parked-budget,parked-child,parked-drift,taken-over}; workflow-recipe/{incident-response,no-outputs,workflow-project}; workflow-instance-fact/{scope-map,packet,evidence,text,payload,truncated}.
- Caveats: recipe hashes placeholders until engine P2b; not-found hand-written until SND-ITEM-404; dev-incident-hold golden arrives with P2b.
- The UI contract unit (feat/sindri-ui-r8-contract) builds its fakes on these: merge the API branch (or main once it lands) into the UI contract branch; rename provisional fixture names to these.
