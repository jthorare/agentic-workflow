using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The loop and budget tests' recipe and instances. The recipe has the shape of the shipped 'workflow.step' (spec §12):
/// 'produce' (loop 'revisions', cap 3, a 200k budget) goes to 'checks', which goes to 'verify' on 'passed' and back to
/// 'produce' on 'failed'; 'verify' goes to 'done' or back to 'produce'. The recipe budget is 400k. Parks are
/// built here by hand, so these tests don't depend on the suspension rules.
/// </summary>
internal static class LoopFixture
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The loop's name.</summary>
    public const string Revisions = "revisions";

    /// <summary>The index of 'checks -> produce' (the loop transition) in <see cref="StepRecipe"/>.</summary>
    public const int ChecksFailed = 2;

    public static Recipe StepRecipe { get; } = BuildRecipe();

    /// <summary>The recipe without any budget.</summary>
    public static Recipe Unbudgeted { get; } = StepRecipe with
    {
        Budget = null,
        States = new Dictionary<string, RecipeState>(StepRecipe.States, StringComparer.Ordinal)
        {
            ["produce"] = StepRecipe.States["produce"] with { Budget = null },
        },
    };

    /// <summary>A mark with these findings (digested), as the decision builds it from 'fact:findings'.</summary>
    public static ProgressMark Findings(string findings) => new(Digest(findings), Failure: null, EmptyDiff: false);

    /// <summary>A mark with this failure (digested), as the decision builds it from a step.failed reason or a refusal's reasons.</summary>
    public static ProgressMark Failure(string failure) => new(Findings: null, Digest(failure), EmptyDiff: false);

    /// <summary>A round that changed nothing.</summary>
    public static ProgressMark EmptyDiff { get; } = new(Findings: null, Failure: null, EmptyDiff: true);

    /// <summary>An instance in <paramref name="state"/> (entry 3, run 3.1), with the loop's counters and the spend given.</summary>
    public static WorkflowInstance In(string state, LoopState? loop = null, TokenSpend? spent = null) => new(
        WorkflowInstanceId.From("WFI-" + new string('0', 30) + "a1"),
        InstanceAddress.Root(ItemKey.From("T-1"), StepRecipe.Id, 1),
        "local",
        Ring: 0,
        new PinnedRecipe(StepRecipe.Id, 1, RecipeHash.From("sha256:" + new string('a', 64)), new Dictionary<RecipeId, RecipeHash>()),
        ItemKey.From("T-1"),
        Attempt: 1,
        Parent: null,
        new Frame(state, 3, new OpenRun(RunToken.For(3, 1), StepRuntime.Desk, Launch: null, Task: null), Child: null),
        Suspension: null,
        PendingPark: null,
        loop is null
            ? new Dictionary<string, LoopState>(StringComparer.Ordinal)
            : new Dictionary<string, LoopState>(StringComparer.Ordinal) { [Revisions] = loop },
        spent ?? TokenSpend.None,
        new Dictionary<FactKey, FactValue>(),
        Outcome: null);

    /// <summary>A spend of <paramref name="total"/> in all, <paramref name="produce"/> of it in 'produce'.</summary>
    public static TokenSpend Spent(int total, int produce, int granted = 0) =>
        new(total, new Dictionary<string, int>(StringComparer.Ordinal) { ["produce"] = produce }, granted);

    /// <summary><paramref name="instance"/> parked as a rule decided (set by hand: the suspension rules aren't under test here).</summary>
    public static WorkflowInstance Parked(WorkflowInstance instance, ParkReason reason, ParkKind kind, ResumeKind resume, WithheldTrigger? withheld = null) =>
        instance with
        {
            Suspension = new Suspension(
                SuspensionKind.Park, reason, kind, Escalation: null, "parked by the test", "service:desk", Mirrors: null,
                new ResumeAction(resume, withheld, Gate: null, Effects: [])),
        };

    /// <summary>A loop park on the transition back into 'produce' (kind loop): the instance was in 'checks', and 'checks -> produce' is withheld.</summary>
    public static WorkflowInstance LoopParked(ParkReason reason, LoopState loop) =>
        Parked(In("checks", loop), reason, ParkKind.Loop, ResumeKind.Transition, ChecksFailedTrigger());

    /// <summary><paramref name="parked"/> after its answer ended the park (by hand): a pending park takes its place.</summary>
    public static WorkflowInstance Popped(WorkflowInstance parked) => parked with { Suspension = parked.PendingPark, PendingPark = null };

    /// <summary>'checks' reported 'failed' (the withheld loop transition's trigger).</summary>
    public static WithheldTrigger ChecksFailedTrigger() => new(ChecksFailed, new StepCompleted(
        "service:desk", RunToken.For(3, 1), OutcomeName.From("failed"), new Dictionary<FactKey, FactValue>(), Tokens: 10, StepRuntime.Desk,
        new string('b', 64)));

    private static string Digest(string text) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    private static Recipe BuildRecipe()
    {
        var states = new Dictionary<string, RecipeState>(StringComparer.Ordinal)
        {
            ["produce"] = new(StateKind.Active, Step("step.produce"), WaitFor: null, Timeout("4h"), Revisions, new BudgetSpec(200_000),
                "Produce the Step output for {item} (round {loop.revisions}/{loop.revisions.cap}).", Outcome: null),
            ["checks"] = new(StateKind.Active, Step("step.checks"), WaitFor: null, Timeout("30m"), Loop: null, Budget: null,
                "Run the Step's deterministic checks.", Outcome: null),
            ["verify"] = new(StateKind.Active, Step("step.verify"), WaitFor: null, Timeout("1h"), Loop: null, Budget: null,
                "Review the output as the verifier.", Outcome: null),
            ["done"] = new(StateKind.Terminal, Step: null, WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, OutcomeName.From("resolved")),
        };
        return new Recipe(
            1, RecipeId.From("workflow.step"), 1, Description: null, Base: null, Urgency: "work", Inputs: [], Outputs: [], "produce", states,
            [
                new(["produce"], "checks", "step.completed", ARecipe.When(OutcomeName.From("done")), [], [], [], [FactKey.From("fact:output")]),
                new(["checks"], "verify", "step.completed", ARecipe.When(OutcomeName.From("passed")), [], [], [], []),
                new(["checks"], "produce", "step.completed", ARecipe.When(OutcomeName.From("failed")), [], [], [], [FactKey.From("fact:findings")]),
                new(["verify"], "done", "step.completed", ARecipe.When(OutcomeName.From("accepted")), [], [], [], []),
                new(["verify"], "produce", "step.completed", ARecipe.When(OutcomeName.From("rejected")), [], [], [], [FactKey.From("fact:findings")]),
            ],
            new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { [Revisions] = new(3, LoopSpec.DefaultNoProgress) },
            new BudgetSpec(400_000), Evals: [], Hash: null);
    }

    private static StepUse Step(string use) => new(BlockId.From(use), new Dictionary<string, WithValue>(StringComparer.Ordinal), []);

    private static TimeoutSpec Timeout(string after) => new(RecipeDuration.From(after), TimeoutAction.Park);
}
