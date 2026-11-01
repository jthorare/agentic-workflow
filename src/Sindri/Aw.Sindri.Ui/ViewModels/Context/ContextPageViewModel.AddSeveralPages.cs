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

        Mount(new AddSeveralPagesViewModel(writes, repo: null, known: Known()), () => noConcept ? OpenNoConcept() : ReadWhole(CancellationToken.None));
    }

    /// <summary>Opens the Accept all suggested sheet over a concept's suggested pages; a saved list reads the section again.</summary>
    /// <param name="concept">The concept the section is open on.</param>
    /// <param name="keys">The suggested rows' node keys, in the section's order.</param>
    internal void OpenAcceptAllSuggested(string? concept, IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (Ports.Pages is not { } writes)
        {
            return;
        }

        var sheet = AddSeveralPagesViewModel.AcceptAll(writes, concept, keys, Known());
        Mount(sheet, () => ReadWhole(CancellationToken.None));
        _ = sheet.LoadSuggested(CancellationToken.None);
    }

    private List<ConceptChoice> Known() => [.. Concepts.Tiles.Select(static t => new ConceptChoice(Id: t.Id, Name: t.Name))];

    // Shows the sheet in the shell's one sheet; a saved list reads the section again, and the sheet goes when it is closed.
    private void Mount(AddSeveralPagesViewModel sheet, Func<Task> reread)
    {
        sheet.Saved += (_, _) => _ = reread();
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
