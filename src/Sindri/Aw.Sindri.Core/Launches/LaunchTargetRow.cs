using Aw.Sindri.Core.Launch;
namespace Aw.Sindri.Core.Launches;

/// <summary>One target of 'GET /launch/targets': the probe's facts and the target's budget.</summary>
/// <param name="Facts">What 'launch.probed' recorded for it.</param>
/// <param name="Budget">Its daily budget and today's spend.</param>
public sealed record LaunchTargetRow(TargetFacts Facts, BudgetFacts Budget);
