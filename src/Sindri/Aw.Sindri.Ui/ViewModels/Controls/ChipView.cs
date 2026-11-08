using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// The one chip every state word is drawn as (fixture.md "State chips", rev 8): the API serves the chip's word and its tone
/// (API §3.8), so the app maps the tone to a look and shows the word exactly as served. It never maps a word to a tone of
/// its own, and a stage name (a recipe's state, shown as plain text) is never a chip.
/// </summary>
/// <param name="Word">The word, verbatim.</param>
/// <param name="Tone">The tone, as served.</param>
internal sealed record ChipView(string Word, ChipTone Tone)
{
    /// <summary>The style class that colours the tone, or null for a neutral chip (the base chip class alone).</summary>
    public string? ToneClass => Tone switch
    {
        ChipTone.Attn => "attn",
        ChipTone.Fail => "fail",
        ChipTone.Ok => "ok",
        ChipTone.Neutral => null,
        _ => null,
    };

    /// <summary>The chip to draw: the served one when there is one, else the fallback a view draws for itself, else none.</summary>
    /// <param name="served">The chip the API served, or null.</param>
    /// <param name="fallback">A chip the client draws itself (the lines that are not yet served), or null.</param>
    /// <returns>The chip, or null when there is nothing to draw.</returns>
    internal static ChipView? Resolve(StateChip? served, StateChip? fallback = null) =>
        (served ?? fallback) is { } chip ? new ChipView(chip.Word, chip.Tone) : null;
}
