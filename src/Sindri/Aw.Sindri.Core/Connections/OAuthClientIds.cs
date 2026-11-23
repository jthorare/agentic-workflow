using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Connections;

/// <summary>Which OAuth client id a sign-in uses (first-run spec decision 15a): the profile's `setup.oauth.github` or `setup.oauth.linear` when set, else the id this build ships in the preset.</summary>
public static class OAuthClientIds
{
    /// <summary>Picks the client id.</summary>
    /// <param name="preset">The provider's preset (its key is github or linear).</param>
    /// <param name="oauth">The profile's `setup.oauth` block, or null.</param>
    /// <returns>The client id, or SND-SETUP-014 when neither holds one.</returns>
    public static Result<string> For(ConnectionPreset preset, SetupOauthSettings? oauth)
    {
        GC.KeepAlive((preset, oauth));
        throw new NotSupportedException("OAuthClientIds is not built yet (Part of #3321)");
    }
}
