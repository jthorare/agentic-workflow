namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One doc stored with errors.</summary>
/// <param name="Doc">The doc id.</param>
/// <param name="Path">Its file.</param>
/// <param name="Errors">What is wrong, in file order.</param>
internal sealed record LibraryBrokenView(string Doc, string Path, IReadOnlyList<LibraryErrorView> Errors);
