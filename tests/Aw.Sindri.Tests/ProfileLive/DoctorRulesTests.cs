using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Tests.ProfileLive;

public sealed class DoctorRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Doctor_has_no_approval_rows()
    {
        var saved = SliceHarness.Profile("5s");
        var live = SliceHarness.Profile("7s");   // edited since it was last read

        var changed = DoctorRules.Profile(live);
        var neverApproved = DoctorRules.Profile(saved);

        Assert.Equal(CheckState.Ok, changed.State);
        Assert.Equal(CheckState.Ok, neverApproved.State);
        Assert.DoesNotContain("approv", changed.Detail + neverApproved.Detail + changed.Fix + neverApproved.Fix, StringComparison.OrdinalIgnoreCase);
    }
}
