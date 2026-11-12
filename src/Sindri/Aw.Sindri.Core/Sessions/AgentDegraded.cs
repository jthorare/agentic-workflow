namespace Aw.Sindri.Core.Sessions;

/// <summary>A source the Agents list could not read in full (Agents page spec §9): data beside the rows, not a refusal.</summary>
/// <param name="Code">'lanes-unavailable', 'source-stale', 'heavy-waits-unreadable', 'behind' or 'unmatched-sender'.</param>
/// <param name="Detail">What is missing, in a sentence.</param>
public sealed record AgentDegraded(string Code, string Detail);
