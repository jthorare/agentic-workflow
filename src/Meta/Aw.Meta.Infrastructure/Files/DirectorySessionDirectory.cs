using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// ISessionDirectory over Claude Code's sessions directory (one &lt;pid&gt;.json per session). The format is Claude Code's
/// own and undocumented, so every record is read defensively: one that can't be read is skipped, never an error.
/// Liveness is ProcessLiveness (the pid's /proc entry on Linux, the OS process list on macOS).
/// </summary>
/// <param name="directory">The sessions directory.</param>
internal sealed class DirectorySessionDirectory(string directory) : ISessionDirectory
{
    public async ValueTask<Result<IReadOnlyList<LiveSession>>> Live(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return Results.Success<IReadOnlyList<LiveSession>>([]);
        }

        var sessions = new List<LiveSession>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            var session = await Read(file, cancellationToken).ConfigureAwait(false);
            if (session is not null && ProcessLiveness.IsAlive(session.Pid))
            {
                sessions.Add(session);
            }
        }

        return sessions;
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
