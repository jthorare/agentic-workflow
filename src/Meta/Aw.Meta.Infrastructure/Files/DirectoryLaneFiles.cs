using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// ILaneFiles over $AW_STATE_DIR/meta/lane-sessions: &lt;lane&gt; holds the session id the CLI's hook reported, &lt;lane&gt;.agent the agent facts the
/// launcher recorded (JSON: agent, processName, hooks, transcripts), &lt;lane&gt;.ambiguous marks a refused guess. Every file is read
/// defensively: one that can't be read counts as absent.
/// </summary>
/// <param name="directory">The lane-sessions directory.</param>
internal sealed class DirectoryLaneFiles(string directory) : ILaneFiles
{
    public async ValueTask<Result<LaneHookTrace?>> HookTrace(string lane, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lane);
        if (!IsLaneName(lane))
        {
            return Results.Success<LaneHookTrace?>(value: null);
        }

        try
        {
            var path = Path.Combine(directory, lane + ".hook");
            if (!File.Exists(path))
            {
                return Results.Success<LaneHookTrace?>(value: null);
            }

            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var fields = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return Results.Success(
                fields.Length == 4 && DateTimeOffset.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                    ? new LaneHookTrace(at, fields[1], fields[2], fields[3])
                    : null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Results.Success<LaneHookTrace?>(value: null);
        }
    }

    public async ValueTask<Result<LaneRecord?>> Record(string lane, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lane);
        if (!IsLaneName(lane))
        {
            return Results.Success<LaneRecord?>(value: null);
        }

        var session = await ReadSession(Path.Combine(directory, lane), cancellationToken).ConfigureAwait(false);
        var agent = await ReadAgent(Path.Combine(directory, lane + ".agent"), cancellationToken).ConfigureAwait(false);
        var ambiguous = File.Exists(Path.Combine(directory, lane + ".ambiguous"));
        return session is null && agent is null && !ambiguous
            ? Results.Success<LaneRecord?>(value: null)
            : Results.Success<LaneRecord?>(new LaneRecord(lane, session?.Id, agent, session?.At, ambiguous));
    }

    public async ValueTask<Result<Unit>> RecordSession(string lane, SessionId session, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lane);
        if (!IsLaneName(lane))
        {
            return MetaErrors.UnknownLane.With($"'{lane}' is not a lane name");
        }

        var temporary = Path.Combine(directory, "." + lane + "." + Guid.NewGuid().ToString("N"));
        try
        {
            _ = Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(temporary, session.Value + "\n", cancellationToken).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(temporary, Path.Combine(directory, lane), overwrite: true);
            File.Delete(Path.Combine(directory, lane + ".ambiguous"));
            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.CommandFailed.With($"lane-sessions/{lane} can't be written: {exception.Message}");
        }
    }

    // The characters team-init accepts for a lane: letters, digits, '.', '_' and '-'. A name of only dots would name the directory
    // itself or its parent, so it never names a file here.
    private static bool IsLaneName(string lane) =>
        lane.Length > 0 && lane.Any(static c => c != '.') && lane.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static async ValueTask<(SessionId Id, DateTimeOffset At)?> ReadSession(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var text = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
            return text.Length == 0 ? null : (SessionId.From(text), new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async ValueTask<LaneAgent?> ReadAgent(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "agent") is not { Length: > 0 } agent)
            {
                return null;
            }

            var hooks = root.TryGetProperty("hooks", out var flag) && flag.ValueKind == JsonValueKind.True;
            return new LaneAgent(agent, Text(root, "processName") ?? agent, hooks, Text(root, "transcripts"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
