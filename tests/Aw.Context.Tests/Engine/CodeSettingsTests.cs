using System.Globalization;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Engine;

public sealed class CodeSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void Code_section_with_globs_validates()
    {
        var settings = Read("""{"code":{"schemaGlobs":["db/**/*.sql"],"testGlobs":["spec/**"]}}""").Value;

        Assert.Equal(["db/**/*.sql"], settings.SchemaGlobs, StringComparer.Ordinal);
        Assert.Equal(["spec/**"], settings.TestGlobs, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Schema_globs_default_and_test_globs_have_no_default_when_the_section_is_absent()
    {
        var settings = Read("""{"minHits":1}""").Value;

        Assert.Equal(ContextSettings.DefaultSchemaGlobs, settings.SchemaGlobs);
        Assert.Empty(settings.TestGlobs);
        Assert.Contains("**/*.sql", settings.SchemaGlobs, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Empty_schemaGlobs_is_allowed_and_means_off()
    {
        var settings = Read("""{"code":{"schemaGlobs":[]}}""").Value;

        Assert.Empty(settings.SchemaGlobs);
        Assert.Empty(settings.TestGlobs);   // why: no built-in list, so an empty profile list means no file is a test
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("../x/**")]
    [InlineData("a/../b")]
    [InlineData("/etc/**")]
    [InlineData("")]
    [InlineData("a\\u0007b")]
    public void Glob_with_dotdot_absolute_empty_or_control_char_is_150_at_its_index(string glob)
    {
        var settings = Read("{\"code\":{\"schemaGlobs\":[\"ok/**\",\"" + glob + "\"]}}");

        Assert.Equal(ContextErrors.CodeGlobInvalid.Code, settings.Error.Code);
        Assert.StartsWith("context.code.schemaGlobs[1]:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_glob_over_2000_characters_is_150_on_testGlobs()
    {
        var settings = Read("{\"code\":{\"testGlobs\":[\"" + new string('a', 2_001) + "\"]}}");

        Assert.Equal(ContextErrors.CodeGlobInvalid.Code, settings.Error.Code);
        Assert.StartsWith("context.code.testGlobs[0]:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("schemaGlobs", 500, true)]
    [InlineData("schemaGlobs", 501, false)]
    [InlineData("testGlobs", 501, false)]
    public void More_than_500_globs_is_151(string key, int count, bool valid)
    {
        var globs = string.Join(',', Enumerable.Range(0, count).Select(i => string.Create(CultureInfo.InvariantCulture, $"\"g{i}/**\"")));
        var settings = Read("{\"code\":{\"" + key + "\":[" + globs + "]}}");

        Assert.Equal(valid, settings.IsSuccess);
        if (!valid)
        {
            Assert.Equal(ContextErrors.CodeGlobsTooMany.Code, settings.Error.Code);
            Assert.StartsWith("context.code." + key + ":", settings.Error.Message, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Unknown_key_under_code_is_refused_naming_the_key_path()
    {
        var settings = Read("""{"code":{"schemaGlob":["x"]}}""");

        Assert.Equal(ContextErrors.SettingsInvalid.Code, settings.Error.Code);
        Assert.StartsWith("context.code.schemaGlob:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Code_deadline_is_read_from_the_code_section()
        => Assert.Equal(TimeSpan.FromSeconds(90), Read("""{"code":{"deadline":"90s"}}""").Value.CodeDeadline);

    [Fact, Trait("Lane", "unit")]
    public void Code_deadline_defaults_to_180s_and_leaves_the_query_deadline_at_10s()
    {
        var settings = Read("""{"minHits":1}""").Value;

        Assert.Equal((TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(10)), (settings.CodeDeadline, settings.Deadline));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("1s5")]
    [InlineData("50ms")]
    [InlineData("86401s")]
    public void An_invalid_code_deadline_is_refused_naming_the_key_path(string value)
    {
        var settings = Read("{\"code\":{\"deadline\":\"" + value + "\"}}");

        Assert.Equal(ContextErrors.SettingsInvalid.Code, settings.Error.Code);
        Assert.StartsWith("context.code.deadline:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_new_codes_are_150_timeout_152_and_150_151_are_attention()
    {
        Assert.Equal(["SND-CONTEXT-150", "SND-CONTEXT-151", "SND-CONTEXT-152"],
            new[] { ContextErrors.CodeGlobInvalid, ContextErrors.CodeGlobsTooMany, ContextErrors.CodeSearchTimedOut }.Select(e => e.Code), StringComparer.Ordinal);
        Assert.Equal(ErrorKind.Timeout, ContextErrors.CodeSearchTimedOut.Kind);
        Assert.All([ContextErrors.CodeGlobInvalid, ContextErrors.CodeGlobsTooMany], e => Assert.Contains(e.Code, ContextErrors.Attention));
        Assert.DoesNotContain(ContextErrors.CodeSearchTimedOut.Code, ContextErrors.Attention);
    }
}
