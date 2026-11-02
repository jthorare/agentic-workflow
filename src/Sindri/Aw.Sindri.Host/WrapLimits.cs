using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host;

/// <summary>
/// The limits file the launcher writes beside the plan and 'session wrap --limits' reads (sandbox spec §3.3; SB-3a). A sandboxed agent must never run
/// unwatched, so a file that cannot be read or names no unit refuses the start.
/// </summary>
/// <param name="Unit">The scope unit.</param>
/// <param name="WallGraceSeconds">The grace inside the wall.</param>
/// <param name="ParkMaxSeconds">The longest single suspension the ceiling allows for.</param>
/// <param name="MemoryMaxBytes">The memory kill line, for the limit report.</param>
/// <param name="WallSeconds">The unsuspended run time allowed, grace included; absent for an attended session.</param>
internal sealed record WrapLimits(
    string Unit,
    long WallGraceSeconds,
    long ParkMaxSeconds,
    long MemoryMaxBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? WallSeconds = null)
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    /// <summary>Reads a limits file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The limits, or SND-REQ-001 when the file is unreadable, malformed or has no unit.</returns>
    internal static Result<WrapLimits> Read(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.RequestInvalid.With($"the limits file {path} cannot be read: {e.Message}");
        }

        var read = AwJson.Deserialize<WrapLimits>(bytes, _json);
        if (!read.IsSuccess)
        {
            return SindriErrors.RequestInvalid.With($"the limits file {path} is not valid: {read.Error.Message}");
        }

        return string.IsNullOrWhiteSpace(read.Value.Unit)
            ? SindriErrors.RequestInvalid.With($"the limits file {path} names no unit")
            : read.Value;
    }
}
