using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>Maps the API's needs-you list, with the command catalog and the pending decisions, onto the rows and feed the view models take.</summary>
internal static class NeedsYouMapper
{
    private const string Decide = "Decide";

    /// <summary>The feed for a served list.</summary>
    /// <param name="list">GET /needs-you.</param>
    /// <param name="catalog">GET /commands: where each action's readiness comes from.</param>
    /// <param name="decisions">The pending decisions, whose options become a decision row's Decide buttons; null keeps the one served action.</param>
    /// <param name="counts">Home's other totals.</param>
    /// <returns>The feed, rows in the API's order.</returns>
    internal static NeedsYouFeed Map(NeedsYouList list, CommandList catalog, DecisionList? decisions, HomeCounts counts)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(catalog);
        var readiness = catalog.Rows.ToDictionary(r => r.Action, r => (r.Cli, Protected: string.Equals(r.Class, "protected", StringComparison.Ordinal)), StringComparer.Ordinal);
        var read = catalog.Rows.Count > 0;
        return new NeedsYouFeed([.. list.Rows.Where(row => !NeedsYouKinds.Retired.Contains(row.Kind, StringComparer.Ordinal)).Select(row => Entry(row, readiness, decisions, read))], list.Empty ?? string.Empty)
        {
            Stalled = list.AsOf.Stalled,
            Rebuilding = list.AsOf.Rebuilding,
            Counts = counts,
        };
    }

    private static NeedsYouEntry Entry(NeedsYouRowView row, Dictionary<string, (string Cli, bool Protected)> readiness, DecisionList? decisions, bool read)
    {
        var subject = row.Subject is { } s ? new NeedsYouSubject(s.Kind, s.Id) : null;
        var pending = PendingDecision(subject?.DecisionId, decisions);
        return new NeedsYouEntry(row.Id, UrgencyOf(row.Urgency))
        {
            Kind = row.Kind,
            Chip = row.Chip ?? Derived(row.Kind),
            ShownId = ShownOf(row, subject),
            Title = row.Title,
            Repo = Text(row.Repo),
            Label = row.Label,
            Reason = row.Reason,
            Since = row.Since,
            PrimaryAction = Text(row.PrimaryAction),
            Actions = [.. row.Actions.SelectMany(id => Actions(row, subject, id, readiness, decisions).Select(a => a with { CatalogRead = read }))],
            Subject = subject,
            ItemId = row.Item,
            Cli = row.Cli,
            RecordedOnly = row.RecordedOnly,
            Notice = Text(row.Notice),
            Open = row.Open,
            Question = Text(pending?.Question),
            FiledBy = Text(pending?.FiledBy),
        };
    }

    // A served text that may be absent, as the empty string.
    private static string Text(string? served) => served ?? string.Empty;

    // The id shown at rest: the decision's own on a decision row, else the row's item.
    private static string ShownOf(NeedsYouRowView row, NeedsYouSubject? subject) => subject?.DecisionId ?? row.Item ?? string.Empty;

    // The pending decision a decision row stands for (its DecisionId), when the list has it.
    private static DecisionView? PendingDecision(string? decisionId, DecisionList? decisions) =>
        decisionId is null ? null : decisions?.Decisions.FirstOrDefault(d => string.Equals(d.Id, decisionId, StringComparison.Ordinal));

    // A Decide on a decision row is one button per option the pending list offers; every other action is one button with its own catalog command.
    private static IEnumerable<NeedsYouAction> Actions(NeedsYouRowView row, NeedsYouSubject? subject, string id, Dictionary<string, (string Cli, bool Protected)> readiness, DecisionList? decisions)
    {
        var (ready, catalogCli, isProtected) = readiness.TryGetValue(id, out var listed) ? (ActionReadiness.Ready, listed.Cli, listed.Protected) : (ActionReadiness.Pending, "", false);
        var decision = string.Equals(id, Decide, StringComparison.Ordinal) ? PendingDecision(subject?.DecisionId, decisions) : null;
        if (decision is null)
        {
            var (cli, hint) = ActionCommand.For(catalogCli, string.Equals(id, row.PrimaryAction, StringComparison.Ordinal), row.Cli, subject, row.Item);
            yield return new NeedsYouAction(id, Labels.Action(id), ready, cli, RejectLike: false) { FillInHint = hint, IsProtected = isProtected };
            yield break;
        }

        foreach (var option in decision.Options)
        {
            yield return new NeedsYouAction(id, option.Label, ready, option.Cli, option.RejectLike) { IsProtected = isProtected, OptionId = option.Id, Recommended = string.Equals(option.Id, decision.Recommended, StringComparison.Ordinal) };
        }
    }

    // The app maps the served word to a look and never invents urgency: one it does not know still shows, as a pulse.
    private static NeedsYouUrgency UrgencyOf(string urgency) => urgency switch
    {
        "colour" => NeedsYouUrgency.Colour,
        "calm" => NeedsYouUrgency.Calm,
        _ => NeedsYouUrgency.Pulse,
    };

    // Until the API serves a chip on the row (U3'-API-9): the one word for the kind, from the chip table. Documented fallback, not a rule of the app.
    private static StateChip Derived(string kind) => kind switch
    {
        NeedsYouKinds.Decision => new StateChip("DECISION", ChipTone.Attn),
        NeedsYouKinds.Failure => new StateChip("FAILED", ChipTone.Fail),
        NeedsYouKinds.Profile => new StateChip("PROFILE", ChipTone.Attn),
        _ => new StateChip(kind.ToUpperInvariant(), ChipTone.Attn),
    };
}
