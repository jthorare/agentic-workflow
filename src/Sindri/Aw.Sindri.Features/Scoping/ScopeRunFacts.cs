using System.Text.Json.Nodes;
using Aw.Context.Core.ScopeGraph;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The agent runs of a scoping instance as the 'runs' fact and the packet's runs.json hold them (issue 966 section 5.1): one object per run, oldest first.</summary>
internal static class ScopeRunFacts
{
    /// <summary>The records as the runs fact: the array the model checks (SCP-CHAL-002, SCP-GAP-002, SCP-REL-002) and runs.json read.</summary>
    /// <param name="records">The ledger's records, oldest first.</param>
    /// <returns>The array, empty when the instance launched no agent.</returns>
    /// <param name="reads">The graph calls each run's session made, by the run's token (issue 1005); a run with none listed has no 'graphReads' (unknown, not zero).</param>
    internal static JsonArray Of(IReadOnlyList<ScopeRunRecord> records, IReadOnlyDictionary<string, IReadOnlyList<GraphRead>>? reads = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        var runs = new JsonArray();
        foreach (var record in records)
        {
            var run = new JsonObject
            {
                ["step"] = record.Step,
                ["state"] = record.State,
                ["round"] = record.Round,
                ["via"] = record.Via,
                ["model"] = record.Model,
                ["requested"] = record.Requested,
                ["vendor"] = record.Vendor,
                ["tokens"] = record.Tokens,
                ["launch"] = record.Launch,
            };
            if (record.RunToken is { } token)
            {
                run["runToken"] = token;
                if (reads?.GetValueOrDefault(token) is { } made)
                {
                    run["graphReads"] = GraphReads(made);
                }
            }

            ScopeJson.Append(runs, run);
        }

        return runs;
    }

    // The nodes read inside the graph (once each, in order) and the steps outside it with the reason the session gave.
    private static JsonObject GraphReads(IReadOnlyList<GraphRead> made) => new()
    {
        ["read"] = ScopeJson.ToArray(made.Where(static r => !r.Outside).Select(static r => r.Node).Distinct(StringComparer.Ordinal)),
        ["outside"] = new JsonArray([.. made.Where(static r => r.Outside).Select(static r => (JsonNode?)new JsonObject { ["node"] = r.Node, ["tool"] = r.Tool, ["reason"] = r.Reason })]),
    };
}
