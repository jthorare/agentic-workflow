using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IDecisionLog contract: the same tests run against the in-memory fake and every real adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies a fresh, empty log and one whose store can't be used.
/// </summary>
public abstract partial class DecisionLogContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty log.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The log.</returns>
    protected abstract ValueTask<IDecisionLog> Create(CancellationToken cancellationToken);

    /// <summary>A log whose store can't be opened (a read-only or missing location).</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The log.</returns>
    protected abstract ValueTask<IDecisionLog> CreateUnavailable(CancellationToken cancellationToken);

    /// <summary>A recorded decision reads back with every field and its detail row.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_recorded_decision_reads_back_with_its_details()
    {
        var log = await Create(_none);
        var decision = A.Decision().Build();

        Expect.That((await log.Record(decision, _none)).IsSuccess, "a new decision is recorded");

        var found = await log.Find(decision.Id, _none);
        Expect.That(found.IsSuccess && DecisionEquality.Same(decision, found.Value), "it reads back with every field and its details");
    }

    /// <summary>Optional fields left empty read back empty: no decision, no skipped providers, bare details.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Empty_optional_fields_read_back_empty()
    {
        var log = await Create(_none);
        var decision = A.Decision().WithOutcome(DecisionOutcome.Escalated).WithNothingTried().WithBareDetails().Build();

        Expect.That((await log.Record(decision, _none)).IsSuccess, "an escalated decision is recorded");

        var found = await log.Find(decision.Id, _none);
        Expect.That(found.IsSuccess && DecisionEquality.Same(decision, found.Value), "null decision, empty skipped list and bare details read back as they were");
    }

    /// <summary>A decision recorded without a detail row reads back without one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_decision_without_details_reads_back_without_them()
    {
        var log = await Create(_none);
        var decision = A.Decision().WithoutDetails().Build();

        Expect.That((await log.Record(decision, _none)).IsSuccess, "a decision without details is recorded");

        var found = await log.Find(decision.Id, _none);
        Expect.That(found.IsSuccess && DecisionEquality.Same(decision, found.Value), "it reads back without details");
    }

    /// <summary>Recording an id twice is AW-JUDGE-003 and keeps the first decision.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Recording_an_id_twice_is_a_conflict_and_keeps_the_first()
    {
        var log = await Create(_none);
        var first = A.Decision().Build();
        _ = await log.Record(first, _none);

        var again = await log.Record(first with { ReasonCode = "second", Details = null }, _none);

        Expect.That(again.IsError && again.Error.Code == JudgeErrors.DecisionExists.Code, "a second record with the same id is AW-JUDGE-003");
        var found = await log.Find(first.Id, _none);
        Expect.That(found.IsSuccess && DecisionEquality.Same(first, found.Value), "the first decision is kept unchanged");
    }

    /// <summary>Finding an unknown id is AW-JUDGE-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Finding_an_unknown_id_is_an_unknown_decision()
    {
        var log = await Create(_none);

        var found = await log.Find(DecisionId.From("missing"), _none);

        Expect.That(found.IsError && found.Error.Code == JudgeErrors.UnknownDecision.Code, "an unknown id is AW-JUDGE-002");
    }

    /// <summary>Undo sets the undone time and changes nothing else.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Undo_sets_the_undone_time()
    {
        var log = await Create(_none);
        var decision = A.Decision().Build();
        _ = await log.Record(decision, _none);
        var at = KnownInstants.Epoch.AddMinutes(5);

        Expect.That((await log.MarkUndone(decision.Id, at, _none)).IsSuccess, "undo succeeds on a known id");

        var found = await log.Find(decision.Id, _none);
        Expect.That(found.IsSuccess && DecisionEquality.Same(decision with { UndoneAt = at }, found.Value), "only undone_at changed");
    }

    /// <summary>Approve sets the approved time and changes nothing else; approving again keeps the first time.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Approve_sets_the_approved_time_once()
    {
        var log = await Create(_none);
        var decision = A.Decision().WithOutcome(DecisionOutcome.Escalated).Build();
        _ = await log.Record(decision, _none);
        var first = KnownInstants.Epoch.AddMinutes(5);

        Expect.That((await log.MarkApproved(decision.Id, first, _none)).IsSuccess, "approve succeeds on a known id");
        Expect.That((await log.MarkApproved(decision.Id, first.AddMinutes(1), _none)).IsSuccess, "approving again succeeds");

        var found = await log.Find(decision.Id, _none);
        Expect.That(found.IsSuccess && DecisionEquality.Same(decision with { ApprovedAt = first }, found.Value), "only approved_at changed, to the first approval's time");
    }

    /// <summary>Undo and approve on an unknown id are AW-JUDGE-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Undo_and_approve_on_an_unknown_id_are_unknown_decisions()
    {
        var log = await Create(_none);
        var missing = DecisionId.From("missing");

        var undo = await log.MarkUndone(missing, KnownInstants.Epoch, _none);
        var approve = await log.MarkApproved(missing, KnownInstants.Epoch, _none);

        Expect.That(undo.IsError && undo.Error.Code == JudgeErrors.UnknownDecision.Code, "undo on an unknown id is AW-JUDGE-002");
        Expect.That(approve.IsError && approve.Error.Code == JudgeErrors.UnknownDecision.Code, "approve on an unknown id is AW-JUDGE-002");
    }
}
