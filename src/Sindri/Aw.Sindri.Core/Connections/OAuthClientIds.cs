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
        ArgumentNullException.ThrowIfNull(preset);
        var fromProfile = preset.Key switch
        {
            "github" => oauth?.Github,
            "linear" => oauth?.Linear,
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(fromProfile))
        {
            return fromProfile.Trim();
        }

        return !string.IsNullOrWhiteSpace(preset.ShippedClient)
            ? preset.ShippedClient.Trim()
            : SindriErrors.SetupNoClientId.With($"This build has no {preset.DisplayName} app id and the profile sets none in setup.oauth.{preset.Key}. Paste a token instead (fix: sindri setup connect {preset.Key} --token-env NAME)");
    }
}
