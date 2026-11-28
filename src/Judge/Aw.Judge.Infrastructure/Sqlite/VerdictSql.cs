using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>The verdict cache's SQL: named constants and the statements that bind them.</summary>
internal static class VerdictSql
{
    internal const string Find = "SELECT choice, confidence, probabilities FROM trait_verdicts WHERE key = $key AND stored_at >= $cutoff";

    internal const string Upsert =
        "INSERT OR REPLACE INTO trait_verdicts (key, choice, confidence, probabilities, stored_at) " +
        "VALUES ($key, $choice, $confidence, $probabilities, $stored_at)";

    internal const string Expire = "DELETE FROM trait_verdicts WHERE stored_at < $cutoff";

    // Everything past the newest $keep rows; rowid breaks a tie in stored_at by write order.
    internal const string Trim =
        "DELETE FROM trait_verdicts WHERE key IN (SELECT key FROM trait_verdicts ORDER BY stored_at DESC, rowid DESC LIMIT -1 OFFSET $keep)";

    internal static SqlStatement Lookup(string key, long cutoff) =>
        new(Find, c => c.Bind("$key", key).Bind("$cutoff", cutoff), "trait verdict");

    internal static SqlStatement Store(string key, ChoiceBatchAnswer answer, long storedAt)
    {
        var probabilities = new JsonObject();
        foreach (var (option, p) in answer.Probabilities)
        {
            probabilities[option] = p;
        }

        return new(Upsert, c => c
            .Bind("$key", key).Bind("$choice", answer.Choice).Bind("$confidence", answer.Confidence)
            .Bind("$probabilities", probabilities.ToJsonString()).Bind("$stored_at", storedAt), "trait verdict");
    }

    internal static SqlStatement ExpireBefore(long cutoff) => new(Expire, c => c.Bind("$cutoff", cutoff), "trait verdicts");

    internal static SqlStatement KeepNewest(int count) => new(Trim, c => c.Bind("$keep", count), "trait verdicts");
}
