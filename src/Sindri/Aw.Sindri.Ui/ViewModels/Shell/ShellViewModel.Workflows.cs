using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Workflows page in the shell (<see cref="ShellViewModel"/>, continued; #2258 PR 17 part 1): it reads when it becomes the page shown (and once on connect when it
/// was shown before Sindri answered), returns to its list when left, and while shown reads again (the list and the level open) after a 'workflow.run.*' or 'workflow.node.*' event,
/// at the follower's pace (one re-read per 250 ms burst). A tab after Projects; G W and the palette open it too.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Workflows page's view model.</summary>
    public WorkflowsPageViewModel Workflows { get; private set; }

    /// <summary>True on Workflows.</summary>
    public bool IsWorkflows => CurrentPage == PageId.Workflows;

    private async Task ShowWorkflowsAndReturn(Uri url)
    {
        await Workflows.Show(url, CancellationToken.None).ConfigureAwait(true);
        await Workflows.ReturnFromConnect(CancellationToken.None).ConfigureAwait(true);   // does nothing unless the owner left through a Connect button
    }

    [MemberNotNull(nameof(Workflows))]
    private void InitWorkflows()
    {
        Workflows = new WorkflowsPageViewModel(_api, _clock);
        Health.Banner.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewsBanner.IsStalled), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(ViewsBanner.Rebuild), StringComparison.Ordinal))
            {
                Workflows.SetViewsStalled(Health.Banner.IsStalled, Health.Banner.Rebuild);   // the list level says "can't load" with the same Rebuild action as the banner
            }
        };
        Workflows.SetViewsStalled(Health.Banner.IsStalled, Health.Banner.Rebuild);
        Workflows.ConnectRequested += _ => GoTo(PageId.Connections);   // "Connect GitHub" on a step's sheet: Connections now, the sheet again on return
        var seen = 0L;
        Health.Follow((url, token) =>
        {
            var heard = Health.WorkflowEvents;
            var moved = heard != seen;
            seen = heard;
            return IsWorkflows && (Workflows.IsReading || moved) ? Workflows.Show(url, token) : Task.CompletedTask;   // shown before connect: its first read; shown and a run moved: read again
        });
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
                _ = ShowWorkflowsAndReturn(url);
            }
        };
    }
}
