using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Agents page in the shell (<see cref="ShellViewModel"/>, continued; U11a T1): it reads while it is the page shown and the window is up.</summary>
internal sealed partial class ShellViewModel
{
    // The page follows the control plane's events (it only reads when shown), is told when it becomes the page, and says "N agents · M asking" through its own region.
    partial void InitAgentsPage()
    {
        Health.Follow(Agents.Refresh);
        Agents.WorkflowsRequested += (_, _) => GoTo(PageId.Workflows);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsAgents), StringComparison.Ordinal))
            {
                Agents.SetShown(IsAgents);
            }
        };
    }

    /// <summary>The window was minimised or restored: a minimised window does not poll for sessions.</summary>
    /// <param name="minimised">True while minimised.</param>
    internal void SetWindowMinimised(bool minimised) => Agents.SetMinimised(minimised);
}
