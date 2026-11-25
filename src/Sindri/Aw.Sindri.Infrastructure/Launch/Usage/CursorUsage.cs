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
#pragma warning disable CA1001 // the semaphore is never waited on through a handle, so it holds nothing to release; the reader lives as long as the process
internal sealed class CursorUsage(string root) : IUsageReader
{
    private const string Marker = "\"usage\"";
    private const string Unknown = "cursor";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, FileState> _files = new(StringComparer.Ordinal);
    private readonly SeenWindow _seen = new();

    /// <inheritdoc/>
    public string Name => "cursor";

    /// <inheritdoc/>
    public bool Verified => false;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        // incremental (#3129): per transcript the bytes already read and the rows found; a row without a timestamp takes the file's write time when asked
        await Task.Yield();   // the directory walk below never runs on the caller's thread
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var files = TranscriptFiles.Jsonl(root, _seen.Widen(from, to));
            foreach (var gone in _files.Keys.Except(files, StringComparer.Ordinal).ToList())
            {
                _ = _files.Remove(gone);
            }

            var rows = new List<ModelTokens>();
            foreach (var path in files)
            {
                var state = _files.TryGetValue(path, out var known) ? known : _files[path] = new FileState();
                await Refresh(path, state, cancellationToken).ConfigureAwait(false);
                var written = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                rows.AddRange(state.Rows.Where(row => Inside(row.At ?? written, (from, to))).Select(static row => row.Tokens));
            }

            return UsageJson.PerModel(rows);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken)
    {
        var rows = new List<ModelTokens>();
        foreach (var file in TranscriptFiles.Jsonl(root, since: null).Where(path => UsageJson.OfSession(path, session.Value)))
        {
            foreach (var line in await TranscriptFiles.Lines(file, Marker, cancellationToken).ConfigureAwait(false))
            {
                if (Row(line) is { } row)
                {
                    rows.Add(row.Tokens);
                }
            }
        }

        return UsageJson.PerModel(rows);
    }

    /// <summary>Reads what was appended to one transcript since the last call (all of it again when the file got smaller).</summary>
    private static async ValueTask Refresh(string path, FileState state, CancellationToken cancellationToken)
    {
        var (lines, offset, restarted) = await TranscriptFiles.LinesFrom(path, state.Offset, Marker, cancellationToken).ConfigureAwait(false);
        if (restarted)
        {
            state.Rows.Clear();
        }

        foreach (var line in lines)
        {
            if (Row(line) is { } row)
            {
                state.Rows.Add(row);
            }
        }

        state.Offset = offset;
    }

    private static (DateTimeOffset? At, ModelTokens Tokens)? Row(string line)
    {
        using var document = UsageJson.Parse(line);
        return document is not null && UsageJson.Object(document.RootElement, "usage") is { } usage
            ? (UsageJson.Timestamp(document.RootElement), new ModelTokens(UsageJson.Text(document.RootElement, "model") ?? Unknown, UsageJson.Count(usage, "input_tokens"), CacheRead: 0, CacheWrite: 0, UsageJson.Count(usage, "output_tokens")))
            : null;
    }

    /// <summary>What was read of one transcript: the byte just past its last complete line, and each usage row with its timestamp (null when the line has none).</summary>
    private sealed class FileState
    {
        public long Offset { get; set; }

        public List<(DateTimeOffset? At, ModelTokens Tokens)> Rows { get; } = [];
    }

    private static bool Inside(DateTimeOffset at, (DateTimeOffset From, DateTimeOffset To)? window) => window is not { } span || (at >= span.From && at < span.To);
}
