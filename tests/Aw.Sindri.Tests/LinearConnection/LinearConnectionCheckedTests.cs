using Aw.Sindri.Core.Linear;
using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.LinearReads;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>
/// GET /sindri/linear/connection runs on every app event while Settings is open, so it resolves no key: the key states and CheckedAt are what the last save found, kept in <see cref="ILinearCheckStore"/>.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LinearConnectionCheckedTests
{
    private const string ReadPointer = "keychain:sindri-index/linear";
    private const string WritePointer = "keychain:sindri-index/linear-write";
    private const string Section = "{\"sources\":{\"linear\":{\"teams\":[\"ENG\"],\"token\":\"" + ReadPointer + "\",\"writeToken\":\"" + WritePointer + "\"}}}";

    private static readonly DateTimeOffset _savedAt = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly SliceHarness _slice = new();
    private readonly RigClock _clock = new();
    private readonly FakeLinearCheckStore _log = new();
    private readonly FakeLinearConnectionStore _store = new();
    private readonly InMemoryProfiles _profiles = new();

    public LinearConnectionCheckedTests()
    {
        _clock.SetUtcNow(_savedAt);
        _profiles.Live = Profile(Section);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Profile(string section)
    {
        var profile = new SindriProfile(1, "thor", Context: JsonDocument.Parse(section).RootElement.Clone());
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private ContextLinearConnectionFacts Facts() => new(_profiles, _profiles, FakeContextEngine.Instance, new FakeLinearSideFacts(), _log);

    private SaveLinearConnectionService Save() => new(new FakeLinearKeyChecks().Reading(new LinearConnectionTeamView("e1", "ENG", "Engineering")), _store, new FakeLinearConnectionApproval(), _profiles, _slice.Callers, _log, _clock);

    private RemoveLinearWriteKeyService Remove() => new(_store, new FakeLinearConnectionApproval(), _profiles, _slice.Callers, _log);

    private IDisposable Owner() => _slice.Callers.Enter(A.Caller().Human().Build());

    [Fact]
    public async Task Reads_after_a_save_resolve_no_key_and_keep_the_time_of_the_save()
    {
        using var entered = Owner();
        _ = await Save().Handle(new SaveLinearConnectionRequest("write", "lin_api_write", Teams: null), Token);
        _ = await Save().Handle(new SaveLinearConnectionRequest("read", "lin_api_read", ["ENG"]), Token);
        _clock.Advance(TimeSpan.FromHours(3));

        var first = (await Facts().Read(Token)).Value;
        _clock.Advance(TimeSpan.FromHours(3));
        var second = (await Facts().Read(Token)).Value;

        Expect.Equal(new LinearKeyStateView("resolves", _savedAt), first.Write);
        Expect.Equal(new LinearKeyStateView("resolves", _savedAt), first.Read);
        Expect.Equal(first, second);
    }

    [Fact]
    public async Task A_key_never_saved_through_settings_is_unchecked_with_no_time_and_resolves_nothing()
    {
        var view = (await Facts().Read(Token)).Value;

        Expect.Equal(new LinearKeyStateView("unchecked", CheckedAt: null), view.Read);
        Expect.Equal(new LinearKeyStateView("unchecked", CheckedAt: null), view.Write);
    }

    [Fact]
    public async Task A_check_of_another_pointer_is_not_trusted_for_the_pointer_the_profile_holds_now()
    {
        _ = await _log.RecordWrite(new LinearStoredCheck("keychain:sindri-index/old-write", "resolves", _savedAt), Token);

        var view = (await Facts().Read(Token)).Value;

        Expect.Equal(new LinearKeyStateView("unchecked", CheckedAt: null), view.Write);
    }

    [Fact]
    public async Task Removing_the_write_key_clears_its_check_and_leaves_the_read_check()
    {
        using var entered = Owner();
        _ = await Save().Handle(new SaveLinearConnectionRequest("write", "lin_api_write", Teams: null), Token);
        _ = await Save().Handle(new SaveLinearConnectionRequest("read", "lin_api_read", ["ENG"]), Token);

        _ = await Remove().Handle(new RemoveLinearWriteKeyRequest(), Token);

        var stored = (await _log.Read(Token)).Value;
        Expect.True(stored.Write is null);
        Expect.True(stored.Read is not null);
    }
}
