using Aw.Context.Core.Models;

namespace Aw.Context.Core.Engine;

/// <summary>One 'context.models.default' or 'context.models.&lt;kind&gt;' entry as written; a null part is not set, and a kind inherits the default key by key.</summary>
/// <param name="Harnesses">The harnesses to try, in order, or null.</param>
/// <param name="Tier">The tier, or null.</param>
/// <param name="Models">A model per harness, which wins over the tier for that harness, or null.</param>
public sealed record ModelEntry(IReadOnlyList<ModelHarness>? Harnesses = null, ModelTier? Tier = null, IReadOnlyDictionary<ModelHarness, string>? Models = null);
