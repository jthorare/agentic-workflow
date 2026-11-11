using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The report check with no rule. The recipe engine that declared payload facts and held the scoping questions to a shape is gone, so an output a recovered session left
/// is accepted as written; the graph engine's agent block (issue 2258 PR 7) checks reports against the node's declared outputs.
/// </summary>
internal sealed class NoReportOutputChecks : IReportOutputChecks
{
    /// <inheritdoc />
    public IReadOnlyList<string> Violations(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs, IReadOnlySet<FactKey> payloads, bool scopingQuestions) => [];
}
