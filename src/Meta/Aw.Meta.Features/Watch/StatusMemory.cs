using System.Globalization;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// What the watch announced of the status lines (#2651), kept under KvKeys.WatchStatus so a re-armed watch neither repeats one nor drops one
/// that came while no watch ran or that the 15-minute timer still held. Stored as "&lt;last STATUS line, round-trip time or -&gt;|&lt;id&gt;,&lt;id&gt;".
/// </summary>
/// <param name="At">When the last STATUS line of its own printed; null when none did.</param>
/// <param name="Ids">The status lines already announced and still unread.</param>
internal sealed record StatusMemory(DateTimeOffset? At, IReadOnlySet<string> Ids)
{
    internal static StatusMemory Empty { get; } = new(At: null, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Reads a kept value; anything it can't read is the empty memory (the next STATUS line may repeat, never drop).</summary>
    internal static StatusMemory Parse(string? value)
    {
        var bar = value?.IndexOf('|', StringComparison.Ordinal) ?? -1;
        if (value is null || bar < 0)
        {
            return Empty;
        }

        DateTimeOffset? at = DateTimeOffset.TryParse(value[..bar], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
        return new StatusMemory(at, value[(bar + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal));
    }

    internal string Format() =>
        (At is { } at ? at.ToString("O", CultureInfo.InvariantCulture) : "-") + "|" + string.Join(',', Ids.Order(StringComparer.Ordinal));
}
