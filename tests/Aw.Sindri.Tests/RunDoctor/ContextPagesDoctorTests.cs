using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.CodeIndexJobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RunDoctor;

/// <summary>The 'context:pages' doctor row: asked right after 'context:map', with the approved profile's pages in the scope it hands the engine.</summary>
public sealed class ContextPagesDoctorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(IReadOnlyList<string> Names, PagesEngineProxy Engine)> Rows(LoadedProfile? approved)
    {
        var h = new SliceHarness();
        var engine = PagesEngineProxy.Create();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));
        var checks = new ContextEngineChecks(engine.Proxy, new MapPublishDoctorCheck(new MapPublishStatusReader(FakeContextEngine.Instance, new NoIndexOutlooks(), h.Store, clock), clock),
            new LinearDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))),
            new CodeIndexDoctor(new CodeIndex.Testing.FakeCodeIndex(), new CodeIndex.Testing.FakeRepoHeads(), new InMemoryJobViews(), new CodeIndex.Testing.FakeEmbedder(), new CodeIndex.Testing.FakePins(), clock, new InMemoryProfiles()));
        var rows = await checks.Check(approved, Ct);
        return ([.. rows.Select(r => r.Name)], engine);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_pages_row_comes_right_after_the_map_row()
    {
        var (names, _) = await Rows(approved: null);

        Assert.Equal(["context:map", "context:pages", "context:roles"], names.Take(3), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_engine_gets_no_scope_without_an_approved_profile_and_the_profile_s_pages_with_one()
    {
        var withPages = SliceHarness.Profile();
        var page = new PageSettings(PagePinId.From("schedule-week"), "Schedule week", Surface: "surface:route/GET:/schedule/week", Actions: [new PageActionSettings(PageActionPinId.From("submit"), "Submit", "action:WeekPage.submit", ["scheduling"])]);
        withPages = withPages with { Profile = withPages.Profile with { Repos = [new RepoEntry("app", "/work/app", new IndexSettings(Pages: [page]))] } };

        var (_, none) = await Rows(approved: null);
        var (_, some) = await Rows(withPages);

        Assert.Null(Assert.Single(none.Scopes));
        var pin = Assert.Single(Assert.Single(Assert.Single(some.Scopes)!.Repos).Pages!);
        Assert.Equal(("schedule-week", "Schedule week", "action:WeekPage.submit"), (pin.Id.Value, pin.Name, Assert.Single(pin.Actions).ActionKey));
    }
}
