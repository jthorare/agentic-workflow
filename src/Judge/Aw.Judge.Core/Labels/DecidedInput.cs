using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Labels;

/// <summary>A decided decision with the input it was made on: what 'label import' turns into an eval item.</summary>
/// <param name="Id">The decision.</param>
/// <param name="Question">The question.</param>
/// <param name="Answer">What was decided.</param>
/// <param name="InputJson">The decision's stored input.</param>
public sealed record DecidedInput(DecisionId Id, string Question, string? Answer, string InputJson);
