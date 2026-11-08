using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>GET /sindri/linear/connection (Linear hub spec 2026-11-04 §3, Settings, Connections, Linear): owner only.</summary>
[Lane(TestLane.Unit)]
public sealed class GetLinearConnectionServiceTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeLinearConnectionFacts _facts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetLinearConnectionService Service() => new(_facts, _slice.Callers);

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_and_nothing_is_read()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var result = await Service().Handle(new GetLinearConnectionRequest(), Token);

        Assert.True(result.IsError);
        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Expect.Equal(LinearConnectionRules.ReadRefusal, result.Error.Message);
        Expect.Equal(0, _facts.Reads);
    }

    [Fact]
    public async Task The_owner_gets_the_facts_as_they_are()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        var view = new LinearConnectionView(new LinearKeyStateView("resolves", DateTimeOffset.UnixEpoch), new LinearKeyStateView("notSet", CheckedAt: null), [new LinearConnectionTeamView("t1", "ENG", "Engineering")], Pending: true);
        _ = _facts.Answering(view);

        var result = await Service().Handle(new GetLinearConnectionRequest(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(view, result.Value);
    }

    [Fact]
    public async Task A_failing_read_is_that_error()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _facts.Failing(SindriErrors.ProfileMissing.With("no profile"));

        var result = await Service().Handle(new GetLinearConnectionRequest(), Token);

        Expect.Equal(SindriErrors.ProfileMissing.Code, result.Error.Code);
    }
}
