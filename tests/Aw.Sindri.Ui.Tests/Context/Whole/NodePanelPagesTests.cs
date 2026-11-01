using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>An action's and a page's node panel: page, concepts, why and code (pages spec §3.4, #1701).</summary>
[Lane(TestLane.Unit)]
public sealed class NodePanelPagesTests
{
    private const string Page = "surface:screen/schedule-week";
    private const string Submit = "action:schedule/submit";
    private const string Approve = "action:schedule/approve";
    private const string Plan = "feature:planning.schedule";
    private const string Approval = "feature:planning.approval";
    private const string File = "code:src/SubmitButton.tsx";
    private const string PageFile = "code:src/ScheduleWeek.tsx";
    private const string Feature = "feature:tabs.tab-manager";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NodeEdge Edge(string from, string type, string to) => new(from, type, to, "test", Confirmed: true);

    private static NodeView View(string key, string kind, NodeEdge[] outs, NodeEdge[] ins) =>
        new("v1", new NodeInfo(key, kind, key, "test", Confirmed: true, "high", Confidence: 1.0), Aliases: [], Out: outs, In: ins);

    // The map: two actions on one page; concepts, code and the page's own code.
    private static readonly Dictionary<string, NodeView> _nodes = new(StringComparer.Ordinal)
    {
        [Submit] = View(Submit, "action", [Edge(Submit, "on", Page), Edge(Submit, "expresses", Plan), Edge(Submit, "expresses", Approval)], [Edge(File, "implements", Submit)]),
        [Approve] = View(Approve, "action", [Edge(Approve, "on", Page), Edge(Approve, "expresses", Approval)], []),
        [Page] = View(Page, "surface", [], [Edge(Submit, "on", Page), Edge(Approve, "on", Page), Edge(PageFile, "implements", Page)]),
        [Feature] = View(Feature, "feature", [Edge(Feature, "touches", Page)], []),
    };

    private static LensesView Lens(int decisions, int records)
    {
        var node = GoldenFiles.Node("context-lenses", "map-tabs");
        var intent = node["bands"]!["intent"]!;
        intent["decisions"] = new JsonArray([.. intent["decisions"]!.AsArray().Take(decisions).Select(d => d!.DeepClone())]);
        intent["records"] = new JsonArray([.. intent["records"]!.AsArray().Take(records).Select(r => r!.DeepClone())]);
        var read = AwJson.Deserialize<LensesView>(Encoding.UTF8.GetBytes(node.ToJsonString()), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        return read.Value;
    }

    private static (NodePanelViewModel Panel, List<string> Jumps, List<string> Anchors) Panel(Func<string, Task<Result<LensesView>?>>? why = null, Dictionary<string, NodeView>? nodes = null)
    {
        List<string> jumps = [];
        List<string> anchors = [];
        var served = nodes ?? _nodes;
        var panel = new NodePanelViewModel(_ => null, jumps.Add)
        {
            Loader = (key, _) => Task.FromResult<Result<NodeView>?>(served.TryGetValue(key, out var v) ? Results.Success(v) : Results.Failure<NodeView>(new Error("SND-CONTEXT-001", ErrorKind.NotFound, "x"))),
            WhyLoader = (key, _) =>
            {
                anchors.Add(key);
                return why is null ? Task.FromResult<Result<LensesView>?>(Results.Success(Lens(decisions: 1, records: 1))) : why(key);
            },
        };
        return (panel, jumps, anchors);
    }

    private static NodeSectionViewModel Section(NodePanelViewModel panel, string heading) => panel.Sections.Single(s => string.Equals(s.Heading, heading, StringComparison.Ordinal));

    private static string[] Keys(NodeSectionViewModel section) => [.. section.Rows.Select(r => r.Key)];

    [Fact]
    public async Task An_action_panel_shows_its_page_two_concepts_a_decision_and_a_record_and_a_code_file()
    {
        var (panel, jumps, anchors) = Panel();

        await panel.Open(Submit, Token);

        Assert.Equal(["Page", "Relates to", "Why", "Code"], panel.Sections.Select(s => s.Heading), StringComparer.Ordinal);
        Assert.Equal([Page], Keys(Section(panel, "Page")));
        Assert.Equal([Plan, Approval], Keys(Section(panel, "Relates to")));
        var why = Section(panel, "Why");
        Assert.Equal(2, why.Rows.Count);
        Assert.Contains(why.Rows, r => r.Key.StartsWith("decision:", StringComparison.Ordinal));
        Assert.Contains(why.Rows, r => r.Key.StartsWith("record:", StringComparison.Ordinal));
        var code = Section(panel, "Code");
        Assert.Equal([File], Keys(code));
        Assert.Equal("Code found", code.Mark);
        Assert.Equal([Submit], anchors);
        Section(panel, "Page").Rows[0].Jump();
        Assert.Equal([Page], jumps);
    }

    [Fact]
    public async Task An_action_with_no_concept_shows_the_empty_Relates_to_line()
    {
        var (panel, _, _) = Panel(nodes: new Dictionary<string, NodeView>(_nodes, StringComparer.Ordinal) { [Submit] = View(Submit, "action", [Edge(Submit, "on", Page)], []) });

        await panel.Open(Submit, Token);

        var section = Section(panel, "Relates to");
        Assert.Empty(section.Rows);
        Assert.True(section.IsEmpty);
        Assert.Equal("Not linked to a concept yet. Edit page to pick one.", section.EmptyText);
    }

    [Fact]
    public async Task An_action_with_no_implements_shows_No_code_found_yet()
    {
        var (panel, _, _) = Panel();

        await panel.Open(Approve, Token);

        var section = Section(panel, "Code");
        Assert.Empty(section.Rows);
        Assert.True(section.IsEmpty);
        Assert.Equal("No code found yet", section.EmptyText);
    }

    [Fact]
    public async Task An_action_with_no_Why_items_shows_the_empty_Why_line()
    {
        var (panel, _, _) = Panel(why: _ => Task.FromResult<Result<LensesView>?>(Results.Success(Lens(decisions: 0, records: 0))));

        await panel.Open(Submit, Token);

        var section = Section(panel, "Why");
        Assert.Empty(section.Rows);
        Assert.True(section.IsEmpty);
        Assert.Equal("No decisions or records about this yet.", section.EmptyText);
    }

    [Fact]
    public async Task A_failed_lens_read_shows_the_Why_error_and_Relates_to_still_shows()
    {
        var (panel, _, _) = Panel(why: _ => Task.FromResult<Result<LensesView>?>(Results.Failure<LensesView>(new Error("SND-CONTEXT-001", ErrorKind.Unavailable, "x"))));

        await panel.Open(Submit, Token);

        var why = Section(panel, "Why");
        Assert.True(why.IsFailed);
        Assert.False(why.IsEmpty);
        Assert.Equal(panel.FailedText, why.FailedText);
        Assert.Equal([Plan, Approval], Keys(Section(panel, "Relates to")));
        Assert.Equal([File], Keys(Section(panel, "Code")));
        Assert.False(panel.IsFailed);
    }

    [Fact]
    public async Task A_page_panel_shows_its_actions_the_union_of_their_concepts_and_its_code()
    {
        var (panel, _, anchors) = Panel();

        await panel.Open(Page, Token);

        Assert.Equal(["Actions", "Relates to", "Why", "Code"], panel.Sections.Select(s => s.Heading), StringComparer.Ordinal);
        Assert.Equal([Submit, Approve], Keys(Section(panel, "Actions")).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal([Approval, Plan], Keys(Section(panel, "Relates to")).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal([PageFile], Keys(Section(panel, "Code")));
        Assert.Equal([Page], anchors);
    }

    [Fact]
    public async Task A_page_with_no_actions_shows_both_page_empties()
    {
        var (panel, _, _) = Panel(nodes: new Dictionary<string, NodeView>(_nodes, StringComparer.Ordinal) { [Page] = View(Page, "surface", [], []) });

        await panel.Open(Page, Token);

        Assert.Equal("No actions on this page yet. Edit page to add one.", Section(panel, "Actions").EmptyText);
        Assert.True(Section(panel, "Actions").IsEmpty);
        Assert.Equal("None of its actions relates to a concept yet.", Section(panel, "Relates to").EmptyText);
        Assert.True(Section(panel, "Relates to").IsEmpty);
    }

    [Fact]
    public async Task A_plain_feature_node_shows_none_of_the_new_sections()
    {
        var (panel, _, anchors) = Panel();

        await panel.Open(Feature, Token);

        Assert.Empty(panel.Sections);
        Assert.Empty(anchors);
        Assert.True(panel.HasGroups);
    }

    [Fact]
    public async Task Opening_another_node_mid_lens_read_drops_the_stale_Why_answer()
    {
        var gate = new TaskCompletionSource<Result<LensesView>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (panel, _, _) = Panel(why: key => string.Equals(key, Submit, StringComparison.Ordinal) ? gate.Task : Task.FromResult<Result<LensesView>?>(Results.Success(Lens(decisions: 0, records: 0))));

        var first = panel.Open(Submit, Token);
        await panel.Open(Approve, Token);
        gate.SetResult(Results.Success(Lens(decisions: 2, records: 1)));
        await first;

        Assert.Equal(Approve, panel.Key);
        Assert.Empty(Section(panel, "Why").Rows);
        Assert.True(Section(panel, "Why").IsEmpty);
    }
}
