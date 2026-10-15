# U8 plan — the Workflows list, the instance frame, the item sheet re-based on it, fact viewers for text, file, item, link and payload (screens spec §8 U8, §4.2, §4.7, §5)

Planned 2026-10-14T23:43:38Z by the UI lane's planner. Plan only: no code, no commits.
Updated 2026-10-14 (same session): the API lane pinned its rev-8 contract unit
(`plans/sindri-api-lane/rev8-goldens-for-ui.md`); the golden names and fields below are theirs. Anything U8 needs beyond
them is an `extra-` fixture or a U8-API-n edit.

Spec: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design.md` ("screens"), approved and merged (origin/main
4a2389bd, #210). Rev 7 (`2026-10-11-sindri-ui-design.md`) still stands for the window and sheet mechanics (§3.2), the
shared states (§4.1), the park and escalation answers (§4.4), controls and action classes (§7), keys (§8), the API
conversation (§9), accessibility (§11), the token plan (§14) and the proof methods (§18.2).

Branch `feat/sindri-ui-u8`, worktree `.claude/worktrees/sindri-ui-u8` (the second slot to free, see "Worktrees and
branches"), from the lead's `feat/sindri-ui-r8-contract`, itself on `origin/feat/sindri-ui-u2`. Merge, never rebase,
when either moves. Draft PR base `feat/sindri-ui-r8-contract` until it merges into U2's line.

Read first: screens status block, §1 (R8-2, R8-3, R8-8, R8-10, R8-12, R8-13), §3, §4.2, §4.7, §4.8, §4.9, §5 (all), §6
(E-20, E-22, E-23, E-31), §8 (U8, U5', U13 rows), §8.1, §8.2, §9 (contract unit, units, hotspots, done); `fixture.md`
(root workflows, PRJ-sync's nine children, ITM-01JE7's sheet, the human queue, the chip table, the protected-action
label, the sentences); `fixes.md` waves 3–6; `impeccable-critique.md` runs 4–5 (the P1s U8 carries); mockups
`workflows.html`, `workflow-project.html`, `workflow-incident.html`, `item-sheet.html`, `prototype.html`. API §3.2
(engine commands), §3.5, §3.8 (`InstanceList`, `InstanceSummary`, `StateCell`, `InstanceView`, `StatusFields`,
`FactRow`, `ChildGroup`, `RecipeView`) and P11's row; WF §3.2 (`urgency`, `outputs`, `inputs[].kind`), §3.10 (path
order), §6.3 (routes: `…/nonce`, `…/commands`, `…/runs/{run}/complete`), §6.4 (the status layout); README X52, X62;
`rev8-goldens-for-ui.md`. U2's plan and code (`.claude/worktrees/sindri-ui-u2`): `ItemSheetViewModel*`, `ItemSheet.axaml`,
`ControlViewModel`/`CatalogViewModel`, `FakeSindriApi*`, `GoldenFiles`, `HealthViewModel.Refresh.cs`,
`scripts/sindri-ui-fixture.sh`.

Rules carried from U0–U3: TDD (red view-model or client test first; then green; then the view; then a headless view
test with key frames). **View models hold no logic:** path order, glyphs, loops, `next`, gates, budgets, timeouts,
commands, fact status and summaries, child grouping and counts are all the API's; the app maps fields to words,
visibility and controls, and never parses YAML or walks a graph (screens §5.1, API §3.8). Compiled bindings, no hex
outside `Theme/`, every control named "<label> <subject id>", timing on the injected `TimeProvider`, all dotnet through
`AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy dotnet`.

## Goal and observable signals (screens §8, U8 row)

**Goal.** The app answers "where is each piece of work in its recipe, and what did it produce?" (screens §2): the
Workflows page lists root instances grouped by recipe, each group's columns drawn from its recipe's states in path
order; any instance opens in **one frame** (subject line, STATES band with the expanded current state, FACTS, CHILDREN,
tabs) that renders every recipe from served data; a desk state's complete form sends its outputs and the instance
resumes; and the item sheet is that frame at 760 px. Fact viewers: `text`, `file`, `item`, `link`, `payload` (an unknown
type renders as `payload`); `scope-map`, `packet`, `evidence`, `decision` are U13's and render as `payload` meanwhile.

| # | Signal (screens §8) | VP journey |
|---|---|---|
| S1 | **Two dev recipes (a three-state triage and a desk round trip) render in the same frame**: the same view type and chrome, each band in its served path order, different states | J1 |
| S2 | **A desk complete with a file output resumes the instance**: the band's desk state turns `●`, the next state `◐`, the fact row `produced` with the file's name, size and hash | J2 |
| S3 | **The item sheet's frames match U2's golden rows**: U2's item-sheet assertions (header, version, timeline, Create, Close and its `SND-ITEM-409`) hold inside the frame, and U2's key-frame regions are unchanged | J3 |
| S4 | (critique, contract unit) one Attach component, open counts from one instance table, a popover on every lock button | J4 |

## Files

New (`src/Sindri/Aw.Sindri.Ui/`), **contract unit (T0, lead):**
- `Api/InstanceList.cs`, `InstanceSummaryView.cs`, `RecipeRefView.cs`, `EngineOwnedView.cs`, `InstanceSubjectView.cs`,
  `InputPresenceView.cs`, `StateCellView.cs` (+ `RoundView`, `LoopView`, `ProgressView`), `WorkflowInstanceView.cs`
  (the API lane's record name), `CurrentStateView.cs`, `StepDeclarationView.cs`, `GateView.cs`, `DeskView.cs`,
  `StatusFieldsView.cs` (+ `StatusHeaderView`, `AnswerView`, `ChildPointerView`, `DriftView`, `StatusInputView`,
  `TimeoutView`, `BudgetView`, `PausedView`, `CommandVerbView`), `FactRowView.cs`, `FactRefView.cs`, `ChildGroupView.cs`,
  `WorkstreamView.cs`, `ChildRowView.cs`, `InstanceCountsView.cs`, `RecipeView.cs` (+ `RecipeInputView`,
  `RecipeOutputView`), `FactView.cs`, `NonceView.cs`, `CompleteStepRequest.cs`, `SindriApiClient.Workflows.cs`.
- `ViewModels/Controls/StateChip.cs` (the one chip table of fixture.md; `For(InstanceSummaryView)`,
  `For(ChildRowView)`, `For(NeedsYouRowView)`), `AttachControlViewModel.cs`, `ProtectedActionViewModel.cs`.
- `Views/Controls/StateChip.axaml`, `AttachButton.axaml` (split: main Attach, chevron Take over · File a request ·
  Cancel; two sizes; the Attach note under it), `ProtectedActionButton.axaml` (lock, verb · terminal, popover).
- `Navigation/PageId.cs`, `DeepLinkKind.cs`, `DeepLink.cs` (seven pages; `workflow/<address>`, `recipe/<id>`,
  `node/<key>`, `session/<id>`, `system/<dependency>`), `ViewModels/Shell/NavEntry.cs` counts, `Views/Shell/ShellWindow.axaml`
  (seven tabs and "arrives with …" placeholders, screens §4.8).

New, **U8 lane:**
- `ViewModels/Workflows/WorkflowsViewModel.cs`, `WorkflowsViewModel.Keys.cs`, `RecipeGroupViewModel.cs`,
  `InstanceRowViewModel.cs`, `StageCellViewModel.cs`, `InputCellViewModel.cs`.
- `ViewModels/Instance/InstanceFrameViewModel.cs`, `.Keys.cs`, `.Commands.cs`, `SubjectLineViewModel.cs`,
  `SubjectAdapters.cs` (§5.4: `item`, `linear-project`, `webhook`, `file`, `text`, unknown → the key; words only),
  `StatesBandViewModel.cs`, `StateNodeViewModel.cs`, `ExpandedStateViewModel.cs`, `ChildrenPanelViewModel.cs`,
  `ChildRowViewModel.cs`, `DeskCompleteViewModel.cs`, `FrameTabsViewModel.cs`, `CancelConfirmViewModel.cs`.
- `ViewModels/Facts/FactViewerRegistry.cs` (U8 creates, U13 extends; screens §9 hotspot), `IFactViewer.cs`,
  `FactRowViewModel.cs`, `TextFactViewer.cs`, `FileFactViewer.cs`, `ItemFactViewer.cs`, `LinkFactViewer.cs`,
  `PayloadFactViewer.cs` (a scrubbed JSON tree from `GET …/facts/{key}`).
- `ViewModels/Labels.Workflows.cs`, `Labels.Instance.cs` (headings, legends, key hints, the confirm line, empty lines;
  never a served sentence).
- `Platform/IFilePicker.cs` (port for the desk complete's file output); Desktop `AvaloniaFilePicker.cs`.
- `Views/Workflows/WorkflowsPage.axaml(.cs)`, `RecipeGroup.axaml(.cs)`, `InstanceRow.axaml(.cs)`, `WorkflowsStyles.axaml`.
- `Views/Instance/InstanceFrame.axaml(.cs)` (the 760 px sheet), `StatesBand.axaml(.cs)`, `ExpandedState.axaml(.cs)`,
  `FactsPanel.axaml(.cs)`, `ChildrenPanel.axaml(.cs)`, `DeskCompleteForm.axaml(.cs)`, `FrameTabs.axaml(.cs)`,
  `InstanceStyles.axaml`; `Views/Facts/FactTemplates.axaml` (one DataTemplate per viewer).

Changed:
- `ViewModels/Item/ItemSheetViewModel*.cs` → the subject-line part of the frame (see "Item sheet re-base");
  `Views/Item/ItemSheet.axaml` → hosts `InstanceFrame`.
- `ViewModels/Shell/ShellViewModel*.cs` (`G W`, `workflow/<address>`, the sheet subject `Instance`, the 760 px width for
  instance sheets), `HealthViewModel.Refresh.cs` (register the Workflows list and the open frame for re-reads).
- `Api/ISindriApi.cs`, `Api/SindriUiJsonContext.cs` (contract unit, then U8 appends at its anchor).
- `Views/SindriApp.axaml` (style includes).
- `tests/Aw.Sindri.Ui.Testing/`: `FakeSindriApi.Workflows.cs` (contract unit), `FakeSindriApi.Routes.cs`,
  `FakeSindriHandler.cs`, `SindriApiContract.cs`, `FixtureInstances.cs` (**the one instance table**, contract unit),
  `A.cs` (`A.InstanceSummary`, `A.InstanceView`, `A.FactRow`, `A.ChildRow`), `FakeFilePicker.cs`, `FilePickerContract.cs`.
- `tests/Aw.Sindri.Ui.Tests/`: `Api/GoldenContractTests.cs`, `Workflows/*`, `Instance/*`, `Facts/*`, `Controls/*`,
  `Item/ItemSheetViewModelTests.cs` (kept green), `Views/WorkflowsFramesTests.cs`, `Views/InstanceFramesTests.cs`,
  `Views/ItemSheetFramesTests.cs`, `Desktop/AvaloniaFilePickerContractTests.cs`.
- `scripts/sindri-ui-fixture.sh` (+ `workflow start <recipe> [--input k=v]`, `workflow complete <address> <fact> <file>`
  under the U8 anchor) and its test.
- `docs/csharp/sindri-ui.md` (U8 section), `PublicAPI.Unshipped.txt` (both projects). `deps.yaml`: none expected.

## Ordered TDD tasks (test names first; one commit red, one green per slice)

**T0 — Contract unit (lead; runs alone before wave 1; screens §9).** Lands on `feat/sindri-ui-r8-contract`; both U3'
and U8 merge it. It carries the critique items of #210 ("U8's contract unit, where the fixture becomes golden JSON").
- *Goldens.* `GoldenContractTests.Every_rev8_golden_reads_through_the_apps_json_context_or_is_a_refusal` (places
  `workflow-instances`, `workflow-instance`, `workflow-status`, `workflow-recipe`);
  `Workflow_status_goldens_read_as_StatusFields_and_equal_the_views_current_status` (each `workflow-status/*` against the
  matching `workflow-instance/*` where both exist); `Not_found_reads_as_ProblemDetails_SND_ITEM_404`;
  `Nullable_members_are_present_and_null_never_missing` (the API lane's rule: `?` fields always present).
- *Client.* `SindriApiClientTests.Instances_sends_root_recipe_and_subject_as_query_parameters`;
  `Instance_url_encodes_the_address_as_one_segment` (`workflow%2FPRJ-sync`); `Instance_maps_SND_ITEM_404_with_its_detail`;
  `InstanceFact_reads_a_truncated_body_and_says_so`; `Recipe_reads_inputs_kind_and_outputs_from`;
  `IssueNonce_then_CompleteStep_sends_outputs_base64_once_and_never_retries`; `Routes_are_under_sindri`
  (`/sindri/workflow/…`, the API lane's "may still change" note);
  `SindriApiClientHeaderTests.Workflow_calls_send_human_ui_and_never_terminal_or_origin`.
- *One instance table (critique: "open counts … derived from one source").* `FixtureInstancesTests.Every_spec_shaped_instance_golden_is_generated_from_the_one_table`;
  `Nav_count_strip_head_and_workflows_head_equal_the_open_rows`; `Children_counts_equal_the_child_rows`
  (4 done · 1 running · 1 parked · 3 queued = 9); `ITM_01JD6_has_one_recipe_everywhere` (the critique's JD6 drift).
- *One chip table.* `StateChipTests.Every_fixture_word_maps_to_its_class` (PARKED/ESCALATED/DECISION/PROFILE/CHANGED/
  ORPHANED/DEGRADED attn; FAILED fail; RUNNING/OK ok; WITH YOU, QUEUED, DONE, DRAFT, APPROVED, DETERMINISTIC, MODEL,
  CHILD, POLLING neutral; ABSENT subtle); `Stage_names_are_never_chips`; `Served_chip_wins_when_present` (U8-API-1).
- *One Attach component (critique run 5 P1 "Attach has four shapes").* `AttachControlTests.Main_is_Attach_and_the_chevron_holds_Take_over_File_a_request_Cancel`;
  `Two_sizes_share_one_template`; `The_attach_note_reads_the_fixture_sentence_under_every_Attach_primary`
  ("Attach joins the live session; your first keystroke takes it over."); `Attach_is_pending_P10a_until_the_catalog_says_ready`
  (dashed "Available with API P10a" chip, never a live-looking button); `Cancel_in_the_chevron_asks_first`;
  `Tool_and_model_steps_get_Open_instance_not_Attach`.
- *A popover on every lock button (critique run 5 P1).* `ProtectedActionTests.Label_is_lock_verb_dot_terminal`;
  `Title_is_Needs_a_typed_confirmation_at_a_terminal`; `Click_opens_a_popover_with_the_served_cli_Copy_and_Open_in_an_agent`;
  `Copy_puts_the_served_cli_on_the_clipboard_and_says_Copied_for_1_5_s`; `Esc_closes_the_popover_and_returns_focus`;
  `It_never_sends_a_command`; `NamedControlsTests.Every_lock_button_has_a_popover` (a tree walk over every page's frames).
- *Shell.* `ShellViewModelTests.Nav_is_Home_Workflows_Work_Recipes_Context_Agents_System`;
  `Unbuilt_pages_show_arrives_with_their_phase` (screens §4.8); `Chords_follow_screens_section_3`
  (`G H/W/Q/R/C/A/.`, `G N`, `G D`, `G S`); `Deep_links_parse_workflow_recipe_node_session_system`.
- *Fake.* `FakeSindriApiContractTests` runs `SindriApiContract`'s new facts against the fake and the client over the
  canned handler.

**T1 — Workflows list view model (screens §4.2 list).**
- `WorkflowsViewModelTests.Groups_follow_the_served_order_grouped_by_recipe_id` (incident and triage groups sort above
  the Linear group while one runs, because the API orders by `urgency`; the app only groups);
  `Each_groups_columns_are_its_rows_states_in_order` (triage `received classify route done`, project seven columns);
  `Head_reads_N_open_N_need_you_N_with_you_from_served_counts` (U8-API-2; fallback from the one table);
  `Recipe_and_Subject_filters_send_recipe_and_subject` (`workflow-instances/recipe-filter`);
  `Collapsed_groups_show_count_and_show_all`; `Empty_shows_the_served_sentence` (`workflow-instances/empty`);
  `Stalled_and_rebuilding_keep_rows_marked_stale`; `Not_served_yet_before_P11`.
- `InstanceRowViewModelTests.Subject_cell_uses_the_kind_adapter` (Linear `ENG · Tab manager v2`, webhook `sentry #8812`);
  `Inputs_show_check_or_cross_per_declared_input`; `A_missing_required_input_blanks_its_stage_as_blocked_dash`;
  `Stage_cell_shows_glyph_then_round_desk_child_or_progress`; `Engine_badge_replaces_the_glyph`
  (`▲ escalated: gate.severity 0.91`); `Now_is_the_served_sentence_cut_to_the_cell`; `Chip_comes_from_StateChip`.
- `WorkflowsKeysTests.Enter_opens_the_instance_frame`; `A_answers_inline_for_desk_and_shows_the_lock_popover_for_protected`
  (U8-API-3; until then `A` opens the frame focused on the answer); `B_hands_back_a_taken_over_row`;
  `X_cancels_after_the_one_line_confirm`; `N_opens_Start` (pending U5'; the button reads "Available with U5'" until
  then); `J_K_move_across_groups`; `Selection_is_kept_by_address_across_refreshes`.

**T2 — The frame: header, subject line, STATES band, expanded state (screens §4.2 frame, §5.1, §5.4).**
- `InstanceFrameViewModelTests.Header_reads_address_title_recipe_at_hash_version_state_since`
  (`workflow/PRJ-sync Offline sync workflow.project@a8c21e7f · v12 · build · since 09:14 (3h)`);
  `Subject_line_comes_from_the_adapter_with_Open_in_links` (`Open in Linear ↗`, `Open in Context ▸` pending U9);
  `Opens_at_760_px` (R8-13); `Initial_focus_is_a_read_action_never_Take_over_or_Cancel` (R8-13, fixes wave 2);
  `Not_found_shows_the_served_detail_with_Back`.
- `StatesBandViewModelTests.Nodes_are_the_served_states_in_order`; `Loop_pairs_draw_the_loop_mark_between_them`
  (`mitigate ◐ r1/3 ⇄ verify`); `Branches_fold_under_their_source_with_the_branch_mark` (`branchOf`);
  `Terminal_states_come_last_as_served`; `Ordered_by_transition_list_says_so_in_the_header`
  (`workflow-instance/transition-list`); `Badge_replaces_the_glyph_on_the_interrupted_state`
  (`workflow-instance/escalated-gate`); `Outcome_shows_on_its_state` (`triage ● sev2`); `Wraps_without_detaching_links`
  (critique: a recipe with more than six states).
- `ExpandedStateViewModelTests.Shows_step_kind_ref_and_declaration`; `Gates_list_id_on_and_threshold`;
  `Next_is_the_served_sentence_whole`; `Budget_reads_state_and_instance_from_StatusFields`;
  `Timeout_reads_parks_or_escalates_in_from_StatusFields`; `Paused_shows_the_timeout_left_on_a_taken_over_instance`;
  `Commands_render_as_controls_by_class_in_the_served_order` (desk live, protected → `ProtectedActionButton`,
  pending from the catalog); `Answer_renders_desk_or_the_lock_popover_from_answer_class`
  (`workflow-status/escalated-gate`, `parked-budget`, `needs-answer-wait`); `Child_pointer_links_the_mirrored_child`
  (`workflow-status/parked-child`); `Drift_says_needs_reapproval` (`parked-drift`); `Done_shows_no_commands`
  (`workflow-status/done`); `Status_text_is_kept_for_Copy_as_text` (the CLI's form, equal by construction).
- `CancelConfirmTests.Cancel_asks_one_line_first_with_Keep_focused` (`Cancel INC-14? The mitigation agent is stopped and
  the comms child closes. [Cancel instance] [Keep]`, the served consequence if U8-API asks land, else the fixture line
  as app copy); `Cancel_sends_ApplyCommand_cancel_once_with_a_fresh_nonce`.

**T3 — Facts panel and the viewer registry (screens §5.2; U8's fourth unit, §9).**
- `FactViewerRegistryTests.Text_file_item_link_payload_have_viewers`; `Unknown_type_renders_as_payload`;
  `Scope_map_packet_evidence_decision_render_as_payload_until_U13`; `U13_can_register_a_viewer_without_editing_U8_files`.
- `FactsPanelViewModelTests.Rows_keep_declaration_order`; `Status_maps_to_produced_in_progress_pending_glyphs`;
  `By_human_reads_you` (the API lane's rule: `human:*` → "you"); `At_reads_the_local_time`;
  `Progress_reads_n_of_m` (`fact:evidence ◐ 4 of 9 packs`); `From_places_the_band_mark_on_its_state`.
- Per viewer: `TextFactViewerTests.Shows_one_line_and_Copy`; `FileFactViewerTests.Shows_name_size_hash_and_Open`;
  `ItemFactViewerTests.Shows_id_title_state_and_Open_opens_the_item_frame`; `LinkFactViewerTests.Shows_host_and_title_and_Open_external`;
  `PayloadFactViewerTests.View_reads_GET_facts_key_and_shows_the_first_three_keys_then_a_tree`;
  `Payload_truncated_says_so`.

**T4 — Children panel (screens §4.2, §5.5).**
- `ChildrenPanelViewModelTests.Groups_follow_the_served_order_under_their_state_name`;
  `Workstreams_subgroup_in_the_packets_order` (`ws-1 UI`, `ws-3 Mobile`); `Rows_without_workstreams_use_rows`;
  `Row_shows_chip_title_state_round_agent_age_and_its_served_actions` (agent and round null before engine P7/P8: blank,
  not "—" words invented); `Mirrored_park_shows_here_and_in_the_band`; `More_shows_N_more_and_Open_all_in_Work`
  (U8-API-7); `Row_Enter_opens_the_child_frame_stacked_with_Back`; `Empty_reads_the_recipe_declares_no_children`.

**T5 — Desk complete (screens §5.3, R8-10; the S2 signal).**
- `DeskCompleteViewModelTests.Form_lists_each_pending_output_with_its_input_control_from_type` (file → picker, text →
  field, link → URL field); `Shows_the_checks_that_run_on_complete_by_name_and_description`;
  `Complete_is_disabled_until_every_required_output_is_set`; `File_over_1_MiB_is_refused_before_sending`
  (WF §6.3's cap; the API still decides); `Complete_issues_a_nonce_then_posts_outputs_once`;
  `Accepted_shows_done_then_the_frame_rereads_with_X_Sindri_After`; `Refusal_shows_the_served_title_and_detail_inline`;
  `Protected_complete_shows_the_lock_popover`; `Cleared_while_open_keeps_the_picked_file_name_and_disables_the_form`.
- `FilePickerContract`: `Returns_the_chosen_path_or_null`, `Never_reads_the_file_itself` (the VM reads it through the
  port's stream); against `FakeFilePicker` and, macOS integration lane, `AvaloniaFilePicker`.

**T6 — Item sheet re-base (screens §4.7, R8-8).** See "Item sheet re-base"; test names there.

**T7 — Tabs (screens §4.2).**
- `FrameTabsViewModelTests.Counts_come_from_counts_timeline_decisions_requests_context`;
  `Timeline_tab_reads_the_items_timeline_for_an_item_instance_and_the_instance_timeline_otherwise` (U8-API-5);
  `Decisions_and_Requests_tabs_host_U3s_list_view_models` (U3' D3/D4: `DecisionListViewModel`, `RequestListViewModel`,
  `ItemDecisionsViewModel`); `Context_tab_reads_arrives_with_U9`; `Null_context_count_hides_the_number`.

**T8 — Shell and re-reads.**
- `ShellViewModelTests.G_W_opens_Workflows`; `Deep_link_workflow_address_opens_the_frame`;
  `Workflows_nav_count_is_the_served_open_count`; `Instance_sheets_open_at_760_and_keep_the_users_width_for_other_sheets`;
  `Behind_a_sheet_the_list_reflows_to_id_title_state` (fixture.md "Behind a sheet").
- `RequeryTests.Workflow_events_reread_the_list_and_the_open_frame`; `Refresh_never_moves_focus_scroll_or_input`;
  `A_frame_whose_instance_ends_keeps_its_content_and_says_done`.

**T9 — Views and key frames (integration lane; full sets heavy).** `WorkflowsFramesTests`: `u8-workflows-root`,
`u8-workflows-recipe-filter`, `u8-workflows-empty`, `u8-workflows-stalled`, `u8-workflows-not-served`.
`InstanceFramesTests`: `u8-frame-project-build`, `u8-frame-incident-loop`, `u8-frame-triage-desk`,
`u8-frame-escalated-gate`, `u8-frame-desk-round-trip`, `u8-frame-triage-three-state`, `u8-frame-transition-list`,
`u8-frame-done`, `u8-frame-not-found`, `u8-frame-cancel-confirm`, `u8-frame-lock-popover`, `u8-frame-desk-complete-form`.
`ItemSheetFramesTests`: `u8-item-sheet-pre-engine` (U2's rows), `u8-item-sheet-instance`. `NamedControlsTests` and the
§11.2 order test (band nodes in order, then the expanded state's controls, facts, children, tabs).

**T10 — Head adapter.** `AvaloniaFilePicker`; `Composition` passes it; the AOT smoke still publishes and starts
(`StartupTests.Composition_builds_with_a_file_picker`).

**T11 — Fixture script (prover; test first).** `workflow_start_starts_a_dev_recipe_instance_through_the_cli`;
`workflow_complete_completes_a_desk_state_with_a_file_through_the_cli` (the cross-check path, never the app's);
`every_workflow_verb_refuses_an_unguarded_home`.

**T12 — Docs and gate.** `docs/csharp/sindri-ui.md` U8 section (views, routes, codes, proof recipe); `PublicAPI`; the
merge gate pasted in the PR.

Units (screens §9): T0 (lead) → **U8-a** view models T1–T5 against the fake → **U8-b** views T9 → **U8-c** proof (T11 and
the VP) → **U8-d** the fact-viewer registry (T3's registry part lands first in U8-a so U13 can start). T6 and T7 go to
U8-a after T2. T10 with U8-b.

## Fixtures

**Pinned by the API lane** (`rev8-goldens-for-ui.md`; `tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`,
linked as `Golden/Api`; field names fixed by API §3.8, record names may change; routes under `/sindri`). Until a branch
U8 can merge carries them, the contract unit writes spec-shaped copies under the **same names**, generated from
`FixtureInstances`, and deletes each when the real file lands.

| Golden | Record | Used for |
|---|---|---|
| `workflow-instances/root`, `recipe-filter`, `empty` | `InstanceList` (E-20) | the list, the filter, the empty sentence (and U3''s strip) |
| `workflow-instance/project-build` | `WorkflowInstanceView` (E-20, E-23) | PRJ-sync: children 4/9 by workstream, facts, the build state expanded |
| `workflow-instance/incident-loop` | same | INC-14: `mitigate ⇄ verify`, the comms child, Attach + Take over + Cancel |
| `workflow-instance/triage-desk` | same | sentry #8812 at `classify · desk`: the desk answer |
| `workflow-instance/escalated-gate` | same | the escalation badge on the band and the protected Accept |
| `workflow-instance/desk-round-trip` | same | the dev desk recipe with an open desk state and a pending file output (S2's before) |
| `workflow-instance/triage-three-state` | same | the dev triage recipe (S1) |
| `workflow-instance/transition-list` | same | `orderedBy: transition-list` |
| `workflow-instance/done` | same | a terminal instance: no commands |
| `workflow-instance/not-found` | ProblemDetails | `SND-ITEM-404` |
| `workflow-status/parked-child`, `desk-task`, `parked-budget`, `escalated-gate`, `taken-over`, `done`, `parked-drift`, `needs-answer-wait` | `StatusFields` (E-23) | one per WF §6.4 layout: the expanded state's answer, child pointer, drift, budget, timeout, paused, commands |
| `workflow-recipe/workflow-project`, `incident-response`, `no-outputs` | `RecipeView` (E-22) | `inputs[].kind` (subject adapters), `outputs[].from`, `states[]`, `loops[]`; `no-outputs` → `[]` |

**Extras U8 still needs** (SpecShaped only, `extra-` prefix, from `FixtureInstances`; each also listed as an API ask):

| Extra | Why | Ask |
|---|---|---|
| `workflow-instance/extra-desk-round-trip-completed` | S2's after: the desk state `done`, the next `active`, the file fact `produced` | ask P11 to record it |
| `workflow-instance/extra-item-produce` | ITM-01JE7 as a child instance for the item sheet (§4.7: produce r2/3, trace, the subject line's parent) | U8-API-6 |
| `workflow-instance/extra-taken-over` | the full view for a taken-over instance (the status golden exists alone) | ask P11 |
| `workflow-instance/extra-stalled` | the shared stalled banner on the view | ask P11 |
| `workflow-instance-fact/extra-payload`, `extra-text`, `extra-truncated` | `GET …/facts/{key}` bodies (`FactView`), which the pinned set doesn't cover | U8-API-8 |
| `workflow-instance-nonce/extra-issued`, `workflow-instance-complete/extra-accepted`, `extra-refused-checks`, `extra-stale-nonce` | the desk complete round trip (WF §6.3 routes) | U8-API-4 |
| `commands/extra-catalog-p11` | `ApplyCommand:*`, `CompleteStep`, `StartInstance` rows | until P11 re-records `commands/catalog` |
| `workflow-instances/extra-counts` | `counts` on the list | U8-API-2 |

U2's goldens stay: `items-id/*`, `items-id-timeline/*`, `items-id-close/*`, `items/*` (the pre-engine item sheet).

## Item sheet re-base (screens §4.7, R8-8)

An item is a child instance, so its sheet becomes the frame at 760 px. U2's `ItemSheetViewModel` keeps its public
surface (ItemId, Title, Repo, StateWord, VersionText, Age, Body, Link, CreatedBy, WaitingOn, Instance, Close with the
version, Create) and becomes the **subject line and item properties** inside `InstanceFrameViewModel`:
1. The sheet reads `GET /items/{id}` (U2, unchanged). When `ItemView.instance` names an address, it also reads
   `GET /workflow/instances/{address}` and renders STATES, FACTS, CHILDREN from it (the live run is the current state
   expanded; the trace is that state's activities).
2. **Pre-engine form** (no instance, before WF P4, or an item never started): the frame chrome with U2's subject line,
   the band area showing the item's state word only, FACTS and CHILDREN as "arrives with API P11" (§4.8), the launch
   block when U6a has landed (§4.7), and the Timeline tab with U2's timeline.
3. Rev 7's status card and "Properties" become the subject line and the facts; Close's `SND-ITEM-409` renders in the
   subject line (§4.7); Create item (U2) is unchanged.
4. Timeline, Decisions, Requests tabs host U2's timeline and U3''s `ItemDecisionsViewModel` (no edit to U3' files).
5. Width: 760 px (R8-13), replacing U2's 520 default for this sheet only.

Tests (S3): every existing `ItemSheetViewModelTests` fact passes unchanged (they bind the same view model);
`ItemSheetFramesTests.Pre_engine_frame_keeps_U2s_rows` (the U2 key frames' header, version, timeline and Close regions
compared region by region against U2's `u2-item-*` frames, re-captured once at 760 and committed as the new baseline);
`Item_with_an_instance_renders_the_frame_from_its_address` (`extra-item-produce`);
`Close_409_shows_the_served_detail_in_the_subject_line`; `Subject_line_names_the_parent_task_and_workstream` (U8-API-6);
`Opening_the_item_from_a_child_row_and_from_Work_lands_on_the_same_frame`.

## API edits still needed (U8-API-n)

| # | Ask | Section | Until answered |
|---|---|---|---|
| **U8-API-1** | `chip: {word, tone}` on `InstanceSummary`, `ChildRow`, `NeedsYouRow`, from fixture.md's one table | screens R8-13, fixture.md "State chips"; API §3.6, §3.8 | `StateChip.For(…)` in the contract unit |
| **U8-API-2** | `GET /workflow/instances?open=true` and `InstanceList.counts {open, needsYou, withYou, running, done}` | screens §3 (tab = open roots), §4.2 head, fixture.md "every count … derived from the rows"; API §3.8 ("the first five rows with `state` not terminal"); critique run 5 P1 | counts from `FixtureInstances` in tests; the app counts non-terminal rows (one function) |
| **U8-API-3** | `answer?` (the `StatusFields.answer` subset: kind, actionId, class, cli) on `InstanceSummary` | screens §4.1 `[Answer]`, §4.2 keys `A`/`B`/`X`; API §3.8 | `A` opens the frame focused on the answer (one extra call) |
| **U8-API-4** | The addressed engine command routes beside the id forms: `POST /workflow/instances/{address}/nonce`, `…/commands`, `…/runs/{run}/complete`, and `current.desk` carries `run` (or a `completeRoute`) | WF §6.3 (id-only routes, `run` in the path); API §3.8 ("id forms stay valid beside the addressed forms" covers GET only) | the client resolves the id from the view if it carries one; else T5 waits |
| **U8-API-5** | An instance timeline and instance-scoped decisions and requests: `GET /workflow/instances/{address}/timeline?after=`, `GET /decisions?instance=`, `GET /requests?instance=` | screens §4.2 tabs ("the ledger events for the address and its children"); API §3.1 (`?item=` only) | tabs read the item's lists for item instances; other instances show the count only |
| **U8-API-6** | `parent? {address, title, task?, workstream?}` on `WorkflowInstanceView` | screens §4.7 subject line ("packet task t3 of PRJ-sync (ws-1 UI)") | subject line without the parent |
| **U8-API-7** | `GET /items?parent=<address>` for "Open all in Work" | screens §4.2 children ("Open all in Work ▸"); API §3.1 (`?state=&repo=` only), §3.8 ("follows the items filter") | the link opens Work unfiltered |
| **U8-API-8** | Record `GET …/facts/{key}` goldens (`FactView`: payload, text, truncated) | API §3.8 `GetInstanceFact` | `extra-` fixtures |
| **U8-API-9** | The Cancel confirm's consequence sentence served (`commands[].confirm`) | fixes wave 2 ("Cancel INC-14? The mitigation agent is stopped and the comms child closes.") | app copy per recipe is impossible without logic; the app shows "Cancel <address>?" only |

Engine-lane asks: WF P2b (E-21, E-22) merged before the real-app proof; the two dev recipes (`recipes/dev/triage-three-state.yaml`,
`desk-round-trip.yaml`, screens §8.2) with a `file` output on the desk state.

## Proven in the real app before P11, and what waits

Before P11 (real app under `sindri-prove.sh` with P3/P4's serve; headless lane against the pinned copies and extras):
- The seven tabs and their "arrives with …" placeholders; `G W` lands on Workflows' not-served panel; deep link
  `workflow/<address>` opens a not-served frame naming the address.
- **The item sheet re-base in its pre-engine form on real items** (S3's U2 rows: header, version, timeline, Create,
  Close and a forced `SND-ITEM-409`), at 760 px, with OS shots.
- The contract unit's components where they already appear: the lock popover on the profile row (P4), the Attach
  component pending P10a, chips on Home.
- Everything else headless: every T1–T9 test and key frame, including S1's two dev-recipe frames and S2's form against
  the extras.

Waits for API P11 + WF P4 (+ WF P2b, the dev recipes): S1 and S2 in the real app, the Workflows list on real data, an
item sheet with a live instance, `A`/`X` on real rows. Waits for U8-API-4: S2's real complete. Waits for P10a/P10b: a live
Attach. U13: the four richer viewers.

## Worktrees and branches (rule: at most three UI worktrees)

Now: `sindri-ui-u0` (#184), `sindri-ui-u1` (#193), `sindri-ui-u2` (#197) hold the three slots.
1. **#184 merges → the u0 slot frees.** The lead uses it for the **rev-8 contract unit** (`feat/sindri-ui-r8-contract`
   from `feat/sindri-ui-u2`): T0 above plus the strip's records. It is small and lands first. The same worktree then
   becomes **U3'** (`feat/sindri-ui-u3`, from the contract branch).
2. **#193 merges → the u1 slot frees.** **U8** (`feat/sindri-ui-u8`, from the contract branch) takes it.

**Recommendation: two parallel branches, both on the contract branch, not one stacked branch.** §9 makes U3' and U8
parallel lanes with different gates (U3' waits on API P4, U8 on P11 + WF P4); stacking U8 on U3' would hold U8's review
behind P4-gated work and make one oversized PR, while the overlap between them is only the shared files below, which
the contract unit settles once. If only one slot frees for a while, run U3' first (its API phase is closer) and start
U8's headless work the day the second frees; U8 needs nothing from U3' but the contract unit.

**Hotspots the lead owns in the contract unit** (lanes append only at anchors afterwards):
- `Views/Shell/ShellWindow.axaml` and `ViewModels/Shell/ShellViewModel*.cs` (navigation: the seven tabs, chords, deep
  links, sheet subjects and widths), `Navigation/PageId.cs`, `DeepLink*.cs`, `NavEntry` counts.
- `Api/ISindriApi.cs` (every E-20/E-22/E-23 method declared, in route order), `Api/SindriUiJsonContext.cs`.
- The fake API: `FakeSindriApi.Workflows.cs`, `FakeSindriApi.Routes.cs`, `FakeSindriHandler.cs`, `SindriApiContract.cs`,
  `FixtureInstances.cs`, `A.cs`, `GoldenFiles`/`GoldenContractTests` slug placement.
- The shared components: `StateChip`, `AttachButton`, `ProtectedActionButton`, `Labels.*` chip words.
- `Views/SindriApp.axaml` (style includes), `Theme/Tokens.axaml` (chip pairs), `scripts/sindri-ui-fixture.sh`
  (anchors), `docs/csharp/sindri-ui.md` (anchors), `PublicAPI.Unshipped.txt`.
- One more, lane-owned by agreement: the item sheet. U8 owns `ItemSheetViewModel*`; U3' writes `ItemDecisionsViewModel`
  and never edits the sheet (U3' D4).

## Owner questions (#210's body, for the orchestrator)

1. **Home draws six attention chips and counts five.** The strip's sentry #8812 row has a `DECISION` chip (a triage
   desk answer) that isn't in Home's five needs-you rows (fixture.md "Open point"). *Recommendation: count it, Home
   says 6.* API P11 already serves desk tasks as needs-you rows of kind `park`, so 6 is what `/needs-you` will return;
   showing 5 would make the app filter served rows. One definition stays true (needs you = every attention chip on the
   page). Corollary for the owner: a desk step the person has already claimed (PRJ-exports, exported and awaiting
   import) stays `WITH YOU` and uncounted, which the API should serve (U8-API-1) rather than the app infer.
2. **"Workflows 12 open" includes four done rows.** *Recommendation: "open" means not terminal, so the tab, the strip
   head and the list head read 8 in the fixture.* Done instances leave the count and the default list; each group keeps
   a collapsed "N done today ▸" line for recent ones. Served by U8-API-2 (`open=true`, `counts.done`).
3. **Could a protected confirmation be typed into ⌘K inside the app instead of a terminal?** *Recommendation: no, not
   in wave 1.* It changes the protected class in three specs (API §4.1 "a typed confirmation … at a terminal", the
   engine's challenge, CTX promote) and the terminal flag that separates the approval tier from desk. The lock
   button's popover (the served command, Copy, Open in an agent) is the in-app path this wave builds; revisit after
   U5' if the copy step measures as the friction, as its own spec decision.

## Contradictions found while planning

- Screens §8 U8's signal includes "a desk complete … resumes the instance", while the U5' row owns "Complete". U8 builds
  the desk complete form (T5) because its signal needs it; U5' takes answers, Start, Take over, Hand back.
- Screens §4.1's strip drawing ("7 open", "… 2 more") vs fixture.md ("12 open", "… 7 more"): fixture wins (§4.9).
- API §3.8's `RecipeStats` example `humanRate: 0.19` vs fixture.md's corrected 30% (U10's; noted for the API lane).
- The pinned `counts.context?` may be null, while §4.2 draws `Context 14`: the tab hides the number when null.

## Risks

| Risk | Mitigation |
|---|---|
| Record names change (the API lane says they may) | Field names are fixed; the UI's records are its own names; a rename touches the JSON context only |
| `workflow-recipe/*` become the full `ShowRecipe` body after engine P2b | U8 reads only `inputs`, `outputs`, `states`, `loops`, `urgency`; a test pins that subset |
| Children's `agent`, `round`, `workstreams` null until engine P7/P8 | Rows render blanks; the workstream grouping is tested on the extras |
| Two lanes edit the shell, the fake and the item sheet | The contract unit lands them first; anchors only after; the item sheet is U8's alone |
| A file output through NativeAOT's file picker | `IFilePicker` port with a contract; the AOT smoke opens it in the integration lane on macOS |
| Desk complete routes are id-only (WF §6.3) | U8-API-4; T5's real proof waits; headless proof on extras |

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

## API rev-8 contract unit GREEN (API lane f9, 2026-10-14)
- Branch feat/sindri-api-rev8-contract @ 249aec3a (off main 4a2389bd); PR + review next. Records in src/Sindri/Aw.Sindri.Core/Workflow/Views/ (§3.8/§8 names; instance view record = InstanceDetail, wire names unchanged; chip {word,tone}; Cancel commands[].confirm = approved sentence).
- 30 goldens under tests/Aw.Sindri.Tests/Golden/Api/: workflow-instances/{root,recipe-filter,open,empty}; workflow-instance/{dev-desk-round-trip,dev-triage-three-state,done,escalated-gate,incident-loop,not-found,project-build,transition-list,triage-desk}; workflow-status/{desk-task,done,escalated-gate,needs-answer-wait,parked-budget,parked-child,parked-drift,taken-over}; workflow-recipe/{incident-response,no-outputs,workflow-project}; workflow-instance-fact/{scope-map,packet,evidence,text,payload,truncated}.
- Caveats: recipe hashes placeholders until engine P2b; not-found hand-written until SND-ITEM-404; dev-incident-hold golden arrives with P2b.
- The UI contract unit (feat/sindri-ui-r8-contract) builds its fakes on these: merge the API branch (or main once it lands) into the UI contract branch; rename provisional fixture names to these.
