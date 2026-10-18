using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// ISessionDirectory over Claude Code's sessions directory (one &lt;pid&gt;.json per session). The format is Claude Code's
/// own and undocumented, so every record is read defensively: one that can't be read is skipped, never an error.
/// A session is live when its pid runs and the process did not start after the record was last written, asked of IProcessLiveness in
/// one batch; a pid the host can't be asked about is kept (unknown is not dead).
/// </summary>
/// <param name="directory">The sessions directory.</param>
/// <param name="liveness">Tells whether the pid still belongs to the session's process.</param>
internal sealed class DirectorySessionDirectory(string directory, IProcessLiveness liveness) : ISessionDirectory
{
    public async ValueTask<Result<IReadOnlyList<LiveSession>>> Live(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return Results.Success<IReadOnlyList<LiveSession>>([]);
        }

        var found = new List<(LiveSession Session, ProcessClaim Claim)>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            if (await Read(file, cancellationToken).ConfigureAwait(false) is { } session)
            {
                found.Add((session, new ProcessClaim(session.Pid, new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero))));
            }
        }

        // one batch for every record; a pid the host could not be asked about stays in the list (unknown is not dead)
        var verdicts = await liveness.Running([.. found.Select(f => f.Claim)], cancellationToken).ConfigureAwait(false);
        return Results.Success<IReadOnlyList<LiveSession>>([.. found.Where((_, i) => verdicts[i].IsLiveOrUnknown()).Select(f => f.Session)]);
    }

    private static async ValueTask<LiveSession?> Read(string file, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("pid", out var pid) || pid.ValueKind != JsonValueKind.Number || !pid.TryGetInt32(out var processId)
                || Text(root, "sessionId") is not { Length: > 0 } id)
            {
                return null;
            }

            return new LiveSession(SessionId.From(id), Text(root, "name") ?? string.Empty, processId, Text(root, "status") ?? string.Empty, Text(root, "cwd") ?? string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
