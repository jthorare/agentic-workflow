using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
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
/// 'workflow complete' on an agent run (P8 U4, API spec §4.2, §8.1; plan Q6): only the session the launcher started for the run, proven by its launch
/// secret, reports it, and only while the run isn't taken over; while it is, only the human may, via the desk.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AgentReportTests
{
    private const string Proof = "the-launch-secret";
    private static readonly InstanceAddress _address = InstanceAddress.From("T-9/step/1");
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('c', 32));

    private readonly TemporalLikeRuntime _approvals = new();   // only its approval check is used
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());
    private readonly FakeLaunchFiles _files = new();

    [Fact(Timeout = 30_000)]
    public async Task The_launched_session_completes_its_agent_run()
    {
        var run = new AgentRun();
        _files.Open(_launch, Proof);

        var done = await Service(run, Session()).Handle(Request(new LaunchSecret(Proof)), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        var sent = Assert.IsType<StepCompleted>(Assert.Single(run.Sent));
        Assert.Equal(StepRuntime.Agent, sent.Via);
    }

    [Fact(Timeout = 30_000)]
    public async Task Another_session_gets_SND_AUTH_002()
    {
        var run = new AgentRun();
        _files.Open(_launch, Proof);

        var refused = await Service(run, Session()).Handle(Request(secret: null), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", refused.Error.Code);
        Assert.Empty(run.Attempted);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_wrong_secret_gets_SND_AUTH_003_and_consumes_no_nonce()
    {
        var run = new AgentRun();
        _files.Open(_launch, Proof);

        var refused = await Service(run, Session()).Handle(Request(new LaunchSecret("not-the-secret")), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-003", refused.Error.Code);
        Assert.Empty(run.Attempted);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_taken_over_run_refuses_the_secret()
    {
        var run = new AgentRun { TakenOver = true };
        _files.Open(_launch, Proof);

        var refused = await Service(run, Session()).Handle(Request(new LaunchSecret(Proof)), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", refused.Error.Code);
        Assert.Empty(run.Attempted);
        Assert.Equal(NonceClaim.Fresh, (await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_human_may_complete_while_taken_over_via_desk()
    {
        var run = new AgentRun { TakenOver = true };

        var done = await Service(run, A.Caller().Human().AtTerminal().Build()).Handle(Request(secret: null), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        var sent = Assert.IsType<StepCompleted>(Assert.Single(run.Sent));
        Assert.Equal(StepRuntime.Desk, sent.Via);
    }

    private CompleteStepService Service(AgentRun run, CallerContext caller) =>
        new(run, run, _approvals, _facts, _nonces, new FixedCaller(caller), new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, _files, new ReportOutputTypes(_approvals, new FakeLauncher())));

    private static CallerContext Session() => A.Caller().Session().Build();

    private static CompleteStepRequest Request(LaunchSecret? secret) =>
        new(_address.Value, RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce, secret);

    private sealed class FixedCaller(CallerContext caller) : ICallerContextAccessor
    {
        public CallerContext? Current => caller;

        public IDisposable Enter(CallerContext context) => throw new NotSupportedException("not entered here");

        public IDisposable EnterService(string service) => throw new NotSupportedException("not entered here");
    }

    /// <summary>One instance whose open run is an agent's on the 'produce' Step, launched as <see cref="_launch"/>; taken over when asked. Records what is sent to it.</summary>
    private sealed class AgentRun : IInstanceDirectory, IInstanceRuntime
    {
        private readonly List<Trigger> _sent = [];
        private readonly List<Trigger> _attempted = [];

        public bool TakenOver { get; init; }

        public IReadOnlyList<Trigger> Sent => _sent;

        public IReadOnlyList<Trigger> Attempted => _attempted;

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceAddress>>(_address);

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException("not started here");

        public ValueTask<Result<InstanceView>> Get(InstanceAddress at, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceView>>(View());

        public ValueTask<Result<CommandResult>> Send(InstanceAddress at, CommandNonce nonce, Trigger command, CancellationToken cancellationToken)
        {
            _attempted.Add(command);
            _sent.Add(command);
            return ValueTask.FromResult<Result<CommandResult>>(new CommandResult(View(), "-> checks"));
        }

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress at, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException("not requested here");

        public ValueTask<Result<Unit>> Signal(InstanceAddress at, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException("not signalled here");

        private InstanceView View() =>
            new(_address, new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64))), TakenOver ? "taken-over" : "produce", Detail: null,
                DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, RunToken.For(1, 1), Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(_address),
                Version = 3,
                AgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, _launch.Value, Task: null),
            };
    }
}
