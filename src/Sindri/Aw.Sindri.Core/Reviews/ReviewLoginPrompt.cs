using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The one-field login prompt shown while <c language="text">review.login</c> is unset (SND-REVIEW-007).</summary>
/// <param name="Code">SND-REVIEW-007.</param>
/// <param name="Text">"Set your GitHub login".</param>
/// <param name="Suggested">The forge credential's own login, or null when Sindri can't tell.</param>
/// <param name="Connection">The GitHub connection's name ('github:sam-rivera'), or null when there is none.</param>
/// <param name="Reason">Why the connection gave no login: unreachable, signIn, unsupportedHost, or none.</param>
public sealed record ReviewLoginPrompt(string Code, string Text, string? Suggested, string? Connection = null, GitHubLoginMiss Reason = GitHubLoginMiss.None)
{
    /// <summary>The prompt for a suggested login.</summary>
    /// <param name="suggested">The credential's login, or null.</param>
    /// <param name="connection">The connection's name, or null.</param>
    /// <param name="reason">Why the connection gave no login.</param>
    /// <returns>The prompt.</returns>
    public static ReviewLoginPrompt For(string? suggested, string? connection = null, GitHubLoginMiss reason = GitHubLoginMiss.None) => new("SND-REVIEW-007", "Set your GitHub login", suggested, connection, reason);
}
