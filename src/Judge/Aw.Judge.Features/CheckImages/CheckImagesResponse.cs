namespace Aw.Judge.Features.CheckImages;

/// <summary>The CheckImages response.</summary>
/// <param name="Lines">What the command prints, one line each.</param>
/// <param name="Held">Whether the bar held: no broken page judged 'looks-right' by Jev.</param>
public sealed record CheckImagesResponse(IReadOnlyList<string> Lines, bool Held);
