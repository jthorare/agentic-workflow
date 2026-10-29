using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Features.ProjectFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Projects;

[Trait("Lane", "unit")]
public sealed class ContextProjectFactsLedTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly StubContext _context = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_project_the_owner_leads_is_in_the_snapshot_even_when_only_the_catalog_has_it()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        using var caller = _h.Callers.EnterService("projects-test");
        var me = new UserRef(ExternalId.From("u-me"), "Joi");
        var led = Fact("led", me, leadIsOwner: true);
        var attached = Fact("attached", lead: null, leadIsOwner: false);
        _context.Answer = new ProjectFactsAnswer(me, [attached], [led, attached], [], FirstReadDone: true);

        var read = await Facts().Read(["linear:project/attached"], Ct);

        Assert.Equal("linear:project/led linear:project/attached", string.Join(' ', read.Value.Projects.Select(static p => p.Key)));
    }

    [Fact]
    public async Task A_led_project_that_is_also_attached_appears_once()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        using var caller = _h.Callers.EnterService("projects-test");
        var me = new UserRef(ExternalId.From("u-me"), "Joi");
        var led = Fact("led", me, leadIsOwner: true);
        _context.Answer = new ProjectFactsAnswer(me, [led], [led], [], FirstReadDone: true);

        var read = await Facts().Read(["linear:project/led"], Ct);

        Assert.Equal("linear:project/led", Assert.Single(read.Value.Projects).Key);
    }

    private ContextProjectFacts Facts() => new(_h.Profiles, _h.Callers, EngineProxy.Of(_context));

    private static ProjectFact Fact(string id, UserRef? lead, bool leadIsOwner) =>
        new(RecordKey.From("linear:project/" + id), "Project " + id, Description: null, "https://linear.app/w/project/" + id, "started", "onTrack", DueAt: null, lead, leadIsOwner,
            _at, ClosedAt: null, OpenIssues: 3, OpenPrs: null, LastActivity: _at, Repos: null, Counting: true);

    /// <summary>Answers Projects as set; the rest of the sync facade is never called by these tests.</summary>
    private sealed class StubContext : IContextProjects, IIndexSync
    {
        public ProjectFactsAnswer Answer { get; set; } = new(Owner: null, [], [], [], FirstReadDone: false);

        public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken) =>
            ValueTask.FromResult((Result<ProjectFactsAnswer>)Answer);

        public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
