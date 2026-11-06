using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Facts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>
/// The findings viewer (model-check design §4.3, M-3u): a row per finding with its message first; MDL-VIOLATION-001 as an ordered step list and a "Full trace, N states" expander; any other rule as a plain row;
/// an empty list as "No findings"; a body that is not a findings list as the payload tree.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FindingsFactViewerTests
{
    private const string Violation = "MDL-VIOLATION-001";

    // Joined with "\n" as TraceSteps.Detail writes it: a raw string would take the checkout's line endings (MA0136).
    private static readonly string _trace = string.Join(
        "\n",
        "1. Start: remaining = 3",
        "2. Elapse: remaining 3 -> 2",
        "3. Takeover: paused no -> yes",
        "4. Then repeats from step 2 (Elapse)",
        "",
        "Full trace:",
        "State 1: <Initial predicate>",
        "/\\ remaining = 3",
        "",
        "State 2: <Elapse line 10, col 5 to line 10, col 20 of module Takeover>",
        "/\\ remaining = 2",
        "",
        "State 3: <Takeover line 14, col 5 to line 14, col 25 of module Takeover>",
        "/\\ paused = TRUE",
        "",
        "Back to state 2: <Elapse line 10, col 5 to line 10, col 20 of module Takeover>");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private FactViewerContext Context() => new(_server, new FakeClipboard(), _clock, () => FactFixtures.Api, FixtureInstances.Detail("project-build").Id, _ => { });

    private static FactRow FindingsRow() => FactFixtures.Row("project-build", "fact:project") with { Type = FactType.Findings };

    private static JsonObject Finding(string rule, string message, string? path = null, int? line = null, string? detail = null) =>
        new() { ["rule"] = rule, ["message"] = message, ["path"] = path, ["line"] = line, ["detail"] = detail };

    private async Task<FindingsFactViewer> Loaded(JsonNode body, bool truncated = false)
    {
        var text = truncated ? "true" : "false";
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"key":"fact:project","type":"findings","body":{{body.ToJsonString()}},"truncated":{{text}}}""");
        var viewer = new FindingsFactViewer(FindingsRow(), Context());
        await viewer.Load(Token);
        return viewer;
    }

    [Fact]
    public async Task A_plain_finding_is_its_message_then_path_line_and_rule_with_its_detail_as_text()
    {
        var viewer = await Loaded(new JsonArray(Finding("BLD-SCOPE-001", "src/a.cs is outside this phase's scope", "src/a.cs", 12, "git diff said:\nM src/a.cs")));

        var row = Assert.Single(viewer.Findings);
        Assert.Equal(("src/a.cs is outside this phase's scope", "src/a.cs:12 · BLD-SCOPE-001"), (row.Message, row.Meta));
        Assert.Equal(("git diff said:\nM src/a.cs", true, false), (row.Detail, row.HasDetail, row.HasSteps));
        Assert.Empty(row.Steps);
        Assert.Equal((true, false, false, false), (viewer.ShowsFindings, viewer.ShowsNone, viewer.ShowsTree, viewer.IsLoading));
    }

    [Fact]
    public async Task A_finding_with_only_a_rule_and_a_message_has_just_the_rule_after_it()
    {
        var viewer = await Loaded(new JsonArray(Finding("BLD-SECRET-001", "A file holds something that looks like a key")));

        var row = Assert.Single(viewer.Findings);
        Assert.Equal(("BLD-SECRET-001", false, string.Empty, false), (row.Meta, row.HasDetail, row.Detail, row.HasSteps));
    }

    [Fact]
    public async Task A_path_with_no_line_shows_the_path_alone_before_the_rule()
    {
        var viewer = await Loaded(new JsonArray(Finding("BLD-SCOPE-001", "Out of scope", "docs/x.md")));

        Assert.Equal("docs/x.md · BLD-SCOPE-001", viewer.Findings[0].Meta);
    }

    [Fact]
    public async Task A_violation_is_a_list_of_its_steps_in_order_and_a_full_trace_behind_a_closed_button_named_for_its_states()
    {
        var viewer = await Loaded(new JsonArray(Finding(Violation, "The timer can run while paused", detail: _trace.ReplaceLineEndings("\n"))));

        var row = Assert.Single(viewer.Findings);
        Assert.Equal(
            ["1. Start: remaining = 3", "2. Elapse: remaining 3 -> 2", "3. Takeover: paused no -> yes", "4. Then repeats from step 2 (Elapse)"],
            row.Steps,
            StringComparer.Ordinal);
        Assert.Equal((true, false, false), (row.HasSteps, row.HasDetail, row.IsTraceOpen));
        Assert.Equal("Full trace, 3 states", row.TraceName);   // State 1, 2, 3; Back to state 2 repeats one
        Assert.StartsWith("State 1: <Initial predicate>", row.FullTrace, StringComparison.Ordinal);
        Assert.EndsWith("Back to state 2: <Elapse line 10, col 5 to line 10, col 20 of module Takeover>", row.FullTrace, StringComparison.Ordinal);
        Assert.DoesNotContain("Full trace:", row.FullTrace, StringComparison.Ordinal);
        Assert.DoesNotContain("Start: remaining", row.FullTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_left_out_line_is_a_step_item_too_and_the_button_counts_every_state_not_only_the_kept_ones()
    {
        const string detail = "1. Start: n = 0\n   ... 15 states left out ...\n6. Stop: n 5 -> 6\n\nFull trace:\nState 1: <Initial predicate>\n/\\ n = 0\n\nState 22: <Stop line 1, col 1 to line 1, col 9 of module M>\n/\\ n = 6";
        var viewer = await Loaded(new JsonArray(Finding(Violation, "n goes past 5", detail: detail)));

        var row = viewer.Findings[0];
        Assert.Equal(["1. Start: n = 0", "... 15 states left out ...", "6. Stop: n 5 -> 6"], row.Steps, StringComparer.Ordinal);
        Assert.Equal("Full trace, 22 states", row.TraceName);   // the kept states end at State 22: the real count, not the 2 shown
    }

    [Fact]
    public async Task One_state_is_named_in_the_singular()
    {
        const string detail = "1. Start: n = 9\n\nFull trace:\nState 1: <Initial predicate>\n/\\ n = 9";
        var viewer = await Loaded(new JsonArray(Finding(Violation, "n starts too high", detail: detail)));

        Assert.Equal("Full trace, 1 state", viewer.Findings[0].TraceName);
    }

    [Fact]
    public async Task A_trace_with_no_state_blocks_counts_its_steps()
    {
        const string detail = "1. Start: n = 0\n2. Next: n 0 -> 1\n\nFull trace:\nthe solver printed this instead";
        var viewer = await Loaded(new JsonArray(Finding(Violation, "n is odd", detail: detail)));

        Assert.Equal("Full trace, 2 states", viewer.Findings[0].TraceName);
        Assert.Equal("the solver printed this instead", viewer.Findings[0].FullTrace);
    }

    [Fact]
    public async Task Windows_line_breaks_in_the_detail_read_the_same()
    {
        var viewer = await Loaded(new JsonArray(Finding(Violation, "The timer can run while paused", detail: _trace.ReplaceLineEndings("\r\n"))));

        var row = viewer.Findings[0];
        Assert.Equal(4, row.Steps.Count);
        Assert.DoesNotContain('\r', row.Steps[0]);
        Assert.Equal("Full trace, 3 states", row.TraceName);
    }

    [Fact]
    public async Task The_trace_button_toggles_and_tells_assistive_tech_through_a_property_change()
    {
        var viewer = await Loaded(new JsonArray(Finding(Violation, "The timer can run while paused", detail: _trace.ReplaceLineEndings("\n"))));
        var row = viewer.Findings[0];
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.IsTraceOpen = true;
        Assert.True(row.IsTraceOpen);
        row.IsTraceOpen = false;

        Assert.False(row.IsTraceOpen);
        Assert.Equal([nameof(FindingRowViewModel.IsTraceOpen), nameof(FindingRowViewModel.IsTraceOpen)], changed, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_violation_with_no_full_trace_is_a_plain_row()
    {
        var viewer = await Loaded(new JsonArray(Finding(Violation, "The timer can run while paused", detail: "TLC printed no states")));

        var row = viewer.Findings[0];
        Assert.Equal((false, "TLC printed no states", true), (row.HasSteps, row.Detail, row.HasDetail));
    }

    [Fact]
    public async Task A_violation_with_no_detail_is_a_plain_row()
    {
        var viewer = await Loaded(new JsonArray(Finding(Violation, "The timer can run while paused")));

        Assert.Equal((false, false), (viewer.Findings[0].HasSteps, viewer.Findings[0].HasDetail));
    }

    [Fact]
    public async Task Another_rule_is_never_parsed_for_steps_even_when_its_detail_looks_like_a_trace()
    {
        var viewer = await Loaded(new JsonArray(Finding("BLD-CMD-001", "dotnet test failed", detail: _trace.ReplaceLineEndings("\n"))));

        var row = viewer.Findings[0];
        Assert.Equal((false, true), (row.HasSteps, row.HasDetail));
        Assert.Contains("Full trace:", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Findings_keep_their_order()
    {
        var viewer = await Loaded(new JsonArray(Finding("A-1", "first"), Finding("B-2", "second"), Finding("C-3", "third")));

        Assert.Equal(["first", "second", "third"], viewer.Findings.Select(f => f.Message), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_empty_list_says_no_findings()
    {
        var viewer = await Loaded(new JsonArray());

        Assert.Equal((true, false, false, "No findings"), (viewer.ShowsNone, viewer.ShowsFindings, viewer.ShowsTree, viewer.NoneText));
        Assert.Empty(viewer.Findings);
    }

    [Theory]
    [InlineData("""{"rule":"A-1","message":"not a list"}""")]
    [InlineData("""[{"rule":"A-1"}]""")]
    [InlineData("""[{"message":"no rule"}]""")]
    [InlineData("""[{"rule":"A-1","message":"fine"},"stray"]""")]
    [InlineData("""[{"rule":1,"message":"rule is a number"}]""")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just text\"")]
    public async Task A_body_that_is_not_a_list_of_findings_is_drawn_as_the_payload_tree_so_no_fact_is_lost(string body)
    {
        var viewer = await Loaded(JsonNode.Parse(body)!);

        Assert.Equal((true, false, false), (viewer.ShowsTree, viewer.ShowsFindings, viewer.ShowsNone));
        Assert.Empty(viewer.Findings);
        Assert.Equal(body.StartsWith('"') ? "just text" : string.Empty, viewer.ScalarLine);
        Assert.Equal(!body.StartsWith('"'), viewer.Tree.Count > 0);
    }

    [Fact]
    public async Task A_cut_body_says_so()
    {
        var viewer = await Loaded(new JsonArray(Finding("A-1", "first")), truncated: true);

        Assert.Equal((Labels.PayloadTruncated, true), (viewer.TruncatedNote, viewer.HasTruncatedNote));
    }

    [Fact]
    public async Task Hidden_characters_in_a_finding_are_replaced()
    {
        var viewer = await Loaded(new JsonArray(Finding("A-1", "bad‮name", "a\u0007b.cs", 3, "x‮y")));

        var row = viewer.Findings[0];
        Assert.DoesNotContain('‮', row.Message + row.Meta + row.Detail);
        Assert.DoesNotContain('\u0007', row.Meta);
    }

    [Fact]
    public async Task The_viewer_is_loading_until_the_body_is_read_and_says_it_is_loaded()
    {
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, """{"key":"fact:project","type":"findings","body":[],"truncated":false}""");
        var viewer = new FindingsFactViewer(FindingsRow(), Context());
        Assert.Equal((true, Labels.ReadingFact), (viewer.IsLoading, viewer.LoadingText));

        await viewer.Load(Token);

        Assert.Equal((false, false, string.Empty), (viewer.IsLoading, viewer.HasProblem, viewer.ProblemText));
        Assert.Equal(Labels.PayloadLoaded(viewer.Row.Label), viewer.Voice.Announcement);
    }

    [Fact]
    public async Task With_no_API_the_viewer_says_sindri_is_not_running_and_shows_no_rows()
    {
        var viewer = new FindingsFactViewer(FindingsRow(), new FactViewerContext(_server, new FakeClipboard(), _clock, () => null, "WFI-any", _ => { }));

        await viewer.Load(Token);

        Assert.Equal((Labels.PayloadNotRunning, true, false, false), (viewer.ProblemText, viewer.HasProblem, viewer.ShowsFindings, viewer.ShowsNone));
        Assert.False(viewer.IsLoading);
    }

    [Fact]
    public async Task A_fact_the_engine_does_not_have_shows_the_served_sentence()
    {
        var viewer = new FindingsFactViewer(FindingsRow() with { Key = "fact:unknown" }, Context());

        await viewer.Load(Token);

        var detail = (string)JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", "not-found"))!["detail"]!;
        Assert.Equal((false, detail, false), (viewer.IsLoading, viewer.ProblemText, viewer.ShowsFindings));
        Assert.False(viewer.ShowsTree);
        Assert.False(viewer.ShowsNone);
    }

    [Fact]
    public async Task A_finding_row_reads_its_message_then_where_it_is_as_one_name()
    {
        var viewer = await Loaded(new JsonArray(Finding("A-1", "first", "src/a.cs", 4)));

        Assert.Equal("first, src/a.cs:4 · A-1", viewer.Findings[0].Name);
    }
}
