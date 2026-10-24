using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.Parity;

/// <summary>Measures the implementation against design frames in a real browser and writes the run's parity.json.</summary>
/// <param name="ManifestPath">The design manifest (absolute); relative design PNG paths resolve against its directory.</param>
/// <param name="RunDirectory">The run directory (absolute); its name is the run id.</param>
/// <param name="LockTimeout">How long to wait for each machine lock.</param>
/// <param name="Host">The --host origin; null drives http://localhost:3000.</param>
/// <param name="AllowPreviewHost">Whether a pr-&lt;n&gt; preview host is allowed.</param>
/// <param name="AllowWrites">Whether write-looking frame steps may run (parity is read-only on every host otherwise).</param>
/// <param name="AppBuild">The app build (commit) under test, when verified.</param>
/// <param name="DbProvenance">The local database's provenance, when known (ignored for preview hosts).</param>
/// <param name="At">The side of the change the caller is at (--at base|head); recorded only.</param>
public sealed record ParityRequest(
    string ManifestPath,
    string RunDirectory,
    TimeSpan LockTimeout,
    string? Host = null,
    bool AllowPreviewHost = false,
    bool AllowWrites = false,
    string? AppBuild = null,
    DbProvenance? DbProvenance = null,
    DataRunAt? At = null);
