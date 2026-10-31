using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.CheckProfile;

/// <summary>Sindri's own profile writes record a profile check at once (#1826): nothing without the lease, a failed check never fails the write, one check at a time, and each writer port checks only after a write that worked.</summary>
public sealed class ProfileRecheckTests
{
    private readonly SliceHarness _h = new();
    private readonly ListLogger<ProfileRecheck> _log = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProfileRecheck Recheck(Action<IServiceCollection>? replace = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IProfileSource>(_h.Profiles)
            .AddSingleton<IApprovedProfile>(_h.Profiles)
            .AddSingleton<ILedgerStore>(_h.Store)
            .AddSingleton(_h.Events)
            .AddSingleton(_h.Callers)
            .AddSingleton<IIdGenerator>(_h.Ids)
            .AddScoped<IService<CheckProfileRequest, CheckProfileResponse>, CheckProfileService>();
        replace?.Invoke(services);
        return new ProfileRecheck(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _h.Held, _h.Callers, _log);
    }

    private async Task Drifted()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("30s"), Token);
        _h.Profiles.Live = SliceHarness.Profile("5s");
    }

    private int Checks() => _h.Store.Events.Count(e => e.Envelope.Type == EventTypes.ProfileChecked);

    [Fact, Trait("Lane", "unit")]
    public async Task Without_the_lease_nothing_is_recorded()
    {
        await Drifted();

        await Recheck().Now(Token);

        Assert.Equal(0, Checks());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_the_lease_a_check_is_recorded_as_the_profile_watch()
    {
        await Drifted();
        _ = await _h.HoldLease();

        await Recheck().Now(Token);

        var recorded = Assert.Single(_h.Store.Events, e => e.Envelope.Type == EventTypes.ProfileChecked);
        Assert.Equal("service:profile-watch", recorded.Envelope.Actor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_check_that_throws_or_fails_is_logged_once_and_never_thrown()
    {
        _ = await _h.HoldLease();
        var recheck = Recheck(s => s.AddScoped<IService<CheckProfileRequest, CheckProfileResponse>>(_ => new ThrowingCheck()));

        await recheck.Now(Token);
        await recheck.Now(Token);

        var line = Assert.Single(_log.Lines);
        Assert.Contains("boom", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Checks_run_one_at_a_time()
    {
        _ = await _h.HoldLease();
        var slow = new SlowCheck();
        var recheck = Recheck(s => s.AddScoped<IService<CheckProfileRequest, CheckProfileResponse>>(_ => slow));

        var first = recheck.Now(Token).AsTask();
        await slow.Entered.Task.WaitAsync(Token);
        var second = recheck.Now(Token).AsTask();
        await Task.Delay(100, Token);
        Assert.Equal(1, slow.Started);   // the second waits at the gate
        slow.Release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal((2, 1), (slow.Started, slow.MostAtOnce));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_writer_port_checks_after_a_write_that_worked_and_not_after_one_that_failed()
    {
        await Drifted();
        _ = await _h.HoldLease();
        var recheck = Recheck();
        var good = new Writers(fail: false);
        var bad = new Writers(fail: true);

        _ = await new RecheckingProfileIndexWriter(bad, recheck).Write([], ifHash: null, Token);
        _ = await new RecheckingProfileRepoWriter(bad, recheck).Append(new RepoEntry("a", "/a"), [], Token);
        _ = await new RecheckingProfileSandboxWriter(bad, recheck).Allow("a", DenialKind.Network, "github.com", Token);
        Assert.Equal(0, Checks());

        _ = await new RecheckingProfileSandboxWriter(good, recheck).Allow("a", DenialKind.Network, "github.com", Token);

        Assert.Equal(1, Checks());   // the allow's write changed the pair: one check, recorded
        _ = await new RecheckingProfileIndexWriter(good, recheck).Write([], ifHash: null, Token);
        _ = await new RecheckingProfileRepoWriter(good, recheck).Append(new RepoEntry("a", "/a"), [], Token);
        Assert.Equal(1, Checks());   // the pair didn't change again: the check ran and recorded nothing new
        Assert.Equal(3, good.Calls);
    }

    private sealed class ThrowingCheck : IService<CheckProfileRequest, CheckProfileResponse>
    {
        public ValueTask<Result<CheckProfileResponse>> Handle(CheckProfileRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class SlowCheck : IService<CheckProfileRequest, CheckProfileResponse>
    {
        private int _now;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Started { get; private set; }

        public int MostAtOnce { get; private set; }

        public async ValueTask<Result<CheckProfileResponse>> Handle(CheckProfileRequest request, CancellationToken cancellationToken)
        {
            Started++;
            MostAtOnce = Math.Max(MostAtOnce, Interlocked.Increment(ref _now));
            _ = Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            _ = Interlocked.Decrement(ref _now);
            return new CheckProfileResponse(Sequence: null);
        }
    }

    // One fake behind every writer port: it succeeds or fails every call.
    private sealed class Writers(bool fail) : IProfileIndexWriter, IProfileRepoWriter, IProfileSandboxWriter
    {
        public int Calls { get; private set; }

        public ValueTask<Result<ProfileHash>> Write(IReadOnlyList<IndexEdit> edits, ProfileHash? ifHash, CancellationToken cancellationToken) =>
            Done(ProfileHash.From(new string('a', 64)));

        public ValueTask<Result<RepoAppended>> Append(RepoEntry repo, IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken) =>
            Done(RepoAppended.Appended);

        public ValueTask<Result<SandboxAllowed>> Allow(string repo, DenialKind kind, string target, CancellationToken cancellationToken) =>
            Done(new SandboxAllowed("github.com", Added: true));

        private ValueTask<Result<T>> Done<T>(T value) => ValueTask.FromResult(Now(value));

        private Result<T> Now<T>(T value)
        {
            Calls++;
            return fail ? Core.SindriErrors.ServiceFailed.With("no") : value;
        }
    }
}
