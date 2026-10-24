namespace Aw.Judge.Core.Traits;

/// <summary>A trait file that couldn't be used.</summary>
/// <param name="File">The file.</param>
/// <param name="Error">Why, in one line.</param>
public sealed record TraitProblem(string File, string Error);
