using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.PrWatch;

/// <summary>The poller on fakes: a clock, the watched pull requests, the forge's digests, the state file in memory and the real ledger store in memory.</summary>
internal sealed class PrWatchRig
{
    public const string Link = "https://forge.test/acme/widgets/pull/7";

    private readonly SliceHarness _h = new();
    private readonly InMemoryFactStore _facts = new();
    private LeaseGrant? _grant;

    /// <param name="facts">Where 'fact:prEvent' is stored; the in-memory store when null.</param>
    public PrWatchRig(IFactStore? facts = null)
    {
        Service = new PrWatchService(Watched, Digests, State, new PrEventWriter(_h.Store, _h.Events, facts ?? _facts, _h.Clock), Remind, _h.Clock);
    }

    public FakeWatchedPulls Watched { get; } = new();

    public FakeDigests Digests { get; } = new();

    public MemoryPrWatchState State { get; } = new();

    public FixedRemind Remind { get; } = new(TimeSpan.FromDays(14));

    public PrWatchService Service { get; }

    public Microsoft.Extensions.Time.Testing.FakeTimeProvider Clock => _h.Clock;

    /// <summary>One scheduled run at the clock's now.</summary>
    public async Task<Result<ScheduledResult>> Poll()
    {
        _grant ??= await _h.HoldLease();
        return await Service.Run(new ScheduledRun(_grant, _h.Clock.GetUtcNow()), TestContext.Current.CancellationToken);
    }

    /// <summary>The 'pr.changed' events appended so far.</summary>
    public async Task<IReadOnlyList<LedgerEvent>> Changed()
    {
        var all = await _h.Store.ReadAfter(0, 1000, TestContext.Current.CancellationToken);
        return [.. all.Value.Where(static e => string.Equals(e.Envelope.Type, "pr.changed", StringComparison.Ordinal))];
    }

    /// <summary>The outcome an appended event carries.</summary>
    public static string Outcome(LedgerEvent row)
    {
        using var payload = JsonDocument.Parse(row.Envelope.Payload);
        return payload.RootElement.GetProperty("outcome").GetString()!; // why: every pr.changed carries one
    }

    /// <summary>The stored 'fact:prEvent' an appended event carries.</summary>
    public JsonDocument PrEvent(LedgerEvent row)
    {
        using var payload = JsonDocument.Parse(row.Envelope.Payload);
        var sha = payload.RootElement.GetProperty("facts").GetProperty("fact:prEvent").GetProperty("sha256").GetString()!; // why: every pr.changed carries it
        return JsonDocument.Parse(Encoding.UTF8.GetString(_facts.Stored[sha]));
    }
}

/// <summary>The digests the forge answers: a row per link, or an error every call.</summary>
internal sealed class FakeDigests : IPullDigests
{
    private readonly Dictionary<string, PullDigestRow> _rows = new(StringComparer.Ordinal);

    /// <summary>The links of each call, in order.</summary>
    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>The error every call answers, or null.</summary>
    public Error? Failure { get; set; }

    public void Set(string link, string version, PullState state = PullState.Open, string head = "aaa", PullChecksState checks = PullChecksState.Pending, int comments = 0) =>
        _rows[link] = new PullDigestRow(link, state, head, checks, comments, version);

    public ValueTask<Result<IReadOnlyList<PullDigestRow>>> Digest(IReadOnlyList<string> links, CancellationToken cancellationToken)
    {
        Calls.Add(links);
        if (Failure is { } error)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<PullDigestRow>>>(error);
        }

        IReadOnlyList<PullDigestRow> rows = [.. links.Where(_rows.ContainsKey).Select(l => _rows[l])];
        return ValueTask.FromResult(Results.Success(rows));
    }
}

internal sealed class MemoryPrWatchState : IPrWatchState
{
    public PrWatchFile Last { get; private set; } = PrWatchFile.Empty;

    public ValueTask<Result<PrWatchFile>> Read(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Last));

    public ValueTask<Result<Unit>> Write(PrWatchFile file, CancellationToken cancellationToken)
    {
        Last = file;
        return ValueTask.FromResult(Results.Success(Unit.Value));
    }
}

internal sealed class FixedRemind(TimeSpan after) : IPrRemindAfter
{
    public TimeSpan After { get; set; } = after;

    public ValueTask<TimeSpan> Get(CancellationToken cancellationToken) => ValueTask.FromResult(After);
}
