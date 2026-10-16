using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.DbRun;

/// <summary>Runs a database script: its queries against a read-only database, and writes the run's summary.json.</summary>
/// <param name="ScriptPath">The script file (absolute).</param>
/// <param name="RunDirectory">The run directory (absolute); its name is the run id.</param>
/// <param name="At">The side the caller is at; recorded only.</param>
/// <param name="LockTimeout">How long to wait for each machine lock.</param>
/// <param name="Repo">The repository under test (absolute): where SQLite files and the targets file are looked for.</param>
/// <param name="AppBuild">The app build (commit) under test, when verified.</param>
/// <param name="ConfigPath">A targets file (absolute) used instead of the repo's, when given.</param>
public sealed record DbRunRequest(
    string ScriptPath,
    string RunDirectory,
    DataRunAt At,
    TimeSpan LockTimeout,
    string Repo,
    string? AppBuild = null,
    string? ConfigPath = null);
