using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>Reads the JSON of 'aw-meta status', 'lanes-context' and 'usage' into a <see cref="LaneSnapshot"/>. Keys match case-insensitively; a key it does not know is ignored.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub of #3733: only the tests use it until the fix commit registers it.")]
internal static class LaneParser
{
    /// <summary>Parses the three outputs.</summary>
    /// <param name="status">'aw-meta status --json' (mergeQueue[]).</param>
    /// <param name="context">'aw-meta lanes-context --json' (lanes[]).</param>
    /// <param name="usage">'aw-meta usage --json' (subjects[]).</param>
    /// <returns>The snapshot, or SND-LANES-001 when any output is not the JSON object it should be.</returns>
    public static Result<LaneSnapshot> Parse(string status, string context, string usage) => throw new NotSupportedException();
}
