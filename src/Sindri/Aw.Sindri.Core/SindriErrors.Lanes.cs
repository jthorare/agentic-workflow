using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-LANES-001: aw-meta is missing, timed out, failed or printed JSON that does not parse, so Home has no lane rows (degraded code lanes-unavailable).</summary>
    public static readonly ErrorDefinition LanesUnavailable = new("SND-LANES-001", ErrorKind.Unavailable, "aw-meta did not answer, so there are no lane rows");
}
