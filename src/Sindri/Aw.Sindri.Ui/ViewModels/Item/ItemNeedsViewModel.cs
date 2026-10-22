using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>
/// The decisions and requests of one item on its sheet (U3' T8): the pending decisions with one button per served option, the
/// requests filed against the item, and the served empty sentence of each list. A decision is decided once from here and never retried.
/// </summary>
internal sealed class ItemNeedsViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly DecisionSender _sender;
    private readonly Platform.IClipboard? _clipboard;
    private readonly Func<DecideState> _readiness;
    private DecisionList? _served;
    private Uri? _url;
    private string _item = "";

    /// <summary>Reads the item's decisions and requests from <paramref name="api"/>.</summary>
    /// <param name="api">The API.</param>
    /// <param name="sender">Sends Decide, with the guard the decision sheet shares.</param>
    /// <param name="readiness">What the catalog says of Decide now; the options take input only when it is ready.</param>
    /// <param name="clipboard">Where the line's Copy puts the served command; null for none.</param>
    internal ItemNeedsViewModel(ISindriApi api, DecisionSender sender, Func<DecideState> readiness, Platform.IClipboard? clipboard = null)
    {
        (_api, _sender, _readiness, _clipboard) = (api, sender, readiness, clipboard);
    }

    /// <summary>The item's needs, reading Decide from <paramref name="catalog"/> and rebuilding the options when the catalog arrives or changes.</summary>
    /// <param name="api">The API.</param>
    /// <param name="sender">Sends Decide.</param>
    /// <param name="catalog">The command catalog.</param>
    /// <returns>The needs.</returns>
    internal static ItemNeedsViewModel Create(ISindriApi api, DecisionSender sender, CatalogViewModel catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var needs = new ItemNeedsViewModel(api, sender, () => StateOf(catalog.For("Decide"), catalog.Listed is not null), catalog.Clipboard);
        catalog.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal))
            {
                needs.Rebuild();
            }
        };
        return needs;
    }

    // A catalog that has not named Decide yet is read as pending, as the mapper reads an action it has no row for.
    private static DecideState StateOf(ControlViewModel? decide, bool read) => decide switch
    {
        null => new DecideState(ActionReadiness.Pending, "", "", read),
        { Kind: ControlKind.Ready } => new DecideState(ActionReadiness.Ready, "", decide.Cli),
        { Kind: ControlKind.Pending } => new DecideState(ActionReadiness.Pending, decide.Stage, decide.Cli),
        _ => new DecideState(ActionReadiness.CliOnly, "", decide.Cli),
    };

    /// <summary>The item's pending decisions, in the API's order.</summary>
    internal IReadOnlyList<ItemDecisionRow> Decisions { get; private set { Set(ref field, value); OnChanged(nameof(HasDecisions)); } } = [];

    /// <summary>True when the item has a decision waiting.</summary>
    internal bool HasDecisions => Decisions.Count > 0;

    /// <summary>The requests filed against the item, in the API's order.</summary>
    internal IReadOnlyList<ItemRequestRow> Requests { get; private set => Set(ref field, value); } = [];

    /// <summary>The served sentence when the item has no decisions; empty when it has some.</summary>
    internal string DecisionsEmpty { get; private set => Set(ref field, value); } = "";

    /// <summary>The served sentence when the item has no requests; empty when it has some.</summary>
    internal string RequestsEmpty { get; private set => Set(ref field, value); } = "";

    /// <summary>The refusal Decide came back with, as served; empty for none.</summary>
    internal string Refusal { get; private set => Set(ref field, value); } = "";

    /// <summary>The sheet moved to another item: nothing of the last one stays (its decisions, requests, empty sentences and refusal).</summary>
    internal void Reset()
    {
        (_url, _item, _served) = (null, "", null);
        Decisions = [];
        Requests = [];
        (DecisionsEmpty, RequestsEmpty, Refusal) = ("", "", "");
    }

    /// <summary>Reads the item's pending decisions and its requests; a failed read keeps what was shown.</summary>
    /// <param name="url">The API.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when both lists are shown.</returns>
    internal async Task Read(Uri url, string itemId, CancellationToken cancellationToken)
    {
        (_url, _item) = (url, itemId);
        var decisions = await _api.Decisions(url, pending: true, itemId, cancellationToken).ConfigureAwait(true);
        var requests = await _api.Requests(url, state: null, itemId, cancellationToken).ConfigureAwait(true);
        if (!string.Equals(itemId, _item, StringComparison.Ordinal))
        {
            return;   // the sheet moved to another item while these were on their way
        }

        if (decisions.IsSuccess)
        {
            // The rows are replaced only when one differs, so the buttons (and the focus on one) survive a re-read (§3.3).
            _served = decisions.Value;
            Rebuild();
            DecisionsEmpty = decisions.Value.Empty ?? "";
        }

        if (requests.IsSuccess)
        {
            Requests = [.. requests.Value.Requests.Select(r => new ItemRequestRow(r.Id, r.Kind, r.Text, r.Actor, r.State, r.DecisionId))];
            RequestsEmpty = requests.Value.Empty ?? "";
        }
    }

    /// <summary>Builds the decision rows again from the last read, as the catalog now reads Decide (it can arrive after the decisions).</summary>
    internal void Rebuild()
    {
        // The rows are replaced only when one differs, so the buttons (and the focus on one) survive a re-read (§3.3).
        var state = _readiness();
        IReadOnlyList<ItemDecisionRow> built = [.. (_served?.Decisions ?? []).Select(d => Row(d, state))];
        if (!built.SequenceEqual(Decisions, ItemDecisionRowComparer.Instance))
        {
            Decisions = built;
        }
    }

    // The line and the options speak of the decision's own served command (its first option's) before the catalog's.
    private ItemDecisionRow Row(DecisionView d, DecideState state)
    {
        var own = state with { Cli = d.Options.Select(o => o.Cli).FirstOrDefault(c => c.Length > 0) ?? state.Cli };
        return new ItemDecisionRow(d.Id, d.Question, d.FiledBy, DecisionOptions.From(d, own, _sender), own.Line, own.Line.Length > 0 ? own.Cli : "");
    }

    /// <summary>Copies a decision's served Decide command.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal async Task CopyDecide(string decisionId)
    {
        var cli = Decisions.FirstOrDefault(d => string.Equals(d.Id, decisionId, StringComparison.Ordinal))?.DecideCli ?? "";
        if (cli.Length > 0 && _clipboard is not null)
        {
            _ = await _clipboard.SetText(cli, CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>Decides one of the item's decisions with an option: sent once, never retried; a refusal shows as served.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <param name="optionId">The option.</param>
    /// <returns>A task that completes when Decide was answered and the lists re-read.</returns>
    internal async Task Choose(string decisionId, string optionId)
    {
        var pressedOn = _item;
        if (_url is not { } url || await _sender.Send(url, decisionId, optionId, note: null).ConfigureAwait(true) is not { } answer
            || !string.Equals(pressedOn, _item, StringComparison.Ordinal))
        {
            return;   // not sent, or the sheet moved to another item while Decide was on its way: its refusal is not this item's
        }

        Refusal = answer.IsError ? answer.Error.Message : "";
        await Read(url, _item, CancellationToken.None).ConfigureAwait(true);
    }
}
