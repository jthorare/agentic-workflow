using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Linear "New issue" dialog (Linear hub spec §3): the teams and lookups it reads, the title rule, the body it sends, the draft it keeps and every answer to a create.</summary>
[Lane(TestLane.Unit)]
public sealed class NewLinearIssueTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task With_one_team_that_team_is_picked_and_its_lookups_are_read() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);

        Assert.Equal((true, "ENG", "ENG"), (vm.IsOpen, vm.Team?.Value?.GetValue<string>(), string.Join(',', f.Server.LinearLookupsRead)));
    }, Token);

    [Fact]
    public Task With_two_teams_none_is_picked_and_nothing_is_read_until_one_is() => NewLinearIssueFixture.Run(async f =>
    {
        await f.Vm.Open(Token);

        Assert.Equal((2, true, 0), (f.Vm.TeamChoices.Count, f.Vm.Team is null, f.Server.LinearLookupsRead.Count));
    }, Token);

    [Fact]
    public Task A_teams_lookups_are_read_once_however_often_it_is_picked() => NewLinearIssueFixture.Run(async f =>
    {
        await f.Vm.Open(Token);

        await f.PickTeam("ENG");
        await f.PickTeam("OPS");
        await f.PickTeam("ENG");

        Assert.Equal(["ENG", "OPS"], f.Server.LinearLookupsRead, StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task The_pickers_offer_the_teams_lookups_with_their_clear_entries_first_and_no_inactive_members() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);

        Assert.Equal(
            "Team default,Backlog,Todo,In Progress,In Review,Done|Unassigned,Joi Example,Sam Lee|No priority,Urgent,High,Medium,Low|bug,api|No cycle,Cycle 14,Cycle 15",
            string.Join('|', new[] { vm.States, vm.Assignees, vm.Priorities, vm.LabelChoices, vm.Cycles }.Select(l => string.Join(',', NewLinearIssueFixture.Names(l)))));
    }, Token);

    [Fact]
    public Task A_blank_title_says_so_under_the_field_speaks_it_asks_for_focus_and_sends_nothing() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        vm.Title = "   ";

        await vm.Submit(Token);

        Assert.Equal(("Title is required", true, "Title is required", 1, 0), (vm.TitleError, vm.IsAnnouncing, vm.Announcement, vm.TitleFocusRequests, f.Server.LinearCreates.Count));
    }, Token);

    [Fact]
    public Task Typing_a_title_clears_the_blank_title_line() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        await vm.Submit(Token);

        vm.Title = "Document the retry budget";

        Assert.Equal((string.Empty, false), (vm.TitleError, vm.IsAnnouncing));
    }, Token);

    [Fact]
    public Task A_submit_with_no_team_says_so_and_asks_for_focus_on_the_team() => NewLinearIssueFixture.Run(async f =>
    {
        await f.Vm.Open(Token);
        f.Vm.Title = "Document the retry budget";

        await f.Vm.Submit(Token);

        Assert.Equal(("Team is required", 1, 0), (f.Vm.TeamError, f.Vm.TeamFocusRequests, f.Server.LinearCreates.Count));
    }, Token);

    [Fact]
    public Task A_team_that_has_not_synced_leaves_the_pickers_empty_says_so_and_still_creates() => NewLinearIssueFixture.Run(async f =>
    {
        f.Server.LinearLookupsAnswer = RouteAnswer.Api("linear-lookups", "not-synced");
        await f.Vm.Open(Token);
        await f.PickTeam("OPS");
        f.Vm.Title = "Document the retry budget";

        await f.Vm.Submit(Token);

        var sent = f.Server.LinearCreates.Single();
        Assert.Equal(
            (0, 0, "OPS's states haven't synced yet. They arrive with the next sync (within 5 min)", "OPS", null),
            (f.Vm.States.Count, f.Vm.Cycles.Count, f.Vm.NotSyncedNote, sent.Team, sent.StateId));
    }, Token);

    [Fact]
    public Task Only_the_fields_that_are_set_go_in_the_body_and_the_title_is_trimmed() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        vm.Title = "  Document the retry budget  ";

        await vm.Submit(Token);

        var sent = f.Server.LinearCreates.Single();
        Assert.Equal(
            ("ENG", "Document the retry budget", null, null, null, null, null, true),
            (sent.Team, sent.Title, sent.Description, sent.StateId, sent.AssigneeId, sent.Priority, sent.CycleId, sent.LabelIds is null));
    }, Token);

    [Fact]
    public Task Every_set_field_goes_in_the_body_by_its_id() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        vm.Title = "Document the retry budget";
        vm.Description = "Say what the budget is.";
        vm.State = NewLinearIssueFixture.Entry(vm.States, "Todo");
        vm.Assignee = NewLinearIssueFixture.Entry(vm.Assignees, "Sam Lee");
        vm.Priority = NewLinearIssueFixture.Entry(vm.Priorities, "High");
        vm.Cycle = NewLinearIssueFixture.Entry(vm.Cycles, "Cycle 15");
        NewLinearIssueFixture.Entry(vm.LabelChoices, "api").IsTicked = true;

        await vm.Submit(Token);

        var sent = f.Server.LinearCreates.Single();
        Assert.Equal(
            ("Say what the budget is.", "00000000-0000-4000-8000-000000000051", "00000000-0000-4000-8000-0000000000a2", 2, "00000000-0000-4000-8000-0000000000c2", "00000000-0000-4000-8000-0000000000d2"),
            (sent.Description, sent.StateId, sent.AssigneeId, sent.Priority, sent.CycleId, string.Join(',', sent.LabelIds ?? [])));
    }, Token);

    [Fact]
    public Task The_clients_id_is_made_with_the_first_typed_text_and_goes_in_the_body() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        var before = vm.ClientId;

        vm.Title = "D";
        var made = vm.ClientId;
        vm.Title = "Document";
        await vm.Submit(Token);

        Assert.Equal((true, true, made), (before is null, Guid.TryParse(made, out _), f.Server.LinearCreates.Single().ClientId));
    }, Token);

    [Fact]
    public Task Closing_keeps_the_draft_and_reopening_offers_to_resume_or_discard_it() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        vm.Title = "Document the retry budget";
        vm.State = NewLinearIssueFixture.Entry(vm.States, "Todo");
        var id = vm.ClientId;

        vm.Close();
        var closed = (vm.IsOpen, vm.Title, vm.State?.Name, vm.ClientId, f.Server.LinearCreates.Count);
        await vm.Open(Token);
        await vm.Reading;

        Assert.Equal(((false, "Document the retry budget", "Todo", id, 0), (true, true, "Todo")), (closed, (vm.IsOpen, vm.ShowsDraft, vm.State?.Name)));
    }, Token);

    [Fact]
    public Task Discard_clears_the_fields_and_the_client_id_and_the_next_text_makes_a_new_one() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        vm.Title = "Document the retry budget";
        vm.Description = "Say what it is.";
        NewLinearIssueFixture.Entry(vm.LabelChoices, "bug").IsTicked = true;
        var old = vm.ClientId;
        vm.Close();
        await vm.Open(Token);
        await vm.Reading;

        vm.Discard();
        var cleared = (vm.Title, vm.Description, vm.ClientId, vm.ShowsDraft, vm.LabelChoices.Any(l => l.IsTicked));
        vm.Title = "Another";

        Assert.Equal(((string.Empty, string.Empty, null, false, false), true), (cleared, vm.ClientId is not null && !string.Equals(vm.ClientId, old, StringComparison.Ordinal)));
    }, Token);

    [Fact]
    public Task A_fresh_dialog_with_nothing_typed_offers_no_draft() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        vm.Close();

        await vm.Open(Token);

        Assert.False(vm.ShowsDraft);
    }, Token);

    [Fact]
    public Task A_created_issue_clears_the_draft_closes_the_dialog_raises_its_key_and_says_so() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        var keys = new List<string>();
        vm.Created += (_, e) => keys.Add(e.Value);
        vm.Title = "Document the retry budget";

        await vm.Submit(Token);

        Assert.Equal(
            (false, string.Empty, null, "ENG-58", "Created ENG-58", true),
            (vm.IsOpen, vm.Title, vm.ClientId, string.Join(',', keys), vm.Announcement, vm.IsAnnouncing));
    }, Token);

    [Fact]
    public Task An_answer_that_is_unknown_locks_the_form_and_Retry_sends_the_same_client_id_and_body() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        f.Server.QueueLinearCreate(NewLinearIssueFixture.Create("unknown"));
        vm.Title = "Document the retry budget";

        await vm.Submit(Token);
        var unknown = (vm.IsUnknown, vm.IsLocked, vm.IsOpen, vm.StatusLine);
        await vm.Retry(Token);

        var sent = f.Server.LinearCreates;
        Assert.Equal(
            ((true, true, true, "Linear didn't confirm the issue. Retry?"), 2, true, false, false),
            (unknown, sent.Count, sent[0].ClientId == sent[1].ClientId && sent[0].Title == sent[1].Title && sent[0].Team == sent[1].Team, vm.IsOpen, vm.IsLocked));
    }, Token);

    [Fact]
    public Task A_call_that_got_no_answer_is_counted_as_unknown() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        vm.Title = "Document the retry budget";
        f.Server.GoDown();

        await vm.Submit(Token);

        Assert.Equal((true, true, "Linear didn't confirm the issue. Retry?", string.Empty), (vm.IsUnknown, vm.IsLocked, vm.StatusLine, vm.ErrorBanner));
    }, Token);

    [Fact]
    public Task A_rate_limit_shows_its_banner_and_keeps_the_form_unlocked_with_its_text() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        f.Server.QueueLinearCreate(NewLinearIssueFixture.RateLimited());
        vm.Title = "Document the retry budget";

        await vm.Submit(Token);

        Assert.Equal(
            (true, false, false, "Document the retry budget"),
            (vm.ErrorBanner.StartsWith("Linear's rate limit: try again after ", StringComparison.Ordinal), vm.IsLocked, vm.IsUnknown, vm.Title));
    }, Token);

    [Fact]
    public Task A_session_refusal_turns_the_form_off_with_no_banner_and_keeps_the_text() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        f.Server.QueueLinearCreate(NewLinearIssueFixture.Create("session-refused"));
        vm.Title = "Document the retry budget";

        await vm.Submit(Token);

        Assert.Equal((true, string.Empty, false, "Document the retry budget"), (vm.IsOff, vm.ErrorBanner, vm.IsLocked, vm.Title));
    }, Token);

    [Fact]
    public Task A_bad_value_shows_a_banner_keeps_the_form_and_typing_clears_the_banner() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        f.Server.QueueLinearCreate(NewLinearIssueFixture.Create("bad-value"));
        vm.Title = "Document the retry budget";

        await vm.Submit(Token);
        var shown = (vm.ErrorBanner.Contains("assigneeId isn't a member of ENG", StringComparison.Ordinal), vm.IsLocked, vm.IsOpen, vm.Title);
        vm.Title = "Document the retry budget now";

        Assert.Equal(((true, false, true, "Document the retry budget"), string.Empty), (shown, vm.ErrorBanner));
    }, Token);

    [Fact]
    public Task The_button_says_Sending_after_300_ms_in_flight() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        var gate = f.Server.LinearCreateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Title = "Document the retry budget";

        var sending = vm.Submit(Token);
        f.Clock.Advance(TimeSpan.FromMilliseconds(299));
        var early = (vm.CreateText, vm.IsLocked);
        f.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var late = vm.CreateText;
        await NewLinearIssueFixture.Release(gate, sending);

        Assert.Equal((("Create issue", true), "Sending…", "Create issue"), (early, late, vm.CreateText));
    }, Token);

    [Fact]
    public Task The_polite_line_says_Checking_after_15_s_in_flight() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        var gate = f.Server.LinearCreateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Title = "Document the retry budget";

        var sending = vm.Submit(Token);
        f.Clock.Advance(TimeSpan.FromSeconds(14));
        var early = vm.StatusLine;
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        var late = vm.StatusLine;
        await NewLinearIssueFixture.Release(gate, sending);

        Assert.Equal((string.Empty, "Linear didn't confirm it. Checking ENG…"), (early, late));
    }, Token);

    [Fact]
    public Task A_second_press_while_a_create_is_in_flight_sends_nothing() => NewLinearIssueFixture.Run(async f =>
    {
        var vm = await f.OpenedOnEng(Token);
        var gate = f.Server.LinearCreateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Title = "Document the retry budget";

        var first = vm.Submit(Token);
        await vm.Submit(Token);
        await NewLinearIssueFixture.Release(gate, first);

        _ = Assert.Single(f.Server.LinearCreates);
    }, Token);
}
