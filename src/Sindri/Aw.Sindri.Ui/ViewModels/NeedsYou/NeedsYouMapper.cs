using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;

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
    /// <param name="readAt">When the list was read: a row's served time left is counted from it. Default for unknown, which leaves the rows' timeouts out.</param>
    /// <returns>The feed, rows in the API's order.</returns>
    internal static NeedsYouFeed Map(NeedsYouList list, CommandList catalog, DecisionList? decisions, HomeCounts counts, DateTimeOffset readAt = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(catalog);
        var readiness = catalog.Rows.ToDictionary(r => r.Action, r => (ReadinessOf(r.Ui), CatalogReadiness.Parse(r.Ui).Stage, r.Cli), StringComparer.Ordinal);
        var read = catalog.Rows.Count > 0;
        return new NeedsYouFeed([.. list.Rows.Select(row => Entry(row, readiness, decisions, read, readAt))], list.Empty ?? string.Empty)
        {
            Stalled = list.AsOf.Stalled,
            Rebuilding = list.AsOf.Rebuilding,
            Counts = counts,
        };
    }

    // The catalog's ui word, read once for the whole app (CatalogReadiness): anything it does not know cannot run.
    internal static ActionReadiness ReadinessOf(string ui) => CatalogReadiness.Parse(ui).Kind switch
    {
        ControlKind.Ready => ActionReadiness.Ready,
        ControlKind.Pending => ActionReadiness.Pending,
        ControlKind.CliOnly => ActionReadiness.CliOnly,
        _ => ActionReadiness.CliOnly,
    };

    private static NeedsYouEntry Entry(NeedsYouRowView row, Dictionary<string, (ActionReadiness Readiness, string Stage, string Cli)> readiness, DecisionList? decisions, bool read, DateTimeOffset readAt)
    {
        var subject = row.Subject is { } s ? new NeedsYouSubject(s.Kind, s.Id) : null;
        row = Unanswerable(row);
        return new NeedsYouEntry(row.Id, UrgencyOf(row.Urgency))
        {
            Kind = row.Kind,
            Chip = row.Chip ?? Derived(row.Kind),
            ShownId = ShownOf(row, subject),
            Title = row.Title,
            Repo = row.Repo ?? string.Empty,
            Label = row.Label,
            Reason = row.Reason,
            Since = row.Since,
            PrimaryAction = row.PrimaryAction ?? string.Empty,
            Actions = [.. row.Actions.SelectMany(id => Actions(row, subject, id, readiness, decisions).Select(a => a with { CatalogRead = read }))],
            Subject = subject,
            ItemId = row.Item,
            Cli = row.Cli,
            Via = row.Via,
            Child = ChildOf(row),
            RecordedOnly = row.RecordedOnly,
            Notice = row.Notice ?? string.Empty,
            Timeout = TimeoutOf(row, readAt),
            Stop = row.Stop,
            Open = row.Open,
            Question = PendingDecision(subject?.DecisionId, decisions)?.Question ?? string.Empty,
        };
    }

    // The served time left, counted from when the list was read; none without the read time.
    private static RowTimeout? TimeoutOf(NeedsYouRowView row, DateTimeOffset readAt) =>
        row.Timeout is { } timeout && readAt != default ? new RowTimeout(timeout.Kind, readAt.AddSeconds(timeout.InSeconds)) : null;

    // The child's address a via child row names; a row that is not via child names none, whatever it carries.
    private static string? ChildOf(NeedsYouRowView row) => InstanceRules.IsChildVia(row.Via) && row.Child is { Length: > 0 } child ? child : null;

    // A child pointer has no answer control and no command to copy: its served action and cli are the engine's placeholder, so the row drops them.
    private static NeedsYouRowView Unanswerable(NeedsYouRowView row) =>
        InstanceRules.IsChildVia(row.Via) ? row with { PrimaryAction = null, Actions = [], Cli = string.Empty } : row;

    // The id shown at rest: the decision's own on a decision row, else the row's item.
    private static string ShownOf(NeedsYouRowView row, NeedsYouSubject? subject) => subject?.DecisionId ?? row.Item ?? string.Empty;

    // The pending decision a decision row stands for (its DecisionId), when the list has it.
    private static DecisionView? PendingDecision(string? decisionId, DecisionList? decisions) =>
        decisionId is null ? null : decisions?.Decisions.FirstOrDefault(d => string.Equals(d.Id, decisionId, StringComparison.Ordinal));

    // A Decide on a decision row is one button per option the pending list offers; every other action is one button with its own catalog command.
    private static IEnumerable<NeedsYouAction> Actions(NeedsYouRowView row, NeedsYouSubject? subject, string id, Dictionary<string, (ActionReadiness Readiness, string Stage, string Cli)> readiness, DecisionList? decisions)
    {
        var (ready, stage, catalogCli) = readiness.GetValueOrDefault(id, (ActionReadiness.Pending, "", ""));
        var decision = string.Equals(id, Decide, StringComparison.Ordinal) ? PendingDecision(subject?.DecisionId, decisions) : null;
        if (decision is null)
        {
            var (cli, hint) = ActionCommand.For(catalogCli, string.Equals(id, row.PrimaryAction, StringComparison.Ordinal), row.Cli, subject, row.Item);
            yield return new NeedsYouAction(id, Labels.Action(id), ready, cli, RejectLike: false) { Stage = stage, FillInHint = hint };
            yield break;
        }

        foreach (var option in decision.Options)
        {
            yield return new NeedsYouAction(id, option.Label, ready, option.Cli, option.RejectLike) { Stage = stage, OptionId = option.Id, Recommended = string.Equals(option.Id, decision.Recommended, StringComparison.Ordinal) };
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
        NeedsYouKinds.Park => new StateChip("PARKED", ChipTone.Attn),
        _ => new StateChip(kind.ToUpperInvariant(), ChipTone.Attn),
    };
}
