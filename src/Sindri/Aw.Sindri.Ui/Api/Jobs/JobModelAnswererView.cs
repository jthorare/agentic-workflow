namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>The harness and model that answered a call.</summary>
/// <param name="Harness">The harness.</param>
/// <param name="Model">The model it ran.</param>
internal sealed record JobModelAnswererView(string Harness, string Model);
