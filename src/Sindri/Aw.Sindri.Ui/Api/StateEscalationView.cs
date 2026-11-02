namespace Aw.Sindri.Ui.Api;

/// <summary>How often a model was retried on a stronger one at a state over its last runs there (the Runs table's footer).</summary>
/// <param name="State">The Step's state id.</param>
/// <param name="Model">The model.</param>
/// <param name="Runs">The recent runs that count.</param>
/// <param name="Retried">How many of them were retried on a stronger model.</param>
/// <param name="EscalationRate">Retried over runs, 0 to 1.</param>
/// <param name="Sentence">The footer sentence as served.</param>
internal sealed record StateEscalationView(string State, string Model, int Runs, int Retried, double EscalationRate, string Sentence);
