namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The diff (§6.2, §6.3): its state and words, the file count and the groups.</summary>
/// <param name="State">ready, empty or error.</param>
/// <param name="Text">The empty or error words; null when ready.</param>
/// <param name="FileCount">How many files; 3,000 at GitHub's cap.</param>
/// <param name="Capped">True at GitHub's 3,000-file cap.</param>
/// <param name="CappedText">"GitHub lists the first 3,000 files; open the rest on GitHub"; null when not capped.</param>
/// <param name="Groups">The file groups, folded or not.</param>
internal sealed record ReviewDiffView(string State, string? Text, int FileCount, bool Capped, IReadOnlyList<ReviewDiffGroupView> Groups, string? CappedText = null);
