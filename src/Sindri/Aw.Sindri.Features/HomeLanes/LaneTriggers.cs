using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>The three lane triggers of Home spec §3.2, as pure rules over a <see cref="LaneSnapshot"/>.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub of #3733: only the tests use it until the fix commit registers it.")]
internal static class LaneTriggers
{
    /// <summary>The rows that hold now. No lane name is special: the meta lane counts like any other.</summary>
    /// <param name="snapshot">What aw-meta said.</param>
    /// <returns>One row per recycling lane, per limited subject and per queued PR.</returns>
    public static IReadOnlyList<LaneRow> Rows(LaneSnapshot snapshot) => throw new NotSupportedException();

    /// <summary>The row id: the kind's wire name, a colon and the key ('lane-recycle:api').</summary>
    /// <param name="kind">One of the three lane kinds.</param>
    /// <param name="key">The row's key.</param>
    /// <returns>The id.</returns>
    public static NeedsYouRowId Id(NeedsYouKind kind, string key) => throw new NotSupportedException();
}
