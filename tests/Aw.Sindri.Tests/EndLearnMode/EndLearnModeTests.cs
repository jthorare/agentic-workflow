using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.EndLearnMode;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.EndLearnMode;

/// <summary>'sindri sandbox learn off' (sandbox spec §3.6): the owner ends network learn mode early, for one repo or all. It only tightens, so there is no approval.</summary>
[Lane(TestLane.Unit)]
public sealed class EndLearnModeTests
{
    private readonly SliceHarness _slice = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private EndLearnModeService Service() => new(_slice.Store, _slice.Held, _slice.Events, _slice.Callers, _slice.Clock);

    [Fact]
    public async Task Learn_off_appends_learn_ended()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new EndLearnModeRequest(RepoName.From("web")), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_slice.Store.Events);
        var payload = _slice.Events.Payload<SandboxLearnEnded>(recorded).Value;
        Expect.Equal((EventTypes.SandboxLearnEnded, string.Create(CultureInfo.InvariantCulture, $"sandbox.learn-ended:web:{_slice.Clock.GetUtcNow().UtcTicks}"), recorded.Sequence), (recorded.Envelope.Type, recorded.IdempotencyKey, result.Value.Sequence));
        Expect.Equal(("web", _slice.Clock.GetUtcNow()), (payload.Repo?.Value, payload.At));
    }

    [Fact]
    public async Task Learn_off_without_a_repo_ends_it_for_every_repo()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new EndLearnModeRequest(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_slice.Store.Events);
        Expect.True(recorded.IdempotencyKey.StartsWith("sandbox.learn-ended:all:", StringComparison.Ordinal));
        Expect.True(_slice.Events.Payload<SandboxLearnEnded>(recorded).Value.Repo is null);
    }

    [Fact]
    public void Learn_off_is_refused_for_an_agent_session()
    {
        var row = new CommandCatalog().Rows.Single(r => string.Equals(r.Action.Value, "EndLearnMode", StringComparison.Ordinal));

        var launched = Authorization.Decide(row.Class, A.Caller().Launched().Build(), row);
        var human = Authorization.Decide(row.Class, A.Caller().Human().Build(), row);

        Expect.Equal(SindriErrors.AuthRefused.Code, launched.Refusal?.Code);
        Expect.True(human.Refusal is null);
    }
}
