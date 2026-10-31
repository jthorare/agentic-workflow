using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Pin sheet in the shell's one sheet (<see cref="ShellViewModel"/>, continued, concept pins spec §3.6): the concept layer asks, the sheet shows here, and it goes when it is saved or closed.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Gets the Pin sheet the concept layer opened, or null.</summary>
    public PinSheetViewModel? ActivePinSheet { get; private set; }

    /// <summary>Gets what the Pin sheet's slot shows: the Pin sheet, or Add several's sheet; null when neither.</summary>
    public object? PinSheetContent { get; private set; }

    /// <summary>Gets a value indicating whether the sheet shows the Pin sheet.</summary>
    public bool IsPinSheet => SheetLink?.Kind == DeepLinkKind.PinSheet;

    /// <summary>The Pin sheet went away: when a write went through, the concept layer reads its pins again and puts focus back on the card's button.</summary>
    internal event EventHandler<PinSheetClosedEventArgs>? PinSheetClosed;

    /// <summary>Opens the Pin sheet for Pin this, Edit pin or New pin.</summary>
    /// <param name="open">What the concept layer hands over.</param>
    internal void OpenPinSheet(PinSheetOpen open)
    {
        ArgumentNullException.ThrowIfNull(open);
        var sheet = new PinSheetViewModel(new PinSheetPorts(_api, new ProfileWriteFlow(_api, Health.ApiUrl, _clock), Health.ApiUrl, _clock));
        sheet.Closed += OnPinSheetClosed;
        (ActivePinSheet, PinSheetContent) = (sheet, sheet);
        OpenSheet(DeepLink.PinSheet);
        Changed(nameof(ActivePinSheet), nameof(PinSheetContent));
        _ = sheet.Open(open, CancellationToken.None);
    }

    private void OnPinSheetOpened() => Changed(nameof(IsPinSheet), nameof(SheetTitleIsMono));

    // Saved, discarded or closed: the sheet closes with no edge tab, since its pin is done, and the layer is told.
    private void OnPinSheetClosed(object? sender, PinSheetClosedEventArgs e)
    {
        if (!ReferenceEquals(sender, ActivePinSheet))
        {
            return;
        }

        (ActivePinSheet, PinSheetContent) = (null, null);
        if (IsPinSheet)
        {
            CloseSheet();
            SheetLink = null;
            Changed(nameof(SheetLink), nameof(SheetSubject), nameof(HasEdgeTab), nameof(IsEdgeTabShown), nameof(IsPinSheet), nameof(SheetTitleIsMono), nameof(SheetTitle), nameof(SheetName), nameof(EdgeTabName), nameof(EdgeTabText));
        }

        Changed(nameof(ActivePinSheet), nameof(PinSheetContent));
        PinSheetClosed?.Invoke(this, e);
    }
}
