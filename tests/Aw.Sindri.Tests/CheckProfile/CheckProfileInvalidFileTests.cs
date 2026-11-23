using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CheckProfile;

namespace Aw.Sindri.Tests.CheckProfile;

/// <summary>The profile recheck tick on an invalid live profile file (#3594): it records that the file went bad instead of failing every tick.</summary>
public sealed class CheckProfileInvalidFileTests
{
    private readonly SliceHarness _h = new();
    private readonly LoadedProfile _a = SliceHarness.Profile("30s");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_file_that_turns_invalid_is_recorded_as_a_check_with_no_profile()
    {
        _ = await _h.Profiles.Save(_a, Token);
        var grant = await _h.HoldLease();
        var service = new CheckProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, _h.Callers, _h.Ids);
        _ = await service.Handle(new CheckProfileRequest(grant), Token);
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var invalid = await service.Handle(new CheckProfileRequest(grant), Token);

        Assert.True(invalid.IsSuccess, invalid.IsError ? invalid.Error.Code : null);
        var checks = _h.Store.Events.Where(e => e.Envelope.Type == EventTypes.ProfileChecked).Select(e => _h.Events.Payload<ProfileChecked>(e).Value).ToList();
        Assert.Equal([(_a.Hash, _a.Hash), (null, null)], checks.Select(c => (c.Approved, c.Live)));
        Assert.False(checks[^1].Drifted);
    }
}
