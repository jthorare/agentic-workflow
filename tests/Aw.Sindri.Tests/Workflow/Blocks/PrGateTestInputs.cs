using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>The context a PR gate gets from a recipe: its 'with:' arguments resolved, and the facts of the instance.</summary>
internal static class PrGateTestInputs
{
    internal const string Workspace = "/work/pr";

    internal static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    internal static GateContext Context(string gate, Dictionary<string, JsonElement> arguments, Dictionary<FactKey, JsonElement>? facts = null) =>
        new(BlockId.From(gate), arguments, facts ?? [], Threshold: null);

    internal static Dictionary<string, JsonElement> Arguments(params (string Name, string Json)[] values) =>
        values.ToDictionary(static v => v.Name, static v => Json(v.Json), StringComparer.Ordinal);

    /// <summary>fact:prState as the snapshot writes it, reduced to the shas the gates read.</summary>
    internal static string PrState(string baseSha = "b0", string headSha = "h1") => $$$"""{"base":{"sha":"{{{baseSha}}}"},"head":{"sha":"{{{headSha}}}"}}""";
}
