namespace Aw.Sindri.Core.Events;

/// <summary>The harness and model that answered a call (<see cref="ContextModelCalled.AnsweredBy"/>).</summary>
/// <param name="Harness">The harness.</param>
/// <param name="Model">The model it ran, as the CLI said when it did.</param>
public sealed record ModelCallAnswerer(string Harness, string Model);
