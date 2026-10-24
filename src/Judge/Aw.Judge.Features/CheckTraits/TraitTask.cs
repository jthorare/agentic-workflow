using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>One Jev request: an excerpt of a file and the traits asked about it.</summary>
/// <param name="Source">The excerpt.</param>
/// <param name="Batch">The traits.</param>
internal sealed record TraitTask(DiffSource Source, IReadOnlyList<Trait> Batch);
