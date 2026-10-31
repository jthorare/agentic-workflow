using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>What a pin-suggest request may ask (concept pins spec §3.6): exactly one mode, and the limits of the list mode. A refusal is SND-CONTEXT-179 and says which.</summary>
[Trait("Lane", "unit")]
public sealed class PinSuggestQueriesTests
{
    private static string Refused(GetContextPinSuggestRequest request)
    {
        var query = PinSuggestQueries.Of(request);
        Assert.True(query.IsError);
        Assert.Equal(ContextErrors.PinSuggestInvalid.Code, query.Error.Code);
        return query.Error.Message;
    }

    [Fact]
    public void No_mode_is_refused_and_says_so() =>
        Assert.Contains("ask for one of", Refused(new GetContextPinSuggestRequest()), StringComparison.Ordinal);

    [Fact]
    public void Two_modes_are_refused_and_say_so() =>
        Assert.Contains("three different questions", Refused(new GetContextPinSuggestRequest(Concept: "concept:billing", Search: "pay")), StringComparison.Ordinal);

    [Fact]
    public void An_empty_search_is_refused() =>
        Assert.Contains("search= is empty", Refused(new GetContextPinSuggestRequest(Search: "  ")), StringComparison.Ordinal);

    [Fact]
    public void A_concept_that_is_not_a_concept_key_is_refused() =>
        Assert.Contains("not a concept key", Refused(new GetContextPinSuggestRequest(Concept: "feature:billing")), StringComparison.Ordinal);

    [Fact]
    public void More_than_a_hundred_lines_are_refused() =>
        Assert.Contains("101 lines", Refused(new GetContextPinSuggestRequest(Line: [.. Enumerable.Range(0, 101).Select(i => "Name " + i.ToString(CultureInfo.InvariantCulture))])), StringComparison.Ordinal);

    [Fact]
    public void A_definition_count_that_is_not_the_line_count_is_refused() =>
        Assert.Contains("def= has 1 values for 2 lines", Refused(new GetContextPinSuggestRequest(Line: ["A", "B"], Def: ["only one"])), StringComparison.Ordinal);

    [Fact]
    public void A_line_over_eighty_characters_with_its_definition_is_refused_and_one_at_eighty_is_not()
    {
        var name = new string('n', 30);

        Assert.Contains("line 2 is over 80", Refused(new GetContextPinSuggestRequest(Line: ["ok", name], Def: ["fine", new string('d', 51)])), StringComparison.Ordinal);   // 30 + 51 = 81
        Assert.True(PinSuggestQueries.Of(new GetContextPinSuggestRequest(Line: [name], Def: [new string('d', 50)])).IsSuccess);   // 30 + 50 = 80
        Assert.Contains("line 1 is over 80", Refused(new GetContextPinSuggestRequest(Line: [new string('n', 81)])), StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_without_a_name_is_refused() =>
        Assert.Contains("line 2 has no name", Refused(new GetContextPinSuggestRequest(Line: ["A", " "])), StringComparison.Ordinal);

    [Fact]
    public void A_search_limit_defaults_to_fifty_and_stops_at_two_hundred()
    {
        Assert.Equal(50, PinSuggestQueries.Of(new GetContextPinSuggestRequest(Search: "pay")).Value.Limit);
        Assert.Equal(200, PinSuggestQueries.Of(new GetContextPinSuggestRequest(Search: "pay", Limit: 5000)).Value.Limit);
        Assert.Equal(7, PinSuggestQueries.Of(new GetContextPinSuggestRequest(Search: "pay", Limit: 7)).Value.Limit);
    }

    [Fact]
    public void Lines_keep_their_order_and_pair_each_definition_by_index()
    {
        var query = PinSuggestQueries.Of(new GetContextPinSuggestRequest(Line: ["Payments", "Auth"], Def: ["take money", ""])).Value;

        Assert.Equal([new PinSuggestLine("Payments", "take money"), new PinSuggestLine("Auth", Definition: null)], query.Lines);
    }
}
