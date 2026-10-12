using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests;

/// <summary>Checks a printed decision row (why/undo/approve) against A.Decision()'s defaults, field by field.</summary>
internal static class DecisionJson
{
    internal static void AssertRow(JsonElement row, string id, string reasonCode, bool undone)
    {
        Assert.Equal(id, row.GetProperty("id").GetString());
        Assert.Equal(KnownInstants.Epoch, row.GetProperty("ts").GetDateTimeOffset());
        Assert.Equal("wake-gate", row.GetProperty("question").GetString());
        Assert.Equal("general", row.GetProperty("content_class").GetString());
        Assert.Equal("claude-cli", row.GetProperty("provider").GetString());
        Assert.Equal("wake", row.GetProperty("decision").GetString());
        Assert.Equal(0.875, row.GetProperty("confidence").GetDouble());
        Assert.Equal(reasonCode, row.GetProperty("reason_code").GetString());
        Assert.Equal(1234, row.GetProperty("latency_ms").GetInt64());
        Assert.Equal("0123456789abcdef", row.GetProperty("input_digest").GetString());
        Assert.Equal(undone ? JsonValueKind.String : JsonValueKind.Null, row.GetProperty("undone_at").ValueKind);
        Assert.True(row.TryGetProperty("approved_at", out _), "every row carries approved_at (null until approved)");
        Assert.Equal(1, row.GetProperty("chain_position").GetInt32());
        var skipped = Assert.Single(row.GetProperty("skipped").EnumerateArray());
        Assert.Equal(("jev", "unavailable"), (skipped.GetProperty("provider").GetString(), skipped.GetProperty("reason").GetString()));
        Assert.Equal("decided", row.GetProperty("outcome").GetString());
    }
}
