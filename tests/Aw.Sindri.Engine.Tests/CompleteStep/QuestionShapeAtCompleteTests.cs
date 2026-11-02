using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>#1204: a fact:questions output with a question that has no recommended option is refused when the questions agent reports it, naming the question, so the same agent fixes its file in the same run (SCP-QST-001).</summary>
[Lane(TestLane.Unit)]
public sealed class QuestionShapeAtCompleteTests
{
    private const string Proof = "the-launch-secret";
    private const string QuestionWithNoRecommended = """[{"id":"Q7","question":"What does the backfill do with unit-less rows?","options":[{"id":"Q7.a","answer":"Expand them.","cites":["R1"]},{"id":"Q7.b","answer":"Drop them.","cites":["R1"],"recommended":false}],"other":true,"skip":true}]""";
    private static readonly InstanceAddress _address = InstanceAddress.From("T-9/step/1");
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));
    private static readonly FactKey _questions = FactKey.From("fact:questions");

    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());
    private readonly FakeLaunchFiles _files = new();
    private readonly FakeLauncher _launcher = new();
    private readonly ReportRefusals _refusals = new();

    [Fact(Timeout = 30_000)]
    public async Task A_question_with_no_recommended_option_is_refused_naming_the_question_so_the_questions_agent_fixes_it()
    {
        var run = new AgentRun("step.interview.questions");

        var refused = await Service(run).Handle(Report(1, (_questions, QuestionWithNoRecommended)), TestContext.Current.CancellationToken);

        Assert.True(refused.IsError, "the report was accepted");
        Assert.Equal("SND-WF-008", refused.Error.Code);
        Assert.Contains("fact:questions Q7 has 0 recommended options", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(run.Sent);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_idea_to_brief_questions_step_is_not_held_to_the_one_recommended_rule()
    {
        var run = new AgentRun("step.brief.questions");

        var accepted = await Service(run).Handle(Report(1, (_questions, QuestionWithNoRecommended)), TestContext.Current.CancellationToken);

        Assert.False(accepted.IsError, accepted.IsError ? accepted.Error.Message : null);
    }

    private CompleteStepService Service(AgentRun run)
    {
        _files.Open(_launch, Proof);
        return new(run, run, run, _facts, _nonces, new FixedCaller(A.Caller().Session().Build()), new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, _files, new ReportOutputTypes(run, _launcher, _refusals)));
    }

    private static CommandNonce Nonce(int n) => CommandNonce.From(n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture));

    private static CompleteStepRequest Report(int nonce, params (FactKey Fact, string Content)[] outputs) =>
        new(_address.Value, RunToken.For(1, 1), OutcomeName.From("done"), outputs.ToDictionary(static o => o.Fact, static o => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(o.Content)), Nonce(nonce), new LaunchSecret(Proof));

    private sealed class FixedCaller(CallerContext caller) : ICallerContextAccessor
    {
        public CallerContext? Current => caller;

        public IDisposable Enter(CallerContext context) => throw new NotSupportedException("not entered here");

        public IDisposable EnterService(string service) => throw new NotSupportedException("not entered here");
    }

    /// <summary>One instance whose open run is an agent's on a Step of a recipe that declares 'fact:questions' as text (not a payload); records what is sent to it.</summary>
    private sealed class AgentRun(string step) : IInstanceDirectory, IInstanceRuntime, IApprovedRecipes
    {
        private static readonly RecipeVersion _version = new(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
        public List<Trigger> Sent { get; } = [];

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceAddress>>(_address);

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException("not started here");

        public ValueTask<Result<InstanceView>> Get(InstanceAddress at, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceView>>(View());

        public ValueTask<Result<CommandResult>> Send(InstanceAddress at, CommandNonce nonce, Trigger command, CancellationToken cancellationToken)
        {
            Sent.Add(command);
            return ValueTask.FromResult<Result<CommandResult>>(new CommandResult(View(), "-> checks"));
        }

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress at, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException("not requested here");

        public ValueTask<Result<Unit>> Signal(InstanceAddress at, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException("not signalled here");

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => throw new NotSupportedException("not asked here");

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(new ApprovedRecipe(_version, Recipe()));

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);

        private Recipe Recipe() =>
            new(1, _version.Id, 1, Description: null, Base: null, Urgency: "work", Name: null, Inputs: [],
                [new RecipeOutput(_questions, "Questions", "text", "produce")],
                "produce", new Dictionary<string, RecipeState>(StringComparer.Ordinal) { ["produce"] = new(StateKind.Active, new StepUse(BlockId.From(step), new Dictionary<string, WithValue>(StringComparer.Ordinal), []), WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, Outcome: null) }, [], new Dictionary<string, LoopSpec>(StringComparer.Ordinal), Budget: null, Evals: [], Hash: null);

        private static InstanceView View() =>
            new(_address, _version, "produce", Detail: null, DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, RunToken.For(1, 1), Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(_address),
                Version = 3,
                AgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, _launch.Value, Task: null),
            };
    }
}
