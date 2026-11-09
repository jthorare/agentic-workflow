using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Resources;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Resources page in the shell (<see cref="ShellViewModel"/>, continued; PR 18b). It is not a tab: G R, the palette and System's Resources link open it. It reads while it is the page shown.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Resources page.</summary>
    public ResourcesPageViewModel ResourcesPage { get; private set; }

    /// <summary>True on Resources.</summary>
    public bool IsResources => CurrentPage == PageId.Resources;

    [MemberNotNull(nameof(ResourcesPage))]
    private void InitResources()
    {
        ResourcesPage = new ResourcesPageViewModel(_api);
        Health.Follow(ResourcesPage.Connected);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsResources), StringComparison.Ordinal))
            {
                ResourcesPage.SetShown(IsResources);
            }
        };
    }
}
