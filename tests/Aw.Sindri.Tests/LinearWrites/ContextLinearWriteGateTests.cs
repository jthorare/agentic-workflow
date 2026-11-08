using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Testing.Linear;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.LinearConnection;
using Aw.Sindri.Tests.LinearReads;

namespace Aw.Sindri.Tests.LinearWrites;

/// <summary>The real write gate over Context (Linear hub spec §5.1), not a fake of it: allowed for the owner with a resolving write key and a picked team, and each refusal with its own code (#2261).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextLinearWriteGateTests
{
    private readonly SliceHarness _slice = new();
    private readonly InMemoryProfiles _profiles = LinearConnectionTestSupport.Profiles();
    private readonly FakeContextLinearWrites _writes = new();
    private readonly FakeLinearSideFacts _facts = new FakeLinearSideFacts().WithTeam(new LinearLookupsResponse(new LinearTeamRef("t1", "ENG", "Engineering"), SyncedAt: null, [], [], [], []));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ContextLinearWriteGate Gate()
    {
        _ = _profiles.Save(_profiles.Live.Value, Token).AsTask().GetAwaiter().GetResult();
        return new ContextLinearWriteGate(new LinearScopes(_profiles, _slice.Callers), _writes, _facts);
    }

    [Fact]
    public async Task The_owner_with_a_resolving_key_and_a_picked_team_is_allowed()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var refusal = await Gate().Refusal("ENG", Token);

        Assert.Null(refusal);
        Expect.Sequence(["ENG"], _facts.AskedTeams);
    }

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_before_the_key_or_the_team_is_read()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var refusal = await Gate().Refusal("ENG", Token);

        Assert.NotNull(refusal);
        Expect.Equal(SindriErrors.AuthRefused.Code, refusal.Code);
        Assert.Empty(_facts.AskedTeams);
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task With_no_profile_the_gate_answers_SND_PROFILE_001()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        var gate = new ContextLinearWriteGate(new LinearScopes(_profiles, _slice.Callers), _writes, _facts);

        var refusal = await gate.Refusal("ENG", Token);

        Assert.NotNull(refusal);
        Expect.Equal(SindriErrors.ProfileMissing.Code, refusal.Code);
        Assert.Empty(_facts.AskedTeams);
    }

    [Fact]
    public async Task A_write_key_that_is_not_set_is_refused_SND_LINEAR_002()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _writes.SetWriteKeyStatus(new LinearWriteKeyNotSet());

        var refusal = await Gate().Refusal("ENG", Token);

        Assert.NotNull(refusal);
        Expect.Equal(SindriErrors.LinearNoWriteKey.Code, refusal.Code);
        Assert.Empty(_facts.AskedTeams);
    }

    [Fact]
    public async Task A_write_key_that_is_the_read_key_is_refused_SND_LINEAR_002()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _writes.SetWriteKeyStatus(new LinearWriteKeySameAsReadKey());

        var refusal = await Gate().Refusal("ENG", Token);

        Assert.NotNull(refusal);
        Expect.Equal(SindriErrors.LinearNoWriteKey.Code, refusal.Code);
    }

    [Fact]
    public async Task A_write_key_Linear_refused_is_refused_SND_LINEAR_002()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _writes.SetWriteKeyStatus(new LinearWriteKeyRefused("401"));

        var refusal = await Gate().Refusal("ENG", Token);

        Assert.NotNull(refusal);
        Expect.Equal(SindriErrors.LinearNoWriteKey.Code, refusal.Code);
    }

    [Fact]
    public async Task A_team_the_profile_did_not_pick_is_refused_SND_LINEAR_003_and_names_the_team()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var refusal = await Gate().Refusal("OPS", Token);

        Assert.NotNull(refusal);
        Expect.Equal(SindriErrors.LinearTeamNotPicked.Code, refusal.Code);
        Assert.Contains("OPS", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_lookups_read_comes_back_as_its_own_error_not_as_a_refusal()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _facts.Failing(ContextErrors.SessionsDisabled.With("the lookups are the owner's"));

        var refusal = await Gate().Refusal("ENG", Token);

        Assert.NotNull(refusal);
        Expect.Equal(ContextErrors.SessionsDisabled.Code, refusal.Code);
    }
}
