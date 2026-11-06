namespace Aw.Context.Core.Linear;

/// <summary>What changed for the viewer since a marker: issues assigned to them, state moves on their issues that came from a sync, and comments that mention them.</summary>
/// <param name="Changes">The changes, oldest first.</param>
/// <param name="Mentions">The mentions, oldest first.</param>
public sealed record LinearSince(IReadOnlyList<LinearSinceChange> Changes, IReadOnlyList<LinearSinceMention> Mentions);
