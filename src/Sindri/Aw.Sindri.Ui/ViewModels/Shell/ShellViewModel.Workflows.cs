using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Workflows page in the shell (<see cref="ShellViewModel"/>, continued; U8 T2, keys in T7): read with every event, whatever page is visible; its open figure is the tab's count.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Workflows page's list.</summary>
    public WorkflowsViewModel Workflows { get; }

    // Everything the page needs once it exists (ShellViewModel's constructor calls this one line).
    private void InitWorkflows()
    {
        Health.Follow(Workflows.Refresh);
        Health.FollowInstances(Workflows.Catch);
        Workflows.OpenRequested += (_, e) => OpenRequestedFromWorkflows(e);
        Workflows.ChildAnswerRequested += (_, e) =>
        {
            FollowChildAnswer(e.Answer);
            OpenerWorkflow = e.RowAddress;   // Esc comes back to this row
        };
        Workflows.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(WorkflowsViewModel.NavCount), StringComparison.Ordinal))
            {
                Navigation.First(n => n.Page == PageId.Workflows).Count = Workflows.NavCount;
            }
        };
        Health.Banner.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewsBanner.IsStalled), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(ViewsBanner.IsRebuilding), StringComparison.Ordinal))
            {
                Workflows.SetStale(Health.Banner.IsStalled || Health.Banner.IsRebuilding);
            }
        };
    }

    private void OpenRequestedFromWorkflows(OpenRequestedEventArgs e)
    {
        if (e.AtDesk && e.Link is { Kind: DeepLinkKind.NeedsYou, Subject: { } rowId })
        {
            OpenNeedsYouInbox(rowId);   // the desk card, not the child its pointer names: the answer form is here
            return;
        }

        if (e.Link.Subject is { } address)
        {
            Instance.Intend(address, e.Intent);   // before the sheet opens: the frame does it when the instance is read
            if (IsSheetOpen && IsInstanceSheet && string.Equals(Instance.Address, address, StringComparison.Ordinal))
            {
                OpenerWorkflow = address;   // Esc comes back to this row, whatever opened the frame first
                Instance.FocusInitial();   // Enter again (or a click) on the row whose frame is open goes into the frame, which stays as it is
                return;
            }
        }

        Navigate(e.Link);
        OpenerWorkflow = e.Link.Subject;
    }

    // The Workflows page's own keys (J / K, Enter, A, X, B, N) belong to the page and never to the sheet.
    private bool WorkflowsKeyPressed(string key, bool inSheet) => !inSheet && IsWorkflows && Workflows.Press(key);
}
