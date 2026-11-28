using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>Maps VerdictSql.Find's columns to a ChoiceBatchAnswer; a row whose probabilities aren't a JSON object of numbers is InvalidDataException.</summary>
internal sealed class VerdictMapper : IRowMapper<ChoiceBatchAnswer>
{
    internal static readonly VerdictMapper Instance = new();

    public ChoiceBatchAnswer Map(SqliteDataReader reader) =>
        new(reader.GetString(0), reader.GetDouble(1), Probabilities(reader.GetString(2)));

    private static Dictionary<string, double> Probabilities(string text)
    {
        try
        {
            Dictionary<string, double> probabilities = new(StringComparer.Ordinal);
            foreach (var (option, value) in JsonNode.Parse(text)?.AsObject() ?? [])
            {
                probabilities[option] = value?.GetValue<double>() ?? throw new InvalidDataException("a cached probability is null");
            }

            return probabilities;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("a cached verdict's probabilities are not a JSON object of numbers", e);
        }
    }
}
