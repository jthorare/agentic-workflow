using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A run parked in a nested child (api #810): one row for the run, pointing at the descendant that decides, with no action and no command of its own and the reason in the descendant's words.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouNestedParkTests
{
    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    private static (NeedsYouRowView Served, NeedsYouRowViewModel Row, int Rows) Nested(string scenario)
    {
        var list = Golden<NeedsYouList>("needs-you", scenario);
        var feed = NeedsYouMapper.Map(list, Golden<CommandList>("commands", "catalog"), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0));
        return (list.Rows[0], new NeedsYouRowViewModel(feed.Rows[0], seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon)), feed.Rows.Count);
    }

    [Theory]
    [InlineData("park-nested")]
    [InlineData("park-nested-refused")]
    public void One_row_per_run_points_at_the_descendant_that_decides_and_offers_nothing_of_its_own(string scenario)
    {
        var (served, row, rows) = Nested(scenario);

        Assert.Equal(1, rows);
        Assert.Equal("child", served.Via);
        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Child!), row.ChildAnswer!.Link);
        Assert.True(row.ChildAnswer.OpensChild);
        Assert.Equal($"Answer in the child {served.Child}", row.ChildAnswer.Name);
        Assert.Empty(row.Entry.PrimaryAction);
        Assert.Empty(row.Entry.Actions);
        Assert.Empty(row.Cli);
        Assert.Empty(row.Controls);
    }

    [Fact]
    public void The_reason_is_the_descendants_own_words_and_names_the_descendant()
    {
        var (served, row, _) = Nested("park-nested");

        Assert.Equal(served.Reason, row.Entry.Reason);
        Assert.Contains(served.Child!, row.Entry.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_dispatch_says_why_in_the_descendants_words()
    {
        var (served, row, _) = Nested("park-nested-refused");

        Assert.Equal(served.Reason, row.Entry.Reason);
        Assert.StartsWith("SND-WF-010: ", row.Entry.Reason, StringComparison.Ordinal);
        Assert.Contains(served.Child!, row.Entry.Reason, StringComparison.Ordinal);
    }
}
