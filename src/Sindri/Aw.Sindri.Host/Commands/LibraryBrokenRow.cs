using Aw.Sindri.Features.GetLibraryDoc;

namespace Aw.Sindri.Host.Commands;

/// <summary>One doc with errors.</summary>
/// <param name="Doc">The doc id.</param>
/// <param name="Path">Its file.</param>
/// <param name="Errors">What is wrong, in file order.</param>
internal sealed record LibraryBrokenRow(string Doc, string Path, IReadOnlyList<LibraryDocErrorView> Errors);
