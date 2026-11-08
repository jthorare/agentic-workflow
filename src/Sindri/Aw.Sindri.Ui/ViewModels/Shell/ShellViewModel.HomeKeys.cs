using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>J and K on Home (<see cref="ShellViewModel"/>, continued): they walk Needs you, then Running, then Queued, and back.</summary>
internal sealed partial class ShellViewModel
{
    // True when the key was used.
    private bool MoveOnHome(int step)
    {
        var panels = NeedsYou.Rows.Count;
        var at = IndexOfSelected();
        if (at >= 0)
        {
            WalkPanels(at, step, panels);
        }
        else if (panels > 0 && (!Queue.HasSelectionOn(QueueScope.Home) || (step < 0 && Queue.IsFirstOnHome)))
        {
            SelectPanelRow(step < 0 ? panels - 1 : 0);
        }
        else
        {
            LeavePanels(step);
        }

        return true;
    }

    private void WalkPanels(int at, int step, int panels)
    {
        var to = at + step;
        if (to >= panels)
        {
            LeavePanels(step);
        }
        else if (to >= 0)
        {
            _ = NeedsYou.Press(step > 0 ? NeedsYouKey.J : NeedsYouKey.K);
        }
        else
        {
            SelectPanelRow(0);
        }
    }

    private void LeavePanels(int step)
    {
        NeedsYou.Deselect();
        Queue.Move(step, QueueScope.Home);
    }

    private void SelectPanelRow(int index)
    {
        Queue.ClearSelection();
        NeedsYou.Select(NeedsYou.Rows[index].Id);
    }
}
