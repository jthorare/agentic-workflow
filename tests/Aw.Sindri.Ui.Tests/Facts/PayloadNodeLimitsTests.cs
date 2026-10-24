using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>The payload tree shows a body's data, not a sample of it: a wide array and a long string are shown in full up to generous limits (q93, UI-L12).</summary>
[Lane(TestLane.Unit)]
public sealed class PayloadNodeLimitsTests
{
    [Fact]
    public void An_array_of_a_thousand_items_shows_every_item()
    {
        using var body = JsonDocument.Parse("[" + string.Join(',', Enumerable.Range(0, 1000)) + "]");

        Assert.Equal(1000, PayloadNodeViewModel.Roots(body.RootElement).Count);
    }

    [Fact]
    public void A_string_of_two_thousand_characters_is_shown_whole()
    {
        var text = new string('x', 2000);
        using var body = JsonDocument.Parse("{\"note\":\"" + text + "\"}");

        Assert.Equal("note: " + text, Assert.Single(PayloadNodeViewModel.Roots(body.RootElement)).Text);
    }
}
