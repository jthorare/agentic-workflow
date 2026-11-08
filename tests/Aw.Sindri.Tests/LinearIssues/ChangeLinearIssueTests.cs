using System.Text.Json;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.LinearReads;
using static Aw.Sindri.Tests.LinearIssues.LinearIssueRows;

namespace Aw.Sindri.Tests.LinearIssues;

/// <summary>
/// ChangeLinearIssue (Linear hub spec 2026-11-04 §3, §5.3, D6): the gate, ids checked against the team's lookups (SND-LINEAR-006), the field-conflict check against Linear's live read (409, nothing sent), a change that already
/// holds (nothing sent), Context's outcomes (applied, stored-late warning 013, unknown 011), and the re-read the answer carries. ENG-12 is in progress (state 52); the team also has In Review (53) and Done (55).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ChangeLinearIssueTests
{
    private const string Progress = "00000000-0000-4000-8000-000000000052";
    private const string Review = "00000000-0000-4000-8000-000000000053";
    private const string Done = "00000000-0000-4000-8000-000000000055";
    private const string Bug = "00000000-0000-4000-8000-0000000000d1";
    private const string Api = "00000000-0000-4000-8000-0000000000d2";

    private readonly FakeContextLinearWrites _writes = new();
    private readonly FakeLinearWriteGate _gate = new();
    private readonly FakeLinearSideFacts _side = new FakeLinearSideFacts().WithTeam(Lookups()).WithTeamsPicked(picked: true);
    private readonly FakeLinearIssueFacts _facts = new FakeLinearIssueFacts().WithSource(Connected(Viewer)).WithDetails(new LinearIssueDetailFacts(Row("ENG-12", Viewer), "The description", Removed: false, []));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LinearIssueKey Key => LinearIssueKey.Parse("ENG-12").Value;

    private static string? Member(Kernel.Error error, string name) => error.Extensions!.First(m => m.Key == name).Value.GetString();

    private ChangeLinearIssueService Service() => new(_gate, new FakeLinearWriteScope(), _side, _facts, _writes, new GetLinearIssueService(_facts, new FakeLinearItemLinks(), _gate));

    private static LinearLookupsResponse Lookups() => new(
        new LinearTeamRef(EngId, "ENG", "Engineering"), KnownInstants.Epoch,
        [new("00000000-0000-4000-8000-000000000051", "Todo", "unstarted", 1), new(Progress, "In Progress", "started", 2), new(Review, "In Review", "started", 3), new(Done, "Done", "completed", 4)],
        [new(ViewerId, "Joi Example", Active: true), new(SamId, "Sam Lee", Active: true)],
        [new(Bug, "bug", "#eb5757"), new(Api, "api", "#5e6ad2")],
        [new("00000000-0000-4000-8000-0000000000c1", 14, "Cycle 14", KnownInstants.Epoch.AddDays(-7), KnownInstants.Epoch.AddDays(7), Current: true)]);

    private static LinearIssueView Live(string stateId = Progress, string title = "Title of ENG-12", string? description = "The description", IReadOnlyList<string>? labels = null, double updatedMinutesAgo = 30) => new(
        ExternalId.From("00000000-0000-4000-8000-00000000010c"), Key, "ENG", title, description, ExternalId.From(stateId), "State", ExternalId.From(ViewerId), Priority: 2, Estimate: null,
        CycleId: null, ProjectId: null, ParentId: null, [.. (labels ?? [Bug]).Select(ExternalId.From)], KnownInstants.Epoch.AddMinutes(-updatedMinutesAgo));

    private static Dictionary<string, JsonElement> Fields(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static ChangeLinearIssueRequest Edit(string key, string opened, string set) => new(key, Fields(opened), Fields(set));

    private static ChangeLinearIssueRequest ToReview() => Edit("ENG-12", $$"""{"stateId":"{{Progress}}"}""", $$"""{"stateId":"{{Review}}"}""");

    private async Task<LinearChangeResult> Ok(ChangeLinearIssueRequest request)
    {
        var result = await Service().Handle(request, Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value;
    }

    [Theory]
    [InlineData("SND-AUTH-002")]
    [InlineData("SND-LINEAR-002")]
    [InlineData("SND-LINEAR-003")]
    public async Task A_write_the_gate_refuses_sends_nothing_and_answers_the_gates_code(string code)
    {
        _ = _gate.Off(code);
        _writes.SetLive(Live());

        var result = await Service().Handle(ToReview(), Token);

        Expect.Equal(code, result.Error.Code);
        Expect.Equal("ENG", Expect.Only(_gate.Asked));
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task The_message_of_a_team_that_was_not_picked_names_the_team_and_where_to_add_it()
    {
        _ = _gate.Off("SND-LINEAR-003");

        var result = await Service().Handle(ToReview(), Token);

        Expect.Equal("Sindri only writes to teams you picked; ENG isn't one (fix: Settings, Connections, Linear, add ENG)", result.Error.Message);
    }

    [Theory]
    [InlineData("""{"stateId":"00000000-0000-4000-8000-0000000000ff"}""", "stateId isn't one of ENG's states (fix: read the lookups again and pick a state from them)")]
    [InlineData("""{"assigneeId":"00000000-0000-4000-8000-0000000000ff"}""", "assigneeId isn't one of ENG's members (fix: read the lookups again and pick a member from them)")]
    [InlineData("""{"labelIds":["00000000-0000-4000-8000-0000000000d1","00000000-0000-4000-8000-0000000000ff"]}""", "labelIds isn't one of ENG's labels (fix: read the lookups again and pick a label from them)")]
    [InlineData("""{"cycleId":"00000000-0000-4000-8000-0000000000ff"}""", "cycleId isn't one of ENG's cycles (fix: read the lookups again and pick a cycle from them)")]
    public async Task An_id_the_team_does_not_have_is_refused_before_anything_is_sent(string set, string message)
    {
        _writes.SetLive(Live());

        var result = await Service().Handle(Edit("ENG-12", "{}", set), Token);

        Expect.Equal("SND-LINEAR-006", result.Error.Code);
        Expect.Equal(message, result.Error.Message);
        Assert.Empty(_writes.Calls);
    }

    [Theory]
    [InlineData("{}", """{"priority":1}""", "priority has no opened value (fix: send the value you opened with each field you change)")]
    [InlineData("""{"title":"Title of ENG-12"}""", """{"title":"x","priority":1,"assigneeId":null}""", "assigneeId and priority have no opened value (fix: send the value you opened with each field you change)")]
    public async Task A_set_field_with_no_opened_value_is_refused_before_linear_is_read(string opened, string set, string message)
    {
        _writes.SetLive(Live());

        var result = await Service().Handle(Edit("ENG-12", opened, set), Token);

        Expect.Equal("SND-LINEAR-006", result.Error.Code);
        Expect.Equal(message, result.Error.Message);
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task An_opened_key_holding_null_counts_as_opened_and_is_compared_with_linear()
    {
        _writes.SetLive(Live());

        var answer = await Ok(Edit("ENG-12", """{"assigneeId":null}""", """{"assigneeId":null}"""));

        var field = Expect.Only(answer.Conflict!.Fields);
        Expect.Equal("assigneeId", field.Field);
        Assert.DoesNotContain(_writes.Calls, static c => c.Method == "Change");
    }

    [Fact]
    public async Task A_cleared_assignee_cycle_and_project_need_no_lookup()
    {
        _writes.SetLive(Live());

        var applied = await Ok(Edit("ENG-12", $$"""{"assigneeId":"{{ViewerId}}","cycleId":null,"projectId":null}""", """{"assigneeId":null,"cycleId":null,"projectId":null}"""));

        Assert.NotNull(applied.Applied);
        var change = Expect.Only(_writes.Calls.Where(static c => c.Method == "Change").ToArray()).Change!;
        Assert.True(change.AssigneeId.IsSet && change.AssigneeId.Value is null && change.CycleId.IsSet && change.ProjectId.IsSet);
    }

    [Theory]
    [InlineData("not a key", "{}", """{"title":"x"}""")]
    [InlineData("ENG-12", "{}", "{}")]
    [InlineData("ENG-12", "{}", """{"colour":"red"}""")]
    [InlineData("ENG-12", "{}", """{"priority":"high"}""")]
    [InlineData("ENG-12", "{}", """{"labelIds":"bug"}""")]
    public async Task A_bad_key_an_empty_change_an_unknown_field_or_a_wrong_type_is_invalid_input(string key, string opened, string set)
    {
        _writes.SetLive(Live());

        var result = await Service().Handle(Edit(key, opened, set), Token);

        Assert.True(result.IsError);
        Assert.Equal(Kernel.ErrorKind.Invalid, result.Error.Kind);
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task An_edit_reads_linear_live_sends_only_the_set_fields_and_answers_with_the_reread_issue()
    {
        _writes.SetLive(Live());

        var answer = await Ok(ToReview());

        var applied = answer.Applied!;
        Assert.Null(answer.Conflict);
        Expect.Equal(("applied", null, null), (applied.Outcome, applied.Warning, applied.AlreadyApplied));
        Expect.Equal("ENG-12", applied.Issue.Key);
        Expect.Sequence(["Live", "Change"], _writes.Calls.Select(static c => c.Method));
        var change = _writes.Calls[1].Change!;
        Expect.Equal(Review, change.StateId.Value.Value);
        Assert.False(change.Title.IsSet || change.Description.IsSet || change.LabelIds.IsSet);
    }

    [Fact]
    public async Task A_change_that_landed_but_was_not_stored_is_applied_with_the_late_store_warning()
    {
        _writes.SetLive(Live());
        _writes.Next(new LinearWriteAppliedNotStored(Live(Review)));

        var applied = (await Ok(ToReview())).Applied!;

        Expect.Equal(("applied", "SND-LINEAR-013"), (applied.Outcome, applied.Warning));
    }

    [Fact]
    public async Task A_write_nobody_confirmed_is_a_gateway_timeout_that_says_unknown()
    {
        _writes.SetLive(Live());
        _writes.Next(new LinearWriteUnknown());

        var result = await Service().Handle(ToReview(), Token);

        Expect.Equal("SND-LINEAR-011", result.Error.Code);
        Expect.Equal(Kernel.ErrorKind.Timeout, result.Error.Kind);
        Expect.Equal("unknown", Member(result.Error, "outcome"));
    }

    [Fact]
    public async Task A_field_linear_changed_since_the_sheet_opened_is_a_conflict_and_nothing_is_sent()
    {
        _writes.SetLive(Live(Done, updatedMinutesAgo: 5));

        var answer = await Ok(ToReview());

        var conflict = answer.Conflict!;
        Assert.Null(answer.Applied);
        Expect.Equal(KnownInstants.Epoch.AddMinutes(-5), conflict.ChangedAt);
        var field = Expect.Only(conflict.Fields);
        Expect.Equal(("stateId", Progress, Done), (field.Field, field.Opened.GetString(), field.Linear.GetString()));
        Assert.DoesNotContain(_writes.Calls, static c => c.Method == "Change");
    }

    [Fact]
    public async Task Fields_linear_still_has_as_the_sheet_opened_them_are_no_conflict_whatever_the_label_order()
    {
        _writes.SetLive(Live(description: null, labels: [Api, Bug]));

        var answer = await Ok(Edit("ENG-12", $$"""{"labelIds":["{{Bug}}","{{Api}}"],"description":""}""", $$"""{"labelIds":["{{Bug}}"],"description":"New text"}"""));

        Assert.NotNull(answer.Applied);
    }

    [Fact]
    public async Task A_description_linear_has_as_nothing_equals_the_empty_one_the_sheet_opened()
    {
        _writes.SetLive(Live(description: null));

        var answer = await Ok(Edit("ENG-12", """{"description":""}""", """{"description":"New text"}"""));

        Assert.NotNull(answer.Applied);
    }

    [Fact]
    public async Task When_every_set_field_already_holds_in_linear_nothing_is_sent_and_the_answer_says_so()
    {
        _writes.SetLive(Live(Review));

        var applied = (await Ok(ToReview())).Applied!;

        Expect.Equal(("applied", true), (applied.Outcome, applied.AlreadyApplied));
        Expect.Equal("Live", Expect.Only(_writes.Calls).Method);
    }

    [Fact]
    public async Task A_rate_limit_is_passed_on_with_the_time_to_retry_at()
    {
        _writes.SetLive(Live());
        _writes.NextError(LinearHubErrors.RateLimited.With("Linear's rate limit: try again after 10:06 UTC. Nothing was changed.").WithExtension("retryAfter", JsonSerializer.SerializeToElement("2026-01-01T10:06:00.0000000Z")));

        var result = await Service().Handle(ToReview(), Token);

        Expect.Equal("SND-LINEAR-012", result.Error.Code);
        Expect.Equal("2026-01-01T10:06:00.0000000Z", Member(result.Error, "retryAfter"));
        Expect.Equal("Linear is rate-limiting this key; nothing was sent (fix: retry after 10:06)", result.Error.Message);
    }

    [Fact]
    public async Task A_live_read_that_fails_is_the_answer_and_nothing_is_sent()
    {
        var result = await Service().Handle(ToReview(), Token);

        Expect.Equal("SND-LINEAR-005", result.Error.Code);
        Assert.DoesNotContain(_writes.Calls, static c => c.Method == "Change");
    }
}
