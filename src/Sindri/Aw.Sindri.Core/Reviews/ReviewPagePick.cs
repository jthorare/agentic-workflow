namespace Aw.Sindri.Core.Reviews;

/// <summary>The settings two project entries offer for one repo (§7.2).</summary>
/// <param name="Choices">The settings names, in profile order.</param>
public sealed record ReviewPagePick(
    IReadOnlyList<string> Choices);
