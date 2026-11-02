namespace Aw.Sindri.Core.Jobs;

/// <summary>The harness and model that answered a stored model call (<see cref="JobModelCall.AnsweredBy"/>).</summary>
/// <param name="Harness">The harness.</param>
/// <param name="Model">The model it ran.</param>
public sealed record JobModelAnswerer(string Harness, string Model);
