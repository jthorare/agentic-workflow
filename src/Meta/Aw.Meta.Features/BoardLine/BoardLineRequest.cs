using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.BoardLine;

/// <summary>The BoardLine request: a lane's prompt hook asks whether the team board changed since the lane last saw it.</summary>
/// <param name="Identity">The lane, from its environment.</param>
public sealed record BoardLineRequest(LaneIdentity Identity);
