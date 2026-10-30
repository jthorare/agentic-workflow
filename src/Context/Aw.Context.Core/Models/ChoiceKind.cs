namespace Aw.Context.Core.Models;

/// <summary>Which pick a call to <see cref="IChoicePort"/> belongs to: each has its own 'pick' key in the approved profile and its own ledger event.</summary>
public enum ChoiceKind
{
    /// <summary>The cluster label pick ('context.models.labeling.pick').</summary>
    Labeling,

    /// <summary>The product-surface pick ('context.models.productSurfaces.pick'), asked by the product-surface step.</summary>
    ProductSurfaces,
}
