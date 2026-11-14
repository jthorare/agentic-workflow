using Aw.Meta.Core.Github;
using Aw.Meta.Tests.MergeRun;

namespace Aw.Meta.Tests.Github;

public sealed class GhJsonQueueTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_pull_request_in_a_merge_queue_reads_its_position_and_entry_state() =>
        Assert.Equal(new MergeQueueView(Enabled: true, InQueue: true, Position: 2, EntryState: "AWAITING_CHECKS", RemovedReason: null), GhJson.QueueView(QueueAnswers.Queued(2)));

    [Fact, Trait("Lane", "unit")]
    public void A_pull_request_the_queue_removed_reads_the_reason() =>
        Assert.Equal(new MergeQueueView(Enabled: true, InQueue: false, Position: null, EntryState: null, RemovedReason: "FAILED_CHECKS"), GhJson.QueueView(QueueAnswers.Removed("FAILED_CHECKS")));

    [Fact, Trait("Lane", "unit")]
    public void A_base_branch_without_a_merge_queue_reads_as_not_enabled() =>
        Assert.Equal(new MergeQueueView(Enabled: false, InQueue: false, Position: null, EntryState: null, RemovedReason: null), GhJson.QueueView(QueueAnswers.Off));

    [Fact, Trait("Lane", "unit")]
    public void A_pull_request_in_a_stack_reads_as_stacked() =>
        Assert.Equal(new MergeQueueView(Enabled: false, InQueue: false, Position: null, EntryState: null, RemovedReason: null) { Stacked = true }, GhJson.QueueView(QueueAnswers.Stacked()));

    [Fact, Trait("Lane", "unit")]
    public void A_pull_request_with_no_stack_reads_as_not_stacked() =>
        Assert.False(GhJson.QueueView(QueueAnswers.Off)!.Stacked);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1]")]
    [InlineData("""{"errors":[{"message":"boom"}],"data":null}""")]
    [InlineData("""{"data":{"repository":{"pullRequest":null}}}""")]
    public void An_answer_without_a_pull_request_is_nothing(string json) => Assert.Null(GhJson.QueueView(json));
}
