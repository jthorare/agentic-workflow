using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// What the Needs-you view models need of a row: its id, urgency, chip, texts, actions and subject. A UI input record of the
/// app's own, so they don't depend on the API's row (the contract unit maps the served rows onto it); the app never sorts
/// or filters what it is given. Only <see cref="Id"/> and <see cref="Urgency"/> are required: the seen and pulse view models
/// need no more.
/// </summary>
/// <param name="Id">The row's id, the key of its seen state and of its selection.</param>
/// <param name="Urgency">The API's urgency.</param>
internal sealed record NeedsYouEntry(string Id, NeedsYouUrgency Urgency)
{
    /// <summary>The row's chip as served (D5: the chip replaces the label and the urgency word); null for none.</summary>
    internal StateChip? Chip { get; init; }

    /// <summary>The row's kind as served (decision, failure, profile, park); empty for a row built without one.</summary>
    internal string Kind { get; init; } = "";

    /// <summary>The id shown at rest and named to screen readers (an item, request or service id); empty when the row shows none.</summary>
    internal string ShownId { get; init; } = "";

    /// <summary>The row's title.</summary>
    internal string Title { get; init; } = "";

    /// <summary>The repo tag; empty for none.</summary>
    internal string Repo { get; init; } = "";

    /// <summary>The served label, the row's secondary line ("failure · heartbeat").</summary>
    internal string Label { get; init; } = "";

    /// <summary>The served reason sentence (why a failure failed, what a profile row changed); the sheets show it.</summary>
    internal string Reason { get; init; } = "";

    /// <summary>When the row began waiting; the age is read from it. Default for unknown.</summary>
    internal DateTimeOffset Since { get; init; }

    /// <summary>What the row's timeout does and when it fires, dated from when the list was read; null when the row serves none.</summary>
    internal RowTimeout? Timeout { get; init; }

    /// <summary>Which desk stop of its run the row waits at ("2 of 3") and that stop's state; null when the row serves none.</summary>
    internal NeedsYouStopView? Stop { get; init; }

    /// <summary>The one place the owner acts on the row (api q65): where a click goes, and the button's words; null from a Sindri that serves none.</summary>
    internal NeedsYouOpenView? Open { get; init; }

    /// <summary>The catalog id of the row's primary action; empty for none.</summary>
    internal string PrimaryAction { get; init; } = "";

    /// <summary>The row's actions as served, each with its catalog id, readiness and cli.</summary>
    internal IReadOnlyList<NeedsYouAction> Actions { get; init; } = [];

    /// <summary>The served subject; null when the API serves none (a profile row before its owner decision).</summary>
    internal NeedsYouSubject? Subject { get; init; }

    /// <summary>The decision this row stands for (its subject's id when the subject is a decision), null when it is not a decision row; every "is this a decision row" test reads this.</summary>
    internal string? DecisionId => Subject?.DecisionId;

    /// <summary>The item the row names, when it names one.</summary>
    internal string? ItemId { get; init; }

    /// <summary>The one served command to copy; empty for none.</summary>
    internal string Cli { get; init; } = "";

    /// <summary>The served via of a row whose answer a child instance decides ("child"); null for any other row.</summary>
    internal string? Via { get; init; }

    /// <summary>The address of the child instance that decides the answer, served on a via child row; null when none is served.</summary>
    internal string? Child { get; init; }

    /// <summary>What a decision row's decision asks, as the pending list serves it; empty for any other row.</summary>
    internal string Question { get; init; } = "";

    /// <summary>True when the decision is only recorded now and acted on later.</summary>
    internal bool RecordedOnly { get; init; }

    /// <summary>The served recorded-only sentence; empty for none.</summary>
    internal string Notice { get; init; } = "";

    /// <summary>Value equality, the action list by its contents: a re-read serves new records for rows that did not change, and the row must see that.</summary>
    /// <param name="other">The other entry.</param>
    /// <returns>True when every field is equal.</returns>
    public bool Equals(NeedsYouEntry? other) =>
        other is not null
        && ((Id, Urgency, Kind, Chip, ShownId, Title, Repo, Label, Reason) == (other.Id, other.Urgency, other.Kind, other.Chip, other.ShownId, other.Title, other.Repo, other.Label, other.Reason))
        && ((Since, PrimaryAction, Subject, ItemId, Cli, Question, RecordedOnly, Notice, Via, Child, Timeout, Stop, Open) == (other.Since, other.PrimaryAction, other.Subject, other.ItemId, other.Cli, other.Question, other.RecordedOnly, other.Notice, other.Via, other.Child, other.Timeout, other.Stop, other.Open))
        && Actions.SequenceEqual(other.Actions);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Urgency, Title, Reason, Since);
}
