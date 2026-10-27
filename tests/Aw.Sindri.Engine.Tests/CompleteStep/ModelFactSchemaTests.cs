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

/// <summary>#1112: a scoping fact that breaks its schema (scopeMap with a top-level 'decisions') is refused when the agent reports it, naming the fact, the path and the schema file, so the same agent fixes it in the same run.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelFactSchemaTests
{
    private const string Proof = "the-launch-secret";
    private const string EmptyMap = """{"surfaces":[],"implications":[],"workstreams":[],"flagged":[],"excluded":[],"unmapped":[],"unresolved":[]}""";
    private const string MapWithDecisions = """{"surfaces":[],"implications":[],"workstreams":[],"flagged":[],"excluded":[],"unmapped":[],"unresolved":[],"decisions":[{"q":"Q1","a":"private-note-text"}]}""";
    private static readonly InstanceAddress _address = InstanceAddress.From("T-9/step/1");
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));
    private static readonly FactKey _scopeMap = FactKey.From("fact:scopeMap");

    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());
    private readonly FakeLaunchFiles _files = new();
    private readonly FakeLauncher _launcher = new();
    private readonly ReportRefusals _refusals = new();

    [Fact(Timeout = 30_000)]
    public async Task A_scopeMap_with_a_field_its_schema_does_not_allow_is_refused_naming_the_fact_the_field_and_the_schema_file()
    {
        var run = new AgentRun();

        var refused = await Service(run).Handle(Report(1, (_scopeMap, MapWithDecisions)), TestContext.Current.CancellationToken);

        Assert.True(refused.IsError, "the report was accepted");
        Assert.Equal("SND-WF-008", refused.Error.Code);
        Assert.Contains("fact:scopeMap/decisions", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains("schemas/scoping/scope-map.json", refused.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-note-text", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(run.Sent);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_same_agent_fixes_the_map_and_reports_again_in_the_same_run()
    {
        var run = new AgentRun();
        var service = Service(run);

        var first = await service.Handle(Report(1, (_scopeMap, MapWithDecisions)), TestContext.Current.CancellationToken);
        var second = await service.Handle(Report(2, (_scopeMap, EmptyMap)), TestContext.Current.CancellationToken);

        Assert.True(first.IsError, "the first report was accepted");
        Assert.False(second.IsError, second.IsError ? second.Error.Message : null);
        Assert.Empty(_launcher.Stops);
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

    /// <summary>One instance whose open run is an agent's on a Step of a recipe that declares 'fact:scopeMap' as text (not a payload); records what is sent to it.</summary>
    private sealed class AgentRun : IInstanceDirectory, IInstanceRuntime, IApprovedRecipes
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

        private static Recipe Recipe() =>
            new(1, _version.Id, 1, Description: null, Base: null, Urgency: "work", Name: null, Inputs: [],
                [new RecipeOutput(_scopeMap, "Scope map", "text", "produce")],
                "produce", new Dictionary<string, RecipeState>(StringComparer.Ordinal), [], new Dictionary<string, LoopSpec>(StringComparer.Ordinal), Budget: null, Evals: [], Hash: null);

        private static InstanceView View() =>
            new(_address, _version, "produce", Detail: null, DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, RunToken.For(1, 1), Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(_address),
                Version = 3,
                AgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, _launch.Value, Task: null),
            };
    }
}
