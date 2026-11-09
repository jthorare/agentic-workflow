using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Memory;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Memory page in the shell (<see cref="ShellViewModel"/>, continued; PR 18a). It is not a tab: G M, the palette, System's Memory link and a 'memory/&lt;name&gt;' link open it.
/// This file holds the shared wiring only (the page's slot and its link); the Memory builder's files hold the page itself.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Memory page.</summary>
    public MemoryPageViewModel MemoryPage { get; private set; }

    /// <summary>True on Memory.</summary>
    public bool IsMemory => CurrentPage == PageId.Memory;

    /// <summary>The store a 'memory/&lt;name&gt;' link named, for the page to show; null when the page was opened without one.</summary>
    internal string? RequestedMemoryStore { get; private set; }

    [MemberNotNull(nameof(MemoryPage))]
    private void InitMemory() => MemoryPage = new MemoryPageViewModel();

    // A 'memory/<name>' link: the Memory page, on that store.
    private void OpenMemoryStore(string store)
    {
        RequestedMemoryStore = store;
        GoTo(PageId.Memory);
    }
}
