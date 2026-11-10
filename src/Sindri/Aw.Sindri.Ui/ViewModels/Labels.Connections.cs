using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Connections page, its sheet and its add form (PR 18a). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page's name and its tab.</summary>
    internal const string ConnectionsPage = "Connections";

    /// <summary>The Memory page's name (it is not a tab: G M, the palette and System's Memory link open it).</summary>
    internal const string MemoryPageName = "Memory";

    /// <summary>System's link to the Memory page.</summary>
    internal const string MemoryLink = "Memory stores";

    /// <summary>The link's accessible name: its visible words first.</summary>
    internal const string MemoryLinkName = "Memory stores, opens the Memory page";

    /// <summary>The page's one-line purpose.</summary>
    internal const string ConnectionsPurpose = "Connect the services Sindri works with. Your sign-in stays in your keychain; this page never shows it.";

    /// <summary>The page before its first answer.</summary>
    internal const string ReadingConnections = "Reading connections…";

    /// <summary>The page when an answer can't be read.</summary>
    internal const string ConnectionsUnreadable = "The connection list can't be read from this Sindri.";

    /// <summary>The page when the first read failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words, with the code and detail.</returns>
    internal static string ConnectionsFailed(Error error) => "Couldn't read the connections just now. " + InstanceRefused(error);

    /// <summary>The page with no connection.</summary>
    internal const string ConnectionsEmpty = "";

    /// <summary>The sheet before its first answer.</summary>
    internal const string ReadingConnection = "Reading the connection…";

    /// <summary>The sheet when Sindri doesn't know the connection.</summary>
    internal const string ConnectionUnknown = "Sindri doesn't know this connection.";

    /// <summary>The sheet when the answer can't be read.</summary>
    internal const string ConnectionUnreadable = "This connection can't be read from this Sindri.";

    /// <summary>The sheet's stale note.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionStale(Error error) => "Couldn't refresh this connection just now. " + InstanceRefused(error);

    /// <summary>The head: "3 connections · 1 needs you".</summary>
    /// <param name="total">How many connections.</param>
    /// <param name="notConnected">How many are not connected.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionsHead(int total, int notConnected)
    {
        var count = string.Create(CultureInfo.InvariantCulture, $"{total} {(total == 1 ? "connection" : "connections")}");
        return notConnected > 0 ? count + string.Create(CultureInfo.InvariantCulture, $" · {notConnected} need{(notConnected == 1 ? "s" : string.Empty)} you") : count;
    }

    /// <summary>A status chip: glyph and word.</summary>
    /// <param name="status">The status as served.</param>
    /// <returns>The glyph and the word.</returns>
    internal static (string Glyph, string Word) ConnectionChip(string status) => status switch
    {
        "connected" => ("✓", "Connected"),
        "needs-sign-in" => ("!", "Needs sign-in"),
        "error" => ("✕", "Error"),
        _ => ("○", status.Length == 0 ? status : char.ToUpperInvariant(status[0]) + status[1..]),
    };

    /// <summary>A kind in plain words.</summary>
    /// <param name="kind">The kind as served.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionKind(string kind) => kind switch
    {
        "oauth" => "OAuth sign-in",
        "rest" => "Key (REST)",
        "cli" => "Key (CLI)",
        "mcp" => "Custom server",
        _ => kind,
    };

    /// <summary>When the token was last refreshed.</summary>
    /// <param name="age">How long ago, or null when it never was.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionRefreshed(TimeSpan? age) => age is { } ago ? "Refreshed " + Age(ago) + " ago" : "Not refreshed yet";

    /// <summary>A connection row's accessible name.</summary>
    /// <param name="name">The connection's name.</param>
    /// <param name="kind">Its kind in words.</param>
    /// <param name="status">Its status in words.</param>
    /// <param name="reason">Why it is not connected, or empty.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionRowName(string name, string kind, string status, string reason) =>
        string.Join(", ", new[] { name, kind, status, reason }.Where(p => p.Length > 0));

    /// <summary>The Test action.</summary>
    internal const string ConnectionTest = "Test";

    /// <summary>The Sign in again action (OAuth and MCP connections only).</summary>
    internal const string ConnectionSignIn = "Sign in again";

    /// <summary>The Remove action.</summary>
    internal const string ConnectionRemove = "Remove";

    /// <summary>The confirm button.</summary>
    internal const string ConnectionRemoveConfirm = "Remove connection";

    /// <summary>The button that backs out of the confirm.</summary>
    internal const string ConnectionKeep = "Keep it";

    /// <summary>The confirm's question.</summary>
    /// <param name="name">The connection's name.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionRemoveAsk(string name) => $"Remove {name}? Its stored secret is deleted too.";

    /// <summary>An action's accessible name: the verb and the connection.</summary>
    /// <param name="verb">The action's words.</param>
    /// <param name="name">The connection's name.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionActionName(string verb, string name) => verb + " " + name;

    /// <summary>The note after a test that passed.</summary>
    /// <param name="tools">How many tools an MCP connection offers, or null.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionTestOk(int? tools) => tools is { } n ? string.Create(CultureInfo.InvariantCulture, $"Test passed · {n} {(n == 1 ? "tool" : "tools")}") : "Test passed";

    /// <summary>The note after a test that failed.</summary>
    /// <param name="reason">Why, as Sindri says it.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionTestFailed(string? reason) => string.IsNullOrEmpty(reason) ? "Test failed" : "Test failed: " + reason;

    /// <summary>The note while a test runs.</summary>
    internal const string ConnectionTesting = "Testing…";

    /// <summary>The note while Sindri waits for the browser.</summary>
    internal const string ConnectionWaitingBrowser = "Waiting for the browser sign-in…";

    /// <summary>The note once a sign-in landed.</summary>
    internal const string ConnectionSignedIn = "Signed in.";

    /// <summary>The note when the sign-in did not finish in time.</summary>
    internal const string ConnectionSignInTimedOut = "The browser sign-in didn't finish in time. Press Sign in again to try once more.";

    /// <summary>The note when Sindri gave an address this app won't open (not http or https).</summary>
    internal const string ConnectionBadConsent = "Sindri gave a sign-in address this app won't open.";

    /// <summary>The note when the system refused to open the browser.</summary>
    /// <param name="url">The consent address.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionBrowserRefused(string url) => "Couldn't open your browser. Open this address to sign in: " + url;

    /// <summary>The note when the sign-in ended with a status that is not connected.</summary>
    /// <param name="reason">Why, or empty.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionSignInFailed(string reason) => reason.Length > 0 ? "The sign-in didn't work: " + reason : "The sign-in didn't work.";

    /// <summary>The detail when a sign-in again failed on a connected row, which keeps working on its old sign-in.</summary>
    /// <param name="code">The failure's code.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionSignInFailedKept(string code) => "it still works on its old sign-in · " + code;

    /// <summary>A write that Sindri refused.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionWriteFailed(Error error) => "Couldn't do that. " + InstanceRefused(error);

    /// <summary>The secondary link that opens the advanced form, and the form's heading.</summary>
    internal const string AddConnection = "Custom connection";

    /// <summary>The form's preset picker label.</summary>
    internal const string ConnectionPresetLabel = "What to connect";

    /// <summary>The form's name label.</summary>
    internal const string ConnectionNameLabel = "Name";

    /// <summary>The key label.</summary>
    internal const string ConnectionKeyLabel = "Key";

    /// <summary>The MCP server label.</summary>
    internal const string ConnectionUrlLabel = "Address";

    /// <summary>The note under a pasted secret.</summary>
    internal const string ConnectionSecretNote = "Sent once to Sindri, which keeps it in your keychain. This app doesn't keep it.";

    /// <summary>The form's send button.</summary>
    internal const string ConnectionAddSend = "Add";

    /// <summary>The form's close button.</summary>
    internal const string ConnectionAddCancel = "Cancel";

    /// <summary>The form's note while it sends.</summary>
    internal const string ConnectionAdding = "Adding…";

    /// <summary>The form's note once the connection is there.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionAdded(string name) => $"Added {name}.";

    /// <summary>The preset picker's rows.</summary>
    internal static readonly (string Id, string Label, string Kind)[] ConnectionPresets =
    [
        ("mcp", "A custom server", "mcp"),
        ("rest", "A service with a key", "rest"),
        ("cli", "A command-line tool with a key", "cli"),
    ];

    /// <summary>The sheet's section for the settings Sindri keeps (never a secret).</summary>
    internal const string ConnectionSettings = "Settings";

    /// <summary>The sheet's line about the stored secret.</summary>
    /// <param name="has">Whether one is stored.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionSecretLine(bool has) => has ? "A secret is stored in your keychain." : "No secret is stored.";

    /// <summary>The Memory page tab-less palette name for the sheet's edge tab.</summary>
    internal const string EdgeTabConnection = "Connection";

    /// <summary>The Connect button on a service tile.</summary>
    internal const string ConnectTile = "Connect";

    /// <summary>The Slack tile's button: Slack has no browser sign-in.</summary>
    internal const string ConnectAddToken = "Add token";

    /// <summary>The tile's Connect inline line when the client id is missing.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The words.</returns>
    internal static string ConnectNeedsClientId(string service) => $"Enter your {service} app's client id";

    /// <summary>The tile's inline line when the token is missing.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The words.</returns>
    internal static string ConnectNeedsToken(string service) => $"Paste a {service} token";

    /// <summary>The reason line under a disabled Add in the custom form.</summary>
    internal const string ConnectionDefaultName = "custom:work";

    /// <summary>The reason line under a disabled Add in the custom form.</summary>
    internal const string ConnectNeedsAddress = "Enter a server address";

    /// <summary>The custom form's reason when the name is missing.</summary>
    internal const string ConnectNeedsName = "Enter a name";

    /// <summary>The tile error when the person cancelled.</summary>
    internal const string ConnectCancelled = "Sign-in cancelled.";

    /// <summary>The tile error for a refused connect: the keychain is named, never a code.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string ConnectTileFailed(Error error) => string.Equals(error.Code, "SND-CONN-006", StringComparison.Ordinal)
        ? "Couldn't save the sign-in to your keychain. Unlock it, then try again."
        : "Couldn't connect. " + InstanceRefused(error);

    /// <summary>A tile's last use.</summary>
    /// <param name="age">How long ago.</param>
    /// <returns>The words.</returns>
    internal static string ConnectionLastUsed(TimeSpan age) => "Last used " + Age(age) + " ago";

    /// <summary>A tile never used.</summary>
    internal const string ConnectionNeverUsed = "Not used yet";

    /// <summary>The tile menu's Disconnect.</summary>
    internal const string ConnectionDisconnect = "Disconnect";

    /// <summary>The tile menu's button name.</summary>
    internal const string ConnectionMore = "More";

    /// <summary>The count: "2 of 5 connected".</summary>
    /// <param name="connected">How many tiles are connected.</param>
    /// <param name="total">How many tiles.</param>
    /// <returns>The words.</returns>
    internal static string ConnectedOf(int connected, int total) => string.Create(CultureInfo.InvariantCulture, $"{connected} of {total} connected");

    /// <summary>The tiles of the Connections page: services with a sign-in path.</summary>
    internal static readonly (string Id, string Title, string Blurb, bool Token)[] ConnectionTiles =
    [
        ("github", "GitHub", "Reviews and pull requests", false),
        ("linear", "Linear", "Issues and projects", false),
        ("slack", "Slack", "Messages, with a pasted token", true),
        ("google", "Google", "Calendar", false),
        ("spotify", "Spotify", "Playback", false),
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
        "github" => ("Create an OAuth app in GitHub, then paste its client id here.", "Open GitHub's OAuth apps", "https://github.com/settings/developers"),
        "linear" => ("Create an OAuth app in Linear, then paste its client id here.", "Open Linear's API settings", "https://linear.app/settings/api"),
        "google" => ("Create an OAuth client in Google, then paste its client id here.", "Open Google's credentials page", "https://console.cloud.google.com/apis/credentials"),
        "spotify" => ("Create an app in Spotify, then paste its client id here.", "Open Spotify's developer dashboard", "https://developer.spotify.com/dashboard"),
        "slack" => ("In Slack, open your app's OAuth & Permissions page and copy the user token.", string.Empty, string.Empty),
        _ => (string.Empty, string.Empty, string.Empty),
    };

    /// <summary>The custom connection link's hint.</summary>
    internal const string ConnectionOthers = "Other connections";
}
