using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Skills;

/// <summary>A skill block's finished result as the watch stored it (skill flows spec §3.3): the workflow completes the run from it as 'agent:result'.</summary>
/// <param name="Outcome">The mapped outcome name.</param>
/// <param name="Outputs">The stored outputs, by fact key.</param>
/// <param name="ReportSha256">The digest of the report: the outcome, each output's sha256 and the result file's sha256.</param>
/// <param name="ResultFileSha256">The SHA-256 of the result file; the 'workflow.transition' carries it as 'resultSha256'; ReportSha256 is a separate digest over the outcome, this and the outputs.</param>
public sealed record SkillFinished(string Outcome, IReadOnlyDictionary<FactKey, FactValue> Outputs, string ReportSha256, string ResultFileSha256);
