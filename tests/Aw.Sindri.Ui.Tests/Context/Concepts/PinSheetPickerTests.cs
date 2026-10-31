using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using static Aw.Sindri.Ui.Tests.Context.Concepts.PinSheetFixtures;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The Pin sheet's feature picker (concept pins spec §3.6): its states, and a search that waits for the typing to rest.</summary>
public sealed class PinSheetPickerTests
{
    private static readonly PinSheetOpen _new = new("api", Card: null, Pin: null, Pins: [], FileHash: FileHash);

    [Fact, Trait("Lane", "unit")]
    public async Task New_pin_lists_the_maps_features_to_add()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests(terms: null, [new PinSuggestFeature("feature:billing.refunds", "Refunds")]);
        var sheet = kit.Sheet();

        await sheet.Open(_new, Token);

        Assert.Equal(["feature:billing.refunds"], [.. sheet.FeatureMatches.Select(f => f.Key)], StringComparer.Ordinal);
        Assert.Equal(string.Empty, sheet.FeaturesNote);
        sheet.AddFeature(sheet.FeatureMatches[0]);
        Assert.Equal([("feature:billing.refunds", true)], sheet.Features.Select(f => (f.Key, f.Ticked)));
        Assert.Empty(sheet.FeatureMatches);
        Assert.True(sheet.IsDirty);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_map_says_to_build_it_first()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests(terms: null, []);
        var sheet = kit.Sheet();

        await sheet.Open(_new, Token);

        Assert.Equal("The map has no features yet. Build it first.", sheet.FeaturesNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_read_says_so_and_try_again_reads_again()
    {
        var kit = new PinSheetFixtures();   // not served
        var sheet = kit.Sheet();
        await sheet.Open(_new, Token);
        Assert.Equal(("Couldn't read the map's features just now. Try again.", true), (sheet.FeaturesNote, sheet.CanTryAgain));

        kit.Suggests(terms: null, [new PinSuggestFeature("feature:a", "A")]);
        await sheet.TryAgain(Token);

        Assert.Equal((string.Empty, false), (sheet.FeaturesNote, sheet.CanTryAgain));
        _ = Assert.Single(sheet.FeatureMatches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_search_reads_once_the_typing_rests_and_says_when_nothing_matches()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests(terms: null, []);
        var sheet = kit.Sheet();
        await sheet.Open(_new, Token);
        var before = kit.Api.ContextPinSuggestAsked.Count;

        sheet.FeatureSearch = "re";
        sheet.FeatureSearch = "refnd";
        kit.Clock.Advance(PinSheetViewModel.SearchDebounce);
        await sheet.Searching.WaitAsync(Token);

        Assert.Equal(before + 1, kit.Api.ContextPinSuggestAsked.Count);
        Assert.Equal("refnd", kit.Api.ContextPinSuggestAsked[^1].Search);
        Assert.Equal("No feature matches \"refnd\".", sheet.FeaturesNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pin_this_has_no_picker()
    {
        var kit = new PinSheetFixtures();
        var sheet = kit.Sheet();

        await sheet.Open(new PinSheetOpen("api", Card("billing"), Pin: null, [], FileHash: FileHash), Token);

        Assert.False(sheet.HasPicker);
    }
}
