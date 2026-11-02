namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>How often a model was retried on a stronger one at a state, over its last 20 runs there (model-pick spec §6.1, "Escalation rate").</summary>
/// <param name="State">The Step's state id.</param>
/// <param name="Model">The model.</param>
/// <param name="Runs">The recent runs that count (a crash is not the model's, so it is left out).</param>
/// <param name="Retried">How many of them were refused or failed their check, so the step was retried on a stronger model.</param>
/// <param name="EscalationRate">Retried over runs, 0 to 1.</param>
public sealed record StateEscalation(string State, string Model, int Runs, int Retried, double EscalationRate)
{
    /// <summary>The footer sentence: 'retried on a stronger model in 3 of 20 runs'.</summary>
    public string Sentence => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"retried on a stronger model in {Retried} of {Runs} runs");
}
