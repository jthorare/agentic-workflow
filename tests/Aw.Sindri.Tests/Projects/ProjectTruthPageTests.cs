using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Truth;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ProjectFacts;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The page's truth (issue 3718): the facts are read once per page, for every card together, never once per project.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectTruthPageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProjectCard Card(string key, params string[] links) =>
        new(key, ProjectOrigin.HandMade, key, Description: null, ProjectState.InProgress, StateSetByOwner: false, Health: null, Lead: null, Target: null, OpenIssues: null, Counting: false,
            OpenPrs: null, PrScope: null, LastActivity: null, FinishedAt: null, [], [.. links.Select(static l => new ProjectLink(l, l))], Notes: null, Synced: true, Version: 1);

    private sealed class CountingFacts : IProjectTruthFacts
    {
        internal List<ProjectSources> Asked { get; } = [];

        public ValueTask<Result<ProjectTruthFacts>> Read(ProjectSources union, CancellationToken cancellationToken)
        {
            Asked.Add(union);
            ReviewPull pull = new("acme/api", 1, "PR 1", "sam", Draft: false, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "abc", [], [], ReviewersRead: true, [], "https://github.com/acme/api/pull/1");
            Result<ProjectTruthFacts> facts = new ProjectTruthFacts([pull], new Dictionary<string, ReviewSummary>(), [], []);
            return ValueTask.FromResult(facts);
        }
    }

    [Fact]
    public async Task A_page_of_three_projects_reads_the_facts_once_for_the_union_of_their_sources()
    {
        var page = new ProjectsPage(Empty: null, Viewer: null, [], [Card("prj_a", "https://github.com/acme/api"), Card("prj_b", "https://github.com/acme/web"), Card("prj_c")], [], []);
        var facts = new CountingFacts();
        IReadOnlyDictionary<string, IReadOnlyList<string>> profile = new Dictionary<string, IReadOnlyList<string>> { ["prj_c"] = ["acme/infra"] };

        var attached = await ProjectTruthPage.Attach(page, profile, facts, Token);

        var asked = Assert.Single(facts.Asked);
        Assert.Equal(new[] { "acme/api", "acme/infra", "acme/web" }, asked.Repos.OrderBy(static r => r, StringComparer.Ordinal).ToArray());
        Assert.Equal(1, attached.Projects.Single(static c => c.Key == "prj_a").Truth.OpenPrs.Count);
        Assert.Equal(0, attached.Projects.Single(static c => c.Key == "prj_b").Truth.OpenPrs.Count);
    }
}
