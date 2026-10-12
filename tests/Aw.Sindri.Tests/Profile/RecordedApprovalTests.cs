using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

public sealed class RecordedApprovalTests
{
    private static readonly Guid _id = new(a: 0, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0, j: 0, k: 0xa1);
    private static readonly Guid _correlation = new(a: 0, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0, j: 0, k: 0xc1);

    [Fact, Trait("Lane", "unit")]
    public void A_recorded_approval_whose_payload_doesnt_read_is_an_error_not_none()
    {
        var harness = new SliceHarness();
        var envelope = new EventEnvelope<string>(_id, EventTypes.ProfileApproved, EventSchema.CurrentVersion, KnownInstants.Epoch,
            "host:sindri", "local", 0, _correlation, CausationId: null, TraceId: null, "{}");
        var latest = Results.Success<LedgerEvent?>(new LedgerEvent(7, 1, envelope, "profile.approved:x", Item: null));

        var recorded = RecordedApproval.From(latest, harness.Events);

        Assert.True(recorded.IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void Doctor_names_both_hashes_when_the_saved_approval_isnt_the_latest_recorded_one()
    {
        var a = SliceHarness.Profile("5s");
        var b = SliceHarness.Profile("7s");

        var check = DoctorRules.Profile(a, Results.Success<LoadedProfile?>(a), Results.Success<RecordedApproval?>(new RecordedApproval(b.Hash, 2)));

        Assert.Equal((CheckState.Warn, $"approval {a.Hash.Abbreviated} is saved but isn't the latest recorded one ({b.Hash.Abbreviated})", "sindri profile approve"),
            (check.State, check.Detail, check.Fix));
    }
}
