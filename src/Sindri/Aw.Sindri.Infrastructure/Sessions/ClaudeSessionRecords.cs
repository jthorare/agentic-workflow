using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Claude Code's session records over a directory (collector spec §4.1 step 3): '*.json' only (a '.key' file is never opened), at most 256 files (the most recently written) of 64 KB.</summary>
/// <param name="directory">The sessions directory (~/.claude/sessions, or AW_SESSIONS_CLAUDE_DIR).</param>
internal sealed class ClaudeSessionRecords(string directory) : IClaudeSessionRecords
{
    internal const int MaxFiles = 256;
    internal const long MaxBytes = 64 * 1024;

    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SessionsJsonContext.Default);

    public Result<IReadOnlyList<ClaudeRecord>> Read(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return Results.Success<IReadOnlyList<ClaudeRecord>>([]);   // Claude Code isn't installed, or has run no session
        }

        string[] files;
        try
        {
            files = [.. Directory.EnumerateFiles(directory, "*.json").OrderByDescending(static f => LastWrite(f)).ThenBy(static f => f, StringComparer.Ordinal).Take(MaxFiles)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.SessionSourceFailed.With("claude-records-unreadable");
        }

        var records = new List<ClaudeRecord>();
        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (One(file) is { } record)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static DateTime LastWrite(string file)
    {
        try
        {
            return File.GetLastWriteTimeUtc(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;   // gone while we listed
        }
    }

    private static ClaudeRecord? One(string file)
    {
        byte[] bytes;
        try
        {
            using var stream = File.OpenRead(file);
            if (stream.Length > MaxBytes)
            {
                return null;
            }

            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // the session ended while we read
        }

        var parsed = AwJson.Deserialize<ClaudeRecordFile>(bytes, _json);
        return parsed.IsError ? null : Map(parsed.Value);
    }

    private static ClaudeRecord Map(ClaudeRecordFile f) => new(f.Pid, ProcStart(f.ProcStart))
    {
        Session = f.SessionId is { } id && SessionId.TryFrom(id, out var parsed) ? parsed : null,
        Cwd = f.Cwd,
        Kind = f.Kind,
        Entrypoint = f.Entrypoint,
        Tmux = f.Tmux,
        Name = f.Name,
        Status = f.Status,
        StatusUpdatedAt = Time(f.StatusUpdatedAt),
        StartedAt = Time(f.StartedAt),
    };

    private static string? ProcStart(System.Text.Json.JsonElement e) =>
        e.ValueKind == System.Text.Json.JsonValueKind.String ? e.GetString() : e.ValueKind == System.Text.Json.JsonValueKind.Number ? e.GetRawText() : null;

    private static DateTimeOffset? Time(long? epochMilliseconds) =>
        epochMilliseconds is { } ms and > 0 and < 253_402_300_799_999 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;
}
