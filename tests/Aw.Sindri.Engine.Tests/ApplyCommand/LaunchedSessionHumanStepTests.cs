using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>A session Sindri launched can't answer a human step through a command or an answer (q100, SND-AUTH-002); an owner session, a human and an agent step are not touched by the rule.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchedSessionHumanStepTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-00000000000000000000000000000001");

    private readonly TemporalLikeRuntime _instance = new() { OpenTask = DeskTaskOpen() };
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());
    private readonly ApplyCommandService _service;

    public LaunchedSessionHumanStepTests()
    {
        _service = new ApplyCommandService(_instance, _instance, _instance, _nonces, _instance);
    }

    public static TheoryData<ApplyCommandKind> Kinds => [ApplyCommandKind.Answer, ApplyCommandKind.Command];

    [Theory(Timeout = 30_000)]
    [MemberData(nameof(Kinds))]
    public async Task A_launched_session_on_a_human_step_is_refused(ApplyCommandKind kind)
    {
        _instance.Current = Session() with { Launch = _launch };

        var result = await _service.Handle(Request(kind), TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", result.IsError ? result.Error.Code : "accepted");
        Assert.Contains("human step", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_instance.Delivered);
    }

    [Theory(Timeout = 30_000)]
    [MemberData(nameof(Kinds))]
    public async Task An_owner_session_on_the_same_step_is_not_refused(ApplyCommandKind kind)
    {
        _instance.Current = Session();

        var result = await _service.Handle(Request(kind), TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.IsError ? result.Error.Message : null);
        _ = Assert.Single(_instance.Delivered);
    }

    [Theory(Timeout = 30_000)]
    [MemberData(nameof(Kinds))]
    public async Task A_launched_session_on_an_agent_step_is_not_refused(ApplyCommandKind kind)
    {
        _instance.OpenAgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, _launch.Value, Task: null);
        _instance.Current = Session() with { Launch = _launch };

        var result = await _service.Handle(Request(kind), TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.IsError ? result.Error.Message : null);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_launched_session_answering_an_open_park_is_refused_and_its_nonce_is_given_back()
    {
        _instance.OpenTask = null;
        _instance.OpenPark = new OpenQuestion(SuspensionKind.Park, ParkReason.Rounds, "Out of rounds");
        _instance.Current = Session() with { Launch = _launch };
        var nonce = CommandNonce.From(new string('d', 32));

        var result = await _service.Handle(Request(ApplyCommandKind.Answer) with { Nonce = nonce }, TestContext.Current.CancellationToken);

        Assert.Equal("SND-AUTH-002", result.IsError ? result.Error.Code : "accepted");
        Assert.Empty(_instance.Delivered);
        var reclaimed = await _nonces.Claim(nonce, "another subject", TestContext.Current.CancellationToken);   // a nonce still held for the refused command would be SND-WF-009
        Assert.False(reclaimed.IsError, reclaimed.IsError ? reclaimed.Error.Message : null);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_launched_session_may_still_cancel_a_human_step()
    {
        _instance.Current = Session() with { Launch = _launch };

        var result = await _service.Handle(new ApplyCommandRequest(_instance.Address.Value, ApplyCommandKind.Cancel), TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.IsError ? result.Error.Message : null);
    }

    private ApplyCommandRequest Request(ApplyCommandKind kind) => kind == ApplyCommandKind.Answer
        ? new ApplyCommandRequest(_instance.Address.Value, kind, AnswerVariant.Text, Text: "go on")
        : new ApplyCommandRequest(_instance.Address.Value, kind, Verb: CommandVerb.From("ship"));

    private static CallerContext Session() => Testing.A.Caller().Session().Build();

    private static DeskTask DeskTaskOpen() =>
        new(RunToken.For(1, 1), "desk", BlockId.From("step.desk.answer"), [], [], new Dictionary<FactKey, InputFact>(), TimeoutAt: null);
}
