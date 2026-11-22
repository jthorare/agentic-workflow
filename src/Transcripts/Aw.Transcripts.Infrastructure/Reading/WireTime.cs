using System.Globalization;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>Reads the ISO 8601 timestamps Claude Code and Codex write.</summary>
internal static class WireTime
{
    public static DateTimeOffset? Parse(string? text) =>
        text is { Length: > 0 } && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
}
