using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The diff (§6.2, §6.3).</summary>
/// <param name="State">ready, empty or error.</param>
/// <param name="Text">The empty or error words.</param>
/// <param name="FileCount">How many files; 3,000 at GitHub's cap.</param>
/// <param name="Capped">True at GitHub's 3,000-file cap.</param>
/// <param name="CappedText">The cap's words; absent when not capped.</param>
/// <param name="Groups">The file groups.</param>
public sealed record ReviewPageDiff(
    string State,
    string? Text,
    int FileCount,
    bool Capped,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CappedText,
    IReadOnlyList<ReviewPageGroup> Groups);
