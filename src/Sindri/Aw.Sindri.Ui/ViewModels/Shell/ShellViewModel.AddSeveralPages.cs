using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Add several pages sheet in the shell's one sheet (<see cref="ShellViewModel"/>, continued, pages spec §3.6): the Context page asks, the sheet shows here, and it goes when it is saved or closed.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Gets the Add several pages sheet the Context page opened, or null.</summary>
    public AddSeveralPagesViewModel? ActiveAddSeveralPages { get; private set; }

    /// <summary>Gets a value indicating whether the sheet shows the Add several pages sheet.</summary>
    public bool IsAddSeveralPages => SheetLink?.Kind == DeepLinkKind.AddSeveralPages;

    private void WireAddSeveralPages() => Context.AddSeveralPagesChanged += (_, _) => OnAddSeveralPagesChanged();

    private void OnAddSeveralPagesOpened() => Changed(nameof(IsAddSeveralPages), nameof(ActiveAddSeveralPages));

    // The sheet is open: it shows in the shell's sheet. It is gone (saved or discarded): the sheet closes with no edge tab, since its work is done.
    private void OnAddSeveralPagesChanged()
    {
        ActiveAddSeveralPages = Context.AddSeveralPages;
        if (ActiveAddSeveralPages is not null)
        {
            OpenSheet(DeepLink.AddSeveralPages);
        }
        else if (IsAddSeveralPages)
        {
            CloseSheet();
            SheetLink = null;
            Changed(nameof(SheetLink), nameof(SheetSubject), nameof(HasEdgeTab), nameof(IsEdgeTabShown), nameof(IsAddSeveralPages), nameof(SheetTitle), nameof(SheetName), nameof(EdgeTabName), nameof(EdgeTabText));
        }

        Changed(nameof(ActiveAddSeveralPages));
    }
}
