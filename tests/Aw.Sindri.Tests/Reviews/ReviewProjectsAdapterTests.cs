using Aw.Kernel;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Features.ReviewListFacts;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The project names the Reviews sections carry (PR review page spec §6.1): every card of the Projects page, finished and hidden ones too.</summary>
[Trait("Lane", "unit")]
public sealed class ReviewProjectsAdapterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProjectCard Card(string key, string name) =>
        new(key, ProjectOrigin.HandMade, name, Description: null, State: null, StateSetByOwner: false, Health: null, Lead: null, Target: null, OpenIssues: null,
            Counting: false, OpenPrs: null, PrScope: null, LastActivity: null, FinishedAt: null, Repos: [], Links: [], Notes: null, Synced: true, Version: 1);

    [Fact]
    public async Task Names_come_from_the_projects_finished_and_hidden_lists_and_a_repeated_key_counts_once()
    {
        var page = new ProjectsPage(Empty: null, Viewer: null, Freshness: [], [Card("prj_a", "Sindri"), Card("prj_b", "Web")], Finished: [Card("prj_c", "Old"), Card("prj_a", "Sindri again")], Hidden: [Card("prj_d", "Quiet")]);

        var names = (await new ProjectReviewNames(new Pages(page)).Names(Ct)).Value;

        Assert.Equal(["prj_a=Sindri", "prj_b=Web", "prj_c=Old", "prj_d=Quiet"], names.OrderBy(static n => n.Key, StringComparer.Ordinal).Select(static n => $"{n.Key}={n.Value}"), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_page_that_cannot_be_read_is_its_error()
    {
        var failure = new Error("SND-PROJECT-001", ErrorKind.Unavailable, "views are stale");

        var names = await new ProjectReviewNames(new Pages(failure)).Names(Ct);

        Assert.Equal(failure, names.Error);
    }

    private sealed class Pages(Result<ProjectsPage> answer) : IProjectPages
    {
        public ValueTask<Result<ProjectsPage>> Read(bool all, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
