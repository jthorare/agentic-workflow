namespace Aw.Sindri.Core.Sandbox;

/// <summary>What the ledger says about a repo's network learn mode (sandbox spec §3.6, owner decision Q13): the window is a repo's first ten runs or seven days.</summary>
/// <param name="FirstLearnRun">When the repo's first run in learn mode started, or null when it has had none.</param>
/// <param name="LearnRuns">How many of the repo's runs started in learn mode.</param>
/// <param name="EndedAt">When the owner ended learn mode early ('sindri sandbox learn off'), or null.</param>
public sealed record LearnHistory(DateTimeOffset? FirstLearnRun, int LearnRuns, DateTimeOffset? EndedAt);
