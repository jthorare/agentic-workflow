using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Common;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A foldered trial recorded before 'LoadRecipe' took the folder argument (8610c89: one argument, serve's block set) replays on this build with no
/// non-determinism: the activity keeps its name and its place in the history, and its recorded result is replayed whatever the call passes now.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TrialPreFolderArgReplayTests
{
    [Fact]
    public async Task A_foldered_trial_history_recorded_before_the_folder_argument_replays()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Workflows", "Fixtures", "trial-prefolder-history.json"), TestContext.Current.CancellationToken);
        var history = WorkflowHistory.FromJson("T-1/skill-trial-review/1", json);
        var replayer = new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>());

        var result = await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false, TestContext.Current.CancellationToken);

        Assert.Null(result.ReplayFailure);
        Assert.Contains("skillTrialFolder", StartedInput(history), StringComparison.Ordinal);   // the recorded start carried the folder fact: the case the new argument is read from
    }

    // why: Temporal stores payloads base64, so the folder fact is in the decoded input of the WorkflowExecutionStarted event, never in the history's JSON text
    private static string StartedInput(WorkflowHistory history) =>
        string.Concat(history.Events.Single(static e => e.WorkflowExecutionStartedEventAttributes is not null).WorkflowExecutionStartedEventAttributes.Input.Payloads_.Select(static p => p.Data.ToStringUtf8()));
}
