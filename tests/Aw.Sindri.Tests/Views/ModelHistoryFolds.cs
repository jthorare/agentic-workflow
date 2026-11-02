using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The ledger rows a model-history test folds through the views harness: a registered recipe, a started instance, an agent launch that names its model, and the Step reports the engine mirrors.</summary>
internal static class ModelHistoryFolds
{
    public const int Version = 3;
    public static readonly RecipeId Recipe = RecipeId.From("workflow.scoping");
    public static readonly RecipeHash Hash = RecipeHash.From("sha256:" + new string('a', 64));
    public static readonly InstanceAddress Address = InstanceAddress.From(A.FirstItemId.Value + "/scoping/1");

    /// <summary>The key of the row of <paramref name="model"/> at <paramref name="state"/> in the test recipe.</summary>
    public static string Key(string state, string model) => ModelHistoryKeys.For(Recipe, Version, state, model);

    /// <summary>The row of <paramref name="model"/> at <paramref name="state"/>, or null.</summary>
    public static async Task<ModelHistoryRow?> Row(this ViewsHarness v, string state, string model) =>
        (await v.Views.Get<ModelHistoryRow>(ViewNames.ModelHistory, Key(state, model), TestContext.Current.CancellationToken)).Value;

    /// <summary>Folds 'recipe.registered' for the test recipe, then 'workflow.started' for its instance, entering <paramref name="state"/>.</summary>
    public static async Task Begin(this ViewsHarness v, string state = "draft", bool register = true)
    {
        if (register)
        {
            await v.Fold(EventTypes.RecipeRegistered, new RecipeRegistered(Recipe, Version, Hash));
        }

        await v.Fold(EventTypes.WorkflowStarted, Payload(state: "", state, "start", run: null, reason: null, started: true), A.FirstItemId);
    }

    /// <summary>The run token of the <paramref name="entry"/>th frame entry's first run.</summary>
    public static string Run(int entry) => RunToken.For(entry, 1).Value;

    /// <summary>Folds the 'launch.started' of the agent run <paramref name="run"/> of the instance, naming <paramref name="model"/> (none when null).</summary>
    public static Task Launched(this ViewsHarness v, string run, string? model, string state = "draft") =>
        v.Fold(EventTypes.LaunchStarted, LaunchPayload(run, state, model), A.FirstItemId);

    /// <summary>The 'launch.started' payload of the agent run <paramref name="run"/> of the instance in <paramref name="state"/>.</summary>
    public static LaunchStarted LaunchPayload(string run, string state, string? model) =>
        new(AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(Address), RunToken.From(run)), A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri",
            new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null), ["tmux", "attach"], CatalogSource.Builtin, "sha256:abc", Address, state, Attempt: 1, Model: model);

    /// <summary>Folds a 'model.picked' for the instance's <paramref name="state"/>, escalated from <paramref name="from"/> when it is not null.</summary>
    public static Task Picked(this ViewsHarness v, string state, string? from, string? kind) =>
        v.Fold(EventTypes.ModelPicked,
            new ModelPicked(AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(Address), RunToken.From("1.1"), escalation: 1), A.FirstItemId, Address, state, Attempt: 1, AgentId.From("claude-code"),
                Model: "claude-opus-5-5", By: "rules", Reason: null, Decision: null, Candidates: [], BriefHash: "sha256:abc", Files: [], Omitted: [], JevMs: 0, JevCalls: 0, ContentSent: false, Unusual: false, from, kind),
            A.FirstItemId);

    /// <summary>Folds a 'step.completed' that leaves <paramref name="from"/> for <paramref name="to"/>.</summary>
    public static Task Completed(this ViewsHarness v, string from, string to, string run) =>
        v.Fold(EventTypes.WorkflowTransition, Payload(from, to, TriggerNames.StepCompleted, run, reason: null, started: false), A.FirstItemId);

    /// <summary>Folds a 'step.failed' in <paramref name="state"/> with <paramref name="reason"/>.</summary>
    public static Task Failed(this ViewsHarness v, string state, string run, string reason, string? failureKind = null) =>
        v.Fold(EventTypes.WorkflowTransition, Payload(state, state, TriggerNames.StepFailed, run, reason, started: false, failureKind), A.FirstItemId);

    /// <summary>The mirrored workflow payload of a start or a transition.</summary>
    public static WorkflowEventPayload Payload(string state, string to, string trigger, string? run, string? reason, bool started, string? failureKind = null)
    {
        var delta = new WorkflowEventDelta(Frame: null, Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null)
        {
            Recipe = started ? Recipe.Value : null,
            RecipeHash = started ? Hash.Value : null,
        };
        var id = WorkflowInstanceId.Of(Address);
        var detail = new WorkflowEventDetail(TriggerKey.For(id, "t"), "agent:session", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null, Answer: null, Amount: null, reason,
            Gates: [], Evidence: [], Grant: null, Override: null, Refusal: null, Effects: [], delta)
        {
            FailureKind = failureKind,
        };
        return new WorkflowEventPayload(Address.Value, state, to, trigger, Outcome: null, run, detail);
    }
}
