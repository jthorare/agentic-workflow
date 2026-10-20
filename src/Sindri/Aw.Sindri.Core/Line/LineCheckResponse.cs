using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A line station's check result (line spec §4.3, §6.3): the outcome, the failing findings, the warnings beside them and every rule's status.</summary>
/// <param name="Station">'design' or 'plan'.</param>
/// <param name="Stage">The stage that ran.</param>
/// <param name="Outcome">Pass or gaps at the draft stage, complete or incomplete at evidence.</param>
/// <param name="Findings">The failing findings only: warnings never reach fact:findings.</param>
/// <param name="Warnings">The warnings, for the stop and the CLI.</param>
/// <param name="Rules">Each rule of the station with its live status.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineCheckResponse(
    string Station,
    LineStage Stage,
    LineOutcome Outcome,
    IReadOnlyList<LineFinding> Findings,
    IReadOnlyList<LineFinding> Warnings,
    IReadOnlyList<LineRuleResult> Rules)
{
    /// <summary>Whether the run is clean: exit 0.</summary>
    [JsonIgnore]
    public bool Passed => Outcome is LineOutcome.Pass or LineOutcome.Complete;
}
