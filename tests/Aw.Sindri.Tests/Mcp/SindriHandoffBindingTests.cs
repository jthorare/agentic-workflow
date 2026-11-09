using System.Text.Json;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>The arguments of sindri_handoff bind to a request, or are refused, with no host (#2258 PR 12).</summary>
public sealed class SindriHandoffBindingTests
{
    [Fact, Trait("Lane", "unit")]
    public void Arguments_bind_with_source_defaulting_to_mcp_and_links_as_label_and_url()
    {
        var bound = SindriBindings.Handoff(Args("""{"title":"Fix it","offerKey":"o-1","links":[{"label":"Ticket","url":"https://example.test/t/1"}],"labels":["bug"]}"""));

        Assert.True(bound.IsSuccess, bound.IsError ? bound.Error.ToString() : null);
        Assert.Equal(("Fix it", "mcp", "o-1"), (bound.Value.Title, bound.Value.Source, bound.Value.OfferKey));
        Assert.Equal("Ticket", Assert.Single(bound.Value.Links!).Label);
        Assert.Equal(["bug"], bound.Value.Labels);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"source":"ci-bot"}""")]
    [InlineData("""{"title":"Fix it","links":["https://example.test"]}""")]
    [InlineData("""{"title":"Fix it","links":[{"label":"Ticket","url":"https://example.test","extra":1}]}""")]
    [InlineData("""{"title":"Fix it","labels":[1]}""")]
    [InlineData("""{"title":"Fix it","nope":true}""")]
    [InlineData("""{"title":"Fix it","repo":"Not A Repo!"}""")]
    public void A_bad_argument_set_is_refused(string json) => Assert.True(SindriBindings.Handoff(Args(json)).IsError);

    private static Dictionary<string, JsonElement> Args(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
}
