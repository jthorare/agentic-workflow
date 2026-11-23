using System.Globalization;

namespace Aw.Sindri.Core.Messages;

/// <summary>Slack's mrkdwn text, parsed into spans (messages spec §6.3, §8.1): Parse then Render gives the same text back. M1b builds both.</summary>
/// <param name="Spans">The spans, in order.</param>
public sealed record Mrkdwn(IReadOnlyList<MrkdwnSpan> Spans)
{
    /// <summary>Parses Slack's mrkdwn.</summary>
    /// <param name="text">The text as Slack sends it.</param>
    /// <returns>The parsed text.</returns>
    public static Mrkdwn Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        throw MessageStubs.NotBuilt("Mrkdwn.Parse", "M1b");
    }

    /// <summary>Writes the spans back as Slack's mrkdwn.</summary>
    /// <returns>The text.</returns>
    public string Render() => throw MessageStubs.NotBuilt(string.Create(CultureInfo.InvariantCulture, $"Mrkdwn.Render ({Spans.Count} spans)"), "M1b");
}
