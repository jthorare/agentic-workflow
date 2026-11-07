using Aw.Kernel.Testing;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The evidence panel's disk lookup (PR review page spec §6.2 table): ui-evidence runs matched by the head sha in their <c language="text">appBuild</c>, else by the PR number in their <c language="text">pr</c>.</summary>
[Lane(TestLane.Integration)]
public sealed class FileReviewEvidenceTests : IDisposable
{
    private const string Head = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private readonly TempHome _state = new();
    private readonly FileReviewEvidence _evidence;

    public FileReviewEvidenceTests()
    {
        _evidence = new FileReviewEvidence(name => name == "AW_STATE_DIR" ? _state.Path : null);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewPullRef Pr => new("acme", "api", 2071);

    public void Dispose() => _state.Dispose();

    private async Task Run(string id, string summary)
    {
        var dir = Path.Combine(_state.Path, "ui-evidence", "runs", id);
        _ = Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "summary.json"), summary, Token);
    }

    [Fact]
    public async Task No_runs_folder_finds_nothing() => Assert.Empty((await _evidence.Find(Pr, Head, Token)).Value);

    [Fact]
    public async Task A_run_built_at_the_head_matches_by_sha_and_a_run_for_the_pr_number_matches_by_number()
    {
        await Run("r1", """{"route":"/reviews","appBuild":"a1b2c3d","pr":null,"finishedAt":"2025-12-31T22:00:00.0000000Z"}""");
        await Run("r2", """{"route":"/work","appBuild":"ffffffffff","pr":"acme/api#2071","finishedAt":"2025-12-31T21:00:00.0000000Z"}""");
        await Run("r3", """{"route":"/other","appBuild":"eeeeeeeeee","pr":"acme/api#99"}""");
        await Run("r4", "{ not json");

        var items = (await _evidence.Find(Pr, Head, Token)).Value;

        Assert.Equal([("/reviews", "sha"), ("/work", "prNumber")], [.. items.Select(static i => (i.Label, i.MatchedBy))]);
        Assert.All(items, static i => Assert.Equal("uiEvidence", i.Kind));
        Assert.Equal("ui-evidence/runs/r1/summary.json", items[0].Path.Replace('\\', '/'));
    }

    [Fact]
    public async Task A_build_shorter_than_seven_characters_is_not_a_sha_match()
    {
        await Run("r1", """{"route":"/reviews","appBuild":"a1b2","pr":null}""");

        Assert.Empty((await _evidence.Find(Pr, Head, Token)).Value);
    }
}
