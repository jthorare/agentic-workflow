namespace Aw.Sindri.Core.Reviews;

/// <summary>A design doc stored with errors.</summary>
/// <param name="Doc">The doc id; its file name when the front matter has none.</param>
/// <param name="Path">The doc's file, with the home folder as <c language="text">~</c>.</param>
/// <param name="Errors">What is wrong, in file order.</param>
public sealed record LibraryBrokenDoc(string Doc, string Path, IReadOnlyList<LibraryDocError> Errors);
