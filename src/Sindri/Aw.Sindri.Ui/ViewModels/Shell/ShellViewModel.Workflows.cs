using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Workflows page in the shell (<see cref="ShellViewModel"/>, continued; #2258 PR 17 part 1): it reads when it becomes the page shown (and once on connect when it
/// was shown before Sindri answered) and returns to its list when left. Part 1 has no live refresh. The page is not a tab: G W and the palette open it.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Workflows page's view model.</summary>
    public WorkflowsPageViewModel Workflows { get; private set; }

    /// <summary>True on Workflows.</summary>
    public bool IsWorkflows => CurrentPage == PageId.Workflows;

    [MemberNotNull(nameof(Workflows))]
    private void InitWorkflows()
    {
        Workflows = new WorkflowsPageViewModel(_api, _clock);
        Health.Follow((url, token) => IsWorkflows && Workflows.IsReading ? Workflows.Show(url, token) : Task.CompletedTask);   // shown before connect: its first read
        PropertyChanged += (_, e) =>
        {
            if (!string.Equals(e.PropertyName, nameof(IsWorkflows), StringComparison.Ordinal))
            {
                return;
            }

            if (!IsWorkflows)
            {
                Workflows.Leave();
            }
            else if (Health.ApiUrl() is { } url)
            {
                _ = Workflows.Show(url, CancellationToken.None);
            }
        };
    }
}
