namespace Aw.Sindri.Core.Checks;

/// <summary>A commit on the branch.</summary>
/// <param name="Sha">The full commit id.</param>
/// <param name="Subject">The first line of its message.</param>
/// <param name="IsMerge">True when it has more than one parent.</param>
public sealed record ChangedCommit(string Sha, string Subject, bool IsMerge);
