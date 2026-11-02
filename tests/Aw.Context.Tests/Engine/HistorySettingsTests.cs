using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Engine;

public sealed class HistorySettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void History_deadline_is_none_unless_the_profile_writes_one()
        => Assert.Null(Read("""{"minHits":1}""").Value.HistoryDeadline);

    [Fact, Trait("Lane", "unit")]
    public void History_deadline_is_read_from_the_history_section()
        => Assert.Equal(TimeSpan.FromSeconds(600), Read("""{"history":{"deadline":"600s"}}""").Value.HistoryDeadline);

    [Theory, Trait("Lane", "unit")]
    [InlineData("1s5")]
    [InlineData("50ms")]
    [InlineData("86401s")]
    public void An_invalid_history_deadline_is_refused_naming_the_key_path(string value)
    {
        var settings = Read("{\"history\":{\"deadline\":\"" + value + "\"}}");

        Assert.Equal(ContextErrors.SettingsInvalid.Code, settings.Error.Code);
        Assert.StartsWith("context.history.deadline:", settings.Error.Message, StringComparison.Ordinal);
    }
}
