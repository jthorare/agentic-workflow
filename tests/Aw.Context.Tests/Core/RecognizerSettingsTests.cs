using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class RecognizerSettingsTests
{
    private const string Good = """{"kind":"flag","glob":"config/flags.yaml","pattern":"^(?<name>[a-z-]+):","keyFrom":"name"}""";

    private static Result<ContextSettings> Read(string recognizers) =>
        SettingsReader.Read(JsonDocument.Parse("{\"extract\":{\"recognizers\":" + recognizers + "}}").RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void No_recognizers_is_the_default()
    {
        Assert.Empty(SettingsReader.Read(section: null).Value.Recognizers);
        Assert.Empty(Read("[]").Value.Recognizers);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recognizer_is_read_as_written()
    {
        var recognizer = Assert.Single(Read("[" + Good + "]").Value.Recognizers);

        Assert.Equal(("flag", "config/flags.yaml", "^(?<name>[a-z-]+):", "name"), (recognizer.Kind, recognizer.Glob, recognizer.Pattern, recognizer.KeyFrom));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""[{"kind":"widget","glob":"a/*.yaml","pattern":"(?<n>x)","keyFrom":"n"}]""", "context.extract.recognizers[0].kind:")]
    [InlineData("""[{"kind":"flag","glob":"../a","pattern":"(?<n>x)","keyFrom":"n"}]""", "context.extract.recognizers[0].glob:")]
    [InlineData("""[{"kind":"flag","glob":"a/*.yaml","pattern":"(unclosed","keyFrom":"n"}]""", "context.extract.recognizers[0].pattern:")]
    [InlineData("""[{"kind":"flag","glob":"a/*.yaml","pattern":"(?<n>x)","keyFrom":"other"}]""", "context.extract.recognizers[0].keyFrom:")]
    [InlineData("""[{"kind":"flag","glob":"a/*.yaml","pattern":"(?<n>x)"}]""", "context.extract.recognizers[0].keyFrom:")]
    [InlineData("""[null]""", "context.extract.recognizers[0]:")]
    public void A_bad_recognizer_is_SND_CONTEXT_130_at_its_key_path(string recognizers, string prefix)
    {
        var settings = Read(recognizers);

        Assert.True(settings.IsError);
        Assert.Equal(ContextErrors.RecognizerInvalid.Code, settings.Error.Code);
        Assert.StartsWith(prefix, settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void More_than_500_recognizers_is_SND_CONTEXT_131()
    {
        var settings = Read("[" + string.Join(',', Enumerable.Repeat(Good, 501)) + "]");

        Assert.Equal(ContextErrors.RecognizerOverCap.Code, settings.Error.Code);
        Assert.StartsWith("context.extract.recognizers:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pattern_over_2000_characters_is_SND_CONTEXT_131()
    {
        var settings = Read("""[{"kind":"flag","glob":"a/*.yaml","pattern":"(?<n>""" + new string('x', 2_010) + """)","keyFrom":"n"}]""");

        Assert.Equal(ContextErrors.RecognizerOverCap.Code, settings.Error.Code);
        Assert.StartsWith("context.extract.recognizers[0].pattern:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"extract":{"rules":[]}}""")]
    [InlineData("""{"extract":{"recognizers":[{"kind":"flag","glob":"a","pattern":"(?<n>x)","keyFrom":"n","flags":"i"}]}}""")]
    public void Unknown_keys_under_extract_stay_SND_CONTEXT_001(string section)
    {
        var settings = SettingsReader.Read(JsonDocument.Parse(section).RootElement.Clone());

        Assert.Equal(ContextErrors.SettingsInvalid.Code, settings.Error.Code);
    }
}
