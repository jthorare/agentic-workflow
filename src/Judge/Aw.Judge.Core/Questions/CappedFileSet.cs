namespace Aw.Judge.Core.Questions;

/// <summary>The result of <see cref="UntrustedText.CappedFiles"/>.</summary>
/// <param name="Kept">The files that stayed, in the order given; a cut file carries its cut text.</param>
/// <param name="Omitted">The files dropped for size, with why 'cap'.</param>
/// <param name="Cut">The file cut at a line, if any.</param>
public sealed record CappedFileSet(IReadOnlyList<ModelPickFile> Kept, IReadOnlyList<ModelPickOmitted> Omitted, IReadOnlyList<ModelPickCut> Cut);
