namespace Aw.Sindri.Core.Reviews;

/// <summary>Another PR that claims an item.</summary>
/// <param name="Number">Its number.</param>
/// <param name="Merged">Whether it merged.</param>
/// <param name="Text">claimed by #2060, merged.</param>
public sealed record ReviewPageClaimedBy(
    int Number,
    bool Merged,
    string Text);
