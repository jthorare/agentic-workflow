using System.Text.Json.Nodes;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>Maps PromptSortSql's columns to the sorter's rows.</summary>
internal static class PromptSortMappers
{
    internal static readonly IRowMapper<SortedAxis> Axis = new AxisMapper();
    internal static readonly IRowMapper<SortRun> Run = new RunMapper();

    private sealed class AxisMapper : IRowMapper<SortedAxis>
    {
        public SortedAxis Map(SqliteDataReader reader) => new(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetDouble(5), reader.IsDBNull(6) ? null : reader.GetDouble(6));
    }

    private sealed class RunMapper : IRowMapper<SortRun>
    {
        public SortRun Map(SqliteDataReader reader) => new(
            reader.GetString(0), reader.GetString(1), Items(reader.GetString(2)), Items(reader.GetString(3)), Items(reader.GetString(4)));
    }

    // A JSON array's elements: a string's text, an object's JSON.
    private static List<string> Items(string json) =>
        JsonNode.Parse(json) is JsonArray array
            ? [.. array.Select(static item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : item?.ToJsonString() ?? "")]
            : [];
}
