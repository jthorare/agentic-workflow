using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.World;

/// <summary>The writes: push, open, update, retarget, merge, rerun.</summary>
internal partial class WorldForge
{
    public async ValueTask<Result<PushOutcome>> Push(PushRequest request, CancellationToken cancellationToken)
    {
        if (git is null)
        {
            return Simulate(request);
        }

        var outcome = await git.Push(request, authorizationHeader: null, cancellationToken).ConfigureAwait(false);
        if (outcome.IsSuccess && outcome.Value == PushOutcome.Pushed)
        {
            RecordTip(request);
        }

        return outcome;
    }

    public ValueTask<Result<ForgeRef>> OpenPull(OpenPullRequest request, CancellationToken cancellationToken) =>
        Done(Guard(request.Repo.Host) ?? Count(() => Results.Success(store.Write(state => Open(state, request)))));

    public ValueTask<Result<Unit>> UpdatePull(UpdatePullRequest request, CancellationToken cancellationToken) =>
        Done(Guard(request.Pull.Host) ?? Write(request.Pull, (_, p) =>
        {
            p.Title = request.Title ?? p.Title;
            p.Body = request.Body ?? p.Body;
            return Results.Ok();
        }));

    public ValueTask<Result<Unit>> Retarget(ForgeRef pull, string newBase, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Write(pull, (state, p) =>
        {
            p.BaseRef = newBase;
            p.BaseSha = BaseTip(state, p);
            return Results.Ok();
        }));

    public ValueTask<Result<MergeOutcome>> Merge(MergeRequest request, CancellationToken cancellationToken) =>
        Done(Guard(request.Pull.Host) ?? Write(request.Pull, (_, p) => MergePull(p, request)));

    public ValueTask<Result<int>> RerunChecks(ForgeRef pull, string sha, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Write(pull, (_, p) =>
        {
            if (!string.Equals(p.ChecksSha, sha, StringComparison.Ordinal))
            {
                return Results.Success(0);
            }

            var rerun = p.Failing.Count;
            if (rerun > 0)
            {
                p.Checks = ChecksState.Pending;
                p.Failing = [];
                p.Reruns += rerun;
            }

            return Results.Success(rerun);
        }));

    private ForgeRef Open(WorldState state, OpenPullRequest request)
    {
        var key = request.Repo.Key;
        var repo = state.Repos.FirstOrDefault(r => r.Key == key);
        if (repo is null)
        {
            repo = new WorldRepo { Key = key };
            state.Repos.Add(repo);
        }

        var remote = state.Tips.FirstOrDefault(t => t.Key.EndsWith("#" + request.Head, StringComparison.Ordinal) && t.Value == request.HeadSha).Key;
        var number = repo.NextNumber++;
        var pull = new WorldPull
        {
            Host = request.Repo.Host,
            Owner = request.Repo.Owner,
            Repo = request.Repo.Name,
            Number = number,
            Title = request.Title,
            Body = request.Body,
            Author = actingLogin,
            Draft = request.Draft,
            Labels = [.. request.Labels ?? []],
            BaseRef = request.Base,
            HeadRef = request.Head,
            HeadSha = request.HeadSha,
            HeadRemote = remote?[..remote.LastIndexOf('#')],
            ChecksSha = request.HeadSha,
        };
        pull.BaseSha = BaseTip(state, pull);
        state.Pulls.Add(pull);
        return new ForgeRef(kind, pull.Host, pull.Owner, pull.Repo, number);
    }

    private static string BaseTip(WorldState state, WorldPull p)
    {
        var tip = state.Tips.FirstOrDefault(t => t.Key.EndsWith("#" + p.BaseRef, StringComparison.Ordinal)).Value;
        return tip ?? Hash("base:" + p.BaseRef);
    }

    private static Result<MergeOutcome> MergePull(WorldPull p, MergeRequest request)
    {
        if (p.Status != PullStatus.Open)
        {
            return new MergeOutcome(MergeStatus.Blocked, MergedSha: null, Reason: p.Status == PullStatus.Merged ? "already merged" : "closed");
        }

        if (!string.Equals(p.HeadSha, request.ExpectedHead, StringComparison.Ordinal))
        {
            return new MergeOutcome(MergeStatus.HeadMoved, MergedSha: null, Reason: null);
        }

        var blocked = p switch
        {
            { Draft: true } => "draft",
            { Checks: ChecksState.Failing or ChecksState.Pending } => "checks",
            { ReviewDecision: "CHANGES_REQUESTED" } => "changes requested",
            { Mergeable: false } => "conflicts",
            _ => null,
        };
        if (blocked is not null)
        {
            return new MergeOutcome(MergeStatus.Blocked, MergedSha: null, Reason: blocked);
        }

        p.Status = PullStatus.Merged;
        p.MergedSha = Hash($"merge:{request.Method}:{p.HeadSha}");
        return new MergeOutcome(MergeStatus.Merged, p.MergedSha, Reason: null);
    }

    private Result<PushOutcome> Simulate(PushRequest request) =>
        Count(() => store.Write(state =>
        {
            var key = Tip(request.Remote, request.Branch);
            var has = state.Tips.TryGetValue(key, out var tip);
            if (request.LeaseSha is null ? has : tip != request.LeaseSha)
            {
                return Results.Success(PushOutcome.HeadMoved);
            }

            Apply(state, request);
            return Results.Success(PushOutcome.Pushed);
        }));

    private void RecordTip(PushRequest request) => store.Write(state =>
    {
        Apply(state, request);
        return Results.Ok();
    });

    private static void Apply(WorldState state, PushRequest request)
    {
        state.Tips[Tip(request.Remote, request.Branch)] = request.Sha;
        foreach (var p in state.Pulls.Where(p => p.HeadRef == request.Branch && p.HeadRemote == request.Remote))
        {
            p.HeadSha = request.Sha;
            p.Checks = ChecksState.None;
            p.ChecksSha = request.Sha;
            p.Failing = [];
            p.Stamp++;
        }
    }
}
