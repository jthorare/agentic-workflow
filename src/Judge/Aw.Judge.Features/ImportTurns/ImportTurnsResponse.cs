namespace Aw.Judge.Features.ImportTurns;

/// <summary>The ImportTurns response.</summary>
/// <param name="Imported">How many new 'turn-progress' items were added.</param>
/// <param name="Skipped">How many turns had no edits or were already imported.</param>
/// <param name="Unreadable">How many files could not be read.</param>
public sealed record ImportTurnsResponse(int Imported, int Skipped, int Unreadable);
