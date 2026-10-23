namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The checks of a pull request, counted.</summary>
/// <param name="Passed">Checks that passed.</param>
/// <param name="Failed">Checks that failed.</param>
/// <param name="Pending">Checks still running or waiting.</param>
internal sealed record PrChecksView(int Passed, int Failed, int Pending);
