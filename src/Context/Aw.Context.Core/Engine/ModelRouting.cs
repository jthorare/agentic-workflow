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
    public static IReadOnlyList<RoutedModel> Resolve(ModelsSettings settings, ModelRequest request, IReadOnlyList<ModelHarness> installed)
    {
        var entry = settings.Entry(ModelKinds.Of(request.Purpose));
        var wanted = entry.Harnesses ?? settings.Default.Harnesses;
        var order = wanted is null ? installed : [.. wanted.Where(installed.Contains)];
        var tier = request.Purpose == ModelPurpose.ConceptScan && request.Tier is { } picked ? picked : entry.Tier ?? settings.Default.Tier ?? ModelTier.Cheap;
        return [.. order.Select(harness => Route(settings, entry, request, harness, tier))];
    }

    private static RoutedModel Route(ModelsSettings settings, ModelEntry entry, ModelRequest request, ModelHarness harness, ModelTier tier)
    {
        if (request.Models is not null && request.Models.TryGetValue(ModelHarnesses.RequestKey(harness), out var named))
        {
            return new RoutedModel(harness, named, ModelSource.Request);
        }

        if (request.Model is { } any)
        {
            return new RoutedModel(harness, any, ModelSource.Request);
        }

        if (entry.Models is not null && entry.Models.TryGetValue(harness, out var kindModel))
        {
            return new RoutedModel(harness, kindModel, ModelSource.KindModels);
        }

        if (settings.Default.Models is not null && settings.Default.Models.TryGetValue(harness, out var defaultModel))
        {
            return new RoutedModel(harness, defaultModel, ModelSource.DefaultModels);
        }

        return settings.Names.Written(harness, tier) is { } written
            ? new RoutedModel(harness, written, ModelSource.Names)
            : new RoutedModel(harness, ModelNames.BuiltInName(harness, tier), ModelSource.Builtin);
    }
}
