using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Core.Reviews;

/// <summary>When the Reviews list shows its one connect action (#2499): no GitHub connection to read with (and no profile token, or one GitHub refused), or a connection GitHub refused or that needs sign-in again.</summary>
public static class ReviewConnectRules
{
    private static readonly HashSet<string> _signInCodes = new(StringComparer.Ordinal) { "AW-CONNECTORS-006", "AW-CONNECTORS-007", "SND-REVIEW-009" };

    /// <summary>Whether a GitHub source's last code means the credential was refused or must be signed in again.</summary>
    /// <param name="code">The source's last error code, or null.</param>
    /// <returns>True for AW-CONNECTORS-006, AW-CONNECTORS-007 and SND-REVIEW-009.</returns>
    public static bool IsSignInCode(string? code) => _signInCodes.Contains(code ?? string.Empty);

    /// <summary>Decides the connect state.</summary>
    /// <param name="account">The GitHub connection as the Connections page has it.</param>
    /// <param name="profileHasToken">True when 'context.sources.github.token' is set (the feed can read without a connection).</param>
    /// <param name="lastCode">The github source's last error code, or null.</param>
    /// <returns>The connect state, or null when Reviews can read.</returns>
    public static ReviewConnect? For(GitHubAccount account, bool profileHasToken, string? lastCode)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.State == GitHubAccountState.NeedsSignIn || (account.State == GitHubAccountState.Connected && IsSignInCode(lastCode)))
        {
            return new ReviewConnect("SND-REVIEW-009", "sign-in-again", "Sign in to GitHub again", account.Connection);
        }

        // a profile token GitHub refused (the owner's case in #2499) is no way to read either: connecting is the fix
        return account.State == GitHubAccountState.Missing && (!profileHasToken || IsSignInCode(lastCode))
            ? new ReviewConnect("SND-REVIEW-008", "connect", "Connect GitHub", Connection: null)
            : null;
    }
}
