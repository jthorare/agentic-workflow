using Aw.Context.Core.Models;

namespace Aw.Context.Core.Engine;

/// <summary>Spec §3.2: the pure part of the router. It decides which harnesses a call tries and which model each runs; nothing here starts a CLI.</summary>
public static class ModelRouting
{
    /// <summary>The route of one call.</summary>
    /// <param name="settings">'context.models'.</param>
    /// <param name="request">The call. Its Models and Model win over the profile; its Tier counts only for a concept scan, which picks its own from the map's size.</param>
    /// <param name="installed">The harnesses found on PATH, in the default order (the AW_PROVIDER one first).</param>
    /// <returns>The harnesses to try, in order, each with its model and where the name came from; empty when none is installed.</returns>
    public static IReadOnlyList<RoutedModel> Resolve(ModelsSettings settings, ModelRequest request, IReadOnlyList<ModelHarness> installed) =>
        throw new NotSupportedException();
}
