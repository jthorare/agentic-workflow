using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>The one place that decides a Step's GitHub credentials: the launch mints the token with it and the plan binds it with it, so they cannot disagree.</summary>
public static class GithubCredentialResolver
{
    /// <summary>
    /// The profile's 'credentials.github', except none for an attended session and for a skill Step that cannot push (<paramref name="skillPushes"/> false: the block does not push, or the run is a trial).
    /// <paramref name="skillPushes"/> is null for a Step that is not a skill block (a recipe phase), which keeps the profile's choice.
    /// </summary>
    public static GithubCredentials For(bool attended, SandboxSettings? settings, bool? skillPushes) =>
        attended || skillPushes == false ? GithubCredentials.None : settings?.Credentials?.Github ?? SandboxDefaults.Github;
}
