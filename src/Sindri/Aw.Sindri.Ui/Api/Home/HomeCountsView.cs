namespace Aw.Sindri.Ui.Api.Home;

/// <summary>How many rows Home holds per bucket.</summary>
/// <param name="NeedsYou">Open rows in needs you.</param>
/// <param name="Moving">Open rows in moving.</param>
/// <param name="Snoozed">Snoozed rows.</param>
/// <param name="Done">Done rows.</param>
internal sealed record HomeCountsView(int NeedsYou, int Moving, int Snoozed, int Done);
