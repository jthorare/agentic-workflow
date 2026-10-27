using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>Create item keeps its layout when the title error shows, and offers the known repos.</summary>
[Lane(TestLane.Unit)]
public sealed class CreateItemLayoutTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CreateItemViewModel> Form()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new CreateItemViewModel(catalog, () => _api, _server);
    }

    [Fact]
    public async Task TitleErrorLine_keeps_a_line_when_there_is_no_error_and_shows_the_error_after_a_blank_submit()
    {
        var form = await Form();
        form.Open();

        Assert.Equal("\u00A0", form.TitleErrorLine);

        await form.Submit(Token);

        Assert.Equal(form.TitleError, form.TitleErrorLine);
        Assert.False(string.Equals("\u00A0", form.TitleErrorLine, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TitleErrorLine_returns_to_the_resting_line_once_a_title_is_typed()
    {
        var form = await Form();
        form.Open();
        await form.Submit(Token);

        form.Title = "Fix the thing";

        Assert.Equal("\u00A0", form.TitleErrorLine);
    }

    [Fact]
    public async Task RepoChoices_offer_the_known_repos_without_all_repos_each_time_the_form_opens()
    {
        var form = await Form();
        IReadOnlyList<string> known = ["All repos", "alpha/one", "beta/two"];
        form.KnownRepos = () => known;

        form.Open();

        Assert.Equal(["alpha/one", "beta/two"], form.RepoChoices);

        known = ["All repos", "gamma/three"];
        form.Open();

        Assert.Equal(["gamma/three"], form.RepoChoices);
    }
}
