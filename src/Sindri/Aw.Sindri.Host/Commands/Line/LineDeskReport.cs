using System.Text.Json.Serialization;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>The report 'line desk' writes to '&lt;out&gt;/report.json': what a desk 'complete' takes (--outcome and one --output fact:&lt;key&gt;=&lt;file&gt; for each entry of Outputs).</summary>
/// <param name="Block">The full Step id.</param>
/// <param name="Outcome">The outcome.</param>
/// <param name="Outputs">Each fact key ('fact:findings') to the file in the out directory that holds it.</param>
/// <param name="Files">The other files written, relative to the out directory.</param>
/// <param name="Findings">The failing findings.</param>
/// <param name="Warnings">The warnings.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LineDeskReport(
    string Block,
    string Outcome,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<string> Files,
    IReadOnlyList<LineFinding> Findings,
    IReadOnlyList<LineFinding> Warnings);
