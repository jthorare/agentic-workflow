using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The config of a 'meetings' node, read and checked.</summary>
/// <param name="Connection">The meeting-notes connection to read with.</param>
/// <param name="Day">The day to read, or null for today in the profile timezone.</param>
/// <param name="MinMinutes">Meetings shorter than this are dropped; 0 keeps all.</param>
/// <param name="Include">What to read of each meeting.</param>
public sealed record MeetingsConfig(ConnectionName Connection, DateOnly? Day, int MinMinutes, MeetingInclude Include)
{
    /// <summary>Reads a node's config.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The config, or the first problem and how to fix it.</returns>
    public static Result<MeetingsConfig> Parse(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Problem(config) is { } problem)
        {
            return SindriErrors.GraphBlockFailed.With("meetings: " + problem.Field + " " + problem.Text);
        }

        var connection = ConnectionName.Parse(Text(config["connection"]) ?? string.Empty);
        if (connection.IsError)
        {
            return connection.Error;
        }

        var minutes = config["minMinutes"] is JsonValue m && m.TryGetValue<int>(out var whole) ? whole : 0;
        var include = string.Equals(Text(config["include"]), "transcript", StringComparison.Ordinal) ? MeetingInclude.Transcript : MeetingInclude.Notes;
        return new MeetingsConfig(connection.Value, ReadDay(config["day"]), minutes, include);
    }

    /// <summary>Finds the first thing wrong with a config that the field types can't say.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The field and the fix, or null when the config fits.</returns>
    public static (string Field, string Text)? Problem(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ConnectionProblem(config["connection"]) ?? DayProblem(config["day"]) ?? MinutesProblem(config["minMinutes"]) ?? IncludeProblem(config["include"]);
    }

    private static (string, string)? ConnectionProblem(JsonNode? node) =>
        Text(node) is { } name && ConnectionName.Parse(name).IsSuccess ? null : ("connection", "must be a connection name ('<service>:<label>', for example 'granola:work'); change it");

    private static (string, string)? DayProblem(JsonNode? node) =>
        Text(node) is not { } day || IsReference(day) || string.Equals(day, "today", StringComparison.Ordinal) || ReadDay(node) is not null
            ? null
            : ("day", "must be 'today' or a date such as 2026-11-09; change it");

    // why: a number field may be one reference, which resolves to a number at run time (BlockSchema.Check)
    private static (string, string)? MinutesProblem(JsonNode? node) =>
        node is null || (Text(node) is { } text && IsReference(text)) || (node is JsonValue { } value && value.TryGetValue<int>(out var whole) && whole >= 0)
            ? null
            : ("minMinutes", "must be a whole number of minutes, 0 or more; change it");

    private static (string, string)? IncludeProblem(JsonNode? node) =>
        Text(node) is not { } include || string.Equals(include, "notes", StringComparison.Ordinal) || string.Equals(include, "transcript", StringComparison.Ordinal)
            ? null
            : ("include", "must be 'notes' or 'transcript'; change it");

    private static bool IsReference(string text) => text.Contains("{{", StringComparison.Ordinal);

    private static DateOnly? ReadDay(JsonNode? node) =>
        Text(node) is { } text && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
