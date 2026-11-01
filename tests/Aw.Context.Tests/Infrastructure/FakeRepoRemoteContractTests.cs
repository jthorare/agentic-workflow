using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class FakeRepoRemoteContractTests : RepoRemoteContract
{
    protected override IRepoRemote Create(Standing standing, out ContextRepo repo)
    {
        repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");
        var fake = new FakeRepoRemote();
        return standing switch
        {
            Standing.HasOrigin => fake.Set(repo.Name, OriginUrl),
            Standing.Unreadable => fake.Unreadable(repo.Name),
            Standing.NoOrigin => fake,
            _ => fake,
        };
    }
}
