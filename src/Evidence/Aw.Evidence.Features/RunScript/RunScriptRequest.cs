using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.RunScript;

/// <summary>Runs a UI script in a real browser and writes the run's summary.json.</summary>
/// <param name="ScriptPath">The script file (absolute).</param>
/// <param name="RunDirectory">The run directory (absolute); its name is the run id.</param>
/// <param name="LockTimeout">How long to wait for each machine lock.</param>
/// <param name="Baseline">The approved baseline screenshot (absolute), when there is one.</param>
/// <param name="AppBuild">The app build (commit) under test, when verified.</param>
/// <param name="Fixtures">The fixture or seed identity, when known.</param>
/// <param name="CacheManifest">The verdict cache file (absolute); null uses &lt;run-dir&gt;/verdict-cache.json.</param>
/// <param name="Host">The --host origin; null drives http://localhost:3000.</param>
/// <param name="AllowPreviewHost">Whether a pr-&lt;n&gt; preview host is allowed.</param>
/// <param name="AllowWrites">Whether write-looking steps may run on a preview host.</param>
/// <param name="At">The side of the change the caller is at (--at base|head); recorded only.</param>
public sealed record RunScriptRequest(
    string ScriptPath,
    string RunDirectory,
    TimeSpan LockTimeout,
    string? Baseline = null,
    string? AppBuild = null,
    string? Fixtures = null,
    string? CacheManifest = null,
    string? Host = null,
    bool AllowPreviewHost = false,
    bool AllowWrites = false,
    DataRunAt? At = null);
