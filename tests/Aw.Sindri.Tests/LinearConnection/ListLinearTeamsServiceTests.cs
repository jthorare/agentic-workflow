using Aw.Context.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>POST /sindri/linear/connection/teams: the teams a pasted read key can see. Owner only; the key is never in an answer.</summary>
[Lane(TestLane.Unit)]
public sealed class ListLinearTeamsServiceTests
{
    private const string Key = "lin_api_secret_read";

    private readonly SliceHarness _slice = new();
    private readonly FakeLinearKeyChecks _checks = new();
    private readonly InMemoryProfiles _profiles = LinearConnectionTestSupport.Profiles();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ListLinearTeamsService Service() => new(_checks, _slice.Callers, LinearConnectionTestSupport.Guard(_profiles));

    [Fact]
    public async Task A_Linear_url_that_waits_for_approval_refuses_the_list_and_Linear_is_not_asked()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        var service = new ListLinearTeamsService(_checks, _slice.Callers, LinearConnectionTestSupport.Pending(_profiles));

        var result = await service.Handle(new ListLinearTeamsRequest(Key), Token);

        Expect.Equal(SindriErrors.LinearUrlNotApproved.Code, result.Error.Code);
        Expect.Equal(LinearUrlGuard.Waiting, result.Error.Message);
        Assert.Empty(_checks.Asked);
    }

    [Fact]
    public async Task With_no_approved_profile_at_all_the_list_is_refused_the_same_way()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        var service = new ListLinearTeamsService(_checks, _slice.Callers, new LinearUrlGuard(_profiles, _profiles));

        var result = await service.Handle(new ListLinearTeamsRequest(Key), Token);

        Expect.Equal(SindriErrors.LinearUrlNotApproved.Code, result.Error.Code);
        Assert.Empty(_checks.Asked);
    }

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_and_Linear_is_not_asked()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var result = await Service().Handle(new ListLinearTeamsRequest(Key), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Empty(_checks.Asked);
    }

    [Fact]
    public async Task The_teams_the_key_reads_come_back_in_Linears_order()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _checks.Reading(new LinearConnectionTeamView("e1", "ENG", "Engineering"), new LinearConnectionTeamView("e2", "OPS", "Operations"));

        var result = await Service().Handle(new ListLinearTeamsRequest(Key), Token);

        Assert.True(result.IsSuccess);
        Expect.Sequence(["ENG", "OPS"], result.Value.Teams.Select(static t => t.Key));
        Expect.Sequence([Key], _checks.Asked);
    }

    [Fact]
    public async Task An_empty_key_is_invalid_and_Linear_is_not_asked()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new ListLinearTeamsRequest("  "), Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_checks.Asked);
    }

    [Fact]
    public async Task A_key_Linear_refuses_is_SND_LINEAR_004_in_the_pages_words_without_the_key()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _checks.Failing(ContextErrors.LinearKeyRefused.With($"Linear refused {Key}"));

        var result = await Service().Handle(new ListLinearTeamsRequest(Key), Token);

        Expect.Equal("SND-LINEAR-004", result.Error.Code);
        Expect.Equal("Linear refused this key (fix: make a new key at linear.app/settings/api and paste it)", result.Error.Message);
        Assert.DoesNotContain(Key, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Linear_not_answering_is_SND_LINEAR_010()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _checks.Failing(ContextErrors.SourceUnavailable.With("no answer"));

        var result = await Service().Handle(new ListLinearTeamsRequest(Key), Token);

        Expect.Equal("SND-LINEAR-010", result.Error.Code);
    }

    [Fact]
    public async Task A_reply_of_an_unknown_shape_passes_as_SND_CONTEXT_072()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _checks.Failing(ContextErrors.LinearUnexpectedReply.With("odd body"));

        var result = await Service().Handle(new ListLinearTeamsRequest(Key), Token);

        Expect.Equal(ContextErrors.LinearUnexpectedReply.Code, result.Error.Code);
    }

    [Fact]
    public void The_request_prints_without_the_key() =>
        Assert.DoesNotContain(Key, new ListLinearTeamsRequest(Key).ToString(), StringComparison.Ordinal);
}
