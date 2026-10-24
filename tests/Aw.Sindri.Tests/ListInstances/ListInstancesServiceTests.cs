using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;
using Microsoft.Extensions.Time.Testing;
using RecipeUrgency = Aw.Sindri.Core.Workflow.Recipes.RecipeUrgency;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>
/// 'workflow list' (engine spec §6.3, API spec §3.8) over a recording port: how the request becomes a typed filter or a raw
/// query, when ended instances are listed, the order of the rows, and the subject match that the search attribute can't do.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ListInstancesServiceTests
{
    private static InstanceView View(string name, string address, RecipeUrgency urgency = RecipeUrgency.Work, string? subjectKey = null, string? label = null) =>
        StatusCases.View(name) with
        {
            Address = InstanceAddress.From(address),
            Urgency = urgency,
            Subject = subjectKey is null ? null : new SubjectRef("file", subjectKey, label ?? subjectKey, Href: null),
        };

    private static async Task<(Result<InstanceList> Result, RecordingInstances Port)> Run(ListInstancesRequest request, params InstanceView[] views)
    {
        var port = new RecordingInstances([.. views.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))]);
        var service = new ListInstancesService(new NoDirectory(), port, new EndedInstanceViews(new FixedReader(views)), new FixedItemViews(), new FixedCaller(current: null), rowBuilder: new InstanceRows(new FakeTimeProvider()));
        return (await service.Handle(request, CancellationToken.None), port);
    }

    [Fact]
    public async Task An_unknown_urgency_is_invalid_input_and_the_port_is_not_asked()
    {
        var (result, port) = await Run(new ListInstancesRequest(Urgency: "urgent"));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(port.Calls);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    public async Task A_root_that_is_not_true_or_false_is_invalid_input(string root)
    {
        var (result, port) = await Run(new ListInstancesRequest(Root: root));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(port.Calls);
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("False")]
    [InlineData("true")]
    public async Task A_root_is_true_or_false_in_any_case(string root)
    {
        var (result, port) = await Run(new ListInstancesRequest(Root: root));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, port.Calls.Count);   // the list, then the root set the counts come from, all and running
    }

    [Fact]
    public async Task A_needs_me_that_is_not_true_or_false_is_invalid_input()
    {
        var (result, _) = await Run(new ListInstancesRequest(NeedsMe: "maybe"));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact]
    public async Task Every_typed_filter_but_the_subject_reaches_the_port_as_the_typed_filter()
    {
        var (result, port) = await Run(new ListInstancesRequest(
            NeedsMe: "true", Recipe: "workflow.step", Urgency: "incident", Subject: "fact:spec", Root: "false"));

        Assert.True(result.IsSuccess);
        var call = port.Calls[0];
        Assert.Equal(new InstanceFilter("workflow.step", State: null, NeedsYou: true, RecipeUrgency.Incident, Item: null, Subject: null, Root: false), call.Filter);
    }

    [Fact]
    public async Task Ended_instances_are_listed_unless_open_is_true()
    {
        var (_, byItem) = await Run(new ListInstancesRequest(Item: ItemKey.From("ITM-1")));
        var (_, byState) = await Run(new ListInstancesRequest(State: "done"));
        var (_, neither) = await Run(new ListInstancesRequest(Recipe: "workflow.step"));
        var (_, open) = await Run(new ListInstancesRequest(Recipe: "workflow.step", Open: "true"));

        Assert.Equal([("ITM-1", true), ("done", true)], [(byItem.Calls[0].Filter!.Item!, byItem.Calls[0].Ended), (byState.Calls[0].Filter!.State!, byState.Calls[0].Ended)]);
        Assert.True(neither.Calls[0].Ended);
        Assert.False(open.Calls[0].Ended);
    }

    [Fact]
    public async Task An_open_of_false_lists_only_the_finished_instances_newest_first()
    {
        var older = View("done", "T-1/step/1") with { Since = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) };
        var newer = View("done", "T-2/step/1") with { Since = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero) };

        var (result, port) = await Run(new ListInstancesRequest(Recipe: "workflow.step", Root: "true", Open: "false"), older, View("desk-task", "T-3/step/1"), newer);

        Assert.Equal(["T-2/step/1", "T-1/step/1"], result.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
        Assert.True(port.Calls[0].Ended);
    }

    [Fact]
    public async Task An_absent_open_lists_the_open_and_the_finished_instances()
    {
        var (result, _) = await Run(new ListInstancesRequest(Recipe: "workflow.step", Root: "true"), View("done", "T-1/step/1"), View("desk-task", "T-2/step/1"));

        Assert.Equal(2, result.Value.Instances.Count);
    }

    private static InstanceView Line(string name, string address) =>
        View(name, address) with { Recipe = View(name, address).Recipe with { Id = RecipeId.From("workflow.line") } };

    private static async Task<(InstanceList List, FixedReader Reader)> RunLines(InstanceView[] all, params InstanceView[] listed)
    {
        var reader = new FixedReader(all);
        var port = new RecordingInstances([.. listed.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))]);
        var service = Service(all, new FixedItemViews(failing: false), caller: null, port: port, reader: reader);
        var result = await service.Handle(new ListInstancesRequest(), CancellationToken.None);
        return (result.Value, reader);
    }

    [Fact]
    public async Task A_root_line_row_reads_its_own_state_as_the_line_stage_without_a_second_read()
    {
        var root = Line("desk-task", "T-1/line/1");

        var (list, reader) = await RunLines([root], root);

        Assert.Equal(root.State, list.Instances[0].LineStage);
        Assert.Equal(["T-1/line/1"], reader.Reads, StringComparer.Ordinal);   // the one read of the row's view; the root is that view
    }

    [Fact]
    public async Task A_child_row_reads_the_state_of_the_line_root_above_it()
    {
        var root = Line("parked-rounds", "T-1/line/1");
        var child = View("desk-task", "T-1/line/1/phase.2");

        var (list, _) = await RunLines([root, child], child);

        Assert.Equal(root.State, list.Instances[0].LineStage);
        Assert.Equal("T-1/line/1", list.Instances[0].RootAddress);
    }

    [Fact]
    public async Task A_row_whose_root_is_not_a_line_instance_has_no_line_stage()
    {
        var root = View("desk-task", "T-1/step/1");
        var child = View("desk-task", "T-1/step/1/phase.2");

        var (list, _) = await RunLines([root, child], root, child);

        Assert.All(list.Instances, static r => Assert.Null(r.LineStage));
    }

    [Fact]
    public async Task Rows_that_share_a_root_read_that_root_once()
    {
        var root = Line("parked-rounds", "T-1/line/1");
        var first = View("desk-task", "T-1/line/1/phase.2");
        var second = View("desk-task", "T-1/line/1/phase.3");

        var (list, reader) = await RunLines([root, first, second], first, second);

        Assert.All(list.Instances, r => Assert.Equal(root.State, r.LineStage));
        Assert.Equal(1, reader.Reads.Count(static a => a == "T-1/line/1"));
    }

    [Fact]
    public async Task A_needs_me_of_false_is_no_filter_and_a_state_still_lists_ended_instances()
    {
        var (_, port) = await Run(new ListInstancesRequest(NeedsMe: "false", State: "parked"));

        var call = port.Calls[0];
        Assert.Equal((new InstanceFilter(State: "parked"), true), (call.Filter, call.Ended));
    }

    [Fact]
    public async Task A_raw_query_alone_is_parenthesised_and_with_typed_filters_follows_them_joined_by_and()
    {
        var (_, alone) = await Run(new ListInstancesRequest(Query: "NeedsYou = true OR State = 'done'"));
        var (_, joined) = await Run(new ListInstancesRequest(Query: "NeedsYou = true OR State = 'done'", Recipe: "workflow.step", Root: "true"));

        Assert.Equal("(NeedsYou = true OR State = 'done')", alone.Calls[0].Query);
        Assert.Equal("Recipe = 'workflow.step' AND (Root = true OR Root IS NULL) AND (NeedsYou = true OR State = 'done')", joined.Calls[0].Query);
    }

    [Fact]
    public async Task Root_false_asks_for_the_children_only()
    {
        var (_, port) = await Run(new ListInstancesRequest(Root: "false"));

        Assert.False(port.Calls[0].Filter!.Root);
        Assert.Contains("Root = false", InstanceFilters.ToQuery(port.Calls[0].Filter!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_typed_list_is_in_the_section_3_8_order_whatever_order_the_port_returns()
    {
        var (result, _) = await Run(
            new ListInstancesRequest(Recipe: "workflow.step"),
            View("done", "T-1/step/1", RecipeUrgency.Incident),
            View("parked-rounds", "T-2/step/1", RecipeUrgency.Incident),
            View("desk-task", "T-3/step/1"),
            View("desk-task", "T-4/step/1", RecipeUrgency.Triage));

        Assert.Equal(["T-4/step/1", "T-3/step/1", "T-2/step/1", "T-1/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_row_label_and_the_sort_key_use_the_same_item_titles()
    {
        static InstanceView OnItem(string item, string address) =>
            View("desk-task", address) with { ItemId = ItemKey.From(item), Subject = new SubjectRef("item", item, item, Href: null) };
        var views = new[] { OnItem("ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/step/1"), OnItem("ITM-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "ITM-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/step/1") };
        var titles = new FixedItemViews(
            failing: false,
            new ItemRowBuilder().WithId(ItemId.From("ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")).Build() with { Title = "Zeta" },
            new ItemRowBuilder().WithId(ItemId.From("ITM-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")).Build() with { Title = "Alpha" });
        var service = new ListInstancesService(
            new NoDirectory(),
            new RecordingInstances([.. views.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))]),
            new EndedInstanceViews(new FixedReader(views)),
            titles,
            new FixedCaller(new CallerContextBuilder().Human().Build()),
            rowBuilder: new InstanceRows(new FakeTimeProvider()));

        var result = await service.Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.Equal(["Alpha", "Zeta"], result.Value.Instances.Select(static r => r.Subject.Label), StringComparer.Ordinal);   // by title; by id it would be Zeta, Alpha
        Assert.Equal(["ITM-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/step/1", "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_raw_query_list_is_in_the_same_order()
    {
        var (result, _) = await Run(
            new ListInstancesRequest(Query: "State = 'produce'"),
            View("done", "T-1/step/1", RecipeUrgency.Incident),
            View("desk-task", "T-3/step/1"),
            View("desk-task", "T-4/step/1", RecipeUrgency.Triage));

        Assert.Equal(["T-4/step/1", "T-3/step/1", "T-1/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Needs_me_is_a_filter_on_the_port_and_the_rows_stay_in_the_api_order()
    {
        var (result, port) = await Run(
            new ListInstancesRequest(NeedsMe: "true"),
            View("desk-task", "T-1/step/1", RecipeUrgency.Incident),
            View("parked-rounds", "T-3/step/1"),
            View("escalated-gate", "T-2/step/1"));

        Assert.True(port.Calls[0].Filter!.NeedsYou);
        Assert.Equal(["T-1/step/1", "T-2/step/1", "T-3/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);   // not parked first; the parked ones by their label
    }

    [Fact]
    public async Task An_instance_that_is_still_starting_is_a_queued_row_beside_the_others_not_a_failed_list()
    {
        var starting = InstanceViews.Starting(InstanceAddress.From("T-9/step/1"), StatusCases.View("desk-task").Recipe, StatusCases.Now, ItemKey.From("T-9"), subject: null);

        var (result, _) = await Run(new ListInstancesRequest(Recipe: "workflow.step"), View("desk-task", "T-3/step/1"), starting, View("taken-over", "T-4/step/1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Instances.Count);
        var row = Assert.Single(result.Value.Instances, static r => r.Address.Value == "T-9/step/1");
        Assert.Equal(("QUEUED", "queued", "starting"), (row.Chip.Word, row.Now, row.State));
        Assert.Contains(result.Value.Instances, static r => r.Chip.Word == "DECISION");
        Assert.Contains(result.Value.Instances, static r => r.Chip.Word == "WITH YOU");
    }

    [Fact]
    public async Task The_rows_are_the_summaries_of_the_listed_views_in_the_api_order()
    {
        var views = new[] { View("desk-task", "T-3/step/1"), View("desk-task", "T-4/step/1", RecipeUrgency.Triage) };

        var (result, _) = await Run(new ListInstancesRequest(Recipe: "workflow.step"), views);

        var expected = new[] { views[1], views[0] }.Select(static v => InstanceSummaries.Of(v, new InstanceSummaryContext(new FakeTimeProvider().GetUtcNow())));
        Assert.Equal(expected.Select(static r => (r.Address, r.State, r.Now, r.Recipe, r.Since)), result.Value.Instances.Select(static r => (r.Address, r.State, r.Now, r.Recipe, r.Since)));
    }

    [Fact]
    public async Task Counts_cover_the_root_set_before_recipe_or_open_narrows()
    {
        var (result, port) = await Run(
            new ListInstancesRequest(Recipe: "workflow.step", Open: "true"),
            View("desk-task", "T-1/step/1"),
            View("parked-rounds", "T-2/step/1"),
            View("taken-over", "T-3/step/1"),
            View("done", "T-4/step/1"));

        Assert.Equal(new ListCounts(Open: 3, NeedsYou: 2, WithYou: 1, Running: 0, Done: 1), result.Value.Counts);
        Assert.Equal(new InstanceFilter(Root: true), port.Calls[1].Filter);   // the second call is the whole root set
        Assert.True(port.Calls[1].Ended);
    }

    [Fact]
    public async Task Open_true_drops_terminal_rows()
    {
        var (result, _) = await Run(new ListInstancesRequest(Open: "true"), View("desk-task", "T-1/step/1"), View("done", "T-2/step/1"));

        Assert.Equal(["T-1/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("")]
    public async Task An_open_that_is_not_true_or_false_is_invalid_input(string open)
    {
        var (result, port) = await Run(new ListInstancesRequest(Open: open));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(port.Calls);
    }

    [Fact]
    public async Task Empty_list_says_No_workflows_are_open()
    {
        var (result, _) = await Run(new ListInstancesRequest(Open: "true"));

        Assert.Equal((InstanceList.EmptyText, 0, new ListCounts(0, 0, 0, 0, 0)), (result.Value.Empty, result.Value.Instances.Count, result.Value.Counts));
        Assert.Equal(new FakeTimeProvider().GetUtcNow(), result.Value.AsOf);
    }

    [Fact]
    public async Task A_subject_matches_the_subject_key_or_a_part_of_its_label_and_is_not_sent_to_the_port()
    {
        var views = new[]
        {
            View("desk-task", "T-1/step/1", subjectKey: "fact:spec", label: "Offline sync spec"),
            View("desk-task", "T-2/step/1", subjectKey: "fact:ticket", label: "APP-12"),
            View("desk-task", "T-3/step/1"),
        };

        var (byKey, port) = await Run(new ListInstancesRequest(Subject: "fact:ticket"), views);
        var (byLabel, _) = await Run(new ListInstancesRequest(Subject: "sync"), views);
        var (none, _) = await Run(new ListInstancesRequest(Subject: "nothing like it"), views);

        Assert.Equal(["T-2/step/1"], byKey.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
        Assert.Equal(["T-1/step/1"], byLabel.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
        Assert.Equal((InstanceList.NoMatchText, 0), (none.Value.Empty, none.Value.Instances.Count));
        Assert.Null(port.Calls[0].Filter!.Subject);
    }

    private const string ItemA = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ItemB = "ITM-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static InstanceView OnItem(string item, string address) =>
        View("desk-task", address) with { ItemId = ItemKey.From(item), Subject = new SubjectRef("item", item, item, Href: null) };

    private static ItemRow Titled(string id, string title, string? repo = null) =>
        new ItemRowBuilder().WithId(ItemId.From(id)).InRepo(repo is null ? null : RepoName.From(repo)).Build() with { Title = title };

    private static ListInstancesService Service(IReadOnlyList<InstanceView> views, FixedItemViews items, CallerContext? caller, string? directoryAddress = null, RecordingInstances? port = null, FixedReader? reader = null) =>
        new(
            directoryAddress is null ? new NoDirectory() : new FixedDirectory(directoryAddress),
            port ?? new RecordingInstances([.. views.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))]),
            new EndedInstanceViews(reader ?? new FixedReader(views)),
            items,
            new FixedCaller(caller),
            rowBuilder: new InstanceRows(new FakeTimeProvider()));

    [Fact]
    public async Task A_subject_filter_matches_the_item_title_the_row_shows_and_still_the_id_as_the_key()
    {
        var views = new[] { OnItem(ItemA, $"{ItemA}/step/1"), OnItem(ItemB, $"{ItemB}/step/1") };
        var items = new FixedItemViews(failing: false, Titled(ItemA, "Offline sync spec"), Titled(ItemB, "Dark mode"));
        var service = Service(views, items, new CallerContextBuilder().Human().Build());

        var byTitle = await service.Handle(new ListInstancesRequest(Subject: "SYNC"), CancellationToken.None);
        var byId = await service.Handle(new ListInstancesRequest(Subject: ItemB), CancellationToken.None);
        var byIdAsLabel = await service.Handle(new ListInstancesRequest(Subject: "ITM-aaaa"), CancellationToken.None);

        Assert.Equal([$"{ItemA}/step/1"], byTitle.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
        Assert.Equal([$"{ItemB}/step/1"], byId.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
        Assert.Empty(byIdAsLabel.Value.Instances);   // the row shows the title, not the id, as its label: one label for display, order and filter
    }

    [Fact]
    public async Task An_instance_with_no_subject_matches_a_subject_filter_by_the_address_its_row_shows()
    {
        var (result, _) = await Run(new ListInstancesRequest(Subject: "T-3/step"), View("desk-task", "T-3/step/1"), View("desk-task", "T-4/step/1"));

        Assert.Equal(["T-3/step/1"], result.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_instance_with_no_input_is_about_its_item_so_the_row_shows_the_item_title_and_the_title_finds_it()
    {
        var noInput = View("desk-task", $"{ItemA}/step/1") with { ItemId = ItemKey.From(ItemA), Subject = null };
        var other = View("desk-task", $"{ItemB}/step/1") with { ItemId = ItemKey.From(ItemB), Subject = null };
        var items = new FixedItemViews(failing: false, Titled(ItemA, "Pager storm"), Titled(ItemB, "Dark mode"));
        var service = Service([noInput, other], items, new CallerContextBuilder().Human().Build());

        var byTitle = await service.Handle(new ListInstancesRequest(Subject: "pager"), CancellationToken.None);
        var byId = await service.Handle(new ListInstancesRequest(Subject: ItemA), CancellationToken.None);

        var row = Assert.Single(byTitle.Value.Instances);
        Assert.Equal(new SubjectRef("item", ItemA, "Pager storm", Href: null), row.Subject);
        Assert.Equal([$"{ItemA}/step/1"], byId.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_title_view_that_cannot_be_read_leaves_the_list_working_with_the_ids_as_labels_in_id_order()
    {
        var views = new[] { OnItem(ItemB, $"{ItemB}/step/1"), OnItem(ItemA, $"{ItemA}/step/1") };
        var service = Service(views, new FixedItemViews(failing: true), new CallerContextBuilder().Human().Build());

        var result = await service.Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([ItemA, ItemB], result.Value.Instances.Select(static r => r.Subject.Label), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_session_lists_every_repos_instances_rows_and_counts()
    {
        var views = new[] { OnItem(ItemA, $"{ItemA}/step/1"), OnItem(ItemB, $"{ItemB}/step/1") };
        var items = new FixedItemViews(failing: false, Titled(ItemA, "Mine", repo: "alpha"), Titled(ItemB, "Theirs", repo: "beta"));
        var service = Service(views, items, new CallerContextBuilder().Session().InRepo("alpha").Build());

        var result = await service.Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.Equal(["Mine", "Theirs"], result.Value.Instances.Select(static r => r.Subject.Label).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(2, result.Value.Counts.Open);
    }

    [Fact]
    public async Task A_human_lists_every_repos_instances()
    {
        var views = new[] { OnItem(ItemA, $"{ItemA}/step/1"), OnItem(ItemB, $"{ItemB}/step/1") };
        var service = Service(views, new FixedItemViews(failing: false), new CallerContextBuilder().Human().Build());

        var result = await service.Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.Equal(2, result.Value.Instances.Count);
    }

    [Fact]
    public async Task A_session_looking_up_an_address_in_another_repo_is_served()
    {
        var views = new[] { OnItem(ItemA, $"{ItemA}/step/1"), OnItem(ItemB, $"{ItemB}/step/1") };
        var session = new CallerContextBuilder().Session().InRepo("alpha").Build();

        var theirs = await Service(views, new FixedItemViews(failing: false), session, directoryAddress: $"{ItemB}/step/1").Handle(new ListInstancesRequest(Address: $"{ItemB}/step/1"), CancellationToken.None);
        var mine = await Service(views, new FixedItemViews(failing: false), session, directoryAddress: $"{ItemA}/step/1").Handle(new ListInstancesRequest(Address: $"{ItemA}/step/1"), CancellationToken.None);

        Assert.Equal([$"{ItemB}/step/1"], theirs.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
        Assert.Equal([$"{ItemA}/step/1"], mine.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Needs_me_lists_a_parent_mirroring_a_listed_child_once_under_the_child()
    {
        var parent = StatusCases.View("parked-no-progress-child");
        var child = StatusCases.View("parked-rounds") with { Address = InstanceAddress.From(parent.Address.Value + "/step.4") };
        var service = Service([parent, child], new FixedItemViews(failing: false), new CallerContextBuilder().Human().Build());

        var result = await service.Handle(new ListInstancesRequest(NeedsMe: "true"), CancellationToken.None);

        Assert.Equal([child.Address.Value], result.Value.Instances.Select(static r => r.Address.Value), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("needsMe", "nothing needs you")]
    [InlineData("open", "No workflows are open.")]
    [InlineData("neither", "No workflows match.")]
    public async Task An_empty_list_says_the_sentence_that_matches_its_filter(string filter, string sentence)
    {
        var request = filter switch
        {
            "needsMe" => new ListInstancesRequest(NeedsMe: "true", Open: "true"),
            "open" => new ListInstancesRequest(Open: "true"),
            _ => new ListInstancesRequest(State: "produce"),
        };

        var result = await Service([], new FixedItemViews(failing: false), new CallerContextBuilder().Human().Build()).Handle(request, CancellationToken.None);

        Assert.Equal(sentence, result.Value.Empty);
    }

    [Fact]
    public async Task The_counts_ask_only_running_roots_for_their_view_and_count_the_ended_ones_from_the_listing()
    {
        var running = View("desk-task", "T-1/step/1");
        var ended = new[] { View("done", "T-2/step/1"), View("done", "T-3/step/1"), View("done", "T-4/step/1") };
        InstanceView[] all = [running, .. ended];
        var reader = new FixedReader(all);
        var port = new RecordingInstances([.. all.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))], running: [running.Address.Value]);
        var service = Service(all, new FixedItemViews(failing: false), new CallerContextBuilder().Human().Build(), port: port, reader: reader);

        var result = await service.Handle(new ListInstancesRequest(Open: "true"), CancellationToken.None);

        Assert.Equal((1, 3), (result.Value.Counts.Open, result.Value.Counts.Done));
        Assert.Equal(["T-1/step/1"], reader.Reads, StringComparer.Ordinal);   // one Query, for the one running root
    }

    [Fact]
    public async Task A_payload_subject_with_no_title_of_its_own_is_labelled_by_the_item_title_not_the_fact_key()
    {
        var view = OnItem(ItemA, $"{ItemA}/scoping/1") with { Subject = new SubjectRef("payload", "fact:scopeInput", "fact:scopeInput", Href: null) };
        var service = Service([view], new FixedItemViews(failing: false, Titled(ItemA, "Toy scoping check")), new CallerContextBuilder().Human().Build());

        var result = await service.Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.Equal(new SubjectRef("payload", "fact:scopeInput", "Toy scoping check", Href: null), Assert.Single(result.Value.Instances).Subject);
    }

    [Fact]
    public async Task A_payload_subject_keeps_its_own_title_and_falls_back_to_the_key_only_when_there_is_no_title_at_all()
    {
        var titled = OnItem(ItemA, $"{ItemA}/scoping/1") with { Subject = new SubjectRef("payload", "fact:scopeInput", "Offline sync brief", Href: null) };
        var bare = OnItem(ItemB, $"{ItemB}/scoping/1") with { Subject = new SubjectRef("payload", "fact:scopeInput", "fact:scopeInput", Href: null) };
        var service = Service([titled, bare], new FixedItemViews(failing: false, Titled(ItemA, "Item title")), new CallerContextBuilder().Human().Build());

        var result = await service.Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.Equal(["Offline sync brief", "fact:scopeInput"], result.Value.Instances.OrderBy(static r => r.Address.Value, StringComparer.Ordinal).Select(static r => r.Subject.Label), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_single_address_path_shows_the_title_too()
    {
        var views = new[] { OnItem(ItemA, $"{ItemA}/step/1") };
        var service = Service(views, new FixedItemViews(failing: false, Titled(ItemA, "Offline sync spec")), new CallerContextBuilder().Human().Build(), directoryAddress: $"{ItemA}/step/1");

        var result = await service.Handle(new ListInstancesRequest(Address: $"{ItemA}/step/1"), CancellationToken.None);

        Assert.Equal("Offline sync spec", Assert.Single(result.Value.Instances).Subject.Label);
    }

    [Fact]
    public async Task The_titles_are_read_by_key_once_per_distinct_item_and_not_at_all_when_no_view_has_an_item()
    {
        var views = new[] { OnItem(ItemA, $"{ItemA}/step/1"), OnItem(ItemA, $"{ItemA}/step/2"), OnItem(ItemB, $"{ItemB}/step/1") };
        var items = new FixedItemViews(failing: false, Titled(ItemA, "A"), Titled(ItemB, "B"));
        var withItems = await Service(views, items, new CallerContextBuilder().Human().Build()).Handle(new ListInstancesRequest(), CancellationToken.None);
        var none = new FixedItemViews(failing: false);
        var withoutItems = await Service([View("desk-task", "T-3/step/1") with { ItemId = null }], none, new CallerContextBuilder().Human().Build()).Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.True(withItems.IsSuccess && withoutItems.IsSuccess);
        Assert.Equal([ItemA, ItemB], items.Reads);
        Assert.Empty(none.Reads);
    }

    [Fact]
    public async Task An_ended_instance_is_read_once_across_lists_and_a_running_one_every_time()
    {
        var started = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var ended = View("done", "T-1/step/1");
        var running = View("desk-task", "T-2/step/1");
        Assert.True(ended.Outcome is not null && running.Outcome is null);
        var views = new[] { ended, running };
        var rows = views.Select(v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false, StartedAt: started)).ToList();
        var reader = new FixedReader(views);
        var service = Service(views, new FixedItemViews(), caller: null, port: new RecordingInstances(rows), reader: reader);

        _ = await service.Handle(new ListInstancesRequest(), CancellationToken.None);
        reader.Reads.Clear();
        var again = await service.Handle(new ListInstancesRequest(), CancellationToken.None);

        Assert.Equal(["T-2/step/1"], reader.Reads, StringComparer.Ordinal);
        Assert.Equal(["T-1/step/1", "T-2/step/1"], again.Value.Instances.Select(static r => r.Address.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
