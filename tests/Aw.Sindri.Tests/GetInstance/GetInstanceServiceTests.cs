using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>'GetInstance' serves the API's <see cref="InstanceDetail"/> (API spec §3.8): its title, parent, counts and status, and the session repo scope.</summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceServiceTests
{
    private const string ItemA = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static GetInstanceService Service(SomeInstances instances, DetailViews? views = null, FixedRepos? repos = null, CallerContext? caller = null, StepRunEvents? stepRuns = null, StepRunRecipes? recipes = null) =>
        new(instances, instances, views ?? new DetailViews(), repos ?? new FixedRepos(), new ListInstances.FixedCaller(caller ?? new CallerContextBuilder().Human().Build()), new FakeTimeProvider(StatusCases.Now), new StepRunReader(stepRuns ?? new StepRunEvents(), recipes ?? new StepRunRecipes()));

    [Fact]
    public async Task Serves_the_detail_with_title_parent_and_counts()
    {
        var parent = StatusCases.View("desk-task") with { Address = InstanceAddress.From("T-1/step/1"), Id = WorkflowInstanceId.Of(InstanceAddress.From("T-1/step/1")) };
        var child = StatusCases.View("done") with { Address = InstanceAddress.From("T-1/step/1/step.4"), Id = WorkflowInstanceId.Of(InstanceAddress.From("T-1/step/1/step.4")), ParentId = parent.Id, ItemId = ItemKey.From(ItemA) };
        var item = new ItemRowBuilder().WithId(ItemId.From(ItemA)).Build() with { Title = "Offline sync" };
        var service = Service(new SomeInstances(parent, child), new DetailViews(item, timeline: 7, decisions: 2, requests: 1));

        var result = await service.Handle(new GetInstanceRequest(child.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var detail = result.Value;
        Assert.Equal(("T-1/step/1/step.4", "Offline sync", new ViewCounts(7, 2, 1, Context: null), StatusCases.Now), (detail.Address.Value, detail.Title, detail.Counts, detail.AsOf));
        Assert.Equal(("T-1/step/1", parent.Id), (detail.Parent!.Address.Value, detail.Parent.Id));
    }

    [Fact]
    public async Task Serves_the_step_runs_folded_from_the_instances_ledger_events()
    {
        var view = StatusCases.View("done");
        var events = new StepRunEvents(StepRunFixtures.Started("produce", "1.1"), StepRunFixtures.Completed("produce", "checks", "1.1", "done", tokens: 1_200, minutes: 5));

        var result = await Service(new SomeInstances(view), stepRuns: events).Handle(new GetInstanceRequest(view.Address.Value), CancellationToken.None);

        var run = Assert.Single(result.Value.StepRuns);
        Assert.Equal(("1.1", "produce", StepRunState.Passed, 1_200, TimeSpan.FromMinutes(5).TotalMilliseconds), (run.Token, run.Step, run.State, run.Tokens, (double)run.DurationMs!));
        Assert.Equal(view.Address, events.Asked);
    }

    [Fact]
    public async Task An_instance_with_no_events_serves_no_step_runs()
    {
        var view = StatusCases.View("done");

        var result = await Service(new SomeInstances(view)).Handle(new GetInstanceRequest(view.Address.Value), CancellationToken.None);

        Assert.Empty(result.Value.StepRuns);
    }

    [Fact]
    public async Task A_ledger_that_cannot_be_read_fails_the_read_with_its_error()
    {
        var view = StatusCases.View("done");

        var result = await Service(new SomeInstances(view), stepRuns: StepRunEvents.Failing()).Handle(new GetInstanceRequest(view.Address.Value), CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task A_root_instance_with_no_item_row_is_titled_by_its_address_and_has_no_parent()
    {
        var view = StatusCases.View("done");
        var result = await Service(new SomeInstances(view)).Handle(new GetInstanceRequest(view.Address.Value), CancellationToken.None);

        Assert.Equal((view.Address.Value, null), (result.Value.Title, result.Value.Parent));
    }

    [Fact]
    public async Task Session_caller_in_another_repo_gets_SND_ITEM_404()
    {
        var view = StatusCases.View("done") with { ItemId = ItemKey.From(ItemA) };
        var repos = new FixedRepos((ItemA, "beta"));
        var instances = new SomeInstances(view);

        var other = await Service(instances, repos: repos, caller: new CallerContextBuilder().Session().InRepo("alpha").Build()).Handle(new GetInstanceRequest(view.Id.Value), CancellationToken.None);
        var own = await Service(instances, repos: repos, caller: new CallerContextBuilder().Session().InRepo("beta").Build()).Handle(new GetInstanceRequest(view.Id.Value), CancellationToken.None);
        var human = await Service(instances, repos: repos).Handle(new GetInstanceRequest(view.Id.Value), CancellationToken.None);

        Assert.Equal(SindriErrors.ItemNotFound.Code, other.Error.Code);
        Assert.True(own.IsSuccess);
        Assert.True(human.IsSuccess);
    }

    [Fact]
    public async Task An_unknown_instance_and_another_repos_answer_the_same_not_found()
    {
        var view = StatusCases.View("done") with { ItemId = ItemKey.From(ItemA) };
        var instances = new SomeInstances(view);
        var unknown = WorkflowInstanceId.From("WFI-" + new string('9', 32));

        var missing = await Service(instances).Handle(new GetInstanceRequest(unknown.Value), CancellationToken.None);
        var elsewhere = await Service(instances, repos: new FixedRepos((ItemA, "beta")), caller: new CallerContextBuilder().Session().InRepo("alpha").Build()).Handle(new GetInstanceRequest(view.Id.Value), CancellationToken.None);

        Assert.Equal((SindriErrors.ItemNotFound.Code, $"No workflow instance {unknown.Value} in this repo."), (missing.Error.Code, missing.Error.Message));
        Assert.Equal((SindriErrors.ItemNotFound.Code, $"No workflow instance {view.Id.Value} in this repo."), (elsewhere.Error.Code, elsewhere.Error.Message));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Status_text_equals_what_workflow_status_prints(string name)
    {
        var view = StatusCases.View(name);

        var result = await Service(new SomeInstances(view)).Handle(new GetInstanceRequest(view.Address.Value), CancellationToken.None);

        Assert.Equal(StatusLayout.Render(view, StatusCases.Now), result.Value.Current.Status.Text);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Status_answer_cli_next_and_commands_equal_the_values_parsed_from_text(string name)
    {
        var view = StatusCases.View(name);

        var status = (await Service(new SomeInstances(view)).Handle(new GetInstanceRequest(view.Address.Value), CancellationToken.None)).Value.Current.Status;

        if (status.Next is { } next)
        {
            Assert.Contains(Words(next), Words(status.Text), StringComparison.Ordinal);
        }

        if (status.Answer is { } answer)
        {
            Assert.Contains(Words(answer.Cli), Words(status.Text), StringComparison.Ordinal);
        }

        var line = status.Text.Split('\n').Select(static l => l.Trim()).FirstOrDefault(static l => l.StartsWith("commands", StringComparison.Ordinal));
        // The commands line lists the verbs ("answer (forwarded)" marks a forwarded answer); an approve has its own drift line, which carries its command.
        var parsed = line is null ? string.Empty : line["commands".Length..].Replace(" (forwarded)", string.Empty, StringComparison.Ordinal).Trim();
        Assert.Equal(string.Join(", ", status.Commands.Where(static c => c.Verb != StatusVerb.Approve).Select(static c => StatusVerbNames.Of(c.Verb)).Distinct(StringComparer.Ordinal)), parsed);   // one word per verb: an answer with two variants is still "answer"
        Assert.All(status.Commands.Where(static c => c.Verb == StatusVerb.Approve), c => Assert.Contains(Words(c.Cli), Words(status.Text), StringComparison.Ordinal));
    }

    /// <summary>The text with every run of whitespace as one space: the layout wraps long lines.</summary>
    private static string Words(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static TheoryData<string> Cases() => [.. StatusCases.Names];
}
