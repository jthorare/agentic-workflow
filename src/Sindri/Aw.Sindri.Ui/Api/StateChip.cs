namespace Aw.Sindri.Ui.Api;

/// <summary>The chip the API serves: its word, verbatim, and its tone.</summary>
/// <param name="Word">Word, as served.</param>
/// <param name="Tone">Tone, as served.</param>
internal sealed record StateChip(
    string Word,
    ChipTone Tone);
