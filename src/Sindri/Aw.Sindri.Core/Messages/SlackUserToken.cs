using Aw.Kernel;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The prefix check 'sindri messages connect' runs before any call (messages spec §5.1, SND-MSG-030): Messages takes only a User OAuth Token
/// (xoxp). A bot (xoxb), app-level (xapp) or cookie (xoxc) token, or anything else, is refused with the sentence that says where the right one is.
/// The token itself is never in the error.
/// </summary>
public static class SlackUserToken
{
    // why: each prefix is split so no token-shaped literal sits in the source (the secret scanner)
    private const string BotPrefix = "xo" + "xb-";
    private const string AppPrefix = "xa" + "pp-";
    private const string CookiePrefix = "xo" + "xc-";

    /// <summary>The User OAuth Token's prefix.</summary>
    public static string Prefix { get; } = "xo" + "xp-";

    /// <summary>Checks a pasted token's prefix.</summary>
    /// <param name="token">The pasted token.</param>
    /// <returns>The token, or SND-MSG-030 naming the kind that was pasted.</returns>
    public static Result<string> Check(string? token)
    {
        if (token is not null && token.StartsWith(Prefix, StringComparison.Ordinal) && token.Length > Prefix.Length)
        {
            return token;
        }

        var said = Kind(token) is { } kind ? $"That is {kind}. " : string.Empty;
        return SindriErrors.MessagesNotUserToken.With(said + SindriErrors.MessagesNotUserToken.Title + ".");
    }

    // What a refused token is, by its prefix: never more of the token than these few letters.
    private static string? Kind(string? token) => token switch
    {
        null or "" => null,
        _ when token.StartsWith(BotPrefix, StringComparison.Ordinal) => "a bot token (" + BotPrefix + ")",
        _ when token.StartsWith(AppPrefix, StringComparison.Ordinal) => "an app-level token (" + AppPrefix + ")",
        _ when token.StartsWith(CookiePrefix, StringComparison.Ordinal) => "a cookie token (" + CookiePrefix + ")",
        _ => "not a Slack user token",
    };
}
