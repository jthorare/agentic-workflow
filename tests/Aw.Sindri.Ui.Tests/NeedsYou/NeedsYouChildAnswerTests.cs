using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A needs-you row that carries via: "child": it says "Answer in the child", offers no answer form and keeps the engine's placeholder off every copy path.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouChildAnswerTests
{
    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    [Fact]
    public void A_child_pointer_row_says_Answer_in_the_child_and_does_not_carry_the_placeholder()
    {
        var row = ChildAnswerRows.Row("child");

        Assert.Equal(Labels.AnswerInChild, row.ChildAnswer!.Text);
        Assert.Equal(Labels.AnswerInChildOpensName(row.ShownId), row.ChildAnswer.Name);   // it opens the instance the row stands for, never a named child
        Assert.Equal(row.Id, row.ChildAnswer.NeedsYouRowId);
        Assert.Empty(row.Cli);
    }

    [Fact]
    public void A_child_pointer_row_is_mapped_without_the_placeholder_action_so_no_answer_control_is_built()
    {
        var served = Golden<NeedsYouList>("needs-you", "park").Rows[0];
        Assert.NotEmpty(served.Actions);   // the engine does serve a placeholder action on it: the row must drop it

        var row = ChildAnswerRows.Row("child");

        Assert.Empty(row.Controls);
        Assert.Empty(row.Entry.Actions);
        Assert.Empty(row.Entry.PrimaryAction);
    }

    private static NeedsYouRowViewModel Mirrored()
    {
        var list = Golden<NeedsYouList>("needs-you", "park-mirrored-child");
        var entry = NeedsYouMapper.Map(list, Golden<CommandList>("commands", "catalog"), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0)).Rows[0];
        return new NeedsYouRowViewModel(entry, seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));
    }

    [Fact]
    public void A_mirrored_child_row_is_named_for_and_opens_the_served_child()
    {
        var served = Golden<NeedsYouList>("needs-you", "park-mirrored-child").Rows[0];
        Assert.Equal("child", served.Via);
        Assert.NotEmpty(served.Child!);

        var row = Mirrored();

        Assert.Equal($"Answer in the child {served.Child}", row.ChildAnswer!.Name);
        Assert.True(row.ChildAnswer.OpensChild);
        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Child!), row.ChildAnswer.Link);
        Assert.Empty(row.Cli);   // the placeholder is still never copied
        Assert.Empty(row.Controls);
    }

    [Fact]
    public void A_child_row_served_no_child_keeps_the_fallback_name()
    {
        var row = ChildAnswerRows.Row("child");   // the older park golden serves via and no child

        Assert.False(row.ChildAnswer!.OpensChild);
        Assert.Equal(Labels.AnswerInChildOpensName(row.ShownId), row.ChildAnswer.Name);
    }

    [Fact]
    public void A_row_that_is_not_via_child_ignores_a_served_child()
    {
        var list = Golden<NeedsYouList>("needs-you", "park-mirrored-child");
        var served = list with { Rows = [list.Rows[0] with { Via = null }] };

        var entry = NeedsYouMapper.Map(served, Golden<CommandList>("commands", "catalog"), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0)).Rows[0];

        Assert.Null(entry.Child);
    }

    [Fact]
    public void A_row_without_via_is_unchanged()
    {
        var served = Golden<NeedsYouList>("needs-you", "park").Rows[0];

        var row = ChildAnswerRows.Row(via: null);

        Assert.Null(row.ChildAnswer);
        Assert.Equal(served.Cli, row.Cli);
        Assert.NotEmpty(row.Controls);
    }
}
