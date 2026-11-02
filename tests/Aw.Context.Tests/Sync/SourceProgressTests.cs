using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;

namespace Aw.Context.Tests.Sync;

/// <summary>A source's progress row (owned-index spec §5.8) and the one-time Linear issue re-read it flags (projects page spec §4.3).</summary>
public sealed class SourceProgressTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_source_in_Recent_that_already_holds_records_is_re_reading()
    {
        var row = SourceProgress.From(LinearSourceIds.Issues, SyncPhase.Recent, lastSync: null, lastCode: null, stored: 1200);

        Assert.Equal((false, false, 1200), (row.Usable, row.Complete, row.ReReading));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_source_with_no_records_or_past_Recent_or_not_checked_is_not_re_reading()
    {
        Assert.Null(SourceProgress.From(LinearSourceIds.Issues, SyncPhase.Recent, lastSync: null, lastCode: null, stored: 0).ReReading);
        Assert.Null(SourceProgress.From(LinearSourceIds.Issues, SyncPhase.Incremental, lastSync: null, lastCode: null, stored: 4000).ReReading);
        Assert.Null(SourceProgress.From(LinearSourceIds.Issues, SyncPhase.Recent, lastSync: null, lastCode: null).ReReading);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_projects_feed_has_its_own_source_id_so_its_own_progress_row()
    {
        Assert.Equal("linear-projects", SourceProgress.From(LinearSourceIds.Projects, SyncPhase.Incremental, lastSync: null, lastCode: null).Source.Value);
        Assert.NotEqual(LinearSourceIds.Issues, LinearSourceIds.Projects);
    }
}
