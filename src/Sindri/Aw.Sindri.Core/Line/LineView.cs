using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>
/// The line board's read model (line spec LN-API1): the packet chain, oldest station first, then one row per phase of the plan in plan order, and the two limits the dispatcher
/// holds the line to. A purged run area shows its frozen packets only (LC-8).
/// </summary>
/// <param name="Stations">Scoping, design and plan, in that order.</param>
/// <param name="Phases">The phase board's rows, in plan order.</param>
/// <param name="Parallel">At most this many phases run at once (<c language="text">workflow.line.parallel</c>).</param>
/// <param name="MaxOpenPrs">At most this many phases are shipped and unmerged (<c language="text">workflow.line.maxOpenPrs</c>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineView(IReadOnlyList<LineStation> Stations, IReadOnlyList<LinePhaseView> Phases, int Parallel, int MaxOpenPrs);
