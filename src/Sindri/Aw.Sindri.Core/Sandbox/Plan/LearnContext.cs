namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What the planner needs to apply the learn window: the repo's learn history and the launch time.</summary>
/// <param name="History">The ledger's learn history for the repo.</param>
/// <param name="Now">The launch time.</param>
public sealed record LearnContext(LearnHistory History, DateTimeOffset Now);
