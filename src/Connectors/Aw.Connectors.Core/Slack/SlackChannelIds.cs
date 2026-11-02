using System.Text.RegularExpressions;

namespace Aw.Connectors.Core.Slack;

/// <summary>Which Slack ids may be read: a public channel ('C') or a private channel ('G'), never a direct message ('D') or anything else.</summary>
public static partial class SlackChannelIds
{
    /// <summary>True when <paramref name="channel"/> has the shape of a readable channel id.</summary>
    /// <param name="channel">A channel id.</param>
    /// <returns>Whether it may be listed and read.</returns>
    public static bool IsReadable(string? channel) => channel is not null && Readable.IsMatch(channel);

    [GeneratedRegex(@"^[CG][A-Z0-9]{2,20}\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Readable { get; }
}
