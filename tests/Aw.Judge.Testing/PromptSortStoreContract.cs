using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IPromptSortStore contract: the same tests run against the in-memory fake and every real adapter. A derived class
/// supplies a fresh, empty store in which a decision <see cref="Decision"/> is already recorded (the real adapter
/// references the decisions table), and one that can't be used.
/// </summary>
public abstract class PromptSortStoreContract
{
    /// <summary>The decision every contract record belongs to; the store a derived class returns already has it.</summary>
    protected static readonly DecisionId Decision = DecisionId.From("sort-1");

    private static readonly PromptSortRecord _record = new(
        Decision,
        [
            new SortedAxis("touchesUi", "true", "false", "blend", "overrode", Probability: 0.9, Confidence: 0.8),
            new SortedAxis("complexity", "M", "S", "heuristic", "undecided", Probability: null, Confidence: null),
        ],
        new SortRun("blend", "sorted", ["brief"], [], ["""{"id":"bugfix","reason":"cooldown"}"""]));

    private static bool SameRun(SortRun a, SortRun b) =>
        string.Equals(a.Mode, b.Mode, StringComparison.Ordinal) && string.Equals(a.Reason, b.Reason, StringComparison.Ordinal) && a.WouldFire.SequenceEqual(b.WouldFire, StringComparer.Ordinal) && a.Fired.SequenceEqual(b.Fired, StringComparer.Ordinal) && a.Suppressed.SequenceEqual(b.Suppressed, StringComparer.Ordinal);

    /// <summary>A fresh store with <see cref="Decision"/> recorded.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IPromptSortStore> Create(CancellationToken cancellationToken);

    /// <summary>A store that can't be opened.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IPromptSortStore> CreateUnavailable(CancellationToken cancellationToken);

    /// <summary>A saved record reads back with every field, its axes ordered by axis name.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_saved_record_reads_back_with_its_axes_ordered_by_name()
    {
        var store = await Create(CancellationToken.None);

        Expect.That((await store.Save(_record, CancellationToken.None)).IsSuccess, "a record is saved");

        var found = await store.Find(Decision, CancellationToken.None);
        Expect.That(found.IsSuccess, "it is found");
        Expect.That(found.Value.Axes.Select(a => a.Axis).SequenceEqual(["complexity", "touchesUi"], StringComparer.Ordinal), "the axes are ordered by name");
        Expect.That(found.Value.Axes.SequenceEqual(_record.Axes.OrderBy(a => a.Axis, StringComparer.Ordinal)), "every axis field survives, nulls included");
        Expect.That(SameRun(found.Value.Run, _record.Run), "the run survives");
    }

    /// <summary>Saving a decision's record again replaces it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Saving_again_replaces_the_record()
    {
        var store = await Create(CancellationToken.None);
        _ = await store.Save(_record, CancellationToken.None);

        var second = _record with { Axes = [_record.Axes[1]], Run = _record.Run with { Reason = "all-undecided" } };
        Expect.That((await store.Save(second, CancellationToken.None)).IsSuccess, "a second save succeeds");

        var found = await store.Find(Decision, CancellationToken.None);
        Expect.That(found.IsSuccess && found.Value.Axes.Count == 1 && string.Equals(found.Value.Run.Reason, "all-undecided", StringComparison.Ordinal), "only the second record is kept");
    }

    /// <summary>A decision the sorter kept nothing for is AW-JUDGE-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_decision_with_no_record_is_unknown()
    {
        var store = await Create(CancellationToken.None);

        var found = await store.Find(DecisionId.From("never-sorted"), CancellationToken.None);

        Expect.That(found.IsError && found.Error.Code == JudgeErrors.UnknownDecision.Code, "AW-JUDGE-002");
    }

    /// <summary>A store that can't be opened answers every call with an error and throws nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unusable_store_answers_with_errors()
    {
        var store = await CreateUnavailable(CancellationToken.None);

        Expect.That((await store.Save(_record, CancellationToken.None)).IsError, "Save is an error");
        Expect.That((await store.Find(Decision, CancellationToken.None)).IsError, "Find is an error");
    }
}
