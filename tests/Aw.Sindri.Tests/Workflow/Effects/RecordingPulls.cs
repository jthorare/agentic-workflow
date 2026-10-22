using Aw.Kernel;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>An <see cref="IPullRequests"/> that records the merges, replies and reactions it gets and answers what a test set; every other member is not used by the PR effects.</summary>
internal sealed class RecordingPulls : IPullRequests
{
    public List<PullMerge> Merges { get; } = [];

    public List<PullReply> Replies { get; } = [];

    public List<PullReaction> Reactions { get; } = [];

    public List<string> Calls { get; } = [];

    public List<(string Link, string NewBase)> Retargets { get; } = [];

    public Result<PullView> GetAnswer { get; set; } = new PullView("https://forge.example/acme/web/pull/7", PullState.Open, "deadbeef", Mergeable: true, ReviewDecision: null, UnresolvedThreads: 0, new PullChecks(PullChecksState.Passing, "deadbeef", []));

    public Result<string> DefaultBranchAnswer { get; set; } = "main";

    public Result<Unit> RetargetAnswer { get; set; } = Unit.Value;

    public Result<PullMergeOutcome> MergeAnswer { get; set; } = new PullMergeOutcome(PullMergeStatus.Merged, "abc123", null);

    public PullDelivery Delivery { get; set; } = PullDelivery.Delivered;

    public ValueTask<Result<PullMergeOutcome>> Merge(PullMerge request, CancellationToken cancellationToken)
    {
        Calls.Add("Merge");
        Merges.Add(request);
        return ValueTask.FromResult(MergeAnswer);
    }

    public ValueTask<Result<PullDelivery>> Reply(PullReply request, CancellationToken cancellationToken)
    {
        Replies.Add(request);
        return ValueTask.FromResult<Result<PullDelivery>>(Delivery);
    }

    public ValueTask<Result<PullDelivery>> React(PullReaction request, CancellationToken cancellationToken)
    {
        Reactions.Add(request);
        return ValueTask.FromResult<Result<PullDelivery>>(Delivery);
    }

    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
    {
        Calls.Add("Get");
        return ValueTask.FromResult(GetAnswer);
    }

    // Engine adds these two to IPullRequests; they are written to its exact signatures.
    public ValueTask<Result<Unit>> Retarget(string link, string newBase, CancellationToken cancellationToken)
    {
        Calls.Add("Retarget");
        Retargets.Add((link, newBase));
        return ValueTask.FromResult(RetargetAnswer);
    }

    public ValueTask<Result<string>> DefaultBranch(PullRepo repo, CancellationToken cancellationToken)
    {
        Calls.Add("DefaultBranch");
        return ValueTask.FromResult(DefaultBranchAnswer);
    }

    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<PullThread>>> Threads(string link, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<PullPushOutcome>> PushBranch(PullBranchPush push, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken) => throw new NotSupportedException();
}
