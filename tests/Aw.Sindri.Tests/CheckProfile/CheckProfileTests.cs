using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CheckProfile;

namespace Aw.Sindri.Tests.CheckProfile;

/// <summary>The profile check (API spec C8): 'profile.checked' only when the (approved, live) pair changes.</summary>
public sealed class CheckProfileTests
{
    private readonly SliceHarness _h = new();
    private readonly LoadedProfile _a = SliceHarness.Profile("30s");
    private readonly LoadedProfile _b = SliceHarness.Profile("5s");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_check_appends_only_when_the_pair_changes()
    {
        var grant = await Approve(_a);
        _h.Profiles.Live = _a;

        var first = await Check(grant);
        var again = await Check(grant);

        _ = Assert.NotNull(first.Sequence);
        Assert.Null(again.Sequence);
        var check = Assert.Single(Checks());
        Assert.Equal((_a.Hash, _a.Hash, false), (check.Approved, check.Live, check.Drifted));
        Assert.Equal(["heartbeat"], check.ApprovedServices.Select(s => s.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_live_profile_and_its_revert_each_write_a_check_that_is_never_drifted()
    {
        var grant = await Approve(_a);
        _h.Profiles.Live = _b;
        _ = await Check(grant);
        _h.Profiles.Live = _a;

        _ = await Check(grant);

        Assert.Equal([(_b.Hash, false), (_a.Hash, false)], Checks().Select(c => (c.Live!.Value, c.Drifted)));
        Assert.All(Checks(), c => Assert.Empty(c.ChangedPaths));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changing_back_to_an_old_hash_writes_again()
    {
        var grant = await Approve(_a);
        foreach (var live in new[] { _a, _b, _a, _b })
        {
            _h.Profiles.Live = live;
            _ = await Check(grant);
        }

        Assert.Equal([false, false, false, false], Checks().Select(c => c.Drifted));
        Assert.Equal(["profile.checked:0", "profile.checked:1", "profile.checked:2", "profile.checked:3"],
            _h.Store.Events.Where(e => e.Envelope.Type == EventTypes.ProfileChecked).Select(e => e.IdempotencyKey), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_live_profile_is_live_null_and_not_drifted()
    {
        var grant = await _h.HoldLease();   // the harness's live profile is missing

        _ = await Check(grant);

        var check = Assert.Single(Checks());
        Assert.False(check.Drifted);
        Assert.Null(check.Live);
        Assert.Empty(check.ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_readable_live_profile_is_checked_and_not_drifted()
    {
        var grant = await _h.HoldLease();
        _h.Profiles.Live = _a;

        _ = await Check(grant);

        var check = Assert.Single(Checks());
        Assert.Equal((_a.Hash, false), (check.Live!.Value, check.Drifted));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_invalid_live_profile_file_is_recorded_as_a_check_with_no_live_hash()
    {
        var grant = await Approve(_a);
        _h.Profiles.Live = _a;
        _ = await Check(grant);
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var result = await new CheckProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, _h.Callers, _h.Ids).Handle(new CheckProfileRequest(grant), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        _ = Assert.NotNull(result.Value.Sequence);
        var last = Checks()[^1];
        Assert.Null(last.Live);
        Assert.False(last.Drifted);
        Assert.Equal(2, Checks().Count);
    }

    private async Task<LeaseGrant> Approve(LoadedProfile profile)
    {
        _ = await _h.Profiles.Save(profile, Token);
        return await _h.HoldLease();
    }

    private async Task<CheckProfileResponse> Check(LeaseGrant grant)
    {
        var service = new CheckProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, _h.Callers, _h.Ids);
        return (await service.Handle(new CheckProfileRequest(grant), Token)).Value;
    }

    private List<ProfileChecked> Checks() => [.. _h.Store.Events
        .Where(e => e.Envelope.Type == EventTypes.ProfileChecked)
        .Select(e => _h.Events.Payload<ProfileChecked>(e).Value)];
}
