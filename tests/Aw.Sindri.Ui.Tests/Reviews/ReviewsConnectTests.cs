using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews tab uses the GitHub connection from the Connections page (#2499): Connect GitHub / Sign in again instead of a typed-in login.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewsConnectTests
{
    private readonly ReviewsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ReviewsPageViewModel> Shown(string scenario)
    {
        _f.Serve(scenario);
        return await _f.Shown(Token);
    }

    private static List<DeepLink> Requests(ReviewsPageViewModel page)
    {
        var links = new List<DeepLink>();
        page.ConnectRequested += (_, e) => links.Add(e.Link);
        return links;
    }

    [Fact]
    public async Task No_connection_shows_one_Connect_GitHub_action_and_nothing_else()
    {
        var page = await Shown("connect-github");

        Assert.True(page.IsConnect);
        Assert.Equal("Connect GitHub", page.ConnectText);
        Assert.False(page.IsLoginPrompt);   // no "Set your GitHub login" box
        Assert.Empty(page.Groups);
        Assert.DoesNotContain("SND-REVIEW-008", page.Error + page.SubLine + page.ConnectText, StringComparison.Ordinal);   // no raw code headline
    }

    [Fact]
    public async Task Pressing_Connect_GitHub_asks_the_shell_to_open_Connections()
    {
        var page = await Shown("connect-github");
        var links = Requests(page);

        page.PressConnect();

        // Guess: no add-with-preset link exists yet, so the builder of the green side picks it; it must be a connection link.
        var link = Assert.Single(links);
        Assert.Equal(DeepLinkKind.Connection, link.Kind);
    }

    [Fact]
    public async Task Sign_in_again_is_labelled_and_opens_the_connection_that_needs_it()
    {
        var page = await Shown("sign-in-again");
        var links = Requests(page);

        Assert.True(page.IsConnect);
        Assert.Equal("Sign in to GitHub again", page.ConnectText);
        Assert.False(page.IsLoginPrompt);

        page.PressConnect();

        var link = Assert.Single(links);
        Assert.Equal((DeepLinkKind.Connection, "connection/github:work"), (link.Kind, link.ToString()));
    }

    [Fact]
    public async Task Sign_in_again_never_shows_the_raw_connectors_code()
    {
        var page = await Shown("sign-in-again");

        Assert.Contains("needs sign-in", page.SubLine, StringComparison.Ordinal);
        Assert.DoesNotContain("AW-CONNECTORS-007", page.SubLine + page.Error + page.StaleText + page.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_login_from_the_connection_renders_the_list_with_no_connect_action()
    {
        var page = await Shown("login-from-connection");

        Assert.False(page.IsConnect);
        Assert.Equal(string.Empty, page.ConnectText);
        Assert.False(page.IsLoginPrompt);
        Assert.Equal(2, page.Groups.Count);
        Assert.Contains("joi-example", page.SubLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_list_with_a_login_and_no_connect_block_is_unchanged()
    {
        var page = await Shown("some");

        Assert.False(page.IsConnect);
        Assert.False(page.IsLoginPrompt);
        Assert.Equal(2, page.Groups.Count);
        Assert.Contains("joi-example", page.SubLine, StringComparison.Ordinal);
    }
}
