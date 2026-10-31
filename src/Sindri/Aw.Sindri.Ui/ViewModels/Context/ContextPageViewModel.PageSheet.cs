using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The page sheet (<see cref="ContextPageViewModel"/>, continued, pages spec §3.6): Name this page, Edit page and New page from either Pages and actions section open in a right sheet
/// in the shell's one sheet; a saved page reads the section again.
/// </summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>Gets the open sheet, or null.</summary>
    public PageSheetViewModel? PageSheet { get; private set; }

    /// <summary>Gets a value indicating whether the sheet shows.</summary>
    public bool ShowPageSheet => PageSheet is not null;

    /// <summary>Raised when the sheet opens or goes: the shell mounts it in its one sheet, or lets it go.</summary>
    internal event EventHandler? PageSheetChanged;

    // Called once the page's parts exist: both sections write through the same port and open the sheet from their buttons.
    private void InitPageSheet()
    {
        Whole.Pages.Writes = Ports.Pages;
        NoConceptPages.Writes = Ports.Pages;
        Whole.Pages.SheetRequested += (_, e) => OpenPageSheet(Whole.Pages, e.Value, concept: OpenedConceptId, () => ReadWhole(CancellationToken.None));
        NoConceptPages.SheetRequested += (_, e) => OpenPageSheet(NoConceptPages, e.Value, concept: null, () => OpenNoConcept());
    }

    // The sheet reads its page and the profile, fills, and goes away when it is saved or closed; a saved page makes the section read again.
    private void OpenPageSheet(PagesSectionViewModel section, string? key, string? concept, Func<Task> reread)
    {
        if (Ports.Pages is not { } writes)
        {
            return;
        }

        var sheet = new PageSheetViewModel(writes, known: [.. Concepts.Tiles.Select(static t => new ConceptChoice(Id: t.Id, Name: t.Name))], otherNames: section.NamesExcept(key), openConcept: concept);
        sheet.Saved += (_, _) =>
        {
            if (key is not null && sheet.SavedId is { } id)
            {
                section.Remember(key, id);
            }

            _ = reread();
        };
        Show(sheet);
        _ = key is null ? sheet.OpenNew(CancellationToken.None) : sheet.OpenPage(key, CancellationToken.None);
    }

    /// <summary>Puts a sheet on show: the shell mounts it, and it goes when it closes.</summary>
    /// <param name="sheet">The sheet.</param>
    internal void Show(PageSheetViewModel sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.Closed += (_, _) =>
        {
            if (ReferenceEquals(PageSheet, sheet))
            {
                PageSheet = null;
                ChangedPageSheet();
            }
        };
        PageSheet = sheet;
        ChangedPageSheet();
    }

    private void ChangedPageSheet()
    {
        OnChanged(nameof(PageSheet));
        OnChanged(nameof(ShowPageSheet));
        PageSheetChanged?.Invoke(this, EventArgs.Empty);
    }
}
