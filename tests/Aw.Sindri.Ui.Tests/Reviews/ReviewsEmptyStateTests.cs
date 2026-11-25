using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews page with GitHub not connected (#2555): one line over the Connect GitHub action, no Library chip with nothing to open, no key hints over an empty list.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewsEmptyStateTests
{
    private readonly ReviewsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<ReviewsPageViewModel> ShownWithLibrary(string scenario, string state, string text)
    {
        var list = ReviewsFixture.Golden(scenario);
        list["library"]!["state"] = state;
        list["library"]!["text"] = text;
        _f.Serve(list);
        return _f.Shown(Token);
    }

    [Fact]
    public async Task Not_connected_says_what_the_page_shows_over_the_action()
    {
        _f.Serve("connect-github");
        var page = await _f.Shown(Token);

        Assert.Equal("Pull requests waiting on your review, with their threads and checks.", page.ConnectLine);
        Assert.Equal("Connect GitHub", page.ConnectText);
    }

    [Fact]
    public async Task Sign_in_again_keeps_the_same_line()
    {
        _f.Serve("sign-in-again");
        var page = await _f.Shown(Token);

        Assert.Equal("Pull requests waiting on your review, with their threads and checks.", page.ConnectLine);
    }

    [Fact]
    public async Task A_list_with_rows_has_no_connect_line()
    {
        _f.Serve("some");
        var page = await _f.Shown(Token);

        Assert.Equal(string.Empty, page.ConnectLine);
    }

    [Fact]
    public async Task The_Library_chip_is_hidden_while_the_library_is_read()
    {
        var page = await ShownWithLibrary("connect-github", "reading", "Library: reading…");

        Assert.False(page.ShowLibrary);
    }

    [Fact]
    public async Task The_Library_chip_is_hidden_when_there_are_no_design_docs()
    {
        var page = await ShownWithLibrary("connect-github", "empty", "Library: no design docs yet");

        Assert.False(page.ShowLibrary);
    }

    [Theory]
    [InlineData("ok", "Library: 41 docs, 3 with errors")]
    [InlineData("error", "Library: 33 docs, 1 folder can't be read")]
    public async Task The_Library_chip_shows_when_its_sheet_has_something_to_open(string state, string text)
    {
        var page = await ShownWithLibrary("some", state, text);

        Assert.True(page.ShowLibrary);
    }

    [Fact]
    public async Task Key_hints_hide_at_zero_rows()
    {
        _f.Serve("connect-github");
        var page = await _f.Shown(Token);

        Assert.False(page.ShowHints);
    }

    [Fact]
    public async Task Key_hints_hide_on_the_empty_list_and_show_with_rows()
    {
        _f.Serve("empty");
        var empty = await _f.Shown(Token);
        _f.Serve("some");
        var some = await _f.Shown(Token);

        Assert.False(empty.ShowHints);
        Assert.True(some.ShowHints);
    }
}
