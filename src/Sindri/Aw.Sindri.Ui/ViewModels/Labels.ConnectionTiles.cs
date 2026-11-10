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

    /// <summary>The tile menu's Rename.</summary>
    internal const string TileRename = "Rename";

    /// <summary>The rename field's name and placeholder.</summary>
    internal const string TileRenameLabel = "Name";

    /// <summary>The rename field's Save.</summary>
    internal const string TileRenameSave = "Save";

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
}
