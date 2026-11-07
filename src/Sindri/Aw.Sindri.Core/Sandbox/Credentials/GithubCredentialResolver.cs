using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>The one place that decides a Step's GitHub credentials: the launch mints the token with it and the plan binds it with it, so they cannot disagree.</summary>
public static class GithubCredentialResolver
{
    /// <summary>
    /// The profile's 'credentials.github', loosened to the Step's ask only when <paramref name="pushApproved"/>, except none for an attended session and for a skill Step that cannot push (<paramref name="skillPushes"/> false: the block does not push, or the run is a trial).
    /// <paramref name="skillPushes"/> is null for a Step that is not a skill block (a recipe phase), which keeps the profile's choice.
    /// </summary>
    /// <param name="attended">True for an attended session, which never gets the token.</param>
    /// <param name="settings">The profile's sandbox section.</param>
    /// <param name="skillPushes">Null for a Step that is not a skill block; else whether the block pushes and the run is not a trial.</param>
    /// <param name="policy">The Step's policy, whose <see cref="StepPolicy.Github"/> is the ask; null for none.</param>
    /// <param name="pushApproved">True when the owner's approval for this repo covers the ask. Without it the ask grants nothing.</param>
    /// <returns>The mode.</returns>
    public static GithubCredentials For(bool attended, SandboxSettings? settings, bool? skillPushes, StepPolicy? policy = null, bool pushApproved = false)
    {
        _ = (policy, pushApproved);   // red stub: the ask is not read yet
        return attended || skillPushes == false ? GithubCredentials.None : settings?.Credentials?.Github ?? SandboxDefaults.Github;
    }
}
