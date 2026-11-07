using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.SuggestLinks;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearHubPh4;

/// <summary>Suggested links (Linear hub spec 2026-11-04 §6.1, TP-8): a stored pull request naming ENG-12 suggests the link; nothing is applied until the owner links it.</summary>
public sealed class SuggestLinksTests
{
    private readonly Ph4Rig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewPull Pull(string title, int number = 7) =>
        new("acme/api", number, title, "sam", Draft: false, KnownInstants.Epoch.AddDays(-1), KnownInstants.Epoch, "abc1234", [], [], ReviewersRead: true, [], Url: "https://github.com/acme/api/pull/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));

    // red: step 2 passes the PR records, the issue facts and the items view to the constructor; the stub takes none yet
    private static SuggestLinksService Service(FakeReviewFacts pulls, Ph4Rig rig)
    {
        ArgumentNullException.ThrowIfNull(pulls);
        ArgumentNullException.ThrowIfNull(rig);
        return new SuggestLinksService();
    }

    private static FakeReviewFacts Pulls(params ReviewPull[] pulls) =>
        new FakeReviewFacts().With(new ReviewFactsSnapshot(pulls, Github: null, new ReviewLibraryFacts(Reading: false, Docs: 0, WithErrors: 0, [])));

    [Fact, Lane(TestLane.Unit)]
    public async Task A_pull_request_naming_an_unheld_issue_suggests_a_link()
    {
        var suggested = await Service(Pulls(Pull("ENG-12: stop dropping rows")), _rig).Handle(new SuggestLinksRequest(), Token);

        Assert.True(suggested.IsSuccess, suggested.IsError ? suggested.Error.ToString() : null);
        Assert.Equal(new LinkSuggestion("ENG-12", "acme/api", 7), Assert.Single(suggested.Value.Suggestions));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_suggestion_is_not_applied_until_the_owner_links_it()
    {
        await _rig.Hold();

        _ = await Service(Pulls(Pull("ENG-12: stop dropping rows")), _rig).Handle(new SuggestLinksRequest(), Token);

        Assert.Empty(_rig.Events(EventTypes.ItemLinked));
        Assert.Empty(_rig.Events(EventTypes.ItemCreated));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_issue_an_open_item_already_holds_is_not_suggested()
    {
        await _rig.Hold();
        var item = await _rig.NewItem();
        using (_rig.Owner())
        {
            _ = await new Aw.Sindri.Features.LinkItem.LinkItemService().Handle(new Aw.Sindri.Features.LinkItem.LinkItemRequest(Aw.Sindri.Core.Ids.ItemRef.To(item), Ph4Rig.Key), Token);
        }

        var suggested = await Service(Pulls(Pull("ENG-12: stop dropping rows")), _rig).Handle(new SuggestLinksRequest(), Token);

        Assert.True(suggested.IsSuccess, suggested.IsError ? suggested.Error.ToString() : null);
        Assert.Empty(suggested.Value.Suggestions);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_title_with_no_issue_key_suggests_nothing()
    {
        var suggested = await Service(Pulls(Pull("Fix the retry budget")), _rig).Handle(new SuggestLinksRequest(), Token);

        Assert.True(suggested.IsSuccess, suggested.IsError ? suggested.Error.ToString() : null);
        Assert.Empty(suggested.Value.Suggestions);
    }
}
