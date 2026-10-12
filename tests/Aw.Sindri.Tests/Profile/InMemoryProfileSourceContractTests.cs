using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Profile;

[Lane(TestLane.Unit)]
public sealed class InMemoryProfileSourceContractTests : ProfileSourceContract
{
    private readonly InMemoryProfiles _profiles = new();

    protected override IProfileSource Create() => _profiles;

    protected override void PutValid() => _profiles.Live = SliceHarness.Profile();

    protected override void PutInvalid() => _profiles.Live = SindriErrors.ProfileInvalid.With("/home/profile.yaml: user: must be non-empty text");

    protected override void PutNone() => _profiles.Live = SindriErrors.ProfileMissing.With("/home/profile.yaml doesn't exist");
}
