using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.StartInstance;

/// <summary>
/// 'sindri workflow start' (engine spec rev 8 §5.1, §6.1, §6.3; "Phase 4" signal 1) against the pinned dev server: the
/// workflow id is the address, an existing '(recipe, item, attempt)' returns that instance, '--attempt next' is refused
/// while an earlier attempt is live, and 'fact:item' must name the same item.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StartInstanceTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Start_prints_the_status_header_and_opens_the_first_desk_task()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);

        var started = await served.Cli("start", "workflow.step", "--item", "T-1", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));

        Assert.Equal((0, string.Empty), (started.Exit, started.Stderr));
        Assert.Matches(@"^T-1/step/1  workflow\.step@[0-9a-f]{12}  v1  produce: desk task  since \d\d:\d\d \(.+\)$", started.Lines[0]);
        var view = await served.Until("T-1/step/1", static v => v.Task is not null);
        Assert.Equal(("produce", "1.1"), (view.State, view.Run?.Value));
        Assert.Equal("spec.md", Assert.Single(view.Task!.Inputs.Values).Name);
    }

    [Fact(Timeout = 120_000)]
    public async Task Starting_a_registered_recipe_nobody_approved_is_SND_RECIPE_003_and_starts_nothing()
    {
        _ = _harness.Register(WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);

        var started = await served.Cli("start", "workflow.item", "--item", "T-6", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));

        Assert.Equal(2, started.Exit);
        Assert.StartsWith("SND-RECIPE-003 ", started.Stderr, StringComparison.Ordinal);
        Assert.Empty(await served.Events("T-6/item/1"));
    }

    [Fact(Timeout = 120_000)]
    public async Task Starting_a_recipe_whose_approval_was_revoked_is_SND_RECIPE_003_and_starts_nothing()
    {
        _harness.Revoke(_harness.Approve(WorkflowsHarness.Shipped("sindri/workflow.item.yaml")));
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);

        var started = await served.Cli("start", "workflow.item", "--item", "T-7", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));

        Assert.Equal(2, started.Exit);
        Assert.StartsWith("SND-RECIPE-003 ", started.Stderr, StringComparison.Ordinal);
        Assert.Contains("revoked", started.Stderr, StringComparison.Ordinal);
        Assert.Empty(await served.Events("T-7/item/1"));
    }

    [Fact(Timeout = 120_000)]
    public async Task Starting_the_same_recipe_item_and_attempt_again_returns_that_instance()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var spec = "fact:spec=" + _harness.File("spec.md", "the spec");
        var first = await served.Cli("start", "workflow.step", "--item", "T-2", "--fact", spec);
        _ = await served.Until("T-2/step/1", static v => v.Task is not null);

        var again = await served.Cli("start", "workflow.step", "--item", "T-2", "--fact", spec);

        Assert.Equal((0, string.Empty), (first.Exit, first.Stderr));
        Assert.Equal((0, string.Empty), (again.Exit, again.Stderr));
        Assert.StartsWith("T-2/step/1  workflow.step@", again.Lines[0], StringComparison.Ordinal);
        Assert.Equal(["workflow.started"], await served.Events("T-2/step/1"));
    }

    [Fact(Timeout = 120_000)]
    public async Task Attempt_next_while_an_attempt_is_live_is_SND_WF_012()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var spec = "fact:spec=" + _harness.File("spec.md", "the spec");
        _ = await served.Cli("start", "workflow.step", "--item", "T-3", "--fact", spec);

        var next = await served.Cli("start", "workflow.step", "--item", "T-3", "--attempt", "next", "--fact", spec);

        Assert.Equal(2, next.Exit);
        Assert.StartsWith("SND-WF-012 ", next.Stderr, StringComparison.Ordinal);
        Assert.Contains("T-3/step/1", next.Stderr, StringComparison.Ordinal);
        Assert.Empty(await served.Events("T-3/step/2"));
    }

    [Fact(Timeout = 120_000)]
    public async Task After_a_cancel_start_shows_the_finished_attempt_and_attempt_next_starts_attempt_2()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var spec = "fact:spec=" + _harness.File("spec.md", "the spec");
        _ = await served.Cli("start", "workflow.step", "--item", "T-4", "--fact", spec);
        var cancelled = await served.Cli("cancel", "T-4/step/1");

        var finished = await served.Cli("start", "workflow.step", "--item", "T-4", "--fact", spec);
        var next = await served.Cli("start", "workflow.step", "--item", "T-4", "--attempt", "next", "--fact", spec);

        Assert.Equal((0, string.Empty), (cancelled.Exit, cancelled.Stderr));
        Assert.Equal((0, string.Empty), (finished.Exit, finished.Stderr));
        Assert.StartsWith("T-4/step/1  workflow.step@", finished.Lines[0], StringComparison.Ordinal);
        Assert.Equal("  start a new one with --attempt next", finished.Lines[^1]);
        Assert.Equal((0, string.Empty), (next.Exit, next.Stderr));
        Assert.StartsWith("T-4/step/2  workflow.step@", next.Lines[0], StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000)]
    public async Task Fact_item_naming_another_item_is_SND_WF_014_and_starts_nothing()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var item = _harness.File("item.json", """{ "id": "T-9", "repo": "agentic-workflow", "spec": "the spec" }""");

        var started = await served.Cli("start", "workflow.step", "--item", "T-5", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"), "--fact", "fact:item=" + item);

        Assert.NotEqual(0, started.Exit);
        Assert.StartsWith("SND-WF-014 ", started.Stderr, StringComparison.Ordinal);
        Assert.Empty(await served.Events("T-5/step/1"));
    }

    [Fact]
    public async Task Next_attempt_refused_while_earlier_is_live()
    {
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var spec = "fact:spec=" + _harness.File("spec.md", "the spec");
        _ = await served.Cli("start", "workflow.step", "--item", "T-8", "--fact", spec);
        _ = await served.Sindri.GetRequiredService<IWorkflowClaims>()
            .Take(new WorkflowClaim(ItemKey.From("T-8"), InstanceAddress.From("T-8/step/1"), 1), ended: null, TestContext.Current.CancellationToken);

        var refused = await served.Cli("start", "workflow.dev-timeout", "--item", "T-8", "--attempt", "next");
        var cancelled = await served.Cli("cancel", "T-8/step/1");
        var started = await served.Cli("start", "workflow.dev-timeout", "--item", "T-8", "--attempt", "next");

        Assert.Equal(2, refused.Exit);
        Assert.StartsWith("SND-WF-012 ", refused.Stderr, StringComparison.Ordinal);
        Assert.Contains("T-8/step/1", refused.Stderr, StringComparison.Ordinal);
        Assert.Equal((0, string.Empty), (cancelled.Exit, cancelled.Stderr));
        Assert.Equal((0, string.Empty), (started.Exit, started.Stderr));
        Assert.StartsWith("T-8/dev-timeout/1  workflow.dev-timeout@", started.Lines[0], StringComparison.Ordinal);
    }
}
