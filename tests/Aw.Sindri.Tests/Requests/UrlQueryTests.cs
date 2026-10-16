using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Requests;

/// <summary>The query string the item, request, decision and context verbs send.</summary>
[Lane(TestLane.Unit)]
public sealed class UrlQueryTests
{
    [Fact]
    public void No_value_is_no_query() => Expect.Equal(string.Empty, UrlQuery.Of([("state", null), ("item", null)]));

    [Fact]
    public void The_first_pair_starts_with_a_question_mark_and_the_rest_with_an_ampersand() =>
        Expect.Equal("?state=open&item=ITM-1", UrlQuery.Of([("state", "open"), ("repo", null), ("item", "ITM-1")]));

    [Fact]
    public void Values_are_percent_encoded() =>
        Expect.Equal("?q=a%20b%26c%3D%C3%A5", UrlQuery.Of([("q", "a b&c=\u00e5")]));
}
