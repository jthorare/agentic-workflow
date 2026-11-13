namespace Aw.Sindri.Core.Launches;

/// <summary>What <see cref="ILaunchSpend.Peek"/> knows now: answered without waiting and without a token.</summary>
/// <param name="Landed">False while the day's first read is in flight; true once the day's value is known, or when the agent has no usage reader.</param>
/// <param name="Spend">The spend when landed, or null when the agent has no usage reader.</param>
public readonly record struct SpendPeek(bool Landed, AgentSpend? Spend);
