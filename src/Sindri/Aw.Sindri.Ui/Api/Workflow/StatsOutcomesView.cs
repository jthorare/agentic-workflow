namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How the finished runs of a recipe got there (GET /workflow/recipes/{id}/stats).</summary>
/// <param name="DoneWithoutHuman">Runs that finished without a person.</param>
/// <param name="DoneAfterPark">Runs that finished after a park was answered.</param>
/// <param name="DoneAfterTakeover">Runs that finished after a takeover.</param>
internal sealed record StatsOutcomesView(int DoneWithoutHuman, int DoneAfterPark, int DoneAfterTakeover);
