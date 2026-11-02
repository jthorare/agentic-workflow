using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GitRepoRemoteContractTests : RepoRemoteContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose() => _repos.ForEach(r => r.Dispose());

    protected override IRepoRemote Create(Standing standing, out ContextRepo repo)
    {
        var temp = new TempGitRepo(RepoReaderContract.Files);
        _repos.Add(temp);
        repo = temp.Repo;
        switch (standing)
        {
            case Standing.HasOrigin:
                _ = temp.Run("remote", "add", "origin", OriginUrl);
                break;
            case Standing.Unreadable:
                repo = new ContextRepo(repo.Name, Path.Combine(Path.GetTempPath(), "aw-no-such-checkout"));
                break;
            case Standing.NoOrigin:
            default:
                break;
        }

        return new GitRepoRemote(new GitCli());
    }
}
