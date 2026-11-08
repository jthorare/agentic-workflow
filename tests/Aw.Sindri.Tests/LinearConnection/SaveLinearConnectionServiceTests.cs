using Aw.Context.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>POST /sindri/linear/connection: a pasted read key with its teams, or a write key. Owner only; the key goes to the store and is never in an answer.</summary>
[Lane(TestLane.Unit)]
public sealed class SaveLinearConnectionServiceTests
{
    private const string ReadKey = "lin_api_secret_read";
    private const string WriteKey = "lin_api_secret_write";

    private readonly SliceHarness _slice = new();
    private readonly FakeLinearKeyChecks _checks = new();
    private readonly FakeLinearConnectionStore _store = new();
    private readonly FakeLinearConnectionApproval _approval = new();
    private readonly InMemoryProfiles _profiles = LinearConnectionTestSupport.Profiles();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SaveLinearConnectionService Service() => new(_checks, _store, _approval, _slice.Callers, new FakeLinearCheckStore(), TimeProvider.System, LinearConnectionTestSupport.Guard(_profiles));

    private IDisposable Owner() => _slice.Callers.Enter(A.Caller().Human().Build());

    private static SaveLinearConnectionRequest Read(params string[] teams) => new("read", ReadKey, teams);

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    public async Task A_Linear_url_that_waits_for_approval_refuses_the_save_before_the_key_goes_anywhere(string kind)
    {
        using var entered = Owner();
        var service = new SaveLinearConnectionService(_checks, _store, _approval, _slice.Callers, new FakeLinearCheckStore(), TimeProvider.System, LinearConnectionTestSupport.Pending(_profiles));

        var result = await service.Handle(new SaveLinearConnectionRequest(kind, kind == "read" ? ReadKey : WriteKey, ["ENG"]), Token);

        Expect.Equal(SindriErrors.LinearBadValue.Code, result.Error.Code);
        Expect.Equal(LinearUrlGuard.Waiting, result.Error.Message);
        Assert.Empty(_checks.Asked);
        Assert.Empty(_store.ReadSaves);
        Assert.Empty(_store.WriteSaves);
        Expect.Equal(0, _approval.Settles);
    }

    [Fact]
    public async Task With_no_approved_profile_at_all_the_save_is_refused_the_same_way()
    {
        using var entered = Owner();
        var service = new SaveLinearConnectionService(_checks, _store, _approval, _slice.Callers, new FakeLinearCheckStore(), TimeProvider.System, new LinearUrlGuard(_profiles, _profiles));

        var result = await service.Handle(Read("ENG"), Token);

        Expect.Equal(SindriErrors.LinearBadValue.Code, result.Error.Code);
        Assert.Empty(_checks.Asked);
        Assert.Empty(_store.ReadSaves);
    }

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_and_nothing_is_asked_or_stored()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var result = await Service().Handle(Read("ENG"), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Expect.Equal(LinearConnectionRules.ChangeRefusal, result.Error.Message);
        Assert.Empty(_checks.Asked);
        Assert.Empty(_store.ReadSaves);
    }

    [Fact]
    public async Task A_read_save_with_no_team_is_SND_LINEAR_003_and_nothing_is_stored()
    {
        using var entered = Owner();

        var result = await Service().Handle(Read(), Token);

        Expect.Equal(SindriErrors.LinearTeamNotPicked.Code, result.Error.Code);
        Expect.Equal(LinearConnectionRules.NoTeams, result.Error.Message);
        Assert.Empty(_store.ReadSaves);
    }

    [Fact]
    public async Task A_read_key_that_cannot_read_a_ticked_team_names_the_team_and_is_not_stored()
    {
        using var entered = Owner();
        _ = _checks.Reading(new LinearConnectionTeamView("e1", "ENG", "Engineering"));

        var result = await Service().Handle(Read("ENG", "OPS"), Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Contains("OPS", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ReadKey, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_store.ReadSaves);
    }

    [Fact]
    public async Task A_read_key_is_stored_with_its_teams_and_the_profile_is_approved_when_that_is_all_that_waits()
    {
        using var entered = Owner();
        _ = _checks.Reading(new LinearConnectionTeamView("e1", "ENG", "Engineering"), new LinearConnectionTeamView("e2", "OPS", "Operations"));

        var result = await Service().Handle(Read("ENG", "OPS"), Token);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Saved);
        Assert.True(result.Value.Approved);
        Assert.Null(result.Value.Write);
        Expect.Equal(_profiles.Live.Value.Hash.Value, result.Value.ProfileHash);
        var (key, teams) = Expect.Only([.. _store.ReadSaves]);
        Expect.Equal(ReadKey, key);
        Expect.Sequence(["ENG", "OPS"], teams);
    }

