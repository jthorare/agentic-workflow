using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Xunit;

namespace Aw.Forge.Testing;

/// <summary>
/// The IForge contract's read side: parsing, the host check, and what a missing object looks like. It needs no seeding, so a
/// real forge (GitHub, in the heavy lane) runs it too; <see cref="ForgeContract"/> adds the writes.
/// </summary>
public abstract class ForgeReadContract
{
    private const int MissingPull = 987654;
    private const string OtherHost = "elsewhere.test";

    /// <summary>Creates the adapter under test, fresh for each test.</summary>
    /// <returns>The adapter.</returns>
    protected abstract ForgeUnderTest Create();

    /// <summary>A pull request link on the run's repository parses to its parts.</summary>
    [Fact]
    public void Parse_reads_a_pull_link()
    {
        var run = Create();

        var pull = Expect.Ok(run.Forge.Parse(Link(run, 7)), "Parse of a valid link");

        Expect.That(pull.Number == 7 && pull.Owner == run.Repo.Owner && pull.Repo == run.Repo.Name && pull.Host == run.Repo.Host, "the ref carries host, owner, repo and number");
        Expect.That(pull.Forge == run.Forge.Kind, "the ref carries the adapter's kind");
    }

    /// <summary>A refusal names the part of the link that failed (AW-FORGE-001).</summary>
    [Fact]
    public void Parse_names_the_failing_part()
    {
        var run = Create();
        var link = Link(run, 7);

        Expect.That(Expect.Fails(run.Forge.Parse("not a link"), "AW-FORGE-001", "Parse of text").Contains("link", StringComparison.Ordinal), "names the link");
        Expect.That(Expect.Fails(run.Forge.Parse(link.Replace("https://", "http://", StringComparison.Ordinal)), "AW-FORGE-001", "Parse of http").Contains("scheme", StringComparison.Ordinal), "names the scheme");
        Expect.That(Expect.Fails(run.Forge.Parse(link.Replace("/pull/7", "/issues/7", StringComparison.Ordinal)), "AW-FORGE-001", "Parse of an issue link").Contains("path", StringComparison.Ordinal), "names the path");
        Expect.That(Expect.Fails(run.Forge.Parse(link.Replace("/pull/7", "/pull/0", StringComparison.Ordinal)), "AW-FORGE-001", "Parse of pull 0").Contains("number", StringComparison.Ordinal), "names the number");
        _ = Expect.Fails(run.Forge.Parse(null), "AW-FORGE-001", "Parse of null");
    }

    /// <summary>A host outside the allowed set is refused with AW-FORGE-002.</summary>
    [Fact]
    public void Parse_refuses_a_host_that_is_not_allowed()
    {
        var run = Create();

        _ = Expect.Fails(run.Forge.Parse($"https://{OtherHost}/acme/widgets/pull/1"), "AW-FORGE-002", "Parse of an unlisted host");
    }

    /// <summary>Every operation refuses a ref or repo on an unlisted host before it sends or reads anything.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unlisted_host_is_refused_before_any_request()
    {
        var run = Create();
        var before = run.Requests?.Invoke();
        var pull = new ForgeRef(run.Forge.Kind, OtherHost, "acme", "widgets", 1);
        var repo = new ForgeRepo(OtherHost, "acme", "widgets");
        var ct = TestContext.Current.CancellationToken;

        _ = Expect.Fails(await run.Forge.GetPull(pull, ct), "AW-FORGE-002", "GetPull");
        _ = Expect.Fails(await run.Forge.ListThreads(pull, ct), "AW-FORGE-002", "ListThreads");
        _ = Expect.Fails(await run.Forge.CheckLog(pull, "build", ct), "AW-FORGE-002", "CheckLog");
        _ = Expect.Fails(await run.Forge.DefaultBranch(repo, ct), "AW-FORGE-002", "DefaultBranch");
        _ = Expect.Fails(await run.Forge.FindPull(new FindPullQuery(repo, "feature", Author: null, Marker: null), ct), "AW-FORGE-002", "FindPull");
        _ = Expect.Fails(await run.Forge.OpenPull(new OpenPullRequest(repo, "feature", "main", Sha('a'), "t", "b", Draft: false), ct), "AW-FORGE-002", "OpenPull");
        _ = Expect.Fails(await run.Forge.Merge(new MergeRequest(pull, MergeMethod.Squash, Sha('a')), ct), "AW-FORGE-002", "Merge");
        Expect.That(run.Requests is null || run.Requests() == before, "no request was counted for a refused host");
    }

    /// <summary>A pull request, its threads and a check log that aren't there are AW-FORGE-004.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_pull_request_is_not_found()
    {
        var run = Create();
        var pull = Expect.Ok(run.Forge.Parse(Link(run, MissingPull)), "Parse");
        var ct = TestContext.Current.CancellationToken;

        _ = Expect.Fails(await run.Forge.GetPull(pull, ct), "AW-FORGE-004", "GetPull of a missing pull");
        _ = Expect.Fails(await run.Forge.ListThreads(pull, ct), "AW-FORGE-004", "ListThreads of a missing pull");
        _ = Expect.Fails(await run.Forge.CheckLog(pull, "build", ct), "AW-FORGE-004", "CheckLog of a missing pull");
    }

    /// <summary>The default branch is a non-empty name.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_default_branch_has_a_name()
    {
        var run = Create();

        var branch = Expect.Ok(await run.Forge.DefaultBranch(run.Repo, TestContext.Current.CancellationToken), "DefaultBranch");

        Expect.That(!string.IsNullOrWhiteSpace(branch), "the default branch is named");
    }

    /// <summary>A head branch with no open pull request finds nothing: a success holding null.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FindPull_finds_nothing_for_an_unused_branch()
    {
        var run = Create();

        var found = Expect.Ok(await run.Forge.FindPull(new FindPullQuery(run.Repo, "aw-contract-no-such-branch", Author: null, Marker: null), TestContext.Current.CancellationToken), "FindPull");

        Expect.That(found is null, "no pull request");
    }

    /// <summary>A link on the run's repository.</summary>
    /// <param name="run">The run.</param>
    /// <param name="number">The pull request number.</param>
    /// <returns>The link.</returns>
    protected static string Link(ForgeUnderTest run, int number)
    {
        ArgumentNullException.ThrowIfNull(run);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://{run.Repo.Host}/{run.Repo.Owner}/{run.Repo.Name}/pull/{number}");
    }

    /// <summary>A 40-character sha made of one character.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The sha.</returns>
    protected static string Sha(char c) => new(c, 40);
}
