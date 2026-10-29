using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Sync;

[Trait("Lane", "unit")]
public sealed class RecordBatchDescriptionTests
{
    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;

    private StoredRecord Build(string title, string text, string? description)
    {
        var item = new RecordItem(
            RecordKey.From("linear:project/p1"), RecordKind.Project, title, text, Author: null, Url: null, State: RecordState.Open, StateRaw: "started",
            CreatedAt: null, UpdatedAt: null, ClosedAt: null, SourceVersion.From("v1"), [], Repo: null, Audience: null, Description: description);
        var page = new RecordPage([item], [], SourceCursor.From("c"), CursorReset: false);
        return RecordBatches.From(SourceId.From("linear-projects"), page, SyncPhase.Incremental, _scrubbers, RecordSamples.Now).Records.Single().Record;
    }

    [Fact]
    public void The_description_is_cleaned_like_the_text()
    {
        var record = Build("P", "P\n\nabout", "Plan <!-- hidden --> one\n\nPlan two");

        Assert.Equal("Plan  one\n\nPlan two", record.Description);
    }

    [Fact]
    public void A_description_the_scrubber_blocks_is_not_stored_but_the_record_is_not_blocked()
    {
        var record = Build("P", "P\n\nabout", "x " + FakeScrubber.BlockMe);

        Assert.Null(record.Description);
        Assert.False(record.Blocked);
    }

    [Fact]
    public void A_blocked_text_or_title_drops_the_description()
    {
        Assert.Null(Build("P", "text " + FakeScrubber.BlockMe, "fine").Description);
        Assert.Null(Build("P " + FakeScrubber.BlockMe, "text", "fine").Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    [InlineData("<!-- only a comment -->")]
    public void A_description_that_is_empty_once_cleaned_is_null(string? description) =>
        Assert.Null(Build("P", "P", description).Description);

    [Fact]
    public void A_long_description_is_capped_at_the_description_cap()
    {
        var record = Build("P", "P", new string('a', RecordBatches.MaxDescription + 500));

        Assert.Equal(RecordBatches.MaxDescription, record.Description!.Length);
        Assert.EndsWith("…", record.Description, StringComparison.Ordinal);
    }
}
