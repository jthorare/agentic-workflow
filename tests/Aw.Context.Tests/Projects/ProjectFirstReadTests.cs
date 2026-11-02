using Aw.Context.Core.Linear;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Features.GetProjects;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Projects;

/// <summary>Issue #1734: 'FirstReadDone' is the stored phase of the projects feed's cursor, whether or not the profile can pull Linear right now (a teams-only profile builds no feed).</summary>
[Trait("Lane", "unit")]
public sealed class ProjectFirstReadTests
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";

    private readonly FakeRecordStore _store = new();
    private readonly FakeSourceIdentityStore _identities = new();
    private readonly FakeIndexSync _sync = new();

    [Fact]
    public async Task A_projects_cursor_past_recent_is_a_done_first_read_with_no_feed_listed()
    {
        await StoreProjectsPhase(SyncPhase.Incremental);

        var answer = await Read();

        Assert.True(answer.FirstReadDone);
    }

    [Fact]
    public async Task A_projects_cursor_still_in_recent_is_not_done_even_when_a_listed_feed_says_otherwise()
    {
        await StoreProjectsPhase(SyncPhase.Recent);
        _sync.Standing = [SourceProgress.From(LinearSourceIds.Projects, SyncPhase.Incremental, lastSync: null, lastCode: null)];

        var answer = await Read();

        Assert.False(answer.FirstReadDone);
    }

    [Fact]
    public async Task No_projects_cursor_is_not_done()
    {
        var answer = await Read();

        Assert.False(answer.FirstReadDone);
    }

    private async Task StoreProjectsPhase(SyncPhase phase)
    {
        var batch = RecordSamples.Batch(LinearSourceIds.Projects, 1) with { Position = new SyncPosition(Cursor: null, phase, BackfillCursor: null) };
        _ = await _store.Commit(batch, TestContext.Current.CancellationToken);
    }

    private async Task<ProjectFactsAnswer> Read()
    {
        var answer = await new GetProjectFactsService(_store, _identities, _sync).Handle(
            new GetProjectFactsRequest(new ProjectFactsRequest([]), MapWorld.HumanWith(Teams)), TestContext.Current.CancellationToken);
        Assert.False(answer.IsError);
        return answer.Value;
    }
}
