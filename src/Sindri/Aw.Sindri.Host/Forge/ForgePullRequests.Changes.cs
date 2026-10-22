using Aw.Forge.Core.Refs;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using PullDigest = Aw.Sindri.Core.Forge.PullDigest;
using Results = Aw.Kernel.Results;

namespace Aw.Sindri.Host.Forge;

/// <summary>Retargeting, default branches and the poller's digest.</summary>
internal sealed partial class ForgePullRequests
{
    public async ValueTask<Result<Unit>> Retarget(string link, string newBase, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var retargeted = await forge.Retarget(pull.Value, newBase, cancellationToken).ConfigureAwait(false);
        return retargeted.IsError ? PullErrors.From(retargeted.Error) : Results.Ok();
    }

    public async ValueTask<Result<string>> DefaultBranch(PullRepo repo, CancellationToken cancellationToken)
    {
        // why: Parse is the forge's one check of a host against the profile's list and of owner and repo names; a pull number stands in because only the repository is read.
        var parsed = forge.Parse($"https://{repo.Host}/{repo.Owner}/{repo.Name}/pull/1");
        if (parsed.IsError)
        {
            return PullErrors.From(parsed.Error);
        }

        var branch = await forge.DefaultBranch(Repo(repo), cancellationToken).ConfigureAwait(false);
        return branch.IsError ? PullErrors.From(branch.Error) : branch.Value;
    }

    public async ValueTask<Result<IReadOnlyList<PullDigest>>> Digest(IReadOnlyList<string> links, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(links);
        var refs = new List<ForgeRef>(links.Count);
        foreach (var link in links)
        {
            var pull = forge.Parse(link);
            if (pull.IsError)
            {
                return PullErrors.From(pull.Error);
            }

            refs.Add(pull.Value);
        }

        var digests = new List<PullDigest>(refs.Count);
        for (var from = 0; from < refs.Count; from += IPullRequests.DigestBatchSize)
        {
            var batch = refs.GetRange(from, Math.Min(IPullRequests.DigestBatchSize, refs.Count - from));
            var read = await forge.GetPullDigest(batch, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return PullErrors.From(read.Error);
            }

            digests.AddRange(read.Value.Select(static d => new PullDigest(d.Ref.Link, State(d.Status), d.HeadSha, Checks(d.Checks), d.CommentCount, d.Version)));
        }

        return Results.Success<IReadOnlyList<PullDigest>>(digests);
    }
}
