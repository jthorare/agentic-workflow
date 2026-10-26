using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The agent runs of a scoping instance as the 'runs' fact and the packet's runs.json hold them (issue 966 section 5.1): one object per run, oldest first.</summary>
internal static class ScopeRunFacts
{
    /// <summary>The records as the runs fact: the array the model checks (SCP-CHAL-002, SCP-GAP-002, SCP-REL-002) and runs.json read.</summary>
    /// <param name="records">The ledger's records, oldest first.</param>
    /// <returns>The array, empty when the instance launched no agent.</returns>
    internal static JsonArray Of(IReadOnlyList<ScopeRunRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var runs = new JsonArray();
        foreach (var record in records)
        {
            ScopeJson.Append(runs, new JsonObject
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
            });
        }

        return runs;
    }
}
