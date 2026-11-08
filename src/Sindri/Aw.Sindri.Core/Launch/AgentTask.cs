namespace Aw.Sindri.Core.Launch;

/// <summary>What a 'run_agent' session is asked to do: the first message, the fields it must report, and the model to run.</summary>
/// <param name="Brief">The session's first message.</param>
/// <param name="Outputs">The declared output fields, name to type ('string?' marks an optional one).</param>
/// <param name="Model">A model id, or null for the agent's default.</param>
public sealed record AgentTask(string Brief, IReadOnlyDictionary<string, string> Outputs, string? Model);
