using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Unit)]
public sealed class FakePullRequestsReviewTests
{
    private static readonly PullRepo _repo = new("forge.test", "acme", "widgets");

    private static string Sha(char c) => new(c, 40);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (FakePullRequests Pulls, FakePull Pull) Open()
    {
        var pulls = new FakePullRequests(["forge.test"]);
        var pull = pulls.Add(_repo, "feature", Sha('a'));
        pull.BaseSha = Sha('b');
        return (pulls, pull);
    }

    [Fact]
    public async Task SeedRuns_counts_every_failing_conclusion_and_pending_runs()
    {
        var (pulls, pull) = Open();
        pulls.SeedRuns(pull.Link, [new("a", App: null, "timed_out", Sha('a')), new("b", App: null, "cancelled", Sha('a')), new("c", App: null, "success", Sha('a')), new("d", App: null, "pending", Sha('a'))]);

        var view = (await pulls.Get(pull.Link, Ct)).Value;

        Assert.Equal(PullChecksState.Failing, view.Checks.State);
        Assert.Equal(["a", "b"], view.Checks.Failing);
        Assert.Equal(1, view.Checks.Passed);
        Assert.Equal(1, view.Checks.Pending);
    }

    [Fact]
    public async Task SeedRuns_with_only_pending_and_passing_runs_is_pending()
    {
        var (pulls, pull) = Open();
        pulls.SeedRuns(pull.Link, [new("c", App: null, "success", Sha('a')), new("d", App: null, "pending", Sha('a'))]);

        Assert.Equal(PullChecksState.Pending, (await pulls.Get(pull.Link, Ct)).Value.Checks.State);
    }

    [Fact]
    public async Task FileText_reads_the_head_text_at_the_head_sha_and_the_old_path_text_at_the_base_sha()
    {
        var (pulls, pull) = Open();
        PullFile[] files = [new("src/new.cs", "src/old.cs", PullFileStatus.Renamed, 1, 1, "@@ -1 +1 @@\n+x")];
        pulls.SeedFiles(pull.Link, files, new Dictionary<string, string>(StringComparer.Ordinal) { ["src/new.cs"] = "head" }, new Dictionary<string, string>(StringComparer.Ordinal) { ["src/old.cs"] = "base" }, capReached: false);

        Assert.Equal("head", (await pulls.FileText(pull.Link, "src/new.cs", Sha('a'), Ct)).Value);
        Assert.Equal("base", (await pulls.FileText(pull.Link, "src/old.cs", Sha('b'), Ct)).Value);
        Assert.Equal("SND-FORGE-003", (await pulls.FileText(pull.Link, "src/new.cs", Sha('b'), Ct)).Error.Code);
    }

    [Fact]
    public async Task Files_reports_the_cap_the_seed_set()
    {
        var (pulls, pull) = Open();
        pulls.SeedFiles(pull.Link, [], new Dictionary<string, string>(StringComparer.Ordinal), baseTexts: null, capReached: true);

        Assert.True((await pulls.Files(pull.Link, Ct)).Value.CapReached);
    }
}
