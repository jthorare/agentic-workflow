namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>One environment variable of the session.</summary>
/// <param name="Name">The name.</param>
/// <param name="Value">The value, or null when 'session wrap' supplies it at start (the launch secret, a model key).</param>
/// <param name="Reason">Why the allowlist has it.</param>
public sealed record PlanEnvVar(string Name, string? Value, string Reason);
