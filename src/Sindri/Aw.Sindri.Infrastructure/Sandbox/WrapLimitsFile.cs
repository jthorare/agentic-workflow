using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The limits file the launcher writes into the run directory for 'sindri session wrap --limits' (#1553). Same wire as the host's WrapLimits, which reads it:
/// without it the limit watchdog never runs and a memory or wall kill ends the session as 'exit', not 'limit'.
/// </summary>
/// <param name="Unit">The scope unit.</param>
/// <param name="WallGraceSeconds">The grace inside the wall.</param>
/// <param name="ParkMaxSeconds">The longest single suspension the ceiling allows for.</param>
/// <param name="MemoryMaxBytes">The memory kill line.</param>
/// <param name="WallSeconds">The unsuspended run time allowed, grace included; absent for an attended session.</param>
internal sealed record WrapLimitsFile(
    string Unit,
    long WallGraceSeconds,
    long ParkMaxSeconds,
    long MemoryMaxBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? WallSeconds = null);
