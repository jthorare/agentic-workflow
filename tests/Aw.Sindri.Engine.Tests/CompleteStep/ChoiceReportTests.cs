using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Workflow.Choice;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>'workflow complete' on a choice Step (DT spec §8.1): the engine writes fact:decision from --outcome, and a hand-written one is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class ChoiceReportTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-7/evidence/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('f', 32));

    private readonly TemporalLikeRuntime _like = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly ChoiceDesk _desk;

    public ChoiceReportTests()
    {
        _desk = new ChoiceDesk(_address);
    }

    [Fact(Timeout = 30_000)]
    public async Task CompleteStep_on_a_choice_step_stores_an_engine_filled_decision()
    {
        var service = new CompleteStepService(_desk, _desk, _like, _facts, new InMemoryCommandNonces(new FakeTimeProvider()), _like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_like, new FakeLauncher())));

        var done = await service.Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        var report = Assert.IsType<StepCompleted>(Assert.Single(_desk.Sent));
        var decision = Assert.Single(report.Outputs);
        Assert.Equal("fact:decision", decision.Key.Value);
        var content = await _facts.Read(decision.Value, TestContext.Current.CancellationToken);
        Assert.Equal($$"""{"choice":"api","provider":"desk","actor":"{{_like.Current!.Actor}}"}""", Encoding.UTF8.GetString(content.Value.Span));
    }

    [Fact(Timeout = 30_000)]
    public async Task CompleteStep_refuses_a_hand_written_engine_filled_output()
    {
        var nonces = new InMemoryCommandNonces(new FakeTimeProvider());
        var service = new CompleteStepService(_desk, _desk, _like, _facts, nonces, _like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_like, new FakeLauncher())));
        var written = new Dictionary<FactKey, ReadOnlyMemory<byte>> { [FactKey.From("fact:decision")] = """{"choice":"db"}"""u8.ToArray() };

        var refused = await service.Handle(Request(written), TestContext.Current.CancellationToken);

        Assert.Equal("SND-CLI-003", refused.Error.Code);
        Assert.Equal("fact:decision is written by the engine on 'step.desk.choose' and 'step.judge.choose' (fix: drop --output fact:decision)", refused.Error.Message);
        Assert.Empty(_desk.Sent);
        Assert.Equal(NonceClaim.Fresh, (await nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_choice_report_retried_with_its_nonce_after_the_instance_moved_on_is_already_applied()
    {
        var service = new CompleteStepService(_desk, _desk, _like, _facts, new InMemoryCommandNonces(new FakeTimeProvider()), _like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_like, new FakeLauncher())));
        var first = await service.Handle(Request(), TestContext.Current.CancellationToken);
        _desk.MoveOn();

        var retry = await service.Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(first.IsError, first.IsError ? first.Error.Message : null);
        Assert.False(retry.IsError, retry.IsError ? retry.Error.Message : null);
        Assert.True(retry.Value.AlreadyApplied);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_choice_report_retried_without_the_view_after_a_lost_first_send_carries_the_retry_fix_when_the_engine_refuses_it()
    {
        var service = new CompleteStepService(_desk, _desk, _like, _facts, new InMemoryCommandNonces(new FakeTimeProvider()), _like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_like, new FakeLauncher())));
        _desk.LoseFirstSend = true;
        var first = await service.Handle(Request(), TestContext.Current.CancellationToken);
        _desk.Unreadable = true;

        var retry = await service.Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-010", first.Error.Code);
        Assert.Equal("SND-WF-008", retry.Error.Code);
        Assert.Contains("retry 'workflow complete' with the same --nonce", retry.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("include the required output", retry.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_choice_report_for_a_stale_run_is_the_stale_run_error_and_attempts_no_engine_filled_output()
    {
        var service = new CompleteStepService(_desk, _desk, _like, _facts, new InMemoryCommandNonces(new FakeTimeProvider()), _like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_like, new FakeLauncher())));
        var stale = new CompleteStepRequest(_address.Value, RunToken.For(2, 1), OutcomeName.From("api"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

        var refused = await service.Handle(stale, TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-005", refused.Error.Code);
        Assert.Empty(_desk.Sent);
        var attempted = Assert.IsType<StepCompleted>(Assert.Single(_desk.Attempted));   // the refused Send is recorded too
        Assert.Empty(attempted.Outputs);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_hand_written_engine_filled_output_is_refused_without_reading_the_instance()
    {
        var nonces = new InMemoryCommandNonces(new FakeTimeProvider());
        var service = new CompleteStepService(_desk, _desk, _like, _facts, nonces, _like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_like, new FakeLauncher())));
        var written = new Dictionary<FactKey, ReadOnlyMemory<byte>> { [FactKey.From("fact:decision")] = """{"choice":"db"}"""u8.ToArray() };
        _desk.Unreadable = true;

        var refused = await service.Handle(Request(written), TestContext.Current.CancellationToken);

        Assert.Equal("SND-CLI-003", refused.Error.Code);
        Assert.Empty(_desk.Sent);
        Assert.Equal(NonceClaim.Fresh, (await nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_fresh_report_on_an_instance_that_cant_be_read_is_that_error_and_claims_no_nonce()
    {
        var nonces = new InMemoryCommandNonces(new FakeTimeProvider());
        var service = new CompleteStepService(_desk, _desk, _like, _facts, nonces, _like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_like, new FakeLauncher())));
        _desk.Unreadable = true;

        var refused = await service.Handle(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-010", refused.Error.Code);
        Assert.Empty(_desk.Sent);
        Assert.Equal(NonceClaim.Fresh, (await nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);   // nothing claimed it, or the claim was given back
    }

    private static CompleteStepRequest Request(Dictionary<FactKey, ReadOnlyMemory<byte>>? outputs = null) =>
        new(_address.Value, RunToken.For(1, 1), OutcomeName.From("api"), outputs ?? [], _nonce);

    /// <summary>One instance waiting at a step.desk.choose state, recording what is sent to it.</summary>
    private sealed class ChoiceDesk(InstanceAddress address) : IInstanceDirectory, IInstanceRuntime
    {
        private static readonly BlockDescriptor _descriptor = FixtureCatalog.Shipped().Find(BlockId.From("step.desk.choose"))!;
        private readonly List<Trigger> _sent = [];
        private readonly List<Trigger> _attempted = [];
        private readonly HashSet<CommandNonce> _seen = [];
        private bool _movedOn;

        public bool Unreadable { get; set; }

        /// <summary>The first Send is lost (unavailable), as a dropped connection after the claim; later ones reach the engine's decision.</summary>
        public bool LoseFirstSend { get; set; }

        public IReadOnlyList<Trigger> Sent => _sent;

        /// <summary>Every command a Send was given, accepted or refused.</summary>
        public IReadOnlyList<Trigger> Attempted => _attempted;

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceAddress>>(address);

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException("not started here");

        /// <summary>The open task is gone: the instance moved to its next state.</summary>
        public void MoveOn() => _movedOn = true;

        public ValueTask<Result<InstanceView>> Get(InstanceAddress at, CancellationToken cancellationToken) =>
            Unreadable ? ValueTask.FromResult<Result<InstanceView>>(WorkflowErrors.NoRuntime.With("down")) : ValueTask.FromResult<Result<InstanceView>>(View());

        public ValueTask<Result<CommandResult>> Send(InstanceAddress at, CommandNonce nonce, Trigger command, CancellationToken cancellationToken)
        {
            _attempted.Add(command);
            if (LoseFirstSend)
            {
                LoseFirstSend = false;
                return ValueTask.FromResult<Result<CommandResult>>(WorkflowErrors.NoRuntime.With("connection lost"));
            }

            if (!_seen.Contains(nonce) && command is StepCompleted { Outputs.Count: 0 })
            {
                // The engine decides on what arrived: a choice report without fact:decision is malformed (its own fix text).
                var route = EvalComposition.Recipe(RouteRecipes.Dev);
                if (EngineFixture.Decide(EngineFixture.At(route, "route-evidence-desk"), route, command) is Rejected rejected)
                {
                    return ValueTask.FromResult<Result<CommandResult>>(rejected.Error);
                }
            }

            // Temporal answers an Update id it has seen with the first result; otherwise the workflow's validator checks the run.
            if (!_seen.Contains(nonce) && (_movedOn || command is not StepCompleted done || done.Run != RunToken.For(1, 1)))
            {
                return ValueTask.FromResult<Result<CommandResult>>(WorkflowErrors.StaleRunToken.With("stale"));
            }

            _ = _seen.Add(nonce);
            _sent.Add(command);
            return ValueTask.FromResult<Result<CommandResult>>(new CommandResult(View(), "-> evidence-api"));
        }

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress at, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException("not requested here");

        public ValueTask<Result<Unit>> Signal(InstanceAddress at, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException("not signalled here");

        private InstanceView View() => new(
            address, new RecipeVersion(RecipeId.From("workflow.dev-evidence-route"), 1, RecipeHash.From("sha256:" + new string('a', 64))), "route-evidence-desk", Detail: null,
            DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null,
            _movedOn ? null : new DeskTask(RunToken.For(1, 1), "route-evidence-desk", _descriptor.Id, _descriptor.Outcomes, _descriptor.Outputs, new Dictionary<FactKey, InputFact>(), TimeoutAt: null),
            RunToken.For(1, 1), Children: [], Outcome: null)
        { Id = WorkflowInstanceId.Of(address), Version = 3 };
    }
}
