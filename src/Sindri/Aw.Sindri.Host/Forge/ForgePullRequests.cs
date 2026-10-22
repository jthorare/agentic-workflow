using System.Diagnostics;
using Aw.Forge.Core;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Results = Aw.Kernel.Results;

namespace Aw.Sindri.Host.Forge;

/// <summary>
/// <see cref="IPullRequests"/> over the forge package's <see cref="IForge"/> (PR babysitter spec §10.2): the one place Sindri's pull-request strings and
/// records meet the forge's refs and DTOs. The forge checks every link and repository against the profile's host list before it sends anything.
/// </summary>
/// <param name="forge">The adapter the profile's 'workflow.forge.adapter' names.</param>
internal sealed partial class ForgePullRequests(IForge forge) : IPullRequests
{
    public async ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
    {
        var parsed = forge.Parse(link);
        if (parsed.IsError)
        {
            return PullErrors.From(parsed.Error);
        }

        var pull = parsed.Value;
        var snapshot = await forge.GetPull(pull, cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError)
        {
            return PullErrors.From(snapshot.Error);
        }

        var threads = await forge.ListThreads(pull, cancellationToken).ConfigureAwait(false);
        if (threads.IsError)
        {
            return PullErrors.From(threads.Error);
        }

        var s = snapshot.Value;
        var open = threads.Value.Count(static t => t.Kind == CommentKind.Thread && !t.Resolved);
        return new PullView(pull.Link, State(s.Status), s.Head.Sha, s.Mergeable, s.ReviewDecision, open, new PullChecks(Checks(s.Checks.State), s.Checks.Sha, s.Checks.Failing), s.Labels, s.Base.Ref);
    }

