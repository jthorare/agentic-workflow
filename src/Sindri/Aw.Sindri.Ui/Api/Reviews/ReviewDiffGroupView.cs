namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>A group of files (§7.1 groups[]): its name and whether it starts folded.</summary>
internal sealed record ReviewDiffGroupView(string Name, bool Folded, IReadOnlyList<ReviewDiffFileView> Files);
