using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Features.ProjectFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Projects;

/// <summary>
/// A project the owner leads that Context holds only in its catalog (#1720), read through the real ContextProjectFacts into the page services:
/// it gets a card, GetProject finds it by its Linear key, and once hidden it sits in the hidden list instead of vanishing.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LedProjectsReachThePageTests
{
    private const string LedKey = ProjectRows.LinearKey;

    private static readonly DateTimeOffset _at = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private readonly ProjectWriteHarness _h = new();
    private readonly OnlyInCatalog _context = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProjectPageReader Reader() => new(_h.Views.Views, new ContextProjectFacts(_h.Slice.Profiles, _h.Slice.Callers, EngineProxy.Of(_context)), _h.Slice.Profiles, FakeContextEngine.Instance, _h.Slice.Clock);

    private async Task ApproveProfile() => _ = await _h.Slice.Profiles.Save(SliceHarness.Profile(), Token);

    [Fact]
    public async Task ListProjects_gives_a_project_the_owner_leads_a_card_though_only_the_catalog_holds_it()
    {
        await ApproveProfile();
        using var entered = _h.Human();

        var result = await new ListProjectsService(Reader(), _h.Views.Progress, _h.Slice.Callers).Handle(new ListProjectsRequest(All: false), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(LedKey, Assert.Single(result.Value.Projects).Key);
    }

    [Fact]
    public async Task GetProject_finds_a_project_the_owner_leads_by_its_linear_key()
    {
        await ApproveProfile();
        using var entered = _h.Human();

        var result = await new GetProjectService(Reader(), _h.Views.Views, _h.Views.Progress, _h.Slice.Callers).Handle(new GetProjectRequest(LedKey), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal((LedKey, false), (result.Value.Project.Key, result.Value.Hidden));
    }

    [Fact]
    public async Task A_hidden_project_the_owner_leads_is_accepted_and_then_found_as_hidden()
    {
        await _h.Lease();
        await ApproveProfile();
        using var entered = _h.Human();

        var hide = await _h.Hide().Handle(new HideProjectRequest(LedKey, Hidden: true, Version: 0), Token);
        await _h.SeedHidden(version: 1, hidden: true);
        var read = await new GetProjectService(Reader(), _h.Views.Views, _h.Views.Progress, _h.Slice.Callers).Handle(new GetProjectRequest(LedKey), Token);

        Assert.True(hide.IsSuccess, hide.IsError ? hide.Error.ToString() : null);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.ToString() : null);
        Assert.Equal((LedKey, true), (read.Value.Project.Key, read.Value.Hidden));
    }

    private static ProjectFact Led() =>
        new(RecordKey.From(LedKey), "Project led", Description: null, "https://linear.app/w/project/led", "started", "onTrack", DueAt: null, new UserRef(ExternalId.From("u-me"), "Joi"), LeadIsOwner: true,
            _at, ClosedAt: null, OpenIssues: 3, OpenPrs: null, LastActivity: _at, Repos: null, Counting: true);

    /// <summary>Answers Projects with the led project in Catalog only, as Context does for a key nobody attached; the rest of the sync facade is never called.</summary>
    private sealed class OnlyInCatalog : IContextProjects, IIndexSync
    {
        public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken) =>
            ValueTask.FromResult((Result<ProjectFactsAnswer>)new ProjectFactsAnswer(new UserRef(ExternalId.From("u-me"), "Joi"), [], [Led()], [], FirstReadDone: true));

        public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<IReadOnlyList<SourceProgress>>([]));

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
