using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The client id a sign-in uses (first-run spec decision 15a): the profile's setup.oauth.* first, then the id the build ships, else SND-SETUP-014.</summary>
public sealed class OAuthClientIdsTests
{
    private static ConnectionPreset Preset(string key, string? shipped) =>
        new(key, key, ConnectionKind.OAuth, "https://example.com/authorize", "https://example.com/token", RevokeUrl: null, DefaultScopes: [], HostGroup: key, LoopbackAllowed: true, SignInNote: string.Empty, PollableEvents: [], ProbeUrl: null)
        {
            ShippedClient = shipped,
        };

    [Theory, Trait("Lane", "unit")]
    [InlineData("github")]
    [InlineData("linear")]
    public void The_profiles_id_wins_over_the_shipped_one(string key)
    {
        var oauth = new SetupOauthSettings(Github: "Iv1.owner-github", Linear: "owner-linear");

        var picked = OAuthClientIds.For(Preset(key, shipped: "shipped"), oauth);

        Assert.Equal(string.Equals(key, "github", StringComparison.Ordinal) ? "Iv1.owner-github" : "owner-linear", picked.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shipped_id_is_used_when_the_profile_sets_none()
        => Assert.Equal("Iv1.shipped", OAuthClientIds.For(Preset("github", "Iv1.shipped"), oauth: null).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void No_id_anywhere_is_setup_014(string? shipped)
    {
        var picked = OAuthClientIds.For(Preset("github", shipped), new SetupOauthSettings());

        Assert.Equal(SindriErrors.SetupNoClientId.Code, picked.Error.Code);
    }
}
