namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One run in the Model picks table: each cell in words, and the accessible names that say the same.</summary>
/// <param name="Step">The Step's name, with " (retry n)" for an escalated run.</param>
/// <param name="Model">The model's short name, "choosing model…" while Jev is asked, or "agent default".</param>
/// <param name="Tokens">Input plus output, "41k".</param>
/// <param name="Cost">"$0.18", "cost unknown", or empty while picking.</param>
/// <param name="Why">The served sentence, or "asking Jev" while picking.</param>
/// <param name="Outcome">refused, failed, passed or running; empty while picking.</param>
/// <param name="IsPicking">True while only 'model.picking' exists.</param>
/// <param name="IsRetry">True for an escalated run.</param>
/// <param name="ModelName">The Model cell's accessible name, "Model: Haiku, picked by Jev".</param>
/// <param name="Name">The row's accessible name: step, model, tokens, cost, why, then outcome.</param>
/// <param name="Panel">The Why panel, or null when the pick has no detail.</param>
internal sealed record ModelPickRowViewModel(
    string Step, string Model, string Tokens, string Cost, string Why, string Outcome, bool IsPicking, bool IsRetry, string ModelName, string Name, WhyPanelViewModel? Panel);
