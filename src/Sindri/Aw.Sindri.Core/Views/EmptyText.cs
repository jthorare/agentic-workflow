using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Core.Views;

/// <summary>The empty, loading and error sentences of the list views (API spec §3.6; P2 and P3): the CLI prints them and every list returns one in 'empty'.</summary>
public static class EmptyText
{
    /// <summary>The services list is empty.</summary>
    /// <returns>The sentence.</returns>
    public static string Services() => "No services are approved: run `sindri profile approve`.";

    /// <summary>The queue has no open item in any group.</summary>
    /// <returns>The sentence.</returns>
    public static string Queue() => "Nothing in the queue. Add one with `sindri item add`.";

    /// <summary>A queue group is empty (UI spec E-18): a sentence for every group, Parked included.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The sentence.</returns>
    public static string Group(QueueGroup group) => group switch
    {
        QueueGroup.WaitingOnYou => "Nothing is waiting on you.",
        QueueGroup.WithYou => "Nothing is with you.",
        QueueGroup.Parked => "Nothing parked.",
        QueueGroup.Running => "Nothing running.",
        QueueGroup.Queued => "Nothing queued.",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "not a queue group"),
    };

    /// <summary>The item list is empty for its filters. Without a state filter the list holds open items only, so the sentence says "open".</summary>
    /// <param name="state">The state filter, or null.</param>
    /// <param name="repo">The repo filter, or null.</param>
    /// <returns>The sentence.</returns>
    public static string Items(ItemState? state, RepoName? repo)
    {
        var what = state is { } s ? "No " + Name(s) + " items" : "No open items";
        return repo is { } r ? what + " in " + r.Value + "." : what + ".";
    }

    /// <summary>An item's timeline has no line yet.</summary>
    /// <returns>The sentence.</returns>
    public static string Timeline() => "Nothing has happened to this item yet.";

    /// <summary>Nothing needs the human (UI screens R8-13; the 'waiting on you' wording is retired).</summary>
    /// <param name="running">How many items run.</param>
    /// <param name="queued">How many items are queued.</param>
    /// <returns>The sentence, zeroes shown.</returns>
    public static string NeedsYou(int running, int queued) =>
        string.Create(CultureInfo.InvariantCulture, $"Nothing needs you. {running} running, {queued} queued.");

    /// <summary>The decision list is empty for its filters.</summary>
    /// <param name="pending">True for the pending list, false for the decided one, null for both.</param>
    /// <param name="item">The item filter, or null.</param>
    /// <returns>The sentence.</returns>
    public static string Decisions(bool? pending, ItemId? item)
    {
        if (item is { } named)
        {
            return "No decisions for " + named.Value + ".";
        }

        return pending is { } waiting ? waiting ? "No decisions are waiting." : "Nothing decided yet." : "No decisions.";
    }

    /// <summary>The request list is empty for its filters.</summary>
    /// <param name="state">The state filter, or null.</param>
    /// <param name="item">The item filter, or null.</param>
    /// <returns>The sentence.</returns>
    public static string Requests(RequestState? state, ItemId? item)
    {
        if (item is { } named)
        {
            return "No requests for " + named.Value + ".";
        }

        return state switch
        {
            null => "No requests filed.",
            RequestState.Open => "No open requests.",
            RequestState.Accepted => "No accepted requests.",
            RequestState.Declined => "No declined requests.",
            RequestState.Voided => "No voided requests.",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a request state"),
        };
    }

    /// <summary>The notice on a row or a decision whose answer is recorded and not yet acted on (API spec §3.4).</summary>
    /// <returns>The sentence.</returns>
    public static string RecordedOnly() => "recorded; acted on once the workflow engine runs";

    /// <summary>The notice on a decision whose item was closed before a human decided it (owner decision of 2026-10-14: the API serves it).</summary>
    /// <returns>The sentence.</returns>
    public static string Voided() => "voided: the item was closed";

    /// <summary>The views are rebuilding.</summary>
    /// <param name="cursor">The projector's cursor.</param>
    /// <param name="head">The ledger head.</param>
    /// <returns>The sentence.</returns>
    public static string Rebuilding(long cursor, long head) =>
        string.Create(CultureInfo.InvariantCulture, $"Rebuilding views (cursor {cursor} of {head})…");

    /// <summary>The projector stalled.</summary>
    /// <param name="at">The sequence it stalled at.</param>
    /// <returns>The sentence.</returns>
    public static string Stalled(long at) =>
        string.Create(CultureInfo.InvariantCulture, $"Views are stalled at {at}: run `sindri views rebuild`");

    /// <summary>The projector can't run (no event is to blame).</summary>
    /// <param name="detail">Why, with its code.</param>
    /// <returns>The sentence.</returns>
    public static string Failing(string? detail) => $"The views can't be updated ({detail}): run `{FixFor(detail)}`";

    /// <summary>The command that fixes a stall: the doctor when the ledger is what can't be read (a rebuild can't help), else a rebuild.</summary>
    /// <param name="detail">The stall's '&lt;code&gt;: &lt;detail&gt;'.</param>
    /// <returns>The command.</returns>
    public static string FixFor(string? detail) =>
        detail is not null && detail.StartsWith("SND-LEDGER", StringComparison.Ordinal) ? "sindri doctor" : "sindri views rebuild";

    /// <summary>The views can't be read.</summary>
    /// <returns>The sentence.</returns>
    public static string Unreadable() => "The views can't be read: run `sindri views rebuild`";

    /// <summary>The sentence a list answers with: stalled or failing, then rebuilding, then its empty sentence when it has no rows, else null.</summary>
    /// <param name="views">The projector's status.</param>
    /// <param name="empty">True when the list has no rows.</param>
    /// <param name="emptySentence">The list's empty sentence.</param>
    /// <returns>The sentence, or null.</returns>
    public static string? For(ViewsStatus views, bool empty, Func<string> emptySentence)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(emptySentence);
        return views.Text ?? (empty ? emptySentence() : null);
    }

    private static string Name(ItemState state) => ItemStateNames.Of(state);
}
