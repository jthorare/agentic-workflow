using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ApproveProfile;

public sealed class ApproveProfileTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ApproveProfileService Service() => new(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events);

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_without_a_hash_shows_the_hash_and_changes_and_records_nothing()
    {
        _h.Profiles.Live = SliceHarness.Profile("5s");
        _ = await _h.HoldLease();

        var shown = (await Service().Handle(new ApproveProfileRequest(Hash: null, Confirmed: false), Token)).Value;

        Assert.Equal(_h.Profiles.Live.Value.Hash, shown.Hash);
        Assert.False(shown.Approved);
        Assert.Contains("services.heartbeat.every", shown.Changes, StringComparer.Ordinal);
        Assert.Null((await _h.Profiles.Latest(Token)).Value);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_with_the_live_hash_and_confirmation_saves_and_records_it()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();

        var approved = (await Service().Handle(new ApproveProfileRequest(live.Hash, Confirmed: true), Token)).Value;

        Assert.True(approved.Approved);
        Assert.Equal(live.Hash, (await _h.Profiles.Latest(Token)).Value!.Hash);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal((EventTypes.ProfileApproved, $"profile.approved:{live.Hash.Value}:after:0"), (recorded.Envelope.Type, recorded.IdempotencyKey));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("wrong-hash", true, "SND-PROFILE-003")]
    [InlineData("live", false, "SND-PROFILE-004")]
    public async Task ApproveProfile_refuses_a_stale_hash_or_a_missing_confirmation(string which, bool confirmed, string code)
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        var hash = which == "live" ? live.Hash : SliceHarness.Profile("9s").Hash;

        var refused = await Service().Handle(new ApproveProfileRequest(hash, confirmed), Token);

        Assert.Equal(code, refused.Error.Code);
        Assert.Null((await _h.Profiles.Latest(Token)).Value);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_of_an_invalid_profile_returns_its_error()
    {
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/p.yaml: user: must be non-empty text");

        Assert.Equal(SindriErrors.ProfileInvalid.Code, (await Service().Handle(new ApproveProfileRequest(Hash: null, Confirmed: false), Token)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_whose_append_fails_leaves_the_approval_unchanged()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        _h.Held.Hold(new LeaseGrant(FakeProcessProbe.Holder(100), 99, KnownInstants.Epoch.AddMinutes(1)));   // an epoch the store doesn't hold

        var refused = await Service().Handle(new ApproveProfileRequest(live.Hash, Confirmed: true), Token);

        Assert.Equal(SindriErrors.LeaseStale.Code, refused.Error.Code);
        Assert.Null((await _h.Profiles.Latest(Token)).Value);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_records_each_approval_of_a_back_and_forth_and_every_surface_agrees()
    {
        var (a, b) = (SliceHarness.Profile("5s"), SliceHarness.Profile("9s"));
        _ = await _h.HoldLease();

        foreach (var profile in new[] { a, b, a, b, a })
        {
            _h.Profiles.Live = profile;
            Assert.True((await Service().Handle(new ApproveProfileRequest(profile.Hash, Confirmed: true), Token)).Value.Approved);

            var preview = (await Service().Handle(new ApproveProfileRequest(Hash: null, Confirmed: false), Token)).Value;
            Assert.Equal((true, false, true), await Surfaces());
        }

        var events = _h.Store.Events;
        Assert.Equal(5, events.Count);
        Assert.Equal(
            [$"profile.approved:{a.Hash.Value}:after:0", .. events.Take(4).Select((e, i) => string.Create(CultureInfo.InvariantCulture, $"profile.approved:{(i % 2 == 0 ? b : a).Hash.Value}:after:{e.Sequence}"))],
            events.Select(e => e.IdempotencyKey), StringComparer.Ordinal);
        Assert.Equal(a.Hash, (await _h.Profiles.Latest(Token)).Value!.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_retried_after_a_failed_save_records_once_and_saves()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        var flaky = new SaveFailsOnce(_h.Profiles);
        var service = new ApproveProfileService(_h.Profiles, flaky, _h.Held, _h.Store, _h.Events);
        var request = new ApproveProfileRequest(live.Hash, Confirmed: true);

        var first = await service.Handle(request, Token);
        var retry = await service.Handle(request, Token);

        Assert.Equal(SindriErrors.ApprovalsUnavailable.Code, first.Error.Code);
        Assert.True(retry.Value.Approved);
        Assert.Equal(live.Hash, (await _h.Profiles.Latest(Token)).Value!.Hash);
        _ = Assert.Single(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_records_a_missing_event_for_an_approval_already_saved()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.Profiles.Save(live, Token);   // saved, but its event never reached the ledger
        _ = await _h.HoldLease();

        var shown = (await Service().Handle(new ApproveProfileRequest(Hash: null, Confirmed: false), Token)).Value;
        var approved = (await Service().Handle(new ApproveProfileRequest(live.Hash, Confirmed: true), Token)).Value;

        Assert.False(shown.Approved);
        Assert.True(approved.Approved);
        Assert.Equal($"profile.approved:{live.Hash.Value}:after:0", Assert.Single(_h.Store.Events).IdempotencyKey);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_surface_says_not_approved_after_a_failed_save_and_an_edit_back()
    {
        var (a, b) = (SliceHarness.Profile("5s"), SliceHarness.Profile("9s"));
        _ = await _h.HoldLease();
        _h.Profiles.Live = a;
        _ = await Service().Handle(new ApproveProfileRequest(a.Hash, Confirmed: true), Token);
        _h.Profiles.Live = b;
        var flaky = new ApproveProfileService(_h.Profiles, new SaveFailsOnce(_h.Profiles), _h.Held, _h.Store, _h.Events);
        Assert.Equal(SindriErrors.ApprovalsUnavailable.Code, (await flaky.Handle(new ApproveProfileRequest(b.Hash, Confirmed: true), Token)).Error.Code);

        _h.Profiles.Live = a;   // edited back: A is still the saved approval, but B's event is the latest recorded one

        Assert.Equal((false, true, false), await Surfaces());
    }

    /// <summary>What the approve preview, the status and validate each say: (approved, needs approval, approved).</summary>
    private async Task<(bool Preview, bool StatusNeedsApproval, bool Validated)> Surfaces()
    {
        var preview = (await Service().Handle(new ApproveProfileRequest(Hash: null, Confirmed: false), Token)).Value;
        var status = (await new GetStatusService(_h.Held, _h.Profiles, _h.Profiles, _h.Store, _h.Events).Handle(new GetStatusRequest(), Token)).Value;
        var validated = (await new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events).Handle(new ValidateProfileRequest(), Token)).Value;
        return (preview.Approved, status.Profile.NeedsApproval, validated.Approved);
    }

    /// <summary>Approvals whose first save fails, as a full disk would.</summary>
    private sealed class SaveFailsOnce(IApprovals inner) : IApprovals
    {
        private bool _failed;

        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => inner.Latest(cancellationToken);

        public ValueTask<Result<Unit>> Save(LoadedProfile profile, CancellationToken cancellationToken)
        {
            if (_failed)
            {
                return inner.Save(profile, cancellationToken);
            }

            _failed = true;
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.ApprovalsUnavailable.With("disk full"));
        }
    }
}
