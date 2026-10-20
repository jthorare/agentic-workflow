using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class FakeDefaultBranchContractTests : DefaultBranchContract
{
    protected override IDefaultBranch Create(Standing standing, out ContextRepo repo, out string commit)
    {
        repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");
        commit = "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567";
        var fake = new FakeDefaultBranch();
        return standing switch
        {
            Standing.AtTip or Standing.BehindTip => fake.Reach(repo.Name, commit),
            Standing.Unreadable => fake.Unreadable(repo.Name),
            Standing.OffDefault or Standing.NoDefaultRef => fake,
            _ => fake,
        };
    }
}
