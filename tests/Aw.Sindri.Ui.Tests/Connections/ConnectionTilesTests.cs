using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>The Connections page opens on service tiles (#2537): tiles are the empty state, the custom form is secondary, a disabled Add says why, and the default view has no jargon.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionTilesTests
{
    private static readonly string[] _jargon = ["MCP server", "service:label", "Server address", "instead of a token"];

    private readonly ConnectionsFixture _f = new();

    [Fact]
    public async Task The_default_view_is_the_service_tiles_and_the_custom_form_is_closed()
    {
        var page = await _f.Shown();

        Assert.Equal(["github", "linear", "slack", "google", "spotify"], page.Tiles.Select(t => t.Id), StringComparer.Ordinal);
        Assert.False(page.Add.IsOpen);
    }

    [Fact]
    public async Task An_empty_page_shows_the_tiles_and_no_empty_box_and_no_count()
    {
        var page = await _f.Shown();

        Assert.False(page.IsEmpty);
        Assert.Equal(string.Empty, page.EmptyText);
        Assert.Equal(string.Empty, page.Head);
    }

    [Fact]
    public async Task A_disabled_Add_carries_a_reason()
    {
        var page = await _f.Shown();
        page.Add.Open();

        Assert.False(page.Add.CanSend);
        Assert.Equal("Enter a server address", page.Add.SendBlockedReason);
    }

    [Fact]
    public async Task The_default_view_has_no_jargon()
    {
        var page = await _f.Shown();
        var words = new List<string> { page.Text.Title, page.Text.Purpose, page.Head, page.EmptyText };
        words.AddRange(page.Tiles.SelectMany(t => new[] { t.Title }));

        foreach (var word in _jargon)
        {
            Assert.DoesNotContain(words, w => w.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }
}
