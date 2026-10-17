namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The fields of a merged entry the hash covers (bin is hashed by its last path segment); the form fields are wire names.</summary>
/// <param name="Bin">The entry's bin.</param>
/// <param name="Open">The open template.</param>
/// <param name="Resume">The resume template, or null.</param>
/// <param name="SessionId">The sessionId mode's wire name.</param>
/// <param name="Mcp">The mcp form's wire name.</param>
/// <param name="Model">The model form's wire name.</param>
/// <param name="Policy">The policy form's wire name.</param>
internal sealed record HashInput(string Bin, IReadOnlyList<string> Open, IReadOnlyList<string>? Resume, string SessionId, string Mcp, string Model, string Policy);
