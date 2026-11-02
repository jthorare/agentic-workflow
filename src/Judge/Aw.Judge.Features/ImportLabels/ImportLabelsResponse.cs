namespace Aw.Judge.Features.ImportLabels;

/// <summary>The ImportLabels response.</summary>
/// <param name="Imported">How many new eval items were added.</param>
/// <param name="SkippedUnparsable">How many decisions had a stored input that isn't JSON.</param>
public sealed record ImportLabelsResponse(int Imported, int SkippedUnparsable);
