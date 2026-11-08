using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// 'workflow list' and 'workflow events' are clients of the control plane's routes, like 'status' (engine spec §6.3,
/// review run 2, architecture-strategist-r2-4): they never start serve, and with none running they exit 1 with SND-API-001.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ReadVerbsClientTests
{
    [Theory(Timeout = 30_000)]
    [InlineData("list")]
    [InlineData("events", "T-3/step/1")]
    [InlineData("events", "T-3/step/1", "--follow")]
    public async Task With_no_control_plane_the_read_verb_is_SND_API_001_with_exit_1_and_starts_nothing(params string[] args)
    {
        using var api = ReadApi.Down();

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, args);

        Assert.Equal((1, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-API-001 not running: ", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_sends_each_filter_to_the_route_and_prints_what_it_answers()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, "list", "--item", "T-3", "--state", "done", "--query", "NeedsYou = false");

        Assert.Equal((0, "No workflows match.\n", string.Empty), (exit, stdout, stderr));
        Assert.Equal(["/sindri/workflow/instances?item=T-3&state=done&query=NeedsYou%20%3D%20false"], api.Calls);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_with_a_state_that_is_not_a_state_name_is_refused_by_the_route_as_SND_CLI_003_with_exit_2()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, "list", "--state", "Not A State");

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-CLI-003 ", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_needs_me_asks_for_needsMe()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "list", "--needs-me");

        Assert.Equal((0, "nothing needs you\n"), (exit, stdout));
        Assert.Equal(["/sindri/workflow/instances?needsMe=true&open=true"], api.Calls);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_sends_the_recipe_urgency_subject_and_root_filters_to_the_route_in_that_order()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken,
            "list", "--root", "false", "--subject", "fact:spec", "--urgency", "incident", "--recipe", "workflow.step");

        Assert.Equal((0, "No workflows are open.\n"), (exit, stdout));
        Assert.Equal(["/sindri/workflow/instances?recipe=workflow.step&urgency=incident&subject=fact%3Aspec&root=false&open=true"], api.Calls);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("--urgency", "urgent", "'urgent' isn't an urgency (fix: --urgency incident, triage or work)")]
    [InlineData("--urgency", "Incident", "'Incident' isn't an urgency (fix: --urgency incident, triage or work)")]
    [InlineData("--root", "maybe", "'maybe' isn't true or false (fix: --root true for top-level instances, --root false for children)")]
    [InlineData("--root", "True", "'True' isn't true or false (fix: --root true for top-level instances, --root false for children)")]
    public async Task A_misspelled_urgency_or_root_is_refused_by_the_client_as_SND_CLI_003_with_the_fix_and_asks_nothing(string flag, string value, string message)
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, "list", flag, value);

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.Contains(message, stderr, StringComparison.Ordinal);
        Assert.StartsWith("SND-CLI-003 ", stderr, StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("--recipe", "a", "--recipe", "b")]
    [InlineData("--urgency", "work", "--urgency", "triage")]
    [InlineData("--subject", "a", "--subject", "b")]
    [InlineData("--root", "true", "--root", "false")]
    public async Task A_filter_given_twice_is_refused_with_the_usage_and_asks_nothing(params string[] flags)
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, _, stderr) = await Run(api, TestContext.Current.CancellationToken, ["list", .. flags]);

        Assert.Equal(2, exit);
        Assert.Contains("usage: sindri workflow list", stderr, StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_filter_with_no_value_is_refused_with_the_usage()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, _, stderr) = await Run(api, TestContext.Current.CancellationToken, "list", "--recipe");

        Assert.Equal(2, exit);
        Assert.Contains("usage: sindri workflow list", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_needs_me_prints_decisions_then_tasks_each_by_address_and_the_line_is_the_pinned_text()
    {
        var task = StatusCases.View("desk-task") with { Address = Core.Launch.InstanceAddress.From("T-1/step/1") };
        var parked = StatusCases.View("parked-rounds") with { Address = Core.Launch.InstanceAddress.From("T-9/step/1") };
        var escalated = StatusCases.View("escalated-gate") with { Address = Core.Launch.InstanceAddress.From("T-2/step/1") };
        using var api = ReadApi.Over(new OneInstance(task), services => services.AddSingleton<IService<ListInstancesRequest, InstanceList>>(new FixedList(Listed(task, parked, escalated))));

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "list", "--needs-me");

        Assert.Equal(0, exit);
        var lines = stdout.TrimEnd('\n').Split('\n');
        Assert.Equal(["Decisions", "Tasks"], lines.Where(static l => !l.StartsWith(' ')), StringComparer.Ordinal);
        Assert.Matches(@"^  T-2/step/1  workflow\.step@[0-9a-f]{12}  (escalated|parked): .+  since .+ \(.+\)$", lines[1]);
        Assert.Matches(@"^  T-9/step/1  workflow\.step@[0-9a-f]{12}  parked: .+  since .+ \(.+\)$", lines[2]);
        Assert.Matches(@"^  T-1/step/1  workflow\.step@[0-9a-f]{12}  produce: .+  since .+ \(.+\)$", lines[4]);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_needs_me_with_only_a_finished_row_says_nothing_needs_you()
    {
        var running = StatusCases.View("done");
        using var api = ReadApi.Over(new OneInstance(running), services => services.AddSingleton<IService<ListInstancesRequest, InstanceList>>(new FixedList(Listed(running))));

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "list", "--needs-me");

        Assert.Equal((0, "nothing needs you\n"), (exit, stdout));
    }

    private static InstanceList Listed(params InstanceView[] views)
    {
        var rows = views.Select(static v => InstanceSummaries.Of(v, new InstanceSummaryContext(StatusCases.Now, ItemTitle: null))).ToList();
        return new InstanceList(rows, InstanceCounts.Of(views), Empty: null, StatusCases.Now);
    }

    private sealed class FixedList(InstanceList list) : IService<ListInstancesRequest, InstanceList>
    {
        public ValueTask<Result<InstanceList>> Handle(ListInstancesRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(list));
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> Run(ReadApi api, CancellationToken ct, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        await using var services = new ServiceCollection().AddSingleton<TimeProvider>(new FakeTimeProvider(StatusCases.Now)).BuildServiceProvider();
        var output = new Output(stdout, stderr, SliceHarness.Json);

        var exit = args[0] == "list" ? await ListCommand.Run(args[1..], api.Desk, services, output, ct) : await EventsCommand.Run(args[1..], api.Desk, services, output, ct);

        return (exit, stdout.ToString(), stderr.ToString());
    }
}
