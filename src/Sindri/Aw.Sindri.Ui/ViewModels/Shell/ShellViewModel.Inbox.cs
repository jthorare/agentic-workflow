using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Needs-you inbox in the shell (<see cref="ShellViewModel"/>, continued; F3 needs-you spec §2): a page that shows in place of
/// Home's panels while Home stays the page (no new tab). The link to a row, Enter on a Home row and Home's "all N" open it; Esc, the
/// breadcrumb and going to any page close it. A decision row still opens its decision sheet until the action bar takes its answer.
/// </summary>
internal sealed partial class ShellViewModel
{
    private static readonly string[] _inboxProperties = [nameof(IsNeedsYouInbox), nameof(IsHomeShown), nameof(IsInboxShown), nameof(IsPagesShown)];

    /// <summary>The inbox page.</summary>
    public NeedsYouPageViewModel Inbox { get; }

    /// <summary>True while the inbox shows in place of Home's panels (Home stays the page).</summary>
    public bool IsNeedsYouInbox { get; private set; }

    /// <summary>Home's panels show: Home, and not the inbox over it.</summary>
    public bool IsHomeShown => IsHome && !IsNeedsYouInbox;

    /// <summary>The inbox shows: it is open and the pages have something to show.</summary>
    public bool IsInboxShown => IsNeedsYouInbox && IsPageVisible;

    /// <summary>The pages' scrolling region shows: the pages are visible and the inbox, which scrolls itself, does not take their place.</summary>
    public bool IsPagesShown => IsPageVisible && !IsNeedsYouInbox && !IsRunPageShown;

    /// <summary>Opens the inbox, on a row when one is named and listed, else on the first row.</summary>
    /// <param name="rowId">The row to select; null for the first.</param>
    internal void OpenNeedsYouInbox(string? rowId = null)
    {
        if (!IsHome)
        {
            GoTo(PageId.Home);
        }

        Queue.ClearSelection();
        Strip.Deselect();
        NeedsYou.PointerOver = false;   // the pointer that held Home's list is not over the inbox's: a hide gives no exit
        IsNeedsYouInbox = true;
        UpdateNeedsYou();   // without Home's filter
        if (rowId is not null)
        {
            NeedsYou.Select(rowId);
        }

        Inbox.Active = true;
        Inbox.SelectFirst();
        Changed(_inboxProperties);
    }

    /// <summary>Leaves the inbox for Home (Esc, the breadcrumb).</summary>
    /// <returns>True when the inbox was open.</returns>
    internal bool LeaveNeedsYouInbox()
    {
        if (!IsNeedsYouInbox)
        {
            return false;
        }

        IsNeedsYouInbox = false;
        Inbox.Active = false;
        NeedsYou.PointerOver = false;   // likewise the inbox's hold: leaving gives no exit
        UpdateNeedsYou();   // Home's filter again
        Changed(_inboxProperties);
        return true;
    }

    private NeedsYouPageViewModel InitInbox(ISindriApi api, IClipboard clipboard)
    {
        var commands = new InstanceCommander(api, Health.ApiUrl);
        var inbox = new NeedsYouPageViewModel(NeedsYou, _clock, clipboard, new DeskReporter(api, Health.ApiUrl), new NeedsYouConfirm(Health.Catalog, Health.ApiUrl, api, _clock, () => NeedsYou.Seen.ReducedMotion),
            new PausedAnswerViewModel(commands, clipboard, _clock), new TakeoverViewModel(api, Health.ApiUrl, commands, _clock));
        inbox.LeaveRequested += (_, _) => LeaveNeedsYouInbox();
        inbox.Bar.DecisionRequested += (_, e) => e.Settled = AnswerDecision(e.RowId, e.OptionId, e.Note);
        inbox.ItemRequested += (_, e) =>
        {
            if (NeedsYou.Row(e.RowId)?.ItemLink is { } link)
            {
                Navigate(link);
            }
        };
        return inbox;
    }

    // J and K walk the inbox in its own order, never Home's capped list behind it; A, D and Enter act on the selected request through the action bar
    // (a key never sends a protected action: the bar copies its command); no other Home key acts while it shows.
    private bool InboxKeyPressed(string key)
    {
        if (!IsNeedsYouInbox)
        {
            return false;
        }

        switch (key)
        {
            case "J" or "K":
                _ = Inbox.Move(key == "J" ? 1 : -1);
                return true;
            case "A":
                _ = Inbox.Bar.PressApprove();
                return true;
            case "D":
                _ = Inbox.Bar.PressSendBack();
                return true;
            case "X" when Inbox.Selected is { Entry.DecisionId: not null }:
                _ = Inbox.Bar.PressSendBack();   // X stays as an alias of D on a decision row
                return true;
            case "Enter":
                Inbox.FocusCard();
                return true;
            default:
                return false;
        }
    }
}
