namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>
/// The heights the run canvas's cards are drawn at. Spec 3 §2 measures each card in a first pass; the cards here are fixed-height (a
/// header, a strip of fixed-height rows, a footer) so the layout is deterministic without a measuring pass.
/// </summary>
internal static class RunCardSizes
{
    /// <summary>The header: icon tile, title and sub line, padded.</summary>
    internal const double Header = 64;

    /// <summary>One step row.</summary>
    internal const double StepRow = 32;

    /// <summary>The strip's padding, top and bottom together.</summary>
    internal const double StripPadding = 20;

    /// <summary>The footer.</summary>
    internal const double Footer = 32;

    /// <summary>The height of the step strip of a stage with <paramref name="steps"/> steps.</summary>
    /// <param name="steps">How many steps.</param>
    /// <returns>The height, px.</returns>
    internal static double Strip(int steps) => steps <= 0 ? 0 : StripPadding + (steps * StepRow);

    /// <summary>The height of a card.</summary>
    /// <param name="node">The card's node.</param>
    /// <returns>The height, px.</returns>
    internal static double Of(RunNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Header + (node.Kind == RunNodeKind.Stage ? Strip(node.Steps.Count) : 0) + Footer;
    }
}
