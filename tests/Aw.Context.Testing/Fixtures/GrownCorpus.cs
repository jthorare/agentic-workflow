using System.Globalization;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// The split-panes history grown to enough pull requests for the backtest's 30-sample branch (S-7A): the five recorded pull requests replayed as templates
/// under synthetic keys SPL-201 and up, with deterministic dates (so the shas are pinned like <see cref="FixtureRepo.Head"/>) and matching synthetic recorded
/// issues. Everything is made up.
/// </summary>
public sealed class GrownCorpus : IDisposable
{
    /// <summary>The first synthetic issue number.</summary>
    public const int FirstNumber = 201;

    private GrownCorpus(FixtureRepo repo, IReadOnlyList<SyntheticIssue> issues)
    {
        Repo = repo;
        Issues = issues;
    }

    /// <summary>The git repo (the initial commit and one merge per pull request).</summary>
    public FixtureRepo Repo { get; }

    /// <summary>The recorded issues, one per pull request, each edited after its merge (so each brief is leaky).</summary>
    public IReadOnlyList<SyntheticIssue> Issues { get; }

    /// <summary>Builds the corpus.</summary>
    /// <param name="parentDirectory">Where to build the repo, or null for a temp folder.</param>
    /// <param name="count">How many pull requests.</param>
    /// <returns>The corpus; dispose it to delete the repo.</returns>
    public static GrownCorpus Build(string? parentDirectory = null, int count = 32)
    {
        var recorded = SplitPanesFixture.History;
        var start = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        var pulls = Enumerable.Range(0, count).Select(i =>
        {
            var template = recorded.PullRequests[i % recorded.PullRequests.Count];
            var number = FirstNumber + i;
            var committed = start.AddDays(i).AddHours(5);
            return new FixturePullRequest(number, "SPL-" + number.ToString(CultureInfo.InvariantCulture), string.Create(CultureInfo.InvariantCulture, $"spl-{number}-{template.Branch["spl-101-".Length..]}"),
                template.Title, template.Dir, committed, committed.AddHours(4));
        }).ToList();
        var history = recorded with { PullRequests = pulls, Initial = recorded.Initial with { At = start.AddDays(-1) } };
        var repo = FixtureRepo.BuildFrom(parentDirectory, history, varied: true);
        var issues = pulls.Select((pr, i) => Issue(recorded.PullRequests[i % recorded.PullRequests.Count], pr, i)).ToList();
        return new GrownCorpus(repo, issues);
    }

    public void Dispose() => Repo.Dispose();

    private static SyntheticIssue Issue(FixturePullRequest template, FixturePullRequest pr, int index)
    {
        var id = string.Create(CultureInfo.InvariantCulture, $"0e5a1b00-0000-4000-8000-{900000000000L + index:D12}");
        var created = pr.CommittedAt.AddDays(-1);
        return new SyntheticIssue(id, pr.IssueKey, pr.Title, created, pr.MergedAt.AddHours(2), BriefFor(template));
    }

    // The template pull request's own brief wording, from the recorded issue of its key, so the grown briefs read like the recorded ones.
    private static IReadOnlyList<string> BriefFor(FixturePullRequest template)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(SplitPanesFixture.ReadText("recorded/documents.json"));
        var entry = node?["documents"]?.AsArray().OfType<System.Text.Json.Nodes.JsonObject>().First(d => ((string?)d["search_doc"]?["semantic_identifier"] ?? string.Empty).Contains(template.IssueKey, StringComparison.Ordinal));
        return [.. (entry?["chunks"]?.AsArray() ?? []).Select(c => (string?)c ?? string.Empty)];
    }
}
