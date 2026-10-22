using Aw.Kernel;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IIndexOutlooks"/> that answers the same outlook for every repo of a hold, or each repo's own, and counts the asks.</summary>
internal sealed class ScriptedOutlooks : IIndexOutlooks
{
    /// <summary>The outlook of every repo with no entry in <see cref="ByRepo"/>.</summary>
    internal IndexOutlook All { get; set; } = IndexOutlook.Absent;

    /// <summary>Per repo overrides.</summary>
    internal Dictionary<string, IndexOutlook> ByRepo { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, the read fails with it.</summary>
    internal Error? Failure { get; set; }

    /// <summary>How many holds were read.</summary>
    internal int Reads { get; private set; }

    public ValueTask<Result<IReadOnlyDictionary<string, IndexOutlookDetail>>> Read(MapHoldState hold, CancellationToken cancellationToken)
    {
        Reads++;
        if (Failure is { } error)
        {
            return ValueTask.FromResult<Result<IReadOnlyDictionary<string, IndexOutlookDetail>>>(error);
        }

        IReadOnlyDictionary<string, IndexOutlookDetail> answer = hold.Heads.Keys.ToDictionary(
            static r => r, r => new IndexOutlookDetail(ByRepo.GetValueOrDefault(r, All), RepoWait.None, RetryAt: null), StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(answer));
    }

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> IndexCommits(IReadOnlyList<string> repos, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> none = new Dictionary<string, string>(StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(none));
    }
}
