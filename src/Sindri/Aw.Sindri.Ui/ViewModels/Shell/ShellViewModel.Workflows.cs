using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Workflows page in the shell (<see cref="ShellViewModel"/>, continued; #2258 PR 17 part 1). Red stub.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Workflows page's view model.</summary>
    public WorkflowsPageViewModel Workflows => throw new NotSupportedException();

    /// <summary>True on Workflows.</summary>
    public bool IsWorkflows => CurrentPage == PageId.Workflows;
}
