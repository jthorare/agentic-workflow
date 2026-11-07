using Aw.Context.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.GetLinearLookups;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearReads;

/// <summary>GET /sindri/linear/lookups/{team} (Linear hub spec 2026-11-04 §3, §4.2): owner only; a team never synced is a 200 with nothing in it.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearLookupsServiceTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeLinearSideFacts _facts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetLinearLookupsService Service() => new(_facts, _slice.Callers);

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_and_nothing_is_read()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var result = await Service().Handle(new GetLinearLookupsRequest("ENG"), Token);

        Assert.True(result.IsError);
        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Empty(_facts.AskedTeams);
    }

    [Fact]
    public async Task Contexts_session_refusal_SND_CONTEXT_020_is_SND_AUTH_002()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _facts.Failing(ContextErrors.SessionsDisabled.With("the pickers' lists are the owner's"));

        var result = await Service().Handle(new GetLinearLookupsRequest("ENG"), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Expect.Equal(GetLinearLookupsService.Refusal, result.Error.Message);
    }

    [Fact]
    public async Task A_team_never_synced_is_a_200_with_no_sync_time_and_empty_lists()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _facts.WithTeam(new LinearLookupsResponse(new LinearTeamRef(Uuid: null, "OPS", Name: null), SyncedAt: null, [], [], [], []));

        var result = await Service().Handle(new GetLinearLookupsRequest("OPS"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Null(result.Value.SyncedAt);
        Assert.Empty(result.Value.States);
        Assert.Empty(result.Value.Members);
        Assert.Empty(result.Value.Labels);
        Assert.Empty(result.Value.Cycles);
        Assert.Null(result.Value.Team.Uuid);
        Assert.Null(result.Value.Team.Name);
        Expect.Sequence(["OPS"], _facts.AskedTeams);
    }

    [Fact]
    public async Task A_synced_team_is_answered_as_stored()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        var synced = new LinearLookupsResponse(new LinearTeamRef("t1", "ENG", "Engineering"), new DateTimeOffset(2025, 12, 31, 23, 58, 0, TimeSpan.Zero),
            [new LinearStateOption("s1", "Todo", "unstarted", 1)], [new LinearMemberOption("u1", "Joi Example", Active: true)], [], []);
        _ = _facts.WithTeam(synced);

        var result = await Service().Handle(new GetLinearLookupsRequest("ENG"), Token);

        Expect.Equal(synced, result.Value);
    }

    [Fact]
    public async Task A_team_outside_the_picked_ones_is_403_SND_LINEAR_003_when_the_profile_picks_teams()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _facts.WithTeamsPicked(true);

        var result = await Service().Handle(new GetLinearLookupsRequest("OPS"), Token);

        Expect.Equal("SND-LINEAR-003", result.Error.Code);
        Expect.Equal(Aw.Kernel.ErrorKind.Forbidden, result.Error.Kind);
        Expect.Equal("Sindri only reads teams you picked; OPS isn't one (fix: Settings, Connections, Linear, add OPS)", result.Error.Message);
    }

    [Fact]
    public async Task A_team_nothing_is_stored_for_is_404_when_the_profile_picks_no_teams()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _facts.WithTeamsPicked(false);

        var result = await Service().Handle(new GetLinearLookupsRequest("OPS"), Token);

        Expect.Equal("SND-LINEAR-404", result.Error.Code);
        Expect.Equal(Aw.Kernel.ErrorKind.NotFound, result.Error.Kind);
        Expect.Equal("Sindri has nothing stored for OPS yet", result.Error.Message);
    }

    [Fact]
    public async Task Inactive_members_and_the_stored_name_sync_time_and_label_colour_stay_in_the_answer()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        var stored = new LinearLookupsResponse(new LinearTeamRef("t1", "ENG", "Engineering"), new DateTimeOffset(2025, 12, 31, 23, 58, 0, TimeSpan.Zero),
            [], [new LinearMemberOption("u3", "Former Member", Active: false)], [new LinearLabelOption("l1", "bug", "#eb5757")], []);
        _ = _facts.WithTeam(stored);

        var result = await Service().Handle(new GetLinearLookupsRequest("ENG"), Token);

        Assert.False(Expect.Only(result.Value.Members).Active);
        Expect.Equal("#eb5757", Expect.Only(result.Value.Labels).Color!);
    }
}
