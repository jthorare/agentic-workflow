using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Features.GetProjects;
using Aw.Context.Testing.Linear;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Context.Testing.Records.LinearSideSamples;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The issue read and the project facts read give a human caller every audience <see cref="RecordMask"/> gives, including <see cref="ProfileAudiencePolicy.HumanExtras"/> (#2177).
/// The record under test has 'owner:library' as its only audience: a key only a human reads, given by no repo and no Linear team.
/// </summary>
[Trait("Lane", "integration")]
public sealed class HumanExtrasAudienceReadsTests : IAsyncLifetime
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";
    private static readonly RecordKey _humanOnlyIssue = RecordKey.From("linear:issue/human-only");
    private static readonly RecordKey _humanOnlyProject = RecordKey.From("linear:project/human-only");
    private static readonly RecordKey _teamProject = RecordKey.From("linear:project/team");

    private LinearHubWorld _world = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _world = await LinearHubWorld.Start(seed => seed.Team("ENG").Member("Ada Fixture").Issue("ENG", "Team issue", new FakeLinearIssueSeed { State = "Todo", Assignee = "Ada Fixture" }));
        _ = await _world.Tick();
        var other = RecordSamples.Record(_humanOnlyIssue.Value, "v1", RecordSamples.Linear) with { Audience = AudienceKey.From(ProfileAudiencePolicy.OwnerLibrary) };
        var store = _world.Provider.GetRequiredService<IRecordStore>();
        _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 99, RecordSamples.Write(other, "words") with { Side = Rows(Fields(Ada, Todo), LinearWrittenFields.None) }) with { Position = null }, Ct);
    }

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    private ContextScope SessionOfIssues() => _world.Scope with { Caller = new ContextCaller(ContextCallerKind.Session, ContextRepoName.From("split-panes"), ContextClient.Mcp) };

    private async Task<LinearIssueRow?> Issue(RecordKey key, ContextScope scope)
    {
        await using var provider = _world.Provider.CreateAsyncScope();
        var result = await provider.ServiceProvider.GetRequiredService<IContextEngine>().IssueGet(key, scope, Ct);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : "");
        return result.Value;
    }

    [Fact]
    public async Task A_human_reads_an_issue_whose_only_audience_is_a_human_extra()
    {
        var team = await Issue(RecordKey.From("linear:issue/" + _world.Linear.Issues.Single().Id), _world.Scope);
        var humanOnly = await Issue(_humanOnlyIssue, _world.Scope);

        Assert.NotNull(team);
        Assert.NotNull(humanOnly);
    }

    [Fact]
    public async Task A_session_does_not_read_an_issue_whose_only_audience_is_a_human_extra()
    {
        var team = await Issue(RecordKey.From("linear:issue/" + _world.Linear.Issues.Single().Id), SessionOfIssues());
        var humanOnly = await Issue(_humanOnlyIssue, SessionOfIssues());

        Assert.NotNull(team);
        Assert.Null(humanOnly);
    }

    [Fact]
    public async Task A_human_reads_the_facts_of_a_project_whose_only_audience_is_a_human_extra()
    {
        var answer = await ProjectFacts(MapWorld.HumanWith(Teams));

        Assert.Contains(_teamProject, answer.Catalog.Select(p => p.Key));
        Assert.Contains(_humanOnlyProject, answer.Catalog.Select(p => p.Key));
    }

    [Fact]
    public async Task A_session_does_not_read_the_facts_of_a_project_whose_only_audience_is_a_human_extra()
    {
        using var section = System.Text.Json.JsonDocument.Parse(Teams);
        var session = MapWorld.Session("aw-split-panes") with { Settings = section.RootElement.Clone() };

        var answer = await ProjectFacts(session);

        Assert.Contains(_teamProject, answer.Catalog.Select(p => p.Key));
        Assert.DoesNotContain(_humanOnlyProject, answer.Catalog.Select(p => p.Key));
    }

    private static async Task<ProjectFactsAnswer> ProjectFacts(ContextScope scope)
    {
        var store = new FakeRecordStore();
        var projects = new[] { Project(_teamProject, "linear:SPL"), Project(_humanOnlyProject, ProfileAudiencePolicy.OwnerLibrary) }.Select(r => RecordSamples.Write(r, r.Title)).ToArray();
        _ = await store.Commit(RecordSamples.Batch(LinearSourceIds.Projects, 1, projects), Ct);
        var answer = await new GetProjectFactsService(store, new FakeSourceIdentityStore(), new FakeIndexSync()).Handle(new GetProjectFactsRequest(new ProjectFactsRequest([]), scope), Ct);
        Assert.False(answer.IsError);
        return answer.Value;
    }

    private static StoredRecord Project(RecordKey key, string audience) =>
        RecordSamples.Record(key.Value, source: LinearSourceIds.Projects) with
        {
            Kind = RecordKind.Project,
            Title = key.Value,
            Audience = AudienceKey.From(audience),
            State = RecordState.Open,
            StateRaw = "started",
        };
}
