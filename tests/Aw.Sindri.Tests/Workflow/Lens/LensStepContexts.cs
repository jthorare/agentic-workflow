using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>Builds a lens-log Step's context the way the 'RunBlockStep' activity does: each fact as the string element of its stored JSON text.</summary>
internal static class LensStepContexts
{
    internal static StepContext Of(string step, params (string Key, string Json)[] facts) => new(
        BlockId.From(step),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal),
        facts.ToDictionary(static f => FactKey.From("fact:" + f.Key), static f => JsonSerializer.SerializeToElement(f.Json)),
        InjectionSections.Empty);

    internal static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    internal static string[] Keys(BlockStepResult result) => [.. result.Result!.Outputs.Keys.Select(static k => k.Value["fact:".Length..]).Order(StringComparer.Ordinal)];
}
