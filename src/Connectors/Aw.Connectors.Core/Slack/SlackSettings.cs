using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Connectors.Core.Slack;

/// <summary>
/// The profile's 'context.sources.slack' entry, checked (owned index spec §5.3, §5.4, §10): the channels to read (by id, with the name for display), the secret pointer to
/// a bot token and the workspace name the permalinks are built from. Only <see cref="Create"/> makes one. No message repeats the token.
/// </summary>
public sealed partial record SlackSettings
{
    private const int MaxChannels = 500;
    private const int MaxNameLength = 80;
    private static readonly HashSet<string> _schemes = new(StringComparer.Ordinal) { "env", "file", "keychain", "op" };

    private SlackSettings(IReadOnlyList<SlackChannelRef> channels, string? token, string workspace)
    {
        Channels = channels;
        Token = token;
        Workspace = workspace;
    }

    /// <summary>The channels read, by id, without duplicates. Never a direct message.</summary>
    public IReadOnlyList<SlackChannelRef> Channels { get; }

    /// <summary>The secret pointer to the bot token ('env:…', 'file:…', 'keychain:…', 'op:…'), never the token itself; null before 'sindri index connect slack'.</summary>
    public string? Token { get; }

    /// <summary>The workspace's name as in 'https://&lt;workspace&gt;.slack.com'.</summary>
    public string Workspace { get; }

    /// <summary>Checks the profile's values.</summary>
    /// <param name="channels">'context.sources.slack.channels'.</param>
    /// <param name="token">'context.sources.slack.token', a secret pointer, or null.</param>
    /// <param name="workspace">'context.sources.slack.workspace'.</param>
    /// <returns>The settings, or AW-CONNECTORS-001 saying which key is wrong (a token is never repeated).</returns>
    public static Result<SlackSettings> Create(IReadOnlyList<SlackChannelRef> channels, string? token, string workspace)
    {
        ArgumentNullException.ThrowIfNull(channels);
        var wrong = CheckChannels(channels);
        if (wrong is null && token is not null && !IsPointer(token))
        {
            wrong = Wrong("token", "this isn't a secret pointer, so it isn't used or shown here; keep the token outside the profile and point to it with env:NAME, file:/path (mode 0600), keychain:service/account or op:vault/item/field");
        }

        if (wrong is not null)
        {
            return wrong;
        }

        return workspace is null || !WorkspaceName.IsMatch(workspace)
            ? Wrong("workspace", "the name in https://<workspace>.slack.com: lowercase letters, digits and '-'")
            : new SlackSettings([.. channels.DistinctBy(static c => c.Channel, StringComparer.Ordinal)], token, workspace);
    }

    /// <summary>The settings without the token pointer.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"SlackSettings {{ Channels = {Channels.Count}, Workspace = {Workspace}, Token = (withheld) }}";

    private static Error? CheckChannels(IReadOnlyList<SlackChannelRef> channels)
    {
        if (channels.Count is 0 or > MaxChannels)
        {
            return Wrong("channels", $"list between 1 and {MaxChannels} channels, each with its Slack id and name");
        }

        if (channels.Any(static c => !SlackChannelIds.IsReadable(c?.Channel)))
        {
            return Wrong("channels", "every channel id must be 'C' or 'G' followed by capitals and digits; a direct message is never read");
        }

        return channels.Any(static c => string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > MaxNameLength || c.Name.Any(char.IsControl))
            ? Wrong("channels", $"every channel needs a name of up to {MaxNameLength} characters (display only)")
            : null;
    }

    private static Error Wrong(string key, string expected)
        => ConnectorsErrors.Invalid.With($"context.sources.slack.{key}: {expected}.");

    private static bool IsPointer(string token)
    {
        var colon = token.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && colon < token.Length - 1 && _schemes.Contains(token[..colon]);
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,62}\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex WorkspaceName { get; }
}
