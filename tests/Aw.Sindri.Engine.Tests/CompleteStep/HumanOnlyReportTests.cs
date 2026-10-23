using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>'workflow complete' on a human-only Step (DT spec §8.5, scoping-station spec W3): a non-human caller is refused SND-AUTH-002 before the nonce is claimed, whatever the chain in front said.</summary>
[Lane(TestLane.Unit)]
public sealed class HumanOnlyReportTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-7/evidence/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('e', 32));

    private readonly TemporalLikeRuntime _approvals = new();   // only its approval check is used
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());

    [Fact(Timeout = 30_000)]
    public async Task A_session_complete_on_a_desk_choose_run_is_SND_AUTH_002_and_claims_nothing()
    {
        var desk = new OpenTask("step.desk.choose");

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", refused.Error.Code);
        Assert.Empty(desk.Sent);
        Assert.Empty(desk.Attempted);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_session_complete_on_the_babysitters_docs_merge_decision_is_SND_AUTH_002_so_it_cannot_reach_merge_docs()
    {
        var desk = new OpenTask("step.pr.docs-merge-decide");

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", refused.Error.Code);
        Assert.Empty(desk.Sent);
        Assert.Empty(desk.Attempted);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_human_completes_the_babysitters_docs_merge_decision()
    {
        var desk = new OpenTask("step.pr.docs-merge-decide");

        var merge = new CompleteStepRequest(_address.Value, RunToken.For(1, 1), OutcomeName.From("merge"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

        var done = await Service(desk, A.Caller().Human().AtTerminal().Build()).Handle(merge, TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        _ = Assert.Single(desk.Sent);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_session_complete_on_a_judge_choose_run_on_the_desk_is_SND_AUTH_002()
    {
        var desk = new OpenTask("step.judge.choose");

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", refused.Error.Code);
        Assert.Empty(desk.Attempted);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_session_complete_on_a_plain_desk_step_passes_the_service()
    {
        var desk = new OpenTask("step.produce");

        var done = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);   // the chain already refuses a session on every complete; this layer is per Step
        _ = Assert.Single(desk.Sent);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_human_in_the_app_or_a_terminal_completes_a_human_only_step(bool terminal)
    {
        var desk = new OpenTask("step.desk.choose");
        var human = terminal ? A.Caller().Human().AtTerminal().Build() : A.Caller().Human().Build();

        var done = await Service(desk, human).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        _ = Assert.Single(desk.Sent);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_stale_run_on_a_human_only_step_is_answered_by_the_send_not_refused()
    {
        var desk = new OpenTask("step.desk.choose");
        var stale = new CompleteStepRequest(_address.Value, RunToken.For(2, 1), OutcomeName.From("api"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

        var refused = await Service(desk, Session()).Handle(stale, TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-005", refused.Error.Code);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_refusal_names_the_block_state_and_the_human_command()
    {
        var desk = new OpenTask("step.desk.choose");

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"CompleteStep on 'step.desk.choose' (state 'route-evidence-desk') is a human-only Step. Ask the human to run it in a terminal or the Sindri app: sindri workflow complete T-7/evidence/1 --run {RunToken.For(1, 1).Value} --outcome api",
            refused.Error.Message);
    }

    /// <summary>Review run 1 (I2): the command in the refusal lists the outputs the Step requires on the reported outcome, as the status line does.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("step.evidence.desk", "recorded", " --output fact:evidenceSummary=<file> --output fact:evidenceReason=<file>")]
    [InlineData("step.owner.decide", "retry", " --output fact:notes=<file>")]
    [InlineData("step.owner.decide", "stop", "")]
    [InlineData("step.phase.decide", "rebuild", " --output fact:buildNotes=<file>")]
    [InlineData("step.phase.decide", "stop", "")]
    [InlineData("step.babysit.watch", "merged", " --output fact:mergedSha=<file>")]
    [InlineData("step.babysit.watch", "rework", " --output fact:reworkNotes=<file>")]
    public async Task The_refusal_command_lists_the_outputs_required_on_the_reported_outcome(string step, string outcome, string outputs)
    {
        var desk = new OpenTask(step);
        var request = new CompleteStepRequest(_address.Value, RunToken.For(1, 1), OutcomeName.From(outcome), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

        var refused = await Service(desk, Session()).Handle(request, TestContext.Current.CancellationToken);

        Assert.EndsWith($"sindri workflow complete T-7/evidence/1 --run {RunToken.For(1, 1).Value} --outcome {outcome}{outputs}", refused.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>Review run 1 (qa-r1-2): a retry with a nonce that is already claimed is refused before anything else, and the claim stays.</summary>
    [Fact(Timeout = 30_000)]
    public async Task A_session_retry_on_a_claimed_nonce_is_refused_and_keeps_the_claim()
    {
        var desk = new OpenTask("step.desk.choose");
        _ = await _nonces.Claim(_nonce, "earlier report", TestContext.Current.CancellationToken);

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", refused.Error.Code);
        Assert.Empty(desk.Attempted);
        Assert.Equal(NonceClaim.Pending, (await _nonces.Claim(_nonce, "earlier report", TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_session_retry_on_a_claimed_nonce_with_an_unreadable_instance_is_that_error()
    {
        var desk = new OpenTask("step.desk.choose") { Unreadable = true };
        _ = await _nonces.Claim(_nonce, "earlier report", TestContext.Current.CancellationToken);

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-010", refused.Error.Code);
        Assert.Empty(desk.Attempted);
    }

    /// <summary>Review run 1 (arch-r1-1): the view read for the human-only rule serves the engine-filled outputs and the approval check too.</summary>
    [Fact(Timeout = 30_000)]
    public async Task A_session_report_reads_the_instance_once()
    {
        var desk = new OpenTask("step.produce");

        var done = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        Assert.Equal(1, desk.Reads);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_fresh_session_report_on_an_instance_that_cant_be_read_is_that_error_and_claims_nothing()
    {
        var desk = new OpenTask("step.desk.choose") { Unreadable = true };

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-010", refused.Error.Code);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    private CompleteStepService Service(OpenTask desk, CallerContext caller)
    {
        var catalog = EvalComposition.Catalog;
        return new CompleteStepService(desk, desk, _approvals, _facts, _nonces, new FixedCaller(caller), new EngineFilledReport(catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_approvals, new FakeLauncher())));
    }

    private static CallerContext Session() => A.Caller().Session().Build();

    private static CompleteStepRequest Request() => new(_address.Value, RunToken.For(1, 1), OutcomeName.From("api"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

    private sealed class FixedCaller(CallerContext caller) : ICallerContextAccessor
    {
        public CallerContext? Current => caller;

        public IDisposable Enter(CallerContext context) => throw new NotSupportedException("not entered here");

        public IDisposable EnterService(string service) => throw new NotSupportedException("not entered here");
    }

    /// <summary>One instance with a task open on the named Step at state 'route-evidence-desk', recording what is sent to it.</summary>
    private sealed class OpenTask(string step) : IInstanceDirectory, IInstanceRuntime
    {
        private readonly List<Trigger> _sent = [];
        private readonly List<Trigger> _attempted = [];

        public bool Unreadable { get; set; }

        public int Reads { get; private set; }

        public IReadOnlyList<Trigger> Sent => _sent;

        public IReadOnlyList<Trigger> Attempted => _attempted;

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceAddress>>(_address);

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException("not started here");

        public ValueTask<Result<InstanceView>> Get(InstanceAddress at, CancellationToken cancellationToken) =>
            Read() && Unreadable ? ValueTask.FromResult<Result<InstanceView>>(WorkflowErrors.NoRuntime.With("down")) : ValueTask.FromResult<Result<InstanceView>>(View());

        public ValueTask<Result<CommandResult>> Send(InstanceAddress at, CommandNonce nonce, Trigger command, CancellationToken cancellationToken)
        {
            _attempted.Add(command);
            if (command is not StepCompleted done || done.Run != RunToken.For(1, 1))
            {
                return ValueTask.FromResult<Result<CommandResult>>(WorkflowErrors.StaleRunToken.With("stale"));
            }

            _sent.Add(command);
            return ValueTask.FromResult<Result<CommandResult>>(new CommandResult(View(), "-> next"));
        }

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress at, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException("not requested here");

        public ValueTask<Result<Unit>> Signal(InstanceAddress at, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException("not signalled here");

        private bool Read()
        {
            Reads++;
            return true;
        }

        private InstanceView View()
        {
            var descriptor = FixtureCatalog.Shipped().Find(BlockId.From(step))!;   // why: the shipped catalog test pins every id used here
            return new InstanceView(
                _address, new RecipeVersion(RecipeId.From("workflow.dev-evidence-route"), 1, RecipeHash.From("sha256:" + new string('a', 64))), "route-evidence-desk", Detail: null,
                DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null,
                new DeskTask(RunToken.For(1, 1), "route-evidence-desk", descriptor.Id, descriptor.Outcomes, descriptor.Outputs, new Dictionary<FactKey, InputFact>(), TimeoutAt: null),
                RunToken.For(1, 1), Children: [], Outcome: null)
            { Id = WorkflowInstanceId.Of(_address), Version = 3 };
        }
    }
}
