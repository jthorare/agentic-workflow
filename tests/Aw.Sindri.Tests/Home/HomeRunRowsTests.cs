using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Tests.Home;

/// <summary>The 'run-failed' row (home spec §3.1, §3.2, §5.1): its id, words, facts, links and actions from one failed run.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeRunRowsTests
{
    private static readonly DateTimeOffset _ended = new(2026, 11, 12, 9, 4, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _read = new(2026, 11, 12, 9, 10, 0, TimeSpan.Zero);

    private static WorkflowRun Failed(string? error, string trigger = "{\"kind\":\"schedule\"}", ItemId? item = null) =>
        new(WorkflowRunId.From("morning-check@3:12"), WorkflowId.From("WF-00000000000000000000000000000001"), 3, trigger, WorkflowRunStatus.Failed, Outputs: null, error, item, _ended.AddMinutes(-2), _ended);

    [Fact]
    public void A_failed_run_makes_an_open_needs_you_row_named_by_its_workflow_and_error()
    {
        var raise = HomeRunRows.Failed(Failed("step 'fetch' failed: GitHub answered 502\nat line 4"), _read, new Masking());

        var row = raise.Row;
        Assert.Equal("sindri:run-failed:morning-check@3:12", row.Id.Value);
        Assert.Equal((1L, HomeKind.RunFailed, HomeGroup.NeedsYou, HomeState.Open), (row.Version, row.Kind, row.Group, row.State));
        Assert.Equal("morning-check failed: step 'fetch' failed: GitHub answered 502", row.Title);
        Assert.Equal(["failed", "morning-check@3:12", "schedule"], row.Facts.Select(static f => f.Text).ToArray());
        Assert.Equal([HomeFactKind.Why, HomeFactKind.Ref, HomeFactKind.From], row.Facts.Select(static f => f.Kind).ToArray());
        Assert.Equal("you", row.WaitsOn);
        Assert.Equal(_ended, row.Since);
        Assert.Equal(HomeRunRows.Weight, row.Rank.Weight);
        Assert.Equal(["retry-run", "open-run", "hand-to-agent"], row.Actions.ToArray());
        Assert.Equal((HomeRunRows.SourceName, _read), (row.Source.Name, row.Source.ReadAt));
        Assert.Equal("run", Assert.Single(row.Links).Kind);
        Assert.Empty(raise.Edges);
    }

    [Fact]
    public void The_error_is_scrubbed_and_cut_to_one_short_line()
    {
        var error = "token sk-live-0123456789 was refused " + new string('x', 200);

        var title = HomeRunRows.Failed(Failed(error), _read, new Masking()).Row.Title;

        Assert.DoesNotContain("sk-live", title, StringComparison.Ordinal);
        Assert.StartsWith("morning-check failed: token [secret] was refused", title, StringComparison.Ordinal);
        Assert.EndsWith("…", title, StringComparison.Ordinal);
        Assert.Equal(HomeRunRows.ErrorChars, title.Length - "morning-check failed: ".Length);
    }

    [Fact]
    public void A_run_with_no_error_is_named_by_its_number_and_a_manual_run_by_who_pressed_it()
    {
        var row = HomeRunRows.Failed(Failed(error: null, "{\"kind\":\"manual\",\"by\":\"human:thor\"}"), _read, new Masking()).Row;

        Assert.Equal("morning-check: run 12 failed", row.Title);
        Assert.Equal("manual by human:thor", row.Facts[2].Text);
    }

    [Fact]
    public void A_run_for_an_item_links_the_item_too()
    {
        var row = HomeRunRows.Failed(Failed("boom", item: ItemId.From("ITM-0000000000000000000000000000000c")), _read, new Masking()).Row;

        Assert.Equal(["run", "item"], row.Links.Select(static l => l.Kind).ToArray());
        Assert.Equal("sindri:item:ITM-0000000000000000000000000000000c", row.Links[1].Id.Value);
    }

    [Fact]
    public void The_row_id_gives_back_its_run_and_other_ids_give_none()
    {
        var run = WorkflowRunId.From("morning-check@3:12");

        Assert.Equal(run, HomeRunRows.RunOf(HomeRunRows.IdOf(run)));
        Assert.Null(HomeRunRows.RunOf(HomeRowId.From("lane:recycle:meta")));
        Assert.Null(HomeRunRows.RunOf(HomeRowId.From("sindri:run-failed:NotARun")));
    }

    private sealed class Masking : ITextScrubber
    {
        public string Scrub(string text) => System.Text.RegularExpressions.Regex.Replace(text, "sk-[a-z]+-[0-9]+", "[secret]", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
    }
}
