namespace Aw.Judge.Features.MapBriefByName;

/// <summary>The MapBriefByName request ('judge brief map-by-name &lt;name&gt;').</summary>
/// <param name="DispatchName">The teammate name the brief was dispatched under.</param>
public sealed record MapBriefByNameRequest(string DispatchName);
