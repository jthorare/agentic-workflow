namespace Aw.Sindri.Core.Launch;

/// <summary>The models a step may run on, before Jev is asked (model-pick spec §4.1). Pure.</summary>
public static class ModelCandidates
{
    /// <summary>
    /// Removes, in order: models below the floor; models above the cap, or all but the pin; the drafter's model for a challenger (all of the agent's models when the drafter's is unknown and the agent is shared);
    /// and, for an escalation, the failed model and everything at or below it. A model is attributed to the first rule that removes it.
    /// </summary>
    /// <param name="ladder">The agent's models from <see cref="ModelBands.Rank"/>, cheapest first.</param>
    /// <param name="constraints">What the step may use.</param>
    /// <returns>The candidates and the removals.</returns>
    public static ModelSelection For(IReadOnlyList<RankedModel> ladder, ModelConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        ArgumentNullException.ThrowIfNull(constraints);
        var capPosition = PositionOf(ladder, constraints.Cap);
        var failedPosition = PositionOf(ladder, constraints.Failed);
        var floorMask = FloorMask(constraints.Floor);
        var remaining = ladder.Select(static (model, position) => (Model: model, Position: position)).ToList();
        var removed = new List<ModelRemoval>();

        Apply(remaining, removed, ModelConstraint.Floor, floorMask != ModelTier.None, c => (c.Model.Tiers & floorMask) == ModelTier.None);
        Apply(remaining, removed, ModelConstraint.Cap, capPosition >= 0, c => c.Position > capPosition);
        Apply(remaining, removed, ModelConstraint.Pin, constraints.Pin is not null, c => !string.Equals(c.Model.Model.Slug, constraints.Pin, StringComparison.Ordinal));
        var sameAgent = constraints.Drafter?.SameAgent == true;
        var drafterSlug = constraints.Drafter?.Model;
        Apply(remaining, removed, ModelConstraint.DrafterUnknown, sameAgent && drafterSlug is null, static _ => true);
        Apply(remaining, removed, ModelConstraint.Drafter, sameAgent && drafterSlug is not null, c => string.Equals(c.Model.Model.Slug, drafterSlug, StringComparison.Ordinal));
        Apply(remaining, removed, ModelConstraint.Failed, failedPosition >= 0, c => c.Position <= failedPosition);

        return new ModelSelection([.. remaining.Select(static c => c.Model)], removed);
    }

    private static void Apply(List<(RankedModel Model, int Position)> remaining, List<ModelRemoval> removed, ModelConstraint rule, bool applies, Func<(RankedModel Model, int Position), bool> removes)
    {
        if (!applies)
        {
            return;
        }

        foreach (var candidate in remaining.Where(removes).ToList())
        {
            removed.Add(new ModelRemoval(candidate.Model, rule));
            _ = remaining.Remove(candidate);
        }
    }

    private static int PositionOf(IReadOnlyList<RankedModel> ladder, string? slug)
    {
        for (var i = 0; slug is not null && i < ladder.Count; i++)
        {
            if (string.Equals(ladder[i].Model.Slug, slug, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // The bands at or above the floor: a floor of Standard is Standard and Strong; None (and Cheap) is every band. Only the lowest set band of the floor counts.
    private static ModelTier FloorMask(ModelTier floor)
    {
        if (floor == ModelTier.None)
        {
            return ModelTier.None;
        }

        if (floor.HasFlag(ModelTier.Cheap))
        {
            return ModelTier.Cheap | ModelTier.Standard | ModelTier.Strong;
        }

        return floor.HasFlag(ModelTier.Standard) ? ModelTier.Standard | ModelTier.Strong : ModelTier.Strong;
    }
}
