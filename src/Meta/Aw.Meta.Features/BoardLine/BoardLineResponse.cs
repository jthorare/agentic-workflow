namespace Aw.Meta.Features.BoardLine;

/// <summary>The BoardLine response.</summary>
/// <param name="Line">The one-line board (latest merge and lane map version), or null when nothing changed since the lane last saw it.</param>
public sealed record BoardLineResponse(string? Line);
