using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Requests;

namespace Aw.Sindri.Tests.ListDecisions;

/// <summary>The decision list (API spec §3.1, U3-API-3, -6, -7, -8): pending oldest first, decided newest first, options with their CLI, `cleared` once settled.</summary>
[Lane(TestLane.Unit)]
public sealed class ListDecisionsTests
{
    private readonly RequestQueryHarness _q = new();

    private ListDecisionsService Service() => new(_q.Views.Views, new ViewItemResolver(_q.Views.Views), _q.Views.Progress, _q.Views.Slice.Callers);

    private async Task<IReadOnlyList<DecisionView>> List(ListDecisionsRequest request) => (await Service().Handle(request, RequestQueryHarness.Token)).Value.Decisions;

    [Fact]
    public async Task ListDecisions_shows_a_pending_decision_with_its_options_label_and_cli()
    {
        var decision = await _q.File("Split the work.", RequestKind.Rescope, repo: "web");
        using var entered = _q.Human();

        var result = await Service().Handle(A.ListDecisionsRequest().Build(), RequestQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var view = Assert.Single(result.Value.Decisions);
        Expect.Equal((decision, DecisionKind.AcceptRequest, ActionClass.Decision, "decision · re-scope", DecisionState.Pending),
            (view.Id, view.Kind, view.Class, view.Label, view.State));
        Expect.Equal(RepoName.From("web"), view.Repo!.Value);
        Assert.StartsWith("Accept this re-scope request?", view.Question, StringComparison.Ordinal);
        Assert.Contains("Split the work.", view.Question, StringComparison.Ordinal);
        Expect.Sequence(["accept", "decline"], [.. view.Options.Select(o => o.Id.Value)]);
        Expect.Sequence([false, true], [.. view.Options.Select(o => o.RejectLike)]);
        Expect.Equal($"sindri decide {decision.Value} decline", view.Options[1].Cli);
        Expect.Equal(DecisionOption.From("accept"), view.Recommended);
        Expect.Equal($"sindri decide {decision.Value} accept", view.Cli);
        Expect.Sequence(["Decide"], [.. view.Actions.Select(a => a.Value)]);
        Expect.Equal((true, EmptyText.RecordedOnly()), (view.RecordedOnly, view.Notice));
        Assert.Null(view.Cleared);
        Assert.Null(view.Choice);
        Assert.Null(result.Value.Empty);
    }

    [Fact]
    public async Task Pending_come_oldest_first_and_decided_newest_first()
    {
        var a = await _q.File("a");
        var b = await _q.File("b");
        var c = await _q.File("c");
        var d = await _q.File("d");
        await _q.Decide(a, "accept");
        await _q.Decide(c, "decline");
        using var entered = _q.Human();

        var all = await List(A.ListDecisionsRequest().Build());

        Expect.Sequence([b, d, c, a], [.. all.Select(v => v.Id)]);
        Expect.Sequence([b, d], [.. (await List(A.ListDecisionsRequest().Pending(value: true).Build())).Select(v => v.Id)]);
        Expect.Sequence([c, a], [.. (await List(A.ListDecisionsRequest().Pending(value: false).Build())).Select(v => v.Id)]);
    }

    [Fact]
    public async Task Decided_view_has_cleared_line()
    {
        var decision = await _q.File("Split the work.");
        await _q.Decide(decision, "accept", note: "go ahead", by: "human:ui");
        using var entered = _q.Human();

        var view = Assert.Single(await List(A.ListDecisionsRequest().Build()));

        Expect.Equal((DecisionState.Decided, "human:ui", DecisionOption.From("accept"), "go ahead"), (view.State, view.DecidedBy, view.Choice!.Value, view.Note));
        Assert.False(string.IsNullOrEmpty(view.Cleared));
        Assert.Contains("accept", view.Cleared, StringComparison.Ordinal);
        Assert.Empty(view.Actions);
        _ = Assert.NotNull(view.DecidedAt);
    }

    [Fact]
    public async Task A_voided_decision_carries_the_voided_sentence_and_clears_nothing_recorded()
    {
        var item = RequestQueryHarness.Item('1');
        await _q.CreateItem(item);
        _ = await _q.File("about it", item: item);
        await _q.Close(item);
        using var entered = _q.Human();

        var view = Assert.Single(await List(A.ListDecisionsRequest().Build()));

        Expect.Equal((DecisionState.Voided, EmptyText.Voided(), EmptyText.Voided(), false), (view.State, view.Notice, view.Cleared, view.RecordedOnly));
        Assert.Empty(view.Actions);
        Assert.Null(view.Choice);
    }

    [Fact]
    public async Task The_item_filter_keeps_that_items_decisions()
    {
        var item = RequestQueryHarness.Item('1');
        await _q.CreateItem(item);
        var about = await _q.File("about it", item: item);
        _ = await _q.File("about nothing");
        using var entered = _q.Human();

        Expect.Sequence([about], [.. (await List(A.ListDecisionsRequest().AboutItem(ItemRef.To(item)).Build())).Select(v => v.Id)]);
        Expect.Equal(item, (await List(A.ListDecisionsRequest().AboutItem(ItemRef.To(item)).Build()))[0].Item!.Value);
    }

    [Fact]
    public async Task A_session_sees_every_repos_decisions()
    {
        _ = await _q.File("mine", repo: "alpha");
        _ = await _q.File("theirs", repo: "beta");
        var nobodys = await _q.File("nobody's");
        using var entered = _q.Session("beta");

        var views = await List(A.ListDecisionsRequest().Build());

        Expect.Equal(3, views.Count);
        Assert.Contains(views, v => v.Id == nobodys);
        Assert.Contains(views, v => v.Question.Contains("mine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_session_in_another_repo_sees_a_repos_decisions()
    {
        _ = await _q.File("theirs", repo: "beta");
        using var entered = _q.Session("alpha");

        var result = await Service().Handle(A.ListDecisionsRequest().Build(), RequestQueryHarness.Token);

        _ = Assert.Single(result.Value.Decisions);
    }

    [Fact]
    public async Task At_most_200_decided_are_listed()
    {
        for (var n = 0; n < 205; n++)
        {
            await _q.Decide(await _q.File("decided " + n.ToString(System.Globalization.CultureInfo.InvariantCulture)), "accept");
        }

        using var entered = _q.Human();

        Expect.Equal(200, (await List(A.ListDecisionsRequest().Pending(value: false).Build())).Count);
    }

    [Fact]
    public async Task Empty_sentences_follow_the_filter()
    {
        using var entered = _q.Human();

        Expect.Equal(EmptyText.Decisions(pending: true, item: null), (await Service().Handle(A.ListDecisionsRequest().Pending(value: true).Build(), RequestQueryHarness.Token)).Value.Empty);
        Expect.Equal(EmptyText.Decisions(pending: false, item: null), (await Service().Handle(A.ListDecisionsRequest().Pending(value: false).Build(), RequestQueryHarness.Token)).Value.Empty);
    }

    [Fact]
    public async Task Without_a_caller_it_is_refused()
    {
        var result = await Service().Handle(A.ListDecisionsRequest().Build(), RequestQueryHarness.Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }
}
