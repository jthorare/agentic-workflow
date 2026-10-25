using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A parent instance on the time-skipping server, its children by address, and the commands the child tests send them (engine P7 U2).</summary>
/// <param name="Sindri">The composed host over the test's home.</param>
/// <param name="Environment">The time-skipping server.</param>
/// <param name="Harness">The test's home.</param>
internal sealed record ChildTree(IServiceProvider Sindri, Temporalio.Testing.WorkflowEnvironment Environment, WorkflowsHarness Harness)
{
    /// <summary>Starts the approved <paramref name="recipe"/> on <paramref name="item"/> with a 'fact:request'.</summary>
    public async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start(string recipe, string item)
    {
        var ct = TestContext.Current.CancellationToken;
        var start = await WorkflowsHarness.RecipeStart(Sindri, recipe, item, ct) with
        {
            Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:request")] = new InputFact("request.txt", Harness.Fact("Export the report.")) },
        };
        return await WorkflowsHarness.Start(Environment.Client, start);
    }

    /// <summary>The instance running at <paramref name="address"/>.</summary>
    public WorkflowHandle<RecipeWorkflow, RecipeResult> At(string address) => Environment.Client.GetWorkflowHandle<RecipeWorkflow, RecipeResult>(address);

    /// <summary>The address <paramref name="parent"/> gives its child of <paramref name="recipe"/> at the parent's <paramref name="entry"/>.</summary>
    public static string ChildOf(string parent, string recipe, int entry) => InstanceAddress.From(parent).Child(RecipeId.From(recipe), entry).Value;

    /// <summary>The parent once it lists exactly one live child; that child's address.</summary>
    public static async Task<(InstanceView Parent, string Child)> OneChild(WorkflowHandle<RecipeWorkflow, RecipeResult> parent)
    {
        var view = await WorkflowsHarness.Until(parent, static v => v.Children.Count == 1);
        return (view, view.Children[0].Address.Value);
    }

    /// <summary>Sends 't.go' to <paramref name="instance"/>, which ends a child that waits for it.</summary>
    public static Task Go(WorkflowHandle<RecipeWorkflow, RecipeResult> instance) =>
        instance.SignalAsync(wf => wf.EventAsync(new WaitSatisfied("service:workflow.events", new EventWaitSource(EventKind.From("t.go"), 1), Outcome: null, new Dictionary<FactKey, FactValue>())));

    /// <summary>Lets two hours pass and a bit (the timeout retries once before a park): a child that waits for 't.go' reaches its timeout.</summary>
    public Task PastTheHour() => WorkflowsHarness.Skip(Environment, TimeSpan.FromMinutes(130), TestContext.Current.CancellationToken);

    /// <summary>The 'answer --resume' a human gives <paramref name="instance"/>.</summary>
    public static Task<CommandResult> Resume(WorkflowHandle<RecipeWorkflow, RecipeResult> instance) =>
        instance.ExecuteUpdateAsync(wf => wf.AnswerAsync(new AnswerGiven("human:cli", Stamp('3'), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null)), new WorkflowUpdateOptions(new string('3', 32)));

    /// <summary>The 'cancel' a human gives <paramref name="instance"/>.</summary>
    public static Task<CommandResult> Cancel(WorkflowHandle<RecipeWorkflow, RecipeResult> instance) =>
        instance.ExecuteUpdateAsync(wf => wf.CancelAsync(new CancelRequested("human:cli", CancelOrigin.Command, Stamp('4'), Sequence: null, Parent: null)), new WorkflowUpdateOptions(new string('4', 32)));

    private static CommandStamp Stamp(char nonce) => new(CommandNonce.From(new string(nonce, 32)), new string('0', 64));
}
