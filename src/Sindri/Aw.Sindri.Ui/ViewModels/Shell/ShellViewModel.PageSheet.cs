using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The page sheet in the shell's one sheet (<see cref="ShellViewModel"/>, continued, pages spec §3.6): the Context page asks, the sheet shows here, and it goes when it is saved or closed.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Gets the page sheet the Context page opened, or null.</summary>
    public PageSheetViewModel? ActivePageSheet { get; private set; }

    /// <summary>Gets a value indicating whether the sheet shows the page sheet.</summary>
    public bool IsPageSheet => SheetLink?.Kind == DeepLinkKind.PageSheet;

    private void WirePageSheet() => Context.PageSheetChanged += (_, _) => OnPageSheetChanged();

    private void OnPageSheetOpened() => Changed(nameof(IsPageSheet), nameof(ActivePageSheet));

    // The sheet is ready: it opens in the shell's sheet. It is gone (saved or discarded): the sheet closes with no edge tab, since its page is done.
    private void OnPageSheetChanged()
    {
        ActivePageSheet = Context.PageSheet;
        if (ActivePageSheet is not null)
        {
            OpenSheet(DeepLink.PageSheet);
        }
        else if (IsPageSheet)
        {
            CloseSheet();
            SheetLink = null;
            Changed(nameof(SheetLink), nameof(SheetSubject), nameof(HasEdgeTab), nameof(IsEdgeTabShown), nameof(IsPageSheet), nameof(SheetTitle), nameof(SheetName), nameof(EdgeTabName), nameof(EdgeTabText));
        }

        Changed(nameof(ActivePageSheet));
    }
}
