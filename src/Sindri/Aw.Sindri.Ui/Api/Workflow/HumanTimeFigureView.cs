namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How often a person came in one way, and how long it took them (GET /workflow/recipes/{id}/stats).</summary>
/// <param name="N">How many times.</param>
/// <param name="MedianSeconds">The typical wait for the person in seconds; null when it never happened.</param>
internal sealed record HumanTimeFigureView(int N, long? MedianSeconds);
