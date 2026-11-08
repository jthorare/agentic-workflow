using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The Needs-you row of a connection that has to be signed in again (connections PR 10b): the pure row, and the projection of its two events.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionRowsTests
{
    private static readonly DateTimeOffset _t = KnownInstants.Epoch;

    [Fact]
    public void The_row_names_the_connection_scrubs_the_reason_and_gives_the_sign_in_command()
    {
        var row = NeedsYouRows.ForConnection(ConnectionSamples.Name(), "invalid_grant", _t, new PassThroughScrubber()).Value;

        Expect.Equal(("connection:linear:work", NeedsYouKind.Connection, SubjectKind.Connection, "linear:work"), (row.Id.Value, row.Kind, row.Subject.Kind, row.Subject.Id.Value));
        Expect.Equal(("sindri connection signin linear:work", _t), (row.Cli, row.Since));
        Assert.Contains("invalid_grant", row.Reason, StringComparison.Ordinal);
        Assert.Contains("linear:work", row.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reason_that_holds_a_secret_is_scrubbed()
    {
        var scrubber = new MaskingScrubber("sk-live-SECRET");

        var row = NeedsYouRows.ForConnection(ConnectionSamples.Name(), "refused sk-live-SECRET", _t, scrubber).Value;

        Assert.DoesNotContain("sk-live-SECRET", row.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_kind_and_the_subject_kind_are_connection_on_the_wire()
    {
        Assert.Equal("\"connection\"", System.Text.Json.JsonSerializer.Serialize(NeedsYouKind.Connection));
        Assert.Equal("\"connection\"", System.Text.Json.JsonSerializer.Serialize(SubjectKind.Connection));
    }

    [Fact]
    public async Task A_needs_sign_in_event_puts_the_row_and_a_signed_in_event_clears_it()
    {
        var views = new ViewsHarness();

        await views.Fold(EventTypes.ConnectionNeedsSignIn, new ConnectionNeedsSignIn("linear:work", "invalid_grant", _t));
        var during = await Ids(views);
        await views.Fold(EventTypes.ConnectionSignedIn, new ConnectionSignedIn("linear:work", _t.AddMinutes(1)));

        Assert.Equal(["connection:linear:work"], during);
        Assert.Empty(await Ids(views));
    }

    [Fact]
    public async Task A_second_needs_sign_in_event_keeps_one_row_and_its_first_since()
    {
        var views = new ViewsHarness();

        await views.Fold(EventTypes.ConnectionNeedsSignIn, new ConnectionNeedsSignIn("linear:work", "invalid_grant", _t));
        await views.Fold(EventTypes.ConnectionNeedsSignIn, new ConnectionNeedsSignIn("linear:work", "invalid_grant", _t.AddMinutes(5)));

        var rows = (await views.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, TestContext.Current.CancellationToken)).Value;
        Assert.Equal(_t, Assert.Single(rows).Row.Since);
    }

    private sealed class MaskingScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[redacted]", StringComparison.Ordinal);
    }

    private static async Task<string[]> Ids(ViewsHarness views)
    {
        var rows = await views.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, TestContext.Current.CancellationToken);
        return [.. rows.Value.Select(static r => r.Row.Id.Value)];
    }
}
