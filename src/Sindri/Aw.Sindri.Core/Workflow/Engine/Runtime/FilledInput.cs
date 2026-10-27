using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What the 'FillInput' activity returns, and so what Temporal records: metadata only, no text (plan decision 2).</summary>
/// <param name="Input">The input block.</param>
/// <param name="Index">Its 1-based place in the Step's 'inputs:' list.</param>
/// <param name="Sections">Its sections in ordinal order of name.</param>
/// <param name="Result">The size and digest of its typed result; null when it returned none.</param>
public sealed record FilledInput(BlockId Input, int Index, IReadOnlyList<InjectionEntry> Sections, InjectionResultEntry? Result = null)
{
    /// <summary>How long the fill took in milliseconds; null on a result recorded before the duration was kept. Not written when null.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? DurationMs { get; init; }
}
