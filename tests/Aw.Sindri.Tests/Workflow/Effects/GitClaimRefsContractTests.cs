using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>The git adapter against a real temp repo with one commit.</summary>
[Lane(TestLane.Integration)]
public sealed class GitClaimRefsContractTests : ClaimRefsContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    /// <summary>A ref lock another git holds is passing: the error is retryable (not 'no runtime'), so the effect retries instead of parking at once.</summary>
    [Fact]
    public async Task A_held_ref_lock_is_a_retryable_failure_not_a_missing_runtime()
    {
        var (refs, repository) = await Create();
        var lockFile = Path.Combine(repository, ".git", "refs", "sindri", "me", "T-7.lock");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(lockFile)!);
        await File.WriteAllTextAsync(lockFile, string.Empty, TestContext.Current.CancellationToken);

        var marked = await refs.Mark(repository, "refs/sindri/me/T-7", TestContext.Current.CancellationToken);

        Assert.True(marked.IsError);
        Assert.Equal("SND-WF-015", marked.Error.Code);
    }

    protected override async ValueTask<(IClaimRefs Refs, string Repository)> Create()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return (new GitClaimRefs(), repo.Path);
    }
}
