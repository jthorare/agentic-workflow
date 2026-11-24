namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Why the function names were not searched in full, and whether the Code index card on the System page can fix it (#4184).</summary>
/// <param name="Words">The reason's words.</param>
/// <param name="CardFixes">True when the coverage sentence ends with "See Code index on the System page."; false for a reason that card can't fix.</param>
internal sealed record NamesNote(string Words, bool CardFixes = true);
