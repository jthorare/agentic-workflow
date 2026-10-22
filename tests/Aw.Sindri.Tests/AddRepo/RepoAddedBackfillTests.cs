using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The backfill of 'repo.added' (setup-once spec §5.1 "Re-run"): it looks in the ledger first, pages through it, writes nothing it cannot be sure of, and remembers what has its event.</summary>
[Lane(TestLane.Unit)]
public sealed class RepoAddedBackfillTests : IDisposable
{
    private readonly AddRepoRig _rig = new();

    private static CancellationToken Token => AddRepoRig.Token;

    public void Dispose() => _rig.Dispose();

    private RepoAddedBackfill Backfill(ILedgerReader reader, KnownRepoEvents? known = null) => new(_rig.Recorder, reader, _rig.Slice.Events, _rig.Git, known ?? new KnownRepoEvents());

    private IEnumerable<LedgerEvent> Recorded() => _rig.Slice.Store.Events.Where(static e => e.Envelope.Type == EventTypes.RepoAdded);

    [Fact]
    public async Task A_repo_without_the_event_gets_it_once_under_the_key_of_its_name()
    {
        _rig.Enter();   // in the test body: an async helper's AsyncLocal would be gone on return
        await _rig.Lease();
        var backfill = Backfill(_rig.Slice.Store);

        await backfill.Ensure(new RepoEntry("demo", "/work/demo"), Token);
        await backfill.Ensure(new RepoEntry("demo", "/work/demo"), Token);

        Assert.Equal(["repo.added:demo"], Recorded().Select(static e => e.IdempotencyKey), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_add_job_and_the_backfill_in_the_window_between_the_profile_write_and_the_jobs_append_leave_one_event()
    {
        _rig.Enter();   // in the test body: an async helper's AsyncLocal would be gone on return
        await _rig.Lease();
        var backfill = Backfill(_rig.Slice.Store);
        _rig.Writer.AfterAppend = entry => backfill.Ensure(entry, Token).AsTask();   // the tick lands after the profile write, before the job's append
        var request = new Features.AddRepo.AddRepoRequest("/work/demo");
        _ = await _rig.Admission().Admit(request, Token);

        var added = await _rig.Service().Handle(request, Token);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.Message : null);
        _ = Assert.Single(Recorded());
    }

    [Fact]
    public async Task A_repo_that_has_its_event_deep_in_a_long_ledger_gets_no_second()
    {
        var grant = await _rig.Slice.HoldLease();
        _rig.Enter();
        for (var i = 0; i < 600; i++)
        {
            var name = RepoName.From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"r{i}"));
            _ = await _rig.Slice.Store.Append(_rig.Slice.Events.Entry(EventTypes.RepoAdded, new RepoAdded(CommandId.From(RealTime.NewGuid()), name, "/w/" + name.Value), RepoEvents.KeyOf(name)), grant, Token);
        }

        await Backfill(_rig.Slice.Store).Ensure([new RepoEntry("r599", "/w/r599"), new RepoEntry("late", "/w/late")], Token);

        Assert.Equal(601, Recorded().Count());
        _ = Assert.Single(Recorded(), static e => e.IdempotencyKey == "repo.added:late");
    }

    [Fact]
    public async Task An_unreadable_ledger_writes_nothing()
    {
        _rig.Enter();   // in the test body: an async helper's AsyncLocal would be gone on return
        await _rig.Lease();

        await Backfill(new ReadFailingLedger(_rig.Slice.Store)).Ensure(new RepoEntry("demo", "/work/demo"), Token);

        Assert.Empty(Recorded());
    }

    [Fact]
    public async Task A_name_that_is_not_a_repo_name_is_skipped()
    {
        _rig.Enter();   // in the test body: an async helper's AsyncLocal would be gone on return
        await _rig.Lease();

        await Backfill(_rig.Slice.Store).Ensure(new RepoEntry("not a name", "/work/x"), Token);

        Assert.Empty(Recorded());
    }

    [Fact]
    public async Task A_repo_known_to_have_its_event_does_not_read_the_ledger_again()
    {
        _rig.Enter();   // in the test body: an async helper's AsyncLocal would be gone on return
        await _rig.Lease();
        var known = new KnownRepoEvents();
        await Backfill(_rig.Slice.Store, known).Ensure(new RepoEntry("demo", "/work/demo"), Token);

        await Backfill(new ReadFailingLedger(_rig.Slice.Store), known).Ensure(new RepoEntry("demo", "/work/demo"), Token);

        Assert.True(known.Has("demo"));
        _ = Assert.Single(Recorded());
    }

    [Fact]
    public async Task A_failed_append_is_not_remembered_so_the_next_try_writes_it()
    {
        _rig.Enter();
        var known = new KnownRepoEvents();
        var backfill = Backfill(_rig.Slice.Store, known);

        await backfill.Ensure(new RepoEntry("demo", "/work/demo"), Token);   // no lease yet
        Assert.False(known.Has("demo"));
        Assert.Empty(Recorded());

        await _rig.Lease();
        await backfill.Ensure(new RepoEntry("demo", "/work/demo"), Token);

        _ = Assert.Single(Recorded());
        Assert.True(known.Has("demo"));
    }
}
