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

/// <summary>
/// An agent Step's outputs are checked against the types its recipe declares when it reports (run23: the design draft wrote 'fact:design' as prose and a later
/// Step parked on it): a payload that isn't a JSON object or array is refused with the fix, once; a second bad report ends the session, which fails the Step.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AgentOutputTypeTests
{
    private const string Proof = "the-launch-secret";
    private static readonly InstanceAddress _address = InstanceAddress.From("T-9/step/1");
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));
    private static readonly FactKey _design = FactKey.From("fact:design");
    private static readonly FactKey _notes = FactKey.From("fact:notes");

    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());
    private readonly FakeLaunchFiles _files = new();
    private readonly FakeLauncher _launcher = new();
    private readonly ReportRefusals _refusals = new();

    [Fact(Timeout = 30_000)]
    public async Task A_payload_written_as_prose_is_refused_with_the_fix_and_nothing_is_stored_or_sent()
    {
        var run = new AgentRun();

        var refused = await Service(run).Handle(Report(1, (_design, "Here is my design: use a queue.")), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-008", refused.Error.Code);
        Assert.Contains("fact:design", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains("JSON", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(run.Sent);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(Nonce(1), "another command", TestContext.Current.CancellationToken)).Value);
        Assert.Empty(_launcher.Stops);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_JSON_object_or_array_payload_passes()
    {
        var run = new AgentRun();

        var object_ = await Service(run).Handle(Report(1, (_design, """{"spec":"# Design","sections":[]}""")), TestContext.Current.CancellationToken);
        var array = await Service(run).Handle(Report(2, (_design, """{"spec":"# Design","decisions":[1,2]}""")), TestContext.Current.CancellationToken);

        Assert.False(object_.IsError, object_.IsError ? object_.Error.Message : null);
        Assert.False(array.IsError, array.IsError ? array.Error.Message : null);
        Assert.Equal(2, run.Sent.Count);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_corrected_second_report_passes_after_one_refusal()
    {
        var run = new AgentRun();
        var service = Service(run);

        var first = await service.Handle(Report(1, (_design, "prose")), TestContext.Current.CancellationToken);
        var second = await service.Handle(Report(2, (_design, """{"spec":"# Design","sections":[]}""")), TestContext.Current.CancellationToken);

        Assert.True(first.IsError);
        Assert.False(second.IsError, second.IsError ? second.Error.Message : null);
        Assert.Empty(_launcher.Stops);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_second_bad_report_is_refused_for_good_and_ends_the_session_so_the_Step_fails()
    {
        var run = new AgentRun();
        var service = Service(run);

        var first = await service.Handle(Report(1, (_design, "prose")), TestContext.Current.CancellationToken);
        var second = await service.Handle(Report(2, (_design, "more prose")), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-008", first.Error.Code);
        Assert.Equal("SND-WF-008", second.Error.Code);
        Assert.Contains("second", second.Error.Message, StringComparison.Ordinal);
        Assert.Empty(run.Sent);
        Assert.Equal([_launch], _launcher.Stops);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_session_ended_for_a_second_bad_report_has_a_one_line_refusal_naming_the_output_for_the_failed_Step()
    {
        var run = new AgentRun();
        var service = Service(run);

        _ = await service.Handle(Report(1, (_design, "prose")), TestContext.Current.CancellationToken);
        _ = await service.Handle(Report(2, (_design, "more\nprose " + new string('x', 1000))), TestContext.Current.CancellationToken);

        var recorded = _refusals.Take(_launch);
        Assert.NotNull(recorded);
        Assert.Contains("fact:design", recorded, StringComparison.Ordinal);
        Assert.Contains("isn't JSON", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', recorded);
        Assert.True(recorded.Length <= ReportRefusals.MaxLength);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_first_refusal_that_the_agent_corrects_leaves_no_refusal_on_the_launch()
    {
        var run = new AgentRun();
        var service = Service(run);

        _ = await service.Handle(Report(1, (_design, "prose")), TestContext.Current.CancellationToken);

        Assert.Null(_refusals.Take(_launch));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_payload_missing_a_required_field_is_refused_naming_the_field_and_nothing_is_stored_or_sent()
    {
        var run = new AgentRun();

        var refused = await Service(run).Handle(Report(1, (_design, """{"sections":[]}""")), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-008", refused.Error.Code);
        Assert.Contains("fact:design", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains("spec", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(run.Sent);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(Nonce(1), "another command", TestContext.Current.CancellationToken)).Value);
        Assert.Empty(_launcher.Stops);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_second_report_with_a_wrongly_typed_field_ends_the_session_and_the_failed_Step_names_the_field()
    {
        var run = new AgentRun();
        var service = Service(run);

        var first = await service.Handle(Report(1, (_design, """{"spec":"# Design","sections":"none"}""")), TestContext.Current.CancellationToken);
        var second = await service.Handle(Report(2, (_design, """{"spec":"# Design","sections":"none"}""")), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-008", first.Error.Code);
        Assert.Contains("sections", first.Error.Message, StringComparison.Ordinal);
        Assert.Equal("SND-WF-008", second.Error.Code);
        Assert.Equal([_launch], _launcher.Stops);
        Assert.Contains("sections", _refusals.Take(_launch), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_payload_corrected_to_the_right_fields_passes_after_one_refusal()
    {
        var run = new AgentRun();
        var service = Service(run);

        var first = await service.Handle(Report(1, (_design, """{"sections":[]}""")), TestContext.Current.CancellationToken);
        var second = await service.Handle(Report(2, (_design, """{"spec":"# Design","sections":[]}""")), TestContext.Current.CancellationToken);

        Assert.True(first.IsError);
        Assert.False(second.IsError, second.IsError ? second.Error.Message : null);
        Assert.Empty(_launcher.Stops);
    }

    [Fact(Timeout = 30_000)]
    public async Task Text_outputs_and_undeclared_facts_are_unchanged()
    {
        var run = new AgentRun();

        var done = await Service(run).Handle(Report(1, (_notes, "any prose at all"), (FactKey.From("fact:scratch"), "also fine")), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        _ = Assert.Single(run.Sent);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_challenge_round_that_is_the_challenge_log_is_refused_naming_the_shape_and_a_real_round_passes()
    {
        var run = new AgentRun();
        var service = Service(run);
        var round = FactKey.From("fact:challengeRound");

        var log = await service.Handle(Report(1, (round, """{"rounds":[{"walked":[{"kind":"lens","name":"code"}],"verdict":"found","round":1}]}""")), TestContext.Current.CancellationToken);
        var good = await service.Handle(Report(2, (round, """{"walked":[{"kind":"lens","name":"code"}],"verdict":"nothingNew"}""")), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-008", log.Error.Code);
        Assert.Contains("fact:challengeRound is a challenge log, not a round", log.Error.Message, StringComparison.Ordinal);
        Assert.False(good.IsError, good.IsError ? good.Error.Message : null);
        _ = Assert.Single(run.Sent);
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

    /// <summary>One instance whose open run is an agent's on a Step of a recipe that declares 'fact:design' payload and 'fact:notes' text; records what is sent to it.</summary>
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
                [new RecipeOutput(_design, "Design", "payload", "produce"), new RecipeOutput(_notes, "Notes", "text", "produce")],
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
