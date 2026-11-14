namespace Aw.Sindri.Core.Setup;

/// <summary>Which agent the workflows run on (first-run spec §3.1, step 5).</summary>
/// <param name="Target">The profile's `workflow.agent.target`, or null while none is set.</param>
public sealed record AgentFact(string? Target);
