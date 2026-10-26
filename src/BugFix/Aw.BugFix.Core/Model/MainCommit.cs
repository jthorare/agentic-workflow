namespace Aw.BugFix.Core.Model;

/// <summary>A commit on main, as the judge's diff looks for a fix main already had.</summary>
/// <param name="Sha">The commit.</param>
/// <param name="Parent">Its only parent (a commit without one is left out).</param>
/// <param name="Message">The full commit message.</param>
public sealed record MainCommit(string Sha, string Parent, string Message);
