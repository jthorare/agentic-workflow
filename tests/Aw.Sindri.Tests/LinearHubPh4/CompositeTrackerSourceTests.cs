using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.LinearHubPh4;

/// <summary>CompositeTrackerSource (Linear hub spec 2026-11-04 §6.1): the file source when AW_SINDRI_TRACKER_DIR is set, else the store source.</summary>
public sealed class CompositeTrackerSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static InMemoryTrackerSource Source(string title) => new(new TrackerSeed([], [new TrackerSeedIssue("ENG-12", title, "Body.", Project: null, [])]));

    // red: step 2 passes the directory setting and both sources to the constructor; the stub takes none yet
    private static CompositeTrackerSource Composite(string? trackerDir, ITrackerSource file, ITrackerSource store)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(store);
        _ = trackerDir;
        return new CompositeTrackerSource();
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task The_file_source_answers_when_AW_SINDRI_TRACKER_DIR_is_set()
    {
        var read = await Composite("/fixtures/tracker", Source("from the file"), Source("from the store")).Read("ENG-12", TrackerRead.Scoping, Token);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.ToString() : null);
        Assert.Equal("from the file", read.Value.Title);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task The_store_source_answers_when_it_is_not_set()
    {
        var read = await Composite(trackerDir: null, Source("from the file"), Source("from the store")).Read("ENG-12", TrackerRead.Scoping, Token);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.ToString() : null);
        Assert.Equal("from the store", read.Value.Title);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_blank_setting_counts_as_not_set()
    {
        var read = await Composite("  ", Source("from the file"), Source("from the store")).Read("ENG-12", TrackerRead.Scoping, Token);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.ToString() : null);
        Assert.Equal("from the store", read.Value.Title);
    }
}
