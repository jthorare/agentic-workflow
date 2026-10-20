using Aw.Sindri.Host.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Aw.Sindri.Tests.Server;

/// <summary>The first cursor an event stream request names (API spec §5.1, C4).</summary>
public sealed class EventStreamBindTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("3", null, 3)]
    [InlineData("3", "1", 3)]
    [InlineData(null, "2", 2)]
    [InlineData(null, null, 0)]
    [InlineData("-1", null, 0)]   // not a sequence: the header is ignored as an unparseable one is
    [InlineData("-1", "2", 2)]
    [InlineData("nope", "2", 2)]
    public void The_cursor_is_the_header_when_it_holds_a_sequence_else_after_else_zero(string? lastEventId, string? after, long expected)
    {
        var context = new DefaultHttpContext();
        if (lastEventId is not null)
        {
            context.Request.Headers["Last-Event-ID"] = lastEventId;
        }

        if (after is not null)
        {
            context.Request.QueryString = new QueryString("?after=" + after);
        }

        var bound = EventStreamOpener.Bind(context.Request);

        Assert.Equal(expected, bound.Value.After);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("?types=workflow.instance-changed", null, 9)]   // derived only, no cursor: the head, not a replay of the history
    [InlineData("?types=workflow.instance-changed&after=3", null, 3)]
    [InlineData("?types=workflow.instance-changed&after=0", null, 0)]   // an explicit 0 is still asked for
    [InlineData("?types=workflow.instance-changed", "4", 4)]
    [InlineData("?types=workflow.instance-changed", "nope", 9)]   // an unparseable Last-Event-ID names no cursor
    [InlineData("?types=workflow.instance-changed", "-1", 9)]   // nor does a negative one
    [InlineData("?types=workflow.instance-changed&after=3", "4", 4)]   // both: Bind's cursor is the header
    [InlineData("?types=workflow.instance-changed&after=3", "-1", 3)]   // a negative header falls through to after=
    [InlineData("?types=workflow.instance-changed&after=", null, 9)]   // an empty after= is no cursor
    [InlineData("?types=workflow.instance-changed,service.heartbeat", null, 0)]   // it also names a ledger type: the usual cursor
    [InlineData("?types=workflow.*", null, 0)]
    [InlineData("", null, 0)]
    public void A_stream_of_only_the_derived_type_with_no_cursor_opens_at_the_head(string query, string? lastEventId, long expected)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        if (lastEventId is not null)
        {
            context.Request.Headers["Last-Event-ID"] = lastEventId;
        }

        var bound = EventStreamOpener.Bind(context.Request).Value;

        Assert.Equal(expected, EventStreamOpener.StartAt(context.Request, bound, head: 9));
    }
}
