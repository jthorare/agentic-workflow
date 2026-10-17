using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A history recorded before the block runtime (DT-J2) replays on the desk path: the 'dt-j-block-runtime' patch is absent from it, so the interpreter keeps the recorded desk run. It needs no server, so it doesn't share the wall-clock collection.</summary>
[Lane(TestLane.Integration)]
public sealed class BlockRuntimeReplayTests
{
    [Fact(Timeout = 120_000)]
    public async Task A_pre_DT_J_history_replays_on_the_desk_path()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/dev-route-desk.json")!;
        using var reader = new StreamReader(stream);
        var history = Temporalio.Common.WorkflowHistory.FromJson("T-1/dev-evidence-route/1", await reader.ReadToEndAsync(ct));

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);

        Assert.Null(replayed.ReplayFailure);
    }
}
