using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// The Cursor usage reader (API P10a Q11). Cursor's agent CLI was not available to check its transcripts against, and the files seen so far carry no token counts,
/// so this reads only the documented shape: a line with a 'usage' object of 'input_tokens' and 'output_tokens' (and an optional 'model' and 'timestamp'; the
/// file's own time stands in for a missing one). It stays <see cref="Verified"/> false until someone checks it against the real CLI: the target is probed as
/// limited and its spend is tokens only, so no budget is applied to it.
/// </summary>
/// <param name="root">The projects directory.</param>
internal sealed class CursorUsage(string root) : IUsageReader
{
    private const string Marker = "\"usage\"";
    private const string Unknown = "cursor";

    /// <inheritdoc/>
    public string Name => "cursor";

    /// <inheritdoc/>
    public bool Verified => false;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Read(TranscriptFiles.Jsonl(root, from), (from, to), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) =>
        await Read([.. TranscriptFiles.Jsonl(root, since: null).Where(path => UsageJson.OfSession(path, session.Value))], window: null, cancellationToken).ConfigureAwait(false);

    private static async ValueTask<IReadOnlyList<ModelTokens>> Read(IReadOnlyList<string> files, (DateTimeOffset From, DateTimeOffset To)? window, CancellationToken cancellationToken)
    {
        var rows = new List<ModelTokens>();
        foreach (var file in files)
        {
            var written = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);
            foreach (var line in await TranscriptFiles.Lines(file, Marker, cancellationToken).ConfigureAwait(false))
            {
                using var document = UsageJson.Parse(line);
                if (document is not null && UsageJson.Object(document.RootElement, "usage") is { } usage && Inside(UsageJson.Timestamp(document.RootElement) ?? written, window))
                {
                    rows.Add(new ModelTokens(UsageJson.Text(document.RootElement, "model") ?? Unknown, UsageJson.Count(usage, "input_tokens"), CacheRead: 0, CacheWrite: 0, UsageJson.Count(usage, "output_tokens")));
                }
            }
        }

        return UsageJson.PerModel(rows);
    }

    private static bool Inside(DateTimeOffset at, (DateTimeOffset From, DateTimeOffset To)? window) => window is not { } span || (at >= span.From && at < span.To);
}
