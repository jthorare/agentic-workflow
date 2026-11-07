using Aw.Kernel.Testing;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using static Aw.Sindri.Tests.LinearIssues.LinearIssueRows;

namespace Aw.Sindri.Tests.LinearIssues;

/// <summary>GetLinearIssue (Linear hub spec §3, Ph3 1a): one stored issue with its description, newest comments, link, PR and write gate; never stored is SND-LINEAR-005.</summary>
[Lane(TestLane.Unit)]
public sealed class GetLinearIssueTests
{
    private readonly FakeLinearIssueFacts _facts = new FakeLinearIssueFacts().WithSource(Connected(Viewer));
    private readonly FakeLinearItemLinks _links = new();
    private readonly FakeLinearWriteGate _gate = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetLinearIssueService Service() => new(_facts, _links, _gate);

    private static LinearIssueDetailFacts Detail(string key, bool removed = false, params LinearCommentView[] comments) =>
        new(Row(key, Viewer), "The description of " + key, removed, comments);

    private async Task<GetLinearIssueResponse> Ask(string key)
    {
        var result = await Service().Handle(new GetLinearIssueRequest(key), Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value;
    }

    [Fact]
    public async Task The_sheet_carries_the_rows_fields_the_description_the_comments_and_the_viewer()
    {
        var comment = new LinearCommentView("c1", Sam, KnownInstants.Epoch.AddHours(-1), "A comment", Sindri: false);
        _ = _facts.WithDetails(Detail("ENG-12", removed: false, comment));

        var one = await Ask("ENG-12");

        Expect.Equal("ENG-12", one.Key);
        Expect.Equal("Title of ENG-12", one.Title);
        Expect.Equal("The description of ENG-12", one.Description);
        Expect.Equal(comment, Expect.Only(one.Comments));
        Expect.Equal(Viewer, one.Viewer!);
        Assert.False(one.Removed);
        Assert.True(one.Write.Allowed);
    }

    [Fact]
    public async Task A_key_typed_in_lower_case_finds_the_issue()
    {
        _ = _facts.WithDetails(Detail("ENG-12"));

        Expect.Equal("ENG-12", (await Ask("eng-12")).Key);
    }

    [Theory]
    [InlineData("ENG-99")]
    [InlineData("eng-99")]
    public async Task An_issue_that_was_never_stored_is_not_found_and_the_message_names_its_key(string key)
    {
        _ = _facts.WithDetails(Detail("ENG-12"));

        var result = await Service().Handle(new GetLinearIssueRequest(key), Token);

        Expect.Equal("SND-LINEAR-005", result.Error.Code);
        Assert.Contains("ENG-99 isn't stored", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_issue_outside_the_callers_teams_is_not_found()
    {
        _ = _facts.VisibleTeams("ENG").WithDetails(Detail("OPS-40"));

        var result = await Service().Handle(new GetLinearIssueRequest("OPS-40"), Token);

        Expect.Equal("SND-LINEAR-005", result.Error.Code);
    }

    [Fact]
    public async Task A_removed_issue_is_its_last_copy_with_writes_off_and_no_code()
    {
        _ = _facts.WithDetails(Detail("ENG-9", removed: true));
        _ = _gate.Off("SND-LINEAR-002");

        var one = await Ask("ENG-9");

        Assert.True(one.Removed);
        Expect.Equal(new LinearWriteGateView(Allowed: false, Code: null), one.Write);
    }

    [Fact]
    public async Task The_write_gate_of_a_live_issue_is_the_gates_answer()
    {
        _ = _facts.WithDetails(Detail("ENG-12"));
        _ = _gate.Off("SND-AUTH-002");

        Expect.Equal(new LinearWriteGateView(Allowed: false, "SND-AUTH-002"), (await Ask("ENG-12")).Write);
    }

    [Fact]
    public async Task The_item_and_pull_request_linked_to_the_record_are_on_the_sheet()
    {
        var detail = Detail("ENG-12");
        _ = _facts.WithDetails(detail);
        _ = _links.With(detail.Row.Record, new LinearItemLinkView("ITM-1", "running"), new LinearPrView(2071, "https://github.com/acme/api/pull/2071", "waiting"));

        var one = await Ask("ENG-12");

        Expect.Equal("ITM-1", one.Link!.Item);
        Expect.Equal(2071, one.Pr!.Number);
    }

    [Fact]
    public async Task A_link_read_that_fails_leaves_the_sheet_without_links()
    {
        _ = _facts.WithDetails(Detail("ENG-12"));
        _ = _links.Failing(Aw.Sindri.Core.SindriErrors.InvalidQuery.With("views are down"));

        Assert.Null((await Ask("ENG-12")).Link);
    }

    [Fact]
    public async Task While_the_viewer_is_unknown_the_sheet_has_no_viewer()
    {
        _ = _facts.WithSource(Connected(viewer: null)).WithDetails(Detail("ENG-12"));

        Assert.Null((await Ask("ENG-12")).Viewer);
    }

    [Fact]
    public async Task A_failing_store_is_the_answers_error()
    {
        var error = Aw.Sindri.Core.SindriErrors.InvalidQuery.With("context is down");
        _ = _facts.Failing(error);

        var result = await Service().Handle(new GetLinearIssueRequest("ENG-12"), Token);

        Expect.Equal(error.Code, result.Error.Code);
    }
}
