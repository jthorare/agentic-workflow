using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The checks a Step's outputs must pass wherever they come in: reported by the agent with 'complete', or read back from its output folder when its session ended with no report
/// (issue 1468). One list of refusals, in the order the agent reads them; empty when every output holds.
/// </summary>
public interface IReportOutputChecks
{
    /// <summary>Why the outputs break the checks.</summary>
    /// <param name="outputs">Each output fact's content.</param>
    /// <param name="payloads">The payload facts the recipe declares: each must be a JSON object or array.</param>
    /// <param name="scopingQuestions">Whether the writing Step is the scoping questions Step, the only one held to the question shape.</param>
    /// <returns>The refusals, naming the fact and never its text.</returns>
    public IReadOnlyList<string> Violations(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs, IReadOnlySet<FactKey> payloads, bool scopingQuestions);
}
