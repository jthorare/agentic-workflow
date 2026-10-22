using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>Builds a build or ship Step's context the way the 'RunBlockStep' activity does: each fact as the string element of its stored JSON text.</summary>
internal static class BuildShipContexts
{
    internal static StepContext Of(string step, Dictionary<string, string> facts, string? address = null) => new(
        BlockId.From(step),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal),
        facts.ToDictionary(static f => FactKey.From("fact:" + f.Key), static f => JsonSerializer.SerializeToElement(f.Value)),
        InjectionSections.Empty)
    {
        Address = address,
    };
}
