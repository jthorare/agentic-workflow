using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>A profile that still names Onyx is refused for one release with the fix (owned-index spec §8.1, §12.3, Q15), never silently ignored.</summary>
public sealed class OnyxLeftoverKeyTests : IDisposable
{
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

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

    [Fact, Trait("Lane", "unit")]
    public void The_retired_onyx_codes_are_never_reused()
    {
        string[] retired = [.. Enumerable.Range(1, 12).Select(static n => "SND-ONYX-" + n.ToString("000", CultureInfo.InvariantCulture)), "SND-SETUP-005", "SND-SETUP-006"];

        Assert.DoesNotContain(SindriErrors.All, e => retired.Contains(e.Code, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Loading_a_profile_yaml_that_still_names_onyx_fails_with_the_fix()
    {
        _files.Write("schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n      url: http://127.0.0.1:3189\n      managed: true\n");

        var loaded = await _files.Source.Load(TestContext.Current.CancellationToken);

        Assert.Equal("SND-INDEX-007", loaded.Error.Code);
        Assert.Contains("sindri profile approve", loaded.Error.Message, StringComparison.Ordinal);
    }
}