    public async ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var log = await forge.CheckLog(pull.Value, check, cancellationToken).ConfigureAwait(false);
        return log.IsError ? PullErrors.From(log.Error) : log.Value;
    }

    public async ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(push.Link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var snapshot = await forge.GetPull(pull.Value, cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError)
        {
            return PullErrors.From(snapshot.Error);
        }

        if (snapshot.Value.Head.Fork)
        {
            return SindriErrors.ForgeRefused.With("the pull request's head branch is on a fork; Sindri doesn't push to forks");
        }

        return await PushTo(new PushTarget(pull.Value, push.Workspace, push.Sha, snapshot.Value.Head.Ref, push.LeaseSha), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<PullPushOutcome>> PushBranch(PullBranchPush push, CancellationToken cancellationToken)
    {
        // why: Parse is the forge's one check of a host against the profile's list and of owner and repo names; a pull number stands in because no pull request exists yet.
        var repo = forge.Parse($"https://{push.Repo.Host}/{push.Repo.Owner}/{push.Repo.Name}/pull/1");
        return repo.IsError
            ? PullErrors.From(repo.Error)
            : await PushTo(new PushTarget(repo.Value, push.Workspace, push.Sha, push.Branch, push.LeaseSha), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<PullMergeOutcome>> Merge(PullMerge request, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(request.Link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var merged = await forge.Merge(new MergeRequest(pull.Value, Method(request.Method), request.ExpectedHead), cancellationToken).ConfigureAwait(false);
        return merged.IsError ? PullErrors.From(merged.Error) : new PullMergeOutcome(MergeStatusOf(merged.Value.Status), merged.Value.MergedSha, merged.Value.Reason);
    }

    public async ValueTask<Result<PullDelivery>> Reply(PullReply request, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(request.Link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        // The forge adapter redacts the text; it is not scrubbed again here.
        var delivery = await forge.Reply(new ReplyRequest(pull.Value, request.ThreadKey, request.Text, request.Marker), cancellationToken).ConfigureAwait(false);
        return delivery.IsError ? PullErrors.From(delivery.Error) : Delivered(delivery.Value);
    }

    public async ValueTask<Result<PullDelivery>> React(PullReaction request, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(request.Link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var delivery = await forge.React(new ReactionRequest(pull.Value, request.ThreadKey, request.Reaction, request.Marker), cancellationToken).ConfigureAwait(false);
        return delivery.IsError ? PullErrors.From(delivery.Error) : Delivered(delivery.Value);
    }

    public async ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var count = await forge.RerunChecks(pull.Value, sha, cancellationToken).ConfigureAwait(false);
        return count.IsError ? PullErrors.From(count.Error) : count.Value;
    }

    public async ValueTask<Result<string>> Self(CancellationToken cancellationToken)
    {
        var login = await forge.Self(cancellationToken).ConfigureAwait(false);
        return login.IsError ? PullErrors.From(login.Error) : login.Value;
    }

    public async ValueTask<Result<IReadOnlyList<PullThread>>> Threads(string link, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var threads = await forge.ListThreads(pull.Value, cancellationToken).ConfigureAwait(false);
        return threads.IsError
            ? PullErrors.From(threads.Error)
            : Results.Success<IReadOnlyList<PullThread>>([.. threads.Value.Select(static t => new PullThread(t.Key, Kind(t.Kind), t.Author, t.Association, t.IsBot, t.Resolved, t.Outdated, t.Path, t.Body, t.Markers, t.CreatedAt, t.Line, t.EditedAt, t.Url))]);
    }

    public async ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken)
    {
        var found = await forge.FindPull(new FindPullQuery(Repo(query.Repo), query.Head, Author: null, query.Marker), cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return PullErrors.From(found.Error);
        }

        return found.Value is { } pull ? Results.Success<OpenedPull?>(new OpenedPull(pull.Link, pull.Number)) : Results.Success<OpenedPull?>(value: null);
    }

    public async ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken)
    {
        var body = request.Body;
        if (request.Marker is { } marker)
        {
            var valid = ForgeMarker.Validate(marker);
            if (valid.IsError)
            {
                return PullErrors.From(valid.Error);
            }

            body = ForgeMarker.Append(body, marker);
        }

        var opened = await forge.OpenPull(new OpenPullRequest(Repo(request.Repo), request.Head, request.Base, request.HeadSha, request.Title, body, request.Draft, request.Labels), cancellationToken).ConfigureAwait(false);
        return opened.IsError ? PullErrors.From(opened.Error) : new OpenedPull(opened.Value.Link, opened.Value.Number);
    }

    public async ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var updated = await forge.UpdatePull(new UpdatePullRequest(pull.Value, title, body), cancellationToken).ConfigureAwait(false);
        return updated.IsError ? PullErrors.From(updated.Error) : Results.Ok();
    }

    private async ValueTask<Result<PullPushOutcome>> PushTo(PushTarget target, CancellationToken cancellationToken)
    {
        // why: the remote is the repository on a host the profile allows (Parse checked it), never what the worktree's git config says.
        var remote = $"https://{target.Pull.Host}/{target.Pull.Owner}/{target.Pull.Repo}.git";
        var pushed = await forge.Push(new PushRequest(target.Workspace, target.Sha, remote, target.Branch, target.LeaseSha), cancellationToken).ConfigureAwait(false);
        if (pushed.IsSuccess)
        {
            return pushed.Value == PushOutcome.Pushed ? PullPushOutcome.Pushed : PullPushOutcome.HeadMoved;
        }

        // A push git ran and the remote turned away (a protected branch, a hook) is an outcome the caller acts on; any other failure is an error.
        return pushed.Error.Code == ForgeErrors.PushFailed.Code ? PullPushOutcome.Rejected : PullErrors.From(pushed.Error);
    }

    private static MergeMethod Method(PullMergeMethod method) => method switch
    {
        PullMergeMethod.Squash => MergeMethod.Squash,
        PullMergeMethod.Rebase => MergeMethod.Rebase,
        PullMergeMethod.Merge => MergeMethod.Merge,
        _ => throw new UnreachableException($"merge method {method} has no mapping"),
    };

    private static PullMergeStatus MergeStatusOf(MergeStatus status) => status switch
    {
        MergeStatus.Merged => PullMergeStatus.Merged,
        MergeStatus.HeadMoved => PullMergeStatus.HeadMoved,
        MergeStatus.Blocked => PullMergeStatus.Blocked,
        _ => throw new UnreachableException($"merge status {status} has no mapping"),
    };

    private static PullDelivery Delivered(Delivery delivery) => delivery == Delivery.Delivered ? PullDelivery.Delivered : PullDelivery.AlreadyDelivered;

    private static ForgeRepo Repo(PullRepo repo) => new(repo.Host, repo.Owner, repo.Name);

    private static PullCommentKind Kind(CommentKind kind) => kind switch
    {
        CommentKind.Thread => PullCommentKind.Thread,
        CommentKind.Review => PullCommentKind.Review,
        CommentKind.Comment => PullCommentKind.Comment,
        _ => throw new UnreachableException($"comment kind {kind} has no mapping"),
    };

    private static PullState State(PullStatus status) => status switch
    {
        PullStatus.Open => PullState.Open,
        PullStatus.Closed => PullState.Closed,
        PullStatus.Merged => PullState.Merged,
        _ => throw new UnreachableException($"pull status {status} has no mapping"),
    };

    private static PullChecksState Checks(ChecksState state) => state switch
    {
        ChecksState.None => PullChecksState.None,
        ChecksState.Pending => PullChecksState.Pending,
        ChecksState.Passing => PullChecksState.Passing,
        ChecksState.Failing => PullChecksState.Failing,
        _ => throw new UnreachableException($"checks state {state} has no mapping"),
    };
}
