using System.Globalization;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// Batches the status lines for the watch (#2651). A status line is never an INBOX event: an unread one not announced yet rides on the next INBOX
/// line, or prints in one STATUS line when the last one is at least <see cref="Every"/> old. What was announced is kept, so the next watch
/// repeats nothing and drops nothing.
/// </summary>
/// <param name="clock">The current time.</param>
/// <param name="kept">What an earlier watch announced, and when its last STATUS line printed.</param>
/// <param name="keep">Keeps what was announced whenever it changes.</param>
internal sealed class StatusBatch(TimeProvider clock, StatusMemory kept, Func<StatusMemory, ValueTask> keep)
{
    /// <summary>The shortest time between two STATUS lines of their own.</summary>
    internal static readonly TimeSpan Every = TimeSpan.FromMinutes(15);

    private StatusMemory _kept = kept;

    /// <summary>The line one poll prints for its mail and its status lines, or null for none.</summary>
    /// <param name="status">The poll's unread status lines with their lanes.</param>
    /// <param name="mail">The poll's INBOX line, or null when no new mail came.</param>
    internal async ValueTask<string?> Line(IReadOnlyList<(string Id, string Lane)> status, string? mail)
    {
        var news = status.Where(s => !_kept.Ids.Contains(s.Id)).ToList();
        if (mail is not null)
        {
            await Announced(status, news, _kept.At).ConfigureAwait(false);
            return news.Count > 0 ? $"{mail}; {Format(news)}" : mail;
        }

        var now = clock.GetUtcNow();
        if (news.Count == 0 || (_kept.At is { } last && now - last < Every))
        {
            return null;
        }

        await Announced(status, news, now).ConfigureAwait(false);
        return Format(news);
    }

    // Keeps the announced ids that are still unread, plus the new ones.
    private async ValueTask Announced(IReadOnlyList<(string Id, string Lane)> status, List<(string Id, string Lane)> news, DateTimeOffset? at)
    {
        if (news.Count == 0)
        {
            return;
        }

        var unread = status.Select(static s => s.Id).ToHashSet(StringComparer.Ordinal);
        _kept = new StatusMemory(at, _kept.Ids.Where(unread.Contains).Concat(news.Select(static s => s.Id)).ToHashSet(StringComparer.Ordinal));
        await keep(_kept).ConfigureAwait(false);
    }

    private static string Format(List<(string Id, string Lane)> news) =>
        string.Create(CultureInfo.InvariantCulture, $"STATUS {news.Count} new (lanes: {string.Join(", ", news.Select(static s => s.Lane).Distinct(StringComparer.Ordinal))})");
}
