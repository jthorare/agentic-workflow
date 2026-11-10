namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of the Connections page's tiles (<see cref="Labels"/>, continued).</summary>
internal static partial class Labels
{
    /// <summary>The tiles of the Connections page: services with a sign-in path. A preset ships no client id (SND-CONN-007), and GitHub and Google also need the client secret at the token endpoint (TokenSet.ClientSecret), so those two ask for it; no preset flag says so, so the table does.</summary>
    internal static readonly (string Id, string Title, string Blurb, ServiceTileMode Mode)[] ConnectionTiles =
    [
        ("github", "GitHub", "Reviews and pull requests", ServiceTileMode.ClientIdAndSecret),
        ("linear", "Linear", "Issues and projects", ServiceTileMode.ClientId),
        ("slack", "Slack", "Messages, with a pasted token", ServiceTileMode.Token),
        ("google", "Google", "Calendar", ServiceTileMode.ClientIdAndSecret),
        ("spotify", "Spotify", "Playback", ServiceTileMode.ClientId),
    ];

    /// <summary>The status chip of a connected tile.</summary>
    internal const string TileConnected = "Connected";

    /// <summary>The status chip of a tile whose sign-in needs doing again.</summary>
    internal const string TileNeedsReconnect = "Needs reconnect";

    /// <summary>The status chip of a tile with no connection.</summary>
    internal const string TileNotConnected = "Not connected";

    /// <summary>The reason on a tile that needs a reconnect when Sindri gave none.</summary>
    internal const string TileSignInExpired = "The sign-in ran out.";

    /// <summary>The primary button of a tile that needs a reconnect, and the menu's item.</summary>
    internal const string TileReconnect = "Reconnect";

    /// <summary>The help under a tile's input: one line, and a link to the provider's page where the client id is made.</summary>
    /// <param name="id">The tile's service id.</param>
    /// <returns>The line, the link's words and the link; the link is empty when there is none.</returns>
    internal static (string Line, string LinkText, string Url) TileHelp(string id) => id switch
    {
        "github" => ("Paste the client ID and secret from your GitHub OAuth app.", "Open GitHub's OAuth apps", "https://github.com/settings/developers"),
        "linear" => ("Create an OAuth app in Linear, then paste its client id here.", "Open Linear's API settings", "https://linear.app/settings/api"),
        "google" => ("Paste the client ID and secret from your Google OAuth client.", "Open Google's credentials page", "https://console.cloud.google.com/apis/credentials"),
        "spotify" => ("Create an app in Spotify, then paste its client id here.", "Open Spotify's developer dashboard", "https://developer.spotify.com/dashboard"),
        "slack" => ("In Slack, open your app's OAuth & Permissions page and copy the user token.", string.Empty, string.Empty),
        _ => (string.Empty, string.Empty, string.Empty),
    };

    /// <summary>A tile's reason line as a sentence, and the raw provider code for its tooltip.</summary>
    /// <param name="service">The service's display name.</param>
    /// <param name="reason">The reason as Sindri served it.</param>
    /// <returns>The sentence and the code (empty when the reason has none).</returns>
    internal static (string Sentence, string Code) TileReason(string service, string reason)
    {
        const string Prefix = "the provider refused the refresh (";
        if (reason.StartsWith(Prefix, StringComparison.Ordinal) && reason.EndsWith(')'))
        {
            return (service + " refused the sign-in refresh. Reconnect to sign in again.", reason[Prefix.Length..^1]);
        }

        // Only a plain first word gets a capital: a key or id at the start stays as served. (#2550's Capitalised does the same; this branch does not have it yet.)
        var word = reason.Split(' ')[0];
        var sentence = word.Length > 0 && word.All(char.IsLetter) && char.IsLower(word[0]) ? char.ToUpperInvariant(reason[0]) + reason[1..] : reason;
        return (sentence.Length == 0 || sentence[^1] is '.' or '!' or '?' ? sentence : sentence + ".", string.Empty);
    }
}