    [Fact]
    public async Task A_read_save_with_other_changes_waiting_is_saved_but_not_approved()
    {
        using var entered = Owner();
        _ = _checks.Reading(new LinearConnectionTeamView("e1", "ENG", "Engineering"));
        _ = _approval.Settling(approved: false);

        var result = await Service().Handle(Read("ENG"), Token);

        Assert.True(result.Value.Saved);
        Assert.False(result.Value.Approved);
    }

    [Fact]
    public async Task A_write_key_equal_to_the_read_key_is_SND_LINEAR_001_and_not_stored()
    {
        using var entered = Owner();
        _ = _store.WithReadKey(WriteKey);

        var result = await Service().Handle(new SaveLinearConnectionRequest("write", WriteKey), Token);

        Expect.Equal(SindriErrors.LinearWriteKeySameAsRead.Code, result.Error.Code);
        Expect.Equal(LinearConnectionRules.SameAsRead, result.Error.Message);
        Assert.Empty(_store.WriteSaves);
    }

    [Fact]
    public async Task A_write_key_Linear_accepts_is_stored_and_resolves()
    {
        using var entered = Owner();

        var result = await Service().Handle(new SaveLinearConnectionRequest("write", WriteKey), Token);

        Assert.True(result.IsSuccess);
        Expect.Equal("resolves", result.Value.Write!.State);
        Expect.Sequence([WriteKey], _store.WriteSaves);
    }

    [Fact]
    public async Task A_write_key_whose_scope_Linear_does_not_say_is_saved_as_scopeUnknown()
    {
        using var entered = Owner();
        _ = _checks.WithScope(LinearKeyScope.Unknown);

        var result = await Service().Handle(new SaveLinearConnectionRequest("write", WriteKey), Token);

        Assert.True(result.Value.Saved);
        Expect.Equal("scopeUnknown", result.Value.Write!.State);
    }

    [Fact]
    public async Task A_write_key_with_no_keychain_to_hold_it_is_SND_LINEAR_014_and_nothing_is_approved()
    {
        using var entered = Owner();
        _ = _store.Failing(SindriErrors.LinearNoKeychain.With("No keychain to keep the write key in"));

        var result = await Service().Handle(new SaveLinearConnectionRequest("write", WriteKey), Token);

        Expect.Equal(SindriErrors.LinearNoKeychain.Code, result.Error.Code);
        Expect.Equal(0, _approval.Settles);
    }

    [Fact]
    public async Task A_write_key_Linear_refuses_is_SND_LINEAR_004_and_not_stored()
    {
        using var entered = Owner();
        _ = _checks.Failing(ContextErrors.LinearKeyRefused.With("refused"));

        var result = await Service().Handle(new SaveLinearConnectionRequest("write", WriteKey), Token);

        Expect.Equal("SND-LINEAR-004", result.Error.Code);
        Assert.Empty(_store.WriteSaves);
    }

    [Theory]
    [InlineData("other", "lin_api_x")]
    [InlineData("read", " ")]
    [InlineData("write", "")]
    public async Task A_kind_that_is_neither_read_nor_write_or_a_blank_key_is_invalid_and_nothing_is_asked(string kind, string key)
    {
        using var entered = Owner();

        var result = await Service().Handle(new SaveLinearConnectionRequest(kind, key, ["ENG"]), Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_checks.Asked);
    }

    [Fact]
    public void The_request_prints_without_the_key() =>
        Assert.DoesNotContain(ReadKey, Read("ENG").ToString(), StringComparison.Ordinal);
}
