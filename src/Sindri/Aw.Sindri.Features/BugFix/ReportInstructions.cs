using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.report.write' is told (#1682): the resolution report's three separate lines.</summary>
internal sealed class ReportInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.report.write");

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException();
}
