namespace Aw.Sindri.Host.Commands;

/// <summary>What 'sindri library validate --json' prints.</summary>
/// <param name="Findings">Every finding, by file then line.</param>
/// <param name="Passed">True when there are none.</param>
internal sealed record LibraryValidateReport(IReadOnlyList<LibraryFindingRow> Findings, bool Passed);
