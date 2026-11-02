using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>Every check a Step's outputs pass: <see cref="PayloadReportChecks"/> and the scoping facts' schemas (#1112: a field the schema forbids is the writer's to fix, not a new drafter's). Used by the report and by the recovery of an ended session's outputs (issue 1468).</summary>
internal sealed class ReportOutputChecker : IReportOutputChecks
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Violations(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs, IReadOnlySet<FactKey> payloads, bool scopingQuestions)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        var wrong = PayloadReportChecks.Instance.Violations(outputs, payloads, scopingQuestions).ToList();
        wrong.AddRange(outputs.Where(static o => ReportFactSchemas.Covers(o.Key)).OrderBy(static o => o.Key.Value, StringComparer.Ordinal)
            .SelectMany(o => ReportFactSchemas.Violations(o.Key, o.Value.Span, scopingQuestions)));
        return wrong;
    }
}
