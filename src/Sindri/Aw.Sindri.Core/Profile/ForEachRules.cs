using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'workflow.forEach' keys (PR 9): 'maxItems' and 'batchSize' are whole numbers inside the ranges the 'for_each' block takes.</summary>
internal static class ForEachRules
{
    /// <summary>Checks the block.</summary>
    /// <param name="settings">The 'workflow.forEach' block, or null.</param>
    /// <returns>The first problem, or null.</returns>
    public static Error? Check(ForEachSettings? settings)
    {
        if (settings?.MaxItems is { } max && max is < 1 or > ForEachLimits.MaxItemsCeiling)
        {
            return Invalid("workflow.forEach.maxItems", max, ForEachLimits.MaxItemsCeiling);
        }

        return settings?.BatchSize is { } batch && batch is < 1 or > ForEachLimits.BatchSizeCeiling
            ? Invalid("workflow.forEach.batchSize", batch, ForEachLimits.BatchSizeCeiling)
            : null;
    }

    private static Error Invalid(string keyPath, int value, int ceiling) =>
        SindriErrors.ProfileInvalid.With(string.Create(CultureInfo.InvariantCulture, $"{keyPath}: {value} is not between 1 and {ceiling}"));
}
