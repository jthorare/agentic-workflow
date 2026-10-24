namespace Aw.Judge.Core.Traits;

/// <summary>The traits of a repo and the files that were skipped.</summary>
/// <param name="Traits">The usable traits; an overlay trait replaces a repo trait of the same name.</param>
/// <param name="Problems">The files that couldn't be used.</param>
public sealed record TraitSet(IReadOnlyList<Trait> Traits, IReadOnlyList<TraitProblem> Problems);
