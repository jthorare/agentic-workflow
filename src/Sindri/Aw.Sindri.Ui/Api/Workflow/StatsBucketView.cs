namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One week of a recipe's runs, as the list and the stats serve it.</summary>
/// <param name="WeekStart">The Monday the week starts on, yyyy-MM-dd.</param>
/// <param name="Runs">Runs that started that week.</param>
/// <param name="HumanRate">The share of those runs that needed a person (0 to 1); null when there were none.</param>
internal sealed record StatsBucketView(string WeekStart, int Runs, double? HumanRate);
