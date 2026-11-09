using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Memory;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Memory page in the shell (<see cref="ShellViewModel"/>, continued; PR 18a). It is not a tab: G M, the palette, System's Memory link and a 'memory/&lt;name&gt;' link open it.
/// The page reads while it is the page shown and re-reads on every event then.
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
    private void InitMemory()
    {
        MemoryPage = new MemoryPageViewModel(_api, _clock);
        Health.Follow(MemoryPage.Refresh);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsMemory), StringComparison.Ordinal))
            {
                MemoryPage.SetShown(IsMemory);
            }
        };
    }

    // A 'memory/<name>' link: the Memory page, on that store.
    private void OpenMemoryStore(string store)
    {
        RequestedMemoryStore = store;
        GoTo(PageId.Memory);
    }
}
