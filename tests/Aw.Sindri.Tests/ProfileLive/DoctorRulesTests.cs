using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.ProfileLive;

public sealed class DoctorRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Doctor_has_no_approval_rows()
    {
        var saved = SliceHarness.Profile("5s");
        var live = SliceHarness.Profile("7s");   // edited since the old approval

        var changed = DoctorRules.Profile(live, Results.Success<LoadedProfile?>(saved), Results.Success<RecordedApproval?>(new RecordedApproval(saved.Hash, 2)));
        var neverApproved = DoctorRules.Profile(live, Results.Success<LoadedProfile?>(value: null), Results.Success<RecordedApproval?>(value: null));

        Assert.Equal(CheckState.Ok, changed.State);
        Assert.Equal(CheckState.Ok, neverApproved.State);
        Assert.DoesNotContain("approv", changed.Detail + neverApproved.Detail + changed.Fix + neverApproved.Fix, StringComparison.OrdinalIgnoreCase);
    }
}
