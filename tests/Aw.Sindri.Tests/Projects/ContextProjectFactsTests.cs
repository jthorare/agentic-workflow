using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ProjectFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Projects;

[Trait("Lane", "unit")]
public sealed class ContextProjectFactsTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly StubContext _context = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Read_maps_each_project_and_names_people_by_their_linear_user_ref()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        using var caller = _h.Callers.EnterService("projects-test");
        var lead = new UserRef(ExternalId.From("u-lead"), "Ana");
        _context.Answer = new ProjectFactsAnswer(new UserRef(ExternalId.From("u-me"), "Joi"), [Fact("p1", lead, leadIsOwner: false)], [Fact("p1", lead, leadIsOwner: false), Fact("p2", lead: null, leadIsOwner: false)], ["ENG"], FirstReadDone: true);

        var read = await Facts().Read(["linear:project/p1", "not a key"], Ct);

        var snapshot = read.Value;
        Assert.Equal(("linear:user/u-me", "Joi"), (snapshot.Owner!.Ref, snapshot.Owner.Name));
        var p1 = Assert.Single(snapshot.Projects);
        Assert.Equal(("linear:project/p1", "Project p1", "started", "atRisk", 3, true), (p1.Key, p1.Title, p1.StateRaw, p1.Health, p1.OpenIssues, p1.Counting));
        Assert.Equal("linear:user/u-lead", p1.Lead!.Ref);
        Assert.Equal("linear:project/p1 linear:project/p2", string.Join(' ', snapshot.Catalog.Select(static f => f.Key)));
        Assert.Null(snapshot.Catalog[1].Lead);
        Assert.Equal("ENG", Assert.Single(snapshot.Teams));
        Assert.True(snapshot.FirstReadDone);
        Assert.Equal("linear:project/p1", Assert.Single(_context.Asked!.Attached).Value);
    }

    [Fact]
    public async Task A_context_failure_is_SND_PROJECT_002_naming_the_cause()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        using var caller = _h.Callers.EnterService("projects-test");
        _context.Failure = new Error("SND-CONTEXT-004", ErrorKind.Unavailable, "the store is not open");

        var read = await Facts().Read([], Ct);
        var freshness = await Facts().Freshness(Ct);

        Assert.Equal(("SND-PROJECT-002", "SND-CONTEXT-004: the store is not open"), (read.Error.Code, read.Error.Message));
        Assert.Equal("SND-PROJECT-002", freshness.Error.Code);
    }

    [Fact]
    public async Task No_approved_profile_is_SND_PROJECT_002_and_context_is_not_asked()
    {
        using var caller = _h.Callers.EnterService("projects-test");

        var read = await Facts().Read([], Ct);

        Assert.Equal(SindriErrors.ProjectFactsUnavailable.Code, read.Error.Code);
        Assert.StartsWith(SindriErrors.ProfileNotApproved.Code, read.Error.Message, StringComparison.Ordinal);
        Assert.Null(_context.Asked);
    }

    [Fact]
    public async Task Freshness_keeps_the_project_issue_and_github_feeds_only()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        using var caller = _h.Callers.EnterService("projects-test");
        _context.Progress =
        [
            Source("linear-projects", usable: true, code: null),
            Source("linear", usable: false, code: "SND-CONTEXT-010"),
            Source("github", usable: true, code: null),
            Source("slack", usable: true, code: null),
        ];

        var rows = (await Facts().Freshness(Ct)).Value;

        Assert.Equal([("linear-projects", true, null), ("linear", false, "SND-CONTEXT-010"), ("github", true, null)], rows.Select(static r => (r.Source, r.Usable, r.LastCode)));
    }

    private ContextProjectFacts Facts() => new(_h.Profiles, _h.Callers, EngineProxy.Of(_context));

    private static ProjectFact Fact(string id, UserRef? lead, bool leadIsOwner) =>
        new(RecordKey.From("linear:project/" + id), "Project " + id, Description: null, "https://linear.app/w/project/" + id, "started", "atRisk", DueAt: null, lead, leadIsOwner,
            _at, ClosedAt: null, OpenIssues: 3, OpenPrs: null, LastActivity: _at, Repos: null, Counting: true);

    private static SourceProgress Source(string id, bool usable, string? code) => new(SourceId.From(id), Phase: null, _at, code, usable, Complete: usable);

    /// <summary>Answers Projects and Progress as set; the rest of the sync facade is never called by the adapter.</summary>
    private sealed class StubContext : IContextProjects, IIndexSync
    {
        public ProjectFactsAnswer Answer { get; set; } = new(Owner: null, [], [], [], FirstReadDone: false);

        public IReadOnlyList<SourceProgress> Progress { get; set; } = [];

        public Error? Failure { get; set; }

        public ProjectFactsRequest? Asked { get; private set; }

        public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken)
        {
            Asked = request;
            return ValueTask.FromResult(Failure is { } f ? (Result<ProjectFactsAnswer>)f : Answer);
        }

        ValueTask<Result<IReadOnlyList<SourceProgress>>> IIndexSync.Progress(ContextScope scope, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Failure is { } f ? (Result<IReadOnlyList<SourceProgress>>)f : Results.Success(Progress));

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
