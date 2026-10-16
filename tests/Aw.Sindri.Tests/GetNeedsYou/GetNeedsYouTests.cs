using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Requests;

namespace Aw.Sindri.Tests.GetNeedsYou;

/// <summary>The human's list (API spec §3.1, §3.6; UI spec E-7, E-16): urgency, then since, then id; a session's repo scope; the empty sentence with the counts.</summary>
[Lane(TestLane.Unit)]
public sealed class GetNeedsYouTests
{
    private readonly RequestQueryHarness _q = new();

    private GetNeedsYouService Service() => new(_q.Views.Views, _q.Views.Progress, _q.Views.Slice.Callers);

    private async Task<GetNeedsYouResponse> Get() => (await Service().Handle(A.GetNeedsYouRequest().Build(), RequestQueryHarness.Token)).Value;

    [Fact]
    public async Task GetNeedsYou_lists_rows_by_urgency_then_since_then_id()
    {
        await _q.Drift();
        await _q.Ran("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001");
        var first = await _q.File("first");
        var second = await _q.File("second");
        using var entered = _q.Human();

        var result = await Service().Handle(A.GetNeedsYouRequest().Build(), RequestQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence(["decision:" + first.Value, "decision:" + second.Value, "failure:service:heartbeat", "profile:drift"], [.. result.Value.Rows.Select(r => r.Id.Value)]);
        Expect.Sequence([NeedsYouKind.Decision, NeedsYouKind.Decision, NeedsYouKind.Failure, NeedsYouKind.Profile], [.. result.Value.Rows.Select(r => r.Kind)]);
        Assert.Null(result.Value.Empty);
        Expect.Equal(_q.Views.Progress.Current.AsOf, result.Value.AsOf);
    }

    [Fact]
    public async Task A_decision_row_names_its_label_item_actions_and_cli()
    {
        var item = RequestQueryHarness.Item('1');
        await _q.CreateItem(item, "web");
        var decision = await _q.File("Split the work.", repo: "web", item: item);
        using var entered = _q.Human();

        var row = Assert.Single((await Get()).Rows);

        Expect.Equal(("decision · re-scope", item, true, EmptyText.RecordedOnly()), (row.Label, row.Item!.Value, row.RecordedOnly, row.Notice));
        Expect.Equal(($"sindri decide {decision.Value} accept", "Decide"), (row.Cli, row.PrimaryAction.Value));
        Assert.Contains(row.PrimaryAction, row.Actions);
    }

    [Fact]
    public async Task Session_sees_only_its_repo_rows()
    {
        _ = await _q.File("mine", repo: "alpha");
        _ = await _q.File("theirs", repo: "beta");
        await _q.Ran("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001");
        using var entered = _q.Session("alpha");

        var rows = (await Get()).Rows;

        Expect.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Repo?.Value == "beta");
        Assert.Contains(rows, r => r.Kind == NeedsYouKind.Failure);
    }

    [Fact]
    public async Task Needs_you_empty_sentence_counts_running_and_queued()
    {
        await _q.CreateItem(RequestQueryHarness.Item('1'));
        await _q.CreateItem(RequestQueryHarness.Item('2'));
        await _q.CreateItem(RequestQueryHarness.Item('3'));
        await _q.Start(RequestQueryHarness.Item('3'));
        using var entered = _q.Human();

        var response = await Get();

        Assert.Empty(response.Rows);
        Expect.Equal(EmptyText.NeedsYou(running: 1, queued: 2), response.Empty);
    }

    [Fact]
    public async Task Stalled_views_replace_the_sentence_and_keep_the_rows()
    {
        _ = await _q.File("kept");
        _q.Views.Publish(stalledAt: 1);
        using var entered = _q.Human();

        var response = await Get();

        Expect.Equal(EmptyText.Stalled(1), response.Empty);
        _ = Assert.Single(response.Rows);
    }

    [Fact]
    public async Task A_decided_request_leaves_the_list()
    {
        var decision = await _q.File("one");
        await _q.Decide(decision, "decline");
        using var entered = _q.Human();

        Assert.Empty((await Get()).Rows);
    }

    [Fact]
    public async Task Every_cli_is_a_filled_CliForm()
    {
        await _q.Drift();
        await _q.Ran("heartbeat", ServiceOutcome.Failed);
        _ = await _q.File("one");
        using var entered = _q.Human();

        var rows = (await Get()).Rows;

        Expect.Equal(3, rows.Count);
        foreach (var row in rows)
        {
            Assert.Matches("^sindri( [A-Za-z0-9][A-Za-z0-9:._-]*)+$", row.Cli);
            Assert.DoesNotContain('<', row.Cli);
            Assert.StartsWith("sindri ", row.Cli, StringComparison.Ordinal);
            Assert.Contains(row.PrimaryAction, row.Actions);
            Assert.StartsWith(row.Kind.ToString(), row.Label, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Without_a_caller_it_is_refused()
    {
        var result = await Service().Handle(A.GetNeedsYouRequest().Build(), RequestQueryHarness.Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }
}
