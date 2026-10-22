namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>How long a person took, by what they answered.</summary>
/// <param name="ParksAnswered">Park to its answer.</param>
/// <param name="EscalationsDecided">Escalation to its decision.</param>
/// <param name="Takeovers">Takeover to handback.</param>
public sealed record StatsHumanTime(HumanTimeFigure ParksAnswered, HumanTimeFigure EscalationsDecided, HumanTimeFigure Takeovers);
