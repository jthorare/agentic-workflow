using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.GitHub;

internal sealed partial class GitHubForge
{
    public async ValueTask<Result<ForgeRef?>> FindPull(FindPullQuery query, CancellationToken cancellationToken)
    {
        if (_api.Guard(query.Repo.Host) is { } refused)
        {
            return refused;
        }

        var head = Uri.EscapeDataString($"{query.Repo.Owner}:{query.Head}");
        var found = await _api.All(query.Repo.Host, $"/repos/{query.Repo.Owner}/{query.Repo.Name}/pulls?state=open&head={head}", cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        foreach (var pull in found.Value)
        {
            if ((query.Author is null || pull.Str("user", "login") == query.Author)
                && (query.Marker is null || ForgeMarker.IsIn(pull.Str("body"), query.Marker)))
            {
                return Results.Success<ForgeRef?>(new ForgeRef(ForgeKind.GitHub, query.Repo.Host, query.Repo.Owner, query.Repo.Name, (int)pull.Long("number")));
            }
        }

        return Results.Success<ForgeRef?>(value: null);
    }

    public async ValueTask<Result<ForgeRef>> OpenPull(OpenPullRequest request, CancellationToken cancellationToken)
    {
        if (_api.Guard(request.Repo.Host) is { } refused)
        {
            return refused;
        }

        var body = J.Object(w =>
        {
            w.WriteString("title", request.Title);
            w.WriteString("head", request.Head);
            w.WriteString("base", request.Base);
            w.WriteString("body", request.Body);
            w.WriteBoolean("draft", request.Draft);
        });
        var reply = await _api.Call(request.Repo.Host, HttpMethod.Post, $"/repos/{request.Repo.Owner}/{request.Repo.Name}/pulls", body, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Ok
            ? new ForgeRef(ForgeKind.GitHub, request.Repo.Host, request.Repo.Owner, request.Repo.Name, (int)reply.Value.Body.Long("number"))
            : GitHubApi.Fail(reply.Value);
    }

    public async ValueTask<Result<Unit>> UpdatePull(UpdatePullRequest request, CancellationToken cancellationToken)
    {
        var body = J.Object(w =>
        {
            if (request.Title is not null)
            {
                w.WriteString("title", request.Title);
            }

            if (request.Body is not null)
            {
                w.WriteString("body", request.Body);
            }
        });
        return await Patch(request.Pull, body, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<Unit>> Retarget(ForgeRef pull, string newBase, CancellationToken cancellationToken) =>
        await Patch(pull, J.Object(w => w.WriteString("base", newBase)), cancellationToken).ConfigureAwait(false);

    public async ValueTask<Result<MergeOutcome>> Merge(MergeRequest request, CancellationToken cancellationToken)
    {
        var pull = request.Pull;
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var method = request.Method switch { MergeMethod.Squash => "squash", MergeMethod.Rebase => "rebase", MergeMethod.Merge => "merge", _ => "merge" };
        var body = J.Object(w =>
        {
            w.WriteString("sha", request.ExpectedHead);
            w.WriteString("merge_method", method);
        });
        var reply = await _api.Call(pull.Host, HttpMethod.Put, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/merge"), body, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Status switch
        {
            >= 200 and < 300 => new MergeOutcome(MergeStatus.Merged, reply.Value.Body.Str("sha"), Reason: null),
            409 => new MergeOutcome(MergeStatus.HeadMoved, MergedSha: null, Reason: null),
            405 or 422 => new MergeOutcome(MergeStatus.Blocked, MergedSha: null, Reason: reply.Value.Body.Str("message")),
            _ => GitHubApi.Fail(reply.Value),
        };
    }

    public async ValueTask<Result<int>> RerunChecks(ForgeRef pull, string sha, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var runs = await CheckRuns(pull, sha, cancellationToken).ConfigureAwait(false);
        if (runs.IsError)
        {
            return runs.Error;
        }

        var rerun = 0;
        foreach (var run in runs.Value.Where(r => Summarise(sha, [r]).State == ChecksState.Failing))
        {
            var reply = await _api.Call(pull.Host, HttpMethod.Post, Inv.S($"{RepoPath(pull)}/check-runs/{run.Long("id")}/rerequest"), body: null, cancellationToken).ConfigureAwait(false);
            if (reply.IsError)
            {
                return reply.Error;
            }

            rerun += reply.Value.Ok ? 1 : 0;
        }

        return rerun;
    }

    private async ValueTask<Result<Unit>> Patch(ForgeRef pull, byte[] body, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var reply = await _api.Call(pull.Host, HttpMethod.Patch, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}"), body, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Ok ? Results.Ok() : GitHubApi.Fail(reply.Value);
    }
}
