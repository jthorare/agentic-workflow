using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Home's Needs-you list in the shell (<see cref="ShellViewModel"/>, continued; U3' T9): the filter, the sheets, and J / K across the panels.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Home's Needs-you list.</summary>
    public NeedsYouViewModel NeedsYou { get; }

    /// <summary>The decision sheet.</summary>
    public DecisionSheetViewModel Decision { get; }

    /// <summary>True when the sheet's subject is a Needs-you row.</summary>
    public bool IsNeedsYouSheet => SheetLink?.Kind == DeepLinkKind.NeedsYou;

    /// <summary>True when the sheet's subject is a decision.</summary>
    public bool IsDecisionSheet => SheetLink?.Kind == DeepLinkKind.Decision;

    /// <summary>True when Home's selected Needs-you row is the one whose sheet is open.</summary>
    internal bool IsSelectedNeedsYouRowOpen => IsHome && NeedsYou.SelectedId is { } id && string.Equals(NeedsYou.SheetRowId, id, StringComparison.Ordinal);

    /// <summary>True when the selected row (a queue row or a Needs-you row) is the one whose sheet is open.</summary>
    internal bool IsSelectedSheetOpen => IsSelectedRowOpen || IsSelectedNeedsYouRowOpen || IsSelectedWorkflowOpen;

    /// <summary>True when the Workflows page's selected row is the instance whose frame is open.</summary>
    internal bool IsSelectedWorkflowOpen => IsWorkflows && IsSheetOpen && IsInstanceSheet && Workflows.SelectedAddress is { } address && string.Equals(address, Instance.Address, StringComparison.Ordinal);

    /// <summary>Marks every current row seen (Shift+A from anywhere but a text field).</summary>
    internal void MarkAllSeen() => NeedsYou.MarkAllSeen();

    // The list shows the rows the Home filter lets through (G D: the decisions); the attention summary always counts them all.
    private void UpdateNeedsYou()
    {
        if (_needsYou is not { } feed)
        {
            return;
        }

        NeedsYou.Update(feed, HomeFilter == HomeFilter.Decisions && !IsNeedsYouInbox ? row => row.DecisionId is not null : null);   // the inbox lists every row, whatever Home's filter says
        FollowDecision();
    }

    // Everything the Needs-you list needs once it and the decision sheet exist (ShellViewModel's constructor calls this one line).
    private void InitNeedsYou(IClipboard clipboard)
    {
        Health.Follow(ReadNeedsYou);
        NeedsYou.SheetRequested += (_, e) => Navigate(LinkOf(e.RowId));
        NeedsYou.ActionRequested += (_, e) => Decide(e);
        Decision.Refused += (_, _) => Navigate(new DeepLink(DeepLinkKind.Decision, Decision.DecisionId));
        NeedsYou.CopyRequested += (_, e) => _ = clipboard.SetText(e.Text, CancellationToken.None);
    }

    // The decision sheet follows its row: updated in place while the row is listed, cleared (form disabled, text kept) when it is gone.
    private void FollowDecision()
    {
        var shown = NeedsYou.Seen.Rows.FirstOrDefault(r => Decision.Shows(r.Id));
        if (shown is not null)
        {
            Decision.Show(shown);
        }
        else
        {
            _ = Decision.Clear();
        }
    }

    // A key or a button asks to run an action: approve-all opens its dialog; a decision option is Decide, sent once with the option, and the sheet shows what came back.
    private void Decide(NeedsYouActionEventArgs request)
    {
        if (string.Equals(request.Action.CatalogId, ApproveAllViewModel.Action, StringComparison.Ordinal))
        {
            OpenApproveAll();
        }
        else if (request.Action.OptionId.Length > 0 && NeedsYou.Row(request.RowId) is { } row)
        {
            Decision.Show(row);
            _ = Decision.Choose(request.Action.OptionId);
        }
    }

    // A decision row opens the decision sheet (by the decision's id); every other row opens its own, keyed on the row id.
    private DeepLink LinkOf(string rowId) => Canonical(new DeepLink(DeepLinkKind.NeedsYou, rowId));

    // A deep link to a row or a decision selects its row on Home (the sheet shows beside it).
    private void SelectRowOf(DeepLink link)
    {
        var row = link.Subject is not { } subject ? null
            : link.Kind == DeepLinkKind.Decision ? NeedsYou.RowOfDecision(subject) : NeedsYou.Row(subject);
        if (row is not null)
        {
            Queue.ClearSelection();
            Strip.Deselect();
            NeedsYou.OpenSheetOn(row.Id);
            if (link.Kind == DeepLinkKind.Decision)
            {
                Decision.Show(row);
            }
        }
    }

    /// <summary>Focus or a click reached a Needs-you row: it is the one selection on Home.</summary>
    /// <param name="rowId">The row's id.</param>
    internal void SelectNeedsYouRow(string rowId)
    {
        Queue.ClearSelection();
        Strip.Deselect();
        NeedsYou.Select(rowId);
    }

    /// <summary>Focus or a click reached a queue row on a page: on Home it is the one selection.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <param name="scope">The page the row is on.</param>
    internal void SelectQueueRow(string itemId, QueueScope scope = QueueScope.Home)
    {
        if (scope == QueueScope.Home)
        {
            NeedsYou.Deselect();
            Strip.Deselect();
        }

        Queue.Select(itemId, scope);
    }

    // A sheet that is not the open Needs-you row's own replaces it: that row is no longer open.
    private void ForgetRowSheetUnless(DeepLink link)
    {
        if (NeedsYou.SheetRowId is not { } open)
        {
            return;
        }

        var own = (link.Kind == DeepLinkKind.NeedsYou && string.Equals(link.Subject, open, StringComparison.Ordinal))
            || (link.Kind == DeepLinkKind.Decision && NeedsYou.Row(open) is { } row && string.Equals(row.Entry.DecisionId, link.Subject, StringComparison.Ordinal));
        if (!own)
        {
            NeedsYou.ForgetSheet();
        }
    }

    /// <summary>Opens a row's sheet (a click on its title, Enter).</summary>
    /// <param name="rowId">The row's id.</param>
    internal void OpenNeedsYouRow(string rowId) => Navigate(LinkOf(rowId));

    // A, X, Y and Enter act on the Needs-you row the selection is on (Home, focus not in the sheet).
    private bool NeedsYouKeyPressed(string key, bool inSheet)
    {
        NeedsYouKey? named = key switch
        {
            "A" => NeedsYouKey.A,
            "X" => NeedsYouKey.X,
            "Y" => NeedsYouKey.Y,
            "Enter" => NeedsYouKey.Enter,
            _ => null,
        };
        if (inSheet || !IsHome)
        {
            return false;
        }

        if (IsNeedsYouInbox)
        {
            return InboxKeyPressed(key);
        }

        return string.Equals(key, "J", StringComparison.Ordinal) || string.Equals(key, "K", StringComparison.Ordinal) ? MoveOnHome(string.Equals(key, "J", StringComparison.Ordinal) ? 1 : -1) : named is { } k && NeedsYou.SelectedId is not null && NeedsYou.Press(k);
    }

    private int IndexOfSelected()
    {
        var id = NeedsYou.SelectedId;
        var rows = NeedsYou.Rows;
        return id is null ? -1 : rows.ToList().FindIndex(r => string.Equals(r.Id, id, StringComparison.Ordinal));
    }
}
