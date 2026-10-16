namespace Aw.Evidence.Core.Data;

/// <summary>A SQLite file. The adapter reads it into a copy under <paramref name="WorkDirectory"/> and queries the copy, never the source.</summary>
/// <param name="SourcePath">The source file (absolute, already vetted to be inside the repo or the run directory).</param>
/// <param name="WorkDirectory">A directory (absolute) the copy is made in; created when missing.</param>
/// <param name="SeedSql">SQL run against the copy, over a separate read-write connection that is closed before the read-only one opens; never against the source.</param>
public sealed record SqliteTarget(string SourcePath, string WorkDirectory, string? SeedSql = null) : DbTarget;
