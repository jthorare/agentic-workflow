namespace Aw.Sindri.Core.Launch;

/// <summary>Puts an agent's models in order and cuts them into the three bands (model-pick spec §3, "Tiers"). Pure.</summary>
public static class ModelBands
{
    /// <summary>
    /// The models cheapest first with their bands. Order: by <see cref="ModelFacts.Order"/>, ties and the listed order kept; models with no order go last, in the order given.
    /// With N models, cheap is the first ceil(N/3), strong the last ceil(N/3) and standard what is left between; when nothing is left between, standard is every model.
    /// </summary>
    /// <param name="models">The agent's models in any order.</param>
    /// <returns>The ranked models, cheapest first.</returns>
    public static IReadOnlyList<RankedModel> Rank(IReadOnlyList<ModelFacts> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        // OrderBy is stable, so models with the same place keep the order given
        var ordered = models.Where(static m => m.Order is not null).OrderBy(static m => m.Order).Concat(models.Where(static m => m.Order is null)).ToList();
        var count = ordered.Count;
        var third = (count + 2) / 3;
        var hasMiddle = count - (2 * third) > 0;
        var ranked = new List<RankedModel>(count);
        for (var i = 0; i < count; i++)
        {
            var tiers = ModelTier.None;
            tiers |= i < third ? ModelTier.Cheap : ModelTier.None;
            tiers |= i >= count - third ? ModelTier.Strong : ModelTier.None;
            tiers |= !hasMiddle || (i >= third && i < count - third) ? ModelTier.Standard : ModelTier.None;
            ranked.Add(new RankedModel(ordered[i], tiers, Unordered: ordered[i].Order is null));
        }

        return ranked;
    }
}
