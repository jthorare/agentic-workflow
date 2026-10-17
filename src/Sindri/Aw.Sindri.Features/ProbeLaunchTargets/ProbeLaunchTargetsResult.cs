namespace Aw.Sindri.Features.ProbeLaunchTargets;

/// <summary>What a probe found, small by design: counts, never the facts (those are in 'launch.probed').</summary>
/// <param name="Changed">True when 'launch.probed' was appended because the facts differ from the last probe's.</param>
/// <param name="Targets">The catalog targets checked.</param>
/// <param name="Present">How many of them have their CLI on this box.</param>
public sealed record ProbeLaunchTargetsResult(bool Changed, int Targets, int Present);
