using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>
/// Where the eval items and their labels are kept (the 'eval_items' and 'labels' tables, migration 0004), and the reads of
/// the decision log the labelling verbs start from. Every call returns an error, never throws, when the store can't be used.
/// </summary>
public interface IEvalStore
{
    /// <summary>Adds an item unless one with the same <see cref="EvalItem.Source"/> exists.</summary>
    /// <param name="item">The item, already redacted and capped.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the item was added, false when its source was already imported.</returns>
    public ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken);

    /// <summary>Finds an item.</summary>
    /// <param name="id">The item id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item, or AW-JUDGE-014 when there is none.</returns>
    public ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken);

    /// <summary>Gives an item a label from a source, replacing that source's earlier label (the other sources' labels stay).</summary>
    /// <param name="item">The item.</param>
    /// <param name="source">Who gives the label.</param>
    /// <param name="label">One of the question's outputs, or 'skip'.</param>
    /// <param name="at">When the label was given.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or AW-JUDGE-014 when there is no such item.</returns>
    public ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>The oldest item with no label (from the source, when one is given), oldest first by creation and then id.</summary>
    /// <param name="question">Only this question's items, or null for every question.</param>
    /// <param name="source">Unlabeled by this source (a label from another source does not count), or null for unlabeled by every source.</param>
    /// <param name="exclude">Items to pass over.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item, or null when none is left.</returns>
    public ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken);

    /// <summary>One question's labelled items, oldest first, with their strongest label each; 'skip' labels are left out.</summary>
    /// <param name="question">The question.</param>
    /// <param name="source">Only labels from this source, or null for any (the strongest of an item's labels wins: override, outcome, adjudicator).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The labelled items.</returns>
    public ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken);

    /// <summary>Counts every question's items, labelled and skipped.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One count per question that has items, by question name.</returns>
    public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken);

    /// <summary>How often a question's outcome and adjudicator labels agree.</summary>
    /// <param name="question">The question.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The shared and agreed counts.</returns>
    public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken);

    /// <summary>The decided decisions that kept their details, from a time on, oldest first: what 'label import' turns into items.</summary>
    /// <param name="question">Only this question, or null for every question.</param>
    /// <param name="since">The start of the window, inclusive.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The decisions with their stored inputs.</returns>
    public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>The 'ask-check' items made from a decision (with its details) that have no outcome label yet: what 'label outcomes' works on.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The candidates, oldest first.</returns>
    public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken);
}
