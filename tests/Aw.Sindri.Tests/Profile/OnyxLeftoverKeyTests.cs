using System.Text.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>A profile that still names Onyx is refused for one release with the fix (owned-index spec §8.1, §12.3, Q15), never silently ignored.</summary>
public sealed class OnyxLeftoverKeyTests
{
    private static SindriProfile With(string context) => new SindriProfile(1, "thor") with { Context = JsonDocument.Parse(context).RootElement.Clone() };

    [Fact, Trait("Lane", "unit")]
    public void A_leftover_onyx_source_is_refused_naming_the_key_and_the_fix()
    {
        var result = ProfileRules.Check(With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","managed":true},"linear":{"token":"env:LINEAR"}}}"""));

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.IndexOnyxInProfile.Code, result.Error.Code);
        Assert.Equal("SND-INDEX-007", result.Error.Code);
        Assert.Contains("context.sources.onyx", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri profile approve", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"sources":{"linear":{"token":"env:LINEAR"}}}""")]
    [InlineData("""{"sources":{}}""")]
    [InlineData("""{}""")]
    public void A_context_section_without_onyx_passes(string context) =>
        Assert.False(ProfileRules.Check(With(context)).IsError);
}
