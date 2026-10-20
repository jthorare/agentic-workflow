using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>What a line station's block Step produced at the desk: its outcome, the facts it writes, files for the run area, and its findings.</summary>
/// <param name="Block">The full Step id.</param>
/// <param name="Outcome">The outcome the desk 'complete' reports: pass or gaps (check), complete or incomplete (evidence).</param>
/// <param name="Outputs">The facts written, as JSON text by key without the 'fact:' prefix.</param>
/// <param name="Files">Files for the run area, paths relative to it: the evidence Step's packet.</param>
/// <param name="Findings">The failing findings, when the Step reports any.</param>
/// <param name="Warnings">Warnings shown beside the report.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineDeskResponse(
    string Block,
    string Outcome,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<LineFile> Files,
    IReadOnlyList<LineFinding> Findings,
    IReadOnlyList<LineFinding> Warnings);
