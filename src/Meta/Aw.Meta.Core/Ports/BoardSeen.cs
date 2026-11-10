namespace Aw.Meta.Core.Ports;

/// <summary>What a lane was last shown of the board.</summary>
/// <param name="BoardVersion">The board version (<see cref="BoardFacts.Version"/>); 0 when no move of main was recorded yet.</param>
/// <param name="LaneMapVersion">The lane map version.</param>
public sealed record BoardSeen(int BoardVersion, int LaneMapVersion);
