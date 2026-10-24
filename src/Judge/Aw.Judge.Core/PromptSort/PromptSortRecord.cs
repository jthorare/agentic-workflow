using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.PromptSort;

/// <summary>What the sorter keeps about one prompt, besides its 'prompt-sort' decision row: the axes and the run.</summary>
/// <param name="Decision">The prompt-sort decision these rows belong to.</param>
/// <param name="Axes">The sorted axes.</param>
/// <param name="Run">How the sort ran.</param>
public sealed record PromptSortRecord(DecisionId Decision, IReadOnlyList<SortedAxis> Axes, SortRun Run);
