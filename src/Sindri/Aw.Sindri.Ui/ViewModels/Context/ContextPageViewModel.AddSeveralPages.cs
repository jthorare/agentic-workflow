using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The Add several pages sheet (<see cref="ContextPageViewModel"/>, continued, pages spec §3.6): opened from either Pages and actions section's head, shown in the shell's one sheet, and a saved list reads the section again.</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>Gets the open Add several pages sheet, or null.</summary>
    public AddSeveralPagesViewModel? AddSeveralPages { get; private set; }

    /// <summary>Gets a value indicating whether the Add several pages sheet shows.</summary>
    public bool ShowAddSeveralPages => AddSeveralPages is not null;

    /// <summary>Raised when the sheet opens or goes: the shell mounts it in its one sheet, or lets it go.</summary>
    internal event EventHandler? AddSeveralPagesChanged;

    /// <summary>Opens the Add several pages sheet; a saved list reads the section it was opened from again.</summary>
    /// <param name="noConcept">True when it was opened from the pages in no concept, false from a concept's Why drill.</param>
    internal void OpenAddSeveralPages(bool noConcept)
    {
        if (Ports.Pages is not { } writes)
        {
            return;
        }

        var sheet = new AddSeveralPagesViewModel(writes, repo: null);
        sheet.Saved += (_, _) => _ = noConcept ? OpenNoConcept() : ReadWhole(CancellationToken.None);
        sheet.Closed += (_, _) =>
        {
            if (ReferenceEquals(AddSeveralPages, sheet))
            {
                AddSeveralPages = null;
                ChangedAddSeveralPages();
            }
        };
        AddSeveralPages = sheet;
        ChangedAddSeveralPages();
    }

    private void ChangedAddSeveralPages()
    {
        OnChanged(nameof(AddSeveralPages));
        OnChanged(nameof(ShowAddSeveralPages));
        AddSeveralPagesChanged?.Invoke(this, EventArgs.Empty);
    }
}
