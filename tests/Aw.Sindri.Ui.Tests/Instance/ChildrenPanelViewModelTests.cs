using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;
using ApiGlyph = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The children panel (screens §4.2, U8 T6): children grouped under the state that spawned them and by workstream, each row's words and actions the served ones, nothing sent.</summary>
[Lane(TestLane.Unit)]
public sealed class ChildrenPanelViewModelTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private ChildrenPanelViewModel Panel(string scenario) => new(FixtureInstances.Detail(scenario), _clipboard, _clock);

    private static IEnumerable<ChildRow> Rows(string scenario) => FixtureInstances.Detail(scenario).Children.SelectMany(FixtureInstances.ChildRows);

    private static ChildRow Child(string scenario, string address) => Rows(scenario).Single(r => r.Address.EndsWith(address, StringComparison.Ordinal));

    private ChildRowViewModel RowOf(string scenario, string address) => new(Child(scenario, address), _clipboard, _clock);

    // A served row edited by one member (what the goldens don't carry yet: a round, an agent, no actions).
    private ChildRowViewModel Edited(string scenario, string address, Action<JsonObject> edit)
    {
        var node = (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(Child(scenario, address), SindriApiClientJson))!; // why: a row is an object
        edit(node);
        return new ChildRowViewModel(FixtureInstances.Parse<ChildRow>(node), _clipboard, _clock);
    }

    private static System.Text.Json.JsonSerializerOptions SindriApiClientJson => SindriApiClient.Json;

    [Theory]
    [InlineData("project-build")]
    [InlineData("project-many-children")]
    public void Groups_follow_the_served_order_under_their_state_name(string scenario)
    {
        var served = FixtureInstances.Detail(scenario).Children;

        var panel = Panel(scenario);

        Assert.Equal(served.Select(g => g.State), panel.Groups.Select(g => g.State), StringComparer.Ordinal);
        Assert.All(panel.Groups, g => Assert.StartsWith(g.State, g.Header, StringComparison.Ordinal));
        Assert.Equal(served.Sum(g => g.Total), int.Parse(panel.Heading.Split(' ')[^1], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Workstreams_subgroup_in_the_served_order()
    {
        var served = FixtureInstances.Detail("project-build").Children.Single();

        var group = Panel("project-build").Groups.Single();

        Assert.True(group.HasStreams);
        Assert.Equal(served.Workstreams.Select(w => w.Stream), group.Streams.Select(s => s.Stream), StringComparer.Ordinal);
        Assert.Equal(served.Workstreams.Select(w => w.Label), group.Streams.Select(s => s.Label), StringComparer.Ordinal);
        Assert.All(group.Streams.Zip(served.Workstreams), pair =>
        {
            Assert.Contains(pair.Second.Stream, pair.First.Title, StringComparison.Ordinal);
            Assert.Equal(pair.Second.Rows.Select(r => r.Address), pair.First.Rows.Select(r => r.Address), StringComparer.Ordinal);
        });
    }

    [Fact]
    public void Rows_without_workstreams_use_rows()
    {
        var served = FixtureInstances.Detail("project-many-children").Children.Single();

        var group = Panel("project-many-children").Groups.Single();

        Assert.False(group.HasStreams);
        Assert.Empty(group.Streams);
        Assert.Equal(served.Rows.Select(r => r.Address), group.Rows.Select(r => r.Address), StringComparer.Ordinal);
    }

    [Fact]
    public void Row_shows_chip_title_state_round_agent_age_and_its_served_actions()
    {
        Assert.All(Rows("project-build"), served =>
        {
            var row = new ChildRowViewModel(served, _clipboard, _clock);
            Assert.Equal((served.Id, served.Address, served.Title, served.State, served.Chip), (row.Id, row.Address, row.Title, row.State, row.Chip));
            Assert.Equal(Labels.RowAge(TimeSpan.FromSeconds(served.AgeSeconds)), row.Age);
            Assert.Equal((string.Empty, string.Empty), (row.RoundText, row.Agent));   // null before engine P7/P8: blank, no word invented
            Assert.Equal(served.Actions.Contains("OpenItem", StringComparer.Ordinal), row.CanOpenItem);
        });

        var running = Edited("project-build", "step.5", n =>
        {
            n["round"] = new JsonObject { ["n"] = 1, ["max"] = 3 };
            n["agent"] = "an agent";
        });
        Assert.Equal(("round 1 of 3", "an agent"), (running.RoundText, running.Agent));
        Assert.Contains("round 1 of 3", running.Meta, StringComparison.Ordinal);
        Assert.Contains("an agent", running.Meta, StringComparison.Ordinal);

        var bare = Edited("project-build", "step.5", n => n["actions"] = new JsonArray());
        Assert.Equal((false, false), (bare.CanOpenItem, bare.HasActions));

        var takeover = RowOf("project-build", "step.5");
        Assert.Equal([Labels.Action("ApplyCommand:takeover")], takeover.Actions.Select(a => a.Label), StringComparer.Ordinal);
        Assert.Contains(takeover.Address, takeover.Actions[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Done_rows_offer_Open_only()
    {
        var done = Rows("project-build").Where(r => r.Glyph == ApiGlyph.Done).ToList();

        Assert.NotEmpty(done);
        Assert.All(done, served =>
        {
            var row = new ChildRowViewModel(served, _clipboard, _clock);
            Assert.Equal((true, true, false, string.Empty), (row.IsDone, row.CanOpenItem, row.HasActions, row.LaunchChip));
        });
    }

    [Fact]
    public void Mirrored_park_shows_on_its_row()
    {
        var parked = Rows("project-build").Single(r => r.MirroredPark is not null);

        var row = new ChildRowViewModel(parked, _clipboard, _clock, Catalog(_ => { }));

        Assert.Contains(parked.MirroredPark!.Reason, row.MirroredText, StringComparison.Ordinal);
        Assert.Contains(Labels.MirroredHere, row.MirroredText, StringComparison.Ordinal);
        Assert.All(Rows("project-build").Where(r => r.MirroredPark is null), r => Assert.Equal(string.Empty, new ChildRowViewModel(r, _clipboard, _clock).MirroredText));
        // the grant the parked child serves is the lock button, and the takeover a chip that says the send is not here yet
        Assert.Equal(parked.Actions.Count - 1, row.Actions.Count);
        _ = Assert.Single(row.Actions, a => a.Protected is not null);
        Assert.Contains(row.Actions, a => a.PendingChip == Labels.AvailableWithU8b);
        Assert.All(row.Actions.Where(a => a.IsPending), a => Assert.Contains(a.PendingChip, a.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void More_shows_N_more_and_Open_all_in_Work_unfiltered()
    {
        var served = FixtureInstances.Detail("project-many-children").Children.Single();
        var panel = Panel("project-many-children");
        var asked = 0;
        panel.WorkRequested += (_, _) => asked++;

        var group = panel.Groups.Single();
        group.OpenAllInWork();

        Assert.Equal((true, served.More, 1), (group.HasMore, group.More, asked));
        Assert.Contains(served.More.ToString(System.Globalization.CultureInfo.InvariantCulture), group.MoreText, StringComparison.Ordinal);
        Assert.Contains(WorkflowNames.Of(served.Recipe, displayName: null), group.OpenAllName, StringComparison.Ordinal);
        Assert.False(Panel("project-build").Groups.Single().HasMore);
    }

    [Fact]
    public void Row_Enter_opens_the_child_frame_by_id_stacked_with_Back()
    {
        var panel = Panel("project-build");
        var opened = new List<(string Address, string? Id)>();
        panel.Opened += (_, e) => opened.Add((e.Address, e.Id));
        var row = panel.Groups.Single().Streams[0].Rows[0];

        row.OpenFrame();

        Assert.Equal([(row.Address, row.Id)], opened);
        Assert.Contains(row.Address, row.OpenFrameName, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_navigates_to_the_item_sheet_and_OpenItem_launch_stays_pending_P10a()
    {
        var panel = Panel("project-build");
        var items = new List<string>();
        panel.ItemRequested += (_, e) => items.Add(e.ItemId);
        var active = panel.Groups.Single().Streams.SelectMany(s => s.Rows).First(r => !r.IsDone);

        active.OpenItem();

        Assert.Equal([active.ItemId], items);
        Assert.Equal(Labels.AvailableWithP10a, active.LaunchChip);
        Assert.Contains(active.ItemId, active.OpenName, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_reads_the_recipe_declares_no_children()
    {
        var panel = Panel("dev-desk-round-trip");

        Assert.DoesNotContain(FixtureInstances.Detail("dev-desk-round-trip").States, s => s.Child);
        Assert.Equal((false, Labels.NoChildren), (panel.HasGroups, panel.EmptyText));
        Assert.Empty(panel.Groups);
    }

    [Fact]
    public void Empty_reads_no_children_yet_when_a_state_of_the_recipe_marks_children()
    {
        var panel = Panel("incident-loop");

        Assert.Contains(FixtureInstances.Detail("incident-loop").States, s => s.Child);
        Assert.Equal((false, Labels.NoChildrenYet), (panel.HasGroups, panel.EmptyText));
    }

    [Fact]
    public void Group_header_names_the_workflow_in_plain_words_never_its_id()
    {
        var served = FixtureInstances.Detail("project-build").Children.Single();

        var group = Panel("project-build").Groups.Single();

        Assert.Equal(served.Recipe, group.Recipe);
        Assert.DoesNotContain(served.Recipe, group.Header, StringComparison.Ordinal);
        Assert.DoesNotContain(served.Recipe, group.OpenAllName, StringComparison.Ordinal);
        Assert.Contains(WorkflowNames.Of(served.Recipe, displayName: null), group.Header, StringComparison.Ordinal);
        Assert.Contains(served.Done.ToString(System.Globalization.CultureInfo.InvariantCulture), group.Counts, StringComparison.Ordinal);
        Assert.Contains(served.Total.ToString(System.Globalization.CultureInfo.InvariantCulture), group.Counts, StringComparison.Ordinal);
    }

    [Fact]
    public void Group_header_shows_the_served_display_name()
    {
        var served = FixtureInstances.Detail("project-build").Children.Single() with { DisplayName = "Build and ship a change" };
        var detail = FixtureInstances.Detail("project-build") with { Children = [served] };

        var group = new ChildrenPanelViewModel(detail, _clipboard, _clock).Groups.Single();

        Assert.Contains("Build and ship a change", group.Header, StringComparison.Ordinal);
        Assert.Contains("Build and ship a change", group.OpenAllName, StringComparison.Ordinal);
        Assert.DoesNotContain(served.Recipe, group.Header + group.OpenAllName, StringComparison.Ordinal);
    }

    // The catalog the API serves, with the rows a test reclassifies.
    private CatalogViewModel Catalog(Action<JsonArray> edit)
    {
        var node = GoldenFiles.Node("commands", "catalog");
        edit(node["rows"]!.AsArray());
        var catalog = new CatalogViewModel(_clipboard, _clock);
        catalog.Show(new Uri("http://127.0.0.1:7410/"), new FakeSindriApi(), FixtureInstances.Parse<CommandList>(node));
        return catalog;
    }

    private static void Edit(JsonArray rows, string action, string member, string value) =>
        rows.First(r => string.Equals((string)r!["action"]!, action, StringComparison.Ordinal))![member] = value;

    [Fact]
    public void The_catalog_decides_which_child_actions_are_protected_and_what_their_chip_says()
    {
        var parked = Rows("project-build").Single(r => r.MirroredPark is not null);
        var served = Catalog(_ => { });
        var reclassified = Catalog(rows => Edit(rows, "ApplyCommand:answer-grant", "class", "desk"));

        var before = new ChildRowViewModel(parked, _clipboard, _clock, served);
        var after = new ChildRowViewModel(parked, _clipboard, _clock, reclassified);

        Assert.Equal(parked.Actions.Count - 1, before.Actions.Count);
        var grant = Labels.Action("ApplyCommand:answer-grant");
        var takeover = Labels.Action("ApplyCommand:takeover");
        Assert.NotNull(before.Actions.Single(a => a.Label == grant).Protected);
        Assert.Null(before.Actions.Single(a => a.Label == takeover).Protected);
        Assert.Null(after.Actions.Single(a => a.Label == grant).Protected);
        Assert.Equal(Labels.AvailableWithU8b, before.Actions.Single(a => a.Label == takeover).PendingChip);
        Assert.Equal(Labels.AvailableWithU8b, after.Actions.Single(a => a.Label == takeover).PendingChip);   // the catalog's ui word is not read: a send is not in the app yet
    }
}
