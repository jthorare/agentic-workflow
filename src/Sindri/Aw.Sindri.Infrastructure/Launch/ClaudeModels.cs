using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

#pragma warning disable MA0182 // until the launch probe fills TargetFacts.Models with it (M-3, after M-1's types)
/// <summary>Claude Code has no command that lists models (spec §3), so Sindri keeps the aliases its --model flag accepts, cheapest first.</summary>
internal static class ClaudeModels
{
    /// <summary>The catalog entry's models.</summary>
    public static IReadOnlyList<ModelFacts> Catalog { get; } =
    [
        new("haiku", "Haiku", ModelSource.Catalog, Order: 0),
        new("sonnet", "Sonnet", ModelSource.Catalog, Order: 1),
        new("opus", "Opus", ModelSource.Catalog, Order: 2),
    ];
}
#pragma warning restore MA0182
