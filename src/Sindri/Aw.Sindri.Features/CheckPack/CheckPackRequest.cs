namespace Aw.Sindri.Features.CheckPack;

/// <summary>Check the lens pack in a directory (line spec §5.3).</summary>
/// <param name="Directory">The pack's directory; its last segment is the pack's id.</param>
public sealed record CheckPackRequest(string Directory);
