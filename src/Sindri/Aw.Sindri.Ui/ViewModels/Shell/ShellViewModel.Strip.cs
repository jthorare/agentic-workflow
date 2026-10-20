using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Home's Workflows strip in the shell (<see cref="ShellViewModel"/>, continued; U3' T17): J / K across the panels, Enter, and the inline answer.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Home's Workflows strip.</summary>
    public WorkflowsStripViewModel Strip { get; }

    // The root workflow list is read with every event, whatever page is visible (until P5's invalidates). A missing route
    // (404 before P11) marks only the strip, and so does a list in a shape this build can't read (SND-UI-003; main still serves P4's
    // shape until P11): a sentence, never an empty box. Any other failure leaves the rows as they were and the next event tries again.
    private async Task ReadStrip(Uri url, CancellationToken cancellationToken)
    {
        var list = await _api.Instances(url, new InstanceQuery(Root: true, Open: true), cancellationToken).ConfigureAwait(true);
        if (list.IsSuccess)
        {
            Strip.Update(list.Value);
        }
        else if (list.Error.Kind == ErrorKind.NotFound)
        {
            Strip.MarkNotServed();
        }
        else if (string.Equals(list.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) && list.Error.Kind == ErrorKind.Invalid)
        {
            Strip.MarkUnreadable();
        }
    }

    // The strip gives rows back to Needs you first and marks its rows stale with the views; "… N more" opens Workflows.
    // Everything the strip needs once it exists (ShellViewModel's constructor calls this one line).
    private void InitStrip()
    {
        Health.Follow(ReadStrip);
        Strip.WorkflowsRequested += (_, _) => GoTo(PageId.Workflows);
        NeedsYou.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(NeedsYouViewModel.Rows), StringComparison.Ordinal))
            {
                Strip.SetNeedsYouShown(NeedsYou.Rows.Count);
            }
        };
        Health.Banner.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewsBanner.IsStalled), StringComparison.Ordinal))
            {
                Strip.SetStale(Health.Banner.IsStalled);
            }
        };
    }

    /// <summary>Focus or a click reached a strip row: it is the one selection on Home.</summary>
    /// <param name="address">The instance's address.</param>
    internal void SelectStripRow(string address)
    {
        Queue.ClearSelection();
        NeedsYou.Deselect();
        _ = Strip.Select(address);
    }

    /// <summary>Opens a strip row's instance (a click on its subject).</summary>
    /// <param name="address">The instance's address.</param>
    internal void OpenStripRow(string address)
    {
        if (Strip.Rows.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal)) is { } row)
        {
            Navigate(row.Link);
            OpenerStrip = row.Address;
        }
    }

    /// <summary>The Answer button of a strip row: the same as A on it.</summary>
    /// <param name="address">The instance's address.</param>
    internal void AnswerStripRow(string address)
    {
        if (Strip.Rows.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal)) is { CanAnswer: true } row)
        {
            Answer(row);
        }
    }

    // Enter opens the selected row's instance; A answers it: the park row for the same instance when there is one (the one
    // answer form is that row's sheet), else the instance itself. Never a protected row: its button is the lock.
    private bool StripKeyPressed(string key, bool inSheet)
    {
        if (inSheet || !IsHome || Strip.SelectedRow is not { } row)
        {
            return false;
        }

        switch (key)
        {
            case "Enter":
                Navigate(row.Link);
                OpenerStrip = row.Address;
                return true;
            case "A" when row.CanAnswer:
                Answer(row);
                return true;
            default:
                return false;
        }
    }

    // The Needs-you row for an instance's park (the P4 needs-you goldens serve no park row yet, so this reads the shape the plan
    // names): a row of the served kind "park" whose subject is the instance (its address or its id), else the park row of the same
    // item. It keys on the served kind, never on the chip's word. None: the caller opens the workflow link.
    private NeedsYouRowViewModel? ParkRowFor(StripRowViewModel row)
    {
        var parks = NeedsYou.Seen.Rows.Where(r => string.Equals(r.Entry.Kind, NeedsYouKinds.Park, StringComparison.Ordinal)).ToList();
        return parks.Find(r => r.Entry.Subject is { } s && (string.Equals(s.Id, row.Address, StringComparison.Ordinal) || string.Equals(s.Id, row.InstanceId, StringComparison.Ordinal)))
            ?? parks.Find(r => row.ItemId is { } item && string.Equals(r.Entry.ItemId, item, StringComparison.Ordinal));
    }

    private void Answer(StripRowViewModel row)
    {
        Strip.Deselect();
        NavigateToRowSheet(ParkRowFor(row) is { } park ? LinkOf(park.Id) : row.Link);
        OpenerStrip = row.Address;
    }

    /// <summary>The strip row the sheet was opened from (its address), where Esc returns focus; null when the sheet came from elsewhere.</summary>
    internal string? OpenerStrip { get; private set; }

    // J / K on Home walk Needs you, then the strip, then Running, then Queued (and back); true when the key was used.
    private bool MoveOnHome(int step)
    {
        var panels = NeedsYou.Rows.Count + Strip.Rows.Count;
        var at = PanelPosition();
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

    // The selected row's place across Needs you and the strip; -1 when neither holds the selection.
    private int PanelPosition()
    {
        var at = IndexOfSelected();
        return at >= 0 ? at : Strip.SelectedRow is { } row ? NeedsYou.Rows.Count + Strip.Rows.ToList().IndexOf(row) : -1;
    }

    private void WalkPanels(int at, int step, int panels)
    {
        var needs = NeedsYou.Rows.Count;
        var to = at + step;
        if (to >= panels)
        {
            LeavePanels(step);
        }
        else if (at < needs && to >= 0 && to < needs)
        {
            _ = NeedsYou.Press(step > 0 ? NeedsYouKey.J : NeedsYouKey.K);
        }
        else
        {
            SelectPanelRow(Math.Max(to, 0));
        }
    }

    private void LeavePanels(int step)
    {
        NeedsYou.Deselect();
        Strip.Deselect();
        Queue.Move(step, QueueScope.Home);
    }

    private void SelectPanelRow(int index)
    {
        var needs = NeedsYou.Rows.Count;
        Queue.ClearSelection();
        if (index < needs)
        {
            Strip.Deselect();
            NeedsYou.Select(NeedsYou.Rows[index].Id);
        }
        else
        {
            NeedsYou.Deselect();
            _ = Strip.Select(Strip.Rows[index - needs].Address);
        }
    }
}
