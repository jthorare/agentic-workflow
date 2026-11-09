using Aw.Context.Testing.Engine;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Tests.ValidateProfile;

public sealed class ValidateProfileTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task ValidateProfile_answers_the_live_hash_with_nothing_to_approve()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        var service = new ValidateProfileService(_h.Profiles, FakeContextEngine.Instance);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.Equal(live.Hash, validated.Value.Hash);
    }
}
