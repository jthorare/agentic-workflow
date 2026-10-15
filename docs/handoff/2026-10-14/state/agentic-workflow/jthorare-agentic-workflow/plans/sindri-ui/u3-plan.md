# U3 plan — Needs you, decisions, requests, Home, the pulse, Mark all seen, badges and the tray menu (UI spec §18 U3)

Planned 2026-10-14 by the UI lane's U3 planner. Plan only: no code, no commits.
Updated 2026-10-14T06:53:06Z: the API lane answered U3-API-1..8 and pinned P4
(`plans/sindri-api-lane/u3-api-answers.md`, `p4-plan.md`). Fixture names are now P4's golden names; the wire types
are as pinned; a UI-spec-edit task (T14) is added.

Branch `feat/sindri-ui-u3`, worktree `.claude/worktrees/sindri-ui-u3`, from `origin/feat/sindri-ui-u2` @ da457bb8
(merge, not rebase, when U2 moves). Draft PR base `feat/sindri-ui-u2`; stack U3 → U2 → U1 (#193) → U0 (#184).
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

## Re-query map (until P5's `invalidates`, every event re-reads every visible view, plus needs-you always)

| Event | Re-reads |
|---|---|
| `request.filed`, `decision.recorded`, `item.state-changed` (a close voids that item's decisions) | needs-you, decisions, requests, queue, items, the open item and its timeline |
| `service.ran` | needs-you, services |
| `profile.checked` | needs-you, profile |

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
- `Views/Shell/AttentionBand.axaml(.cs)`.

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
- `Views/Shell/HomePage.axaml(.cs)` (Needs you panel; the **rev-8 strip slot**), `ShellWindow.axaml` (title binding,
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
- `scripts/sindri-ui-fixture.sh` (+ `request`, `decide`, `fail`, `unfail`; `items --large` adds 12 requests) and
  `scripts/tests/sindri-ui-fixture.test.sh`.
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
- `ShellViewModelTests.Home_panels_order_is_needs_you_strip_slot_running_queued_health`;
  `Workflows_strip_slot_exists_and_is_never_visible_in_U3`; `G_N_opens_home_with_needs_you_expanded`;
  `G_D_opens_decisions`; `Shift_A_marks_all_seen_from_anywhere_but_a_text_field`;
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

Units for the team (§19): T1 alone (contract); then **A** = T3+T4+T9, **B** = T6+T7+T8, **C** = T2+T5+T11; views (T10)
per unit; T12 by the prover; T13 by the integrator; T14 by the lead after the owner answers. Hotspots appended at
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

## The rev-8 strip slot (named, empty)

Rev 8 (not approved) adds the **Workflows strip** between Needs you and the two columns (rev 8 §4.1, U3'), fed by
E-20 and E-21, which land with API P11. U3 builds none of it:
- `HomePage.axaml` gets one named, empty element between the Needs you panel and the Running/Queued grid:
  `<ContentControl x:Name="WorkflowsStripSlot" IsVisible="False" AutomationProperties.Name="Workflows strip slot" />`,
  with a comment pointing to rev 8 §4.1 and U3'. No view model, no route, no label, no fixture, no focus stop.
- `Workflows_strip_slot_exists_and_is_never_visible_in_U3` pins it. The fold rule stays rev 7's (§3.2).

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

## Owner answers (2026-10-14, via orchestrator) — T14 unblocked
- (a) §4.1 SND-API-004 → SND-API-011: go.
- (b) §8: X = the option with `rejectLike: true`; X hidden when no option is marked. Reword the "catalog's shortcut" line.
- (c)+(e) recordedOnly and voided sentences are served by the API; drop them from UI §4.3/§4.7 copy (render the served text).
- (d) §4.3 failure row stands (RunService, pending:P6); API §3.6 changes on its side (U3-API-2).
- G8: profile is a needs-you subject kind: `subject: {kind: "profile", id: <live hash>}` on the profile-drift row (keep the null-subject extra as a defensive test only).
- Rev 8: not approved; WorkflowsStripSlot stays empty and hidden.
All spec edits test-first in the U3 PR.
