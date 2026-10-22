using System.Reflection;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers the five calls a map refresh makes from scripted results, logs them in order and refuses every other call.</summary>
public class RefreshEngineProxy : DispatchProxy
{
    /// <summary>The calls made, in order.</summary>
    public IList<string> Log { get; } = [];

    /// <summary>What <see cref="IContextEngine.MapHeads"/> answers.</summary>
    public Result<IReadOnlyList<RepoHead>> Heads { get; set; } = new List<RepoHead> { new(ContextRepoName.From("app"), "abc123", OnDefault: true) };

    /// <summary>What <see cref="IContextMap.BuildMap"/> answers.</summary>
    public Result<MapBuildReport> Built { get; set; } = new MapBuildReport(Version(2, MapVersionStatus.Draft), [], [], [new MapHead(ContextRepoName.From("app"), "abc123", OnDefault: true)]);

    /// <summary>What <see cref="IContextMap.ReviewQueue"/> answers.</summary>
    public Result<ReviewQueueView> Queue { get; set; } = new ReviewQueueView("v2@abc123", ReviewVersion.None, [], 0, 0, new QueueCounts(3, 0, 0), new Dictionary<string, int>(StringComparer.Ordinal), ReviewTruncated.None);

    /// <summary>What <see cref="IContextMap.CleanDraft"/> answers.</summary>
    public Result<CleanupReport> Cleaned { get; set; } = new CleanupReport(5, 2, 1, CleanupMode.Agent);

    /// <summary>What <see cref="IContextMap.Promote"/> answers.</summary>
    public Result<MapPromoted> Promoted { get; set; } = new MapPromoted(Version(2, MapVersionStatus.Current), Version(1, MapVersionStatus.Retained));

    /// <summary>What <see cref="IContextMap.Held"/> answers: a draft v2 over a current v1, no hold.</summary>
    public Result<MapPublishState> State { get; set; } = new MapPublishState(Hold: null, LatestDraft: 2, Current: 1, CurrentHeads: []);

    /// <summary>The start of the wait an earlier hold of the chain began, or null: <see cref="IContextMap.Hold"/> answers it as the hold's start.</summary>
    public DateTimeOffset? Chain { get; set; }

    /// <summary>What <see cref="IContextMap.Hold"/> answers instead of echoing the hold back, when set.</summary>
    public Result<MapHold>? HoldFailure { get; set; }

    /// <summary>The holds asked for, in order.</summary>
    public IList<MapHold> Holds { get; } = [];

    /// <summary>The versions released, in order.</summary>
    public IList<int> Released { get; } = [];

    /// <summary>The expectedVersion each cleanup and promote carried, in order.</summary>
    public IList<int?> Expected { get; } = [];

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static RefreshEngineProxy Create() => (RefreshEngineProxy)(object)Create<IContextEngine, RefreshEngineProxy>();

    private static MapVersion Version(int number, MapVersionStatus status) => new(number, ContextRepoName.From("app"), "abc123", KeyVersion: 1, DateTimeOffset.UnixEpoch, status);

    private Result<T> Expect<T>(object?[]? args, Result<T> answer)
    {
        Expected.Add((int?)args![2]);
        return answer;
    }

    private Result<MapHold> Hold(MapHold hold)
    {
        Holds.Add(hold);
        return HoldFailure ?? (hold with { Since = Chain is { } since && since < hold.Since ? since : hold.Since });
    }

    private Result<Unit> Release(int version)
    {
        Released.Add(version);
        return Unit.Value;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var name = targetMethod?.Name;
        Log.Add(name ?? string.Empty);
        return name switch
        {
            nameof(IContextEngine.MapHeads) => new ValueTask<Result<IReadOnlyList<RepoHead>>>(Heads),
            nameof(IContextEngine.BuildMap) => new ValueTask<Result<MapBuildReport>>(Built),
            nameof(IContextEngine.ReviewQueue) => new ValueTask<Result<ReviewQueueView>>(Queue),
            nameof(IContextEngine.CleanDraft) => new ValueTask<Result<CleanupReport>>(Expect(args, Cleaned)),
            nameof(IContextEngine.Promote) => new ValueTask<Result<MapPromoted>>(Expect(args, Promoted)),
            nameof(IContextEngine.Held) => new ValueTask<Result<MapPublishState>>(State),
            nameof(IContextEngine.Hold) => new ValueTask<Result<MapHold>>(Hold((MapHold)args![0]!)),
            nameof(IContextEngine.Release) => new ValueTask<Result<Unit>>(Release((int)args![0]!)),
            _ => throw new NotSupportedException(name),
        };
    }
}
