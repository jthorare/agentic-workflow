using Aw.Evidence.Core.Guard;
using Aw.Evidence.Features.GhPublish;
using Aw.Evidence.Infrastructure.Publishing;
using Aw.Evidence.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// The owner's check of gh-publish against a real GitHub repository (spec §12, unit 1.2). Skips unless AW_EVIDENCE_GH_SCRATCH_REPO
/// (owner/name of a scratch repository) is set and gh can write to it (GH_TOKEN or a gh login). It pushes to the branch
/// ui-evidence/pr-999999 of that repository, on top of whatever an earlier run left there.
/// </summary>
public sealed class GhPublishScratchRepoTests
{
    private const string Run = "/runs/scratch-1";
    private const string Shot = "/runs/scratch-1/desktop-1-click.png";

    [Fact, Lane(TestLane.Heavy)]
    public async Task A_run_is_pushed_to_a_real_repository_and_its_commit_pinned_url_is_returned()
    {
        var repo = Environment.GetEnvironmentVariable("AW_EVIDENCE_GH_SCRATCH_REPO");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(repo), "set AW_EVIDENCE_GH_SCRATCH_REPO=owner/name to run this against GitHub");
        var files = new InMemoryRunFiles()
            .With($"{Run}/summary.json", AwJson.Serialize(A.RunSummary().Build(), TestJson.Options))
            .With(Shot, [1, 2, 3]);

        var result = await new GhPublishService(files, new ProcessGhRunner(), TestJson.Options)
            .Handle(new GhPublishRequest(Run, repo, 999999, DbProvenance.Seeded, [Shot]), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal("ui-evidence/pr-999999", result.Value.Branch);
        Assert.StartsWith($"https://github.com/{repo}/blob/{result.Value.Commit}/pr-999999/scratch-1/", result.Value.Urls[Shot], StringComparison.Ordinal);
    }
}
