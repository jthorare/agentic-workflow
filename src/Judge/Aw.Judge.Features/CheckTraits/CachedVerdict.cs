using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>A trait's verdict on an excerpt reused from the verdict cache instead of asking Jev.</summary>
/// <param name="Task">The excerpt (its batch is the one trait).</param>
/// <param name="Trait">The trait.</param>
/// <param name="Answer">Jev's earlier answer.</param>
internal sealed record CachedVerdict(TraitTask Task, Trait Trait, ChoiceBatchAnswer Answer);
