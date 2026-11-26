namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of the Connections page's tiles (<see cref="Labels"/>, continued).</summary>
internal static partial class Labels
{
    /// <summary>
    /// The tiles of the Connections page: services with a sign-in path. Mode is what the browser sign-in needs: a preset ships no client id (SND-CONN-007), and GitHub and Google also need the client
    /// secret at the token endpoint (TokenSet.ClientSecret); Slack has no browser sign-in. TakesKey marks a service with a token option, which the tile offers first (#4357, the presets' "auth" order).
    /// </summary>
    internal static readonly (string Id, string Title, string Blurb, ServiceTileMode Mode, bool TakesKey)[] ConnectionTiles =
    [
        ("github", "GitHub", "Reviews and pull requests", ServiceTileMode.ClientIdAndSecret, true),
        ("linear", "Linear", "Issues and projects", ServiceTileMode.ClientId, true),
        ("slack", "Slack", "Messages, with a pasted token", ServiceTileMode.Token, true),
        ("google", "Google", "Calendar", ServiceTileMode.ClientIdAndSecret, false),
        ("spotify", "Spotify", "Playback", ServiceTileMode.ClientId, false),
    ];

    /// <summary>The swap to a service's own OAuth app, under the token field.</summary>
    internal const string TileUseOAuth = "Use an OAuth app instead";

    /// <summary>The swap back to a pasted token, under the client id fields.</summary>
    internal const string TileUseToken = "Use a token instead";

    /// <summary>
    /// The three numbered steps above a tile's token field: where to make the token and what to give it (first-run spec decision 15a's fine-grained set for GitHub). GitHub's five permissions
    /// sit under step 2, one per line in the muted style (#4368), so they don't run together in the 260 px tile.
    /// </summary>
    /// <param name="id">The tile's service id.</param>
    /// <returns>The steps; empty for a service with no token.</returns>
    internal static IReadOnlyList<TokenStep> TileTokenSteps(string id) => id switch
    {
        "github" =>
        [
            new("1. Open GitHub's new fine-grained token page."), new("2. Pick your repos and give it these five permissions:"),
            Detail("Pull requests: read and write"), Detail("Contents: read and write"), Detail("Checks: read"), Detail("Issues: read"), Detail("Metadata: read"),
            new("3. Paste the token here."),
        ],
        "linear" => [new("1. Open Linear, Settings, API, Personal API keys."), new("2. Create a key named Sindri."), new("3. Paste the key here.")],
        "slack" => [new("1. Open your Slack app's OAuth & Permissions page."), new("2. Copy the User OAuth Token (it starts with xoxp-)."), new("3. Paste the token here.")],
        _ => [],
    };

    // One line under a step: a permission the token needs.
    private static TokenStep Detail(string text) => new(text, IsDetail: true);

    /// <summary>The tile menu's Rename.</summary>
    internal const string TileRename = "Rename";

    /// <summary>The rename field's name and placeholder.</summary>
    internal const string TileRenameLabel = "Name";

    /// <summary>The rename field's Save.</summary>
    internal const string TileRenameSave = "Save";

    /// <summary>Why a rename was refused: the new name is another connection's.</summary>
    /// <param name="service">The service's title.</param>
    /// <param name="label">The label typed.</param>
    /// <returns>The words.</returns>
    internal static string RenameTaken(string service, string label) => $"Another {service} connection is already called '{label}'. Pick another name.";

    /// <summary>The line under the rename field while it is empty.</summary>
    internal const string RenameEmpty = "Type a name.";

    /// <summary>Why a rename was refused: the label is not allowed.</summary>
    internal const string RenameBadLabel = "Use lowercase letters, digits and '-' for the name.";

    /// <summary>Why a rename was refused: the connection is gone.</summary>
    internal const string RenameGone = "This connection no longer exists. Reload the page.";

    /// <summary>Why a rename was refused: the label is the one it has.</summary>
    internal const string RenameSame = "That's already its name.";

    /// <summary>Any other refusal or failure of a rename: one fixed sentence, none of the server's or the transport's text.</summary>
    internal const string RenameFailed = "Couldn't rename it. Try again.";

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

    /// <summary>The help under a tile's input: one line, and a link to the provider's page where the client id or the token is made. A token's line is empty: its steps say it.</summary>
    /// <param name="id">The tile's service id.</param>
    /// <param name="token">True while the tile takes a token.</param>
    /// <returns>The line, the link's words and the link; the link is empty when there is none.</returns>
    internal static (string Line, string LinkText, string Url) TileHelp(string id, bool token) => (id, token) switch
    {
        ("github", true) => (string.Empty, "Open GitHub's token page", "https://github.com/settings/personal-access-tokens/new"),
        ("linear", true) => (string.Empty, "Open Linear's API settings", "https://linear.app/settings/api"),
        ("slack", true) => (string.Empty, "Open your Slack apps", "https://api.slack.com/apps"),
        (_, true) => (string.Empty, string.Empty, string.Empty),
        _ => TileOAuthHelp(id),
    };

    /// <summary>Whether a tile's help line already tells the person to paste the client ID, so the inline "Enter your X app's client ID" line would say it twice (#4396, #4486). Keyed on the wording, so a help line that says something else keeps the inline line.</summary>
    /// <param name="line">The help line (<see cref="TileHelp"/>'s first part).</param>
    /// <returns>True when the line names the client ID.</returns>
    internal static bool HelpAsksForClientId(string line) => throw new NotSupportedException();

    // The OAuth app's help: where the client id (and secret) come from.
    private static (string Line, string LinkText, string Url) TileOAuthHelp(string id) => id switch
    {
        "github" => ("Paste the client ID and secret from your GitHub OAuth app.", "Open GitHub's OAuth apps", "https://github.com/settings/developers"),
        "linear" => ("Create an OAuth app in Linear, then paste its client ID here.", "Open Linear's API settings", "https://linear.app/settings/api"),
        "google" => ("Paste the client ID and secret from your Google OAuth client.", "Open Google's credentials page", "https://console.cloud.google.com/apis/credentials"),
        "spotify" => ("Create an app in Spotify, then paste its client ID here.", "Open Spotify's developer dashboard", "https://developer.spotify.com/dashboard"),
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
