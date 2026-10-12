namespace Aw.Context.Testing.Fixtures;

/// <summary>A direct commit on 'main' (the initial scaffold).</summary>
/// <param name="Dir">The resource folder holding the files it writes, e.g. 'app/00-initial'.</param>
/// <param name="Message">The commit message.</param>
/// <param name="At">The author and committer date.</param>
public sealed record FixtureCommit(string Dir, string Message, DateTimeOffset At);
