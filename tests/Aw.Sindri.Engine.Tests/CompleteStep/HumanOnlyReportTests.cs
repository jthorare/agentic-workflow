using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;
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

/// <summary>
/// 'workflow complete' on a Step a human decides (owner q100, 2026-10-24): who started the agent decides, not which Step it is. A plain session (an agent the owner
/// started, no launch secret) answers every one; a session Sindri launched reports with its launch secret, and a secret on a desk run is refused SND-AUTH-002 before the nonce is claimed.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class HumanOnlyReportTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-7/evidence/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('e', 32));

    private readonly TemporalLikeRuntime _approvals = new();   // only its approval check is used
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());

    [Theory(Timeout = 30_000)]
    [InlineData("step.desk.choose")]
    [InlineData("step.judge.choose")]
    [InlineData("step.owner.decide")]
    [InlineData("step.evidence.desk")]
    [InlineData("step.scope.review")]
    [InlineData("step.interview.answer")]
    [InlineData("step.pr.docs-merge-decide")]
    [InlineData("step.phase.decide")]
    [InlineData("step.produce")]
    public async Task A_plain_session_answers_a_step_a_human_decides(string step)
    {
        var desk = new OpenTask(step);

        var done = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        _ = Assert.Single(desk.Sent);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("step.desk.choose")]
    [InlineData("step.scope.review")]
    [InlineData("step.pr.docs-merge-decide")]
    public async Task A_session_Sindri_launched_is_refused_on_a_desk_step_and_claims_nothing(string step)
    {
        var desk = new OpenTask(step);

        var refused = await Service(desk, Session()).Handle(Request(new LaunchSecret("launched-child-secret")), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", refused.Error.Code);
        Assert.Empty(desk.Sent);
        Assert.Empty(desk.Attempted);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_human_in_the_app_or_a_terminal_completes_a_step(bool terminal)
    {
        var desk = new OpenTask("step.desk.choose");
        var human = terminal ? A.Caller().Human().AtTerminal().Build() : A.Caller().Human().Build();

        var done = await Service(desk, human).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        _ = Assert.Single(desk.Sent);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_stale_run_is_answered_by_the_send()
    {
        var desk = new OpenTask("step.desk.choose");
        var stale = new CompleteStepRequest(_address.Value, RunToken.For(2, 1), OutcomeName.From("api"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

        var refused = await Service(desk, Session()).Handle(stale, TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-005", refused.Error.Code);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_session_report_on_an_instance_that_cant_be_read_is_that_error_and_claims_nothing()
    {
        var desk = new OpenTask("step.desk.choose") { Unreadable = true };

        var refused = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-010", refused.Error.Code);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    /// <summary>Review run 1 (arch-r1-1): the view read serves the engine-filled outputs and the approval check too.</summary>
    [Fact(Timeout = 30_000)]
    public async Task A_session_report_reads_the_instance_once()
    {
        var desk = new OpenTask("step.produce");

        var done = await Service(desk, Session()).Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        Assert.Equal(1, desk.Reads);
    }

    private CompleteStepService Service(OpenTask desk, CallerContext caller)
    {
        var catalog = EvalComposition.Catalog;
        return new CompleteStepService(desk, desk, _approvals, _facts, _nonces, new FixedCaller(caller), new EngineFilledReport(catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_approvals, new FakeLauncher(), new ReportRefusals())));
    }

    private static CallerContext Session() => A.Caller().Session().Build();

    private static CompleteStepRequest Request(LaunchSecret? secret = null) => new(_address.Value, RunToken.For(1, 1), OutcomeName.From("api"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce, secret);

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
