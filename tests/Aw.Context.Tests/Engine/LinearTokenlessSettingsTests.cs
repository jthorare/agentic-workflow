using System.Text.Json;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Engine;

// #2508: a profile that names Linear's url and teams but no token pointer is read with the Connections page's Linear connection, so it passes settings.
public sealed class LinearTokenlessSettingsTests
{
    private static Kernel.Result<ContextSettings> Read(string linear) =>
        SettingsReader.Read(JsonDocument.Parse("{\"sources\":{\"linear\":" + linear + "}}").RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void A_url_and_teams_with_no_token_read_ok_with_no_token_pointer()
    {
        var settings = Read("""{"url":"http://127.0.0.1:4010/graphql","teams":["ENG"]}""");

        Assert.True(settings.IsSuccess);
        Assert.Null(settings.Value.Linear!.Token);
        Assert.Equal(["ENG"], settings.Value.LinearTeams);
    }

    [Fact, Trait("Lane", "unit")]
    public void Teams_alone_give_no_native_linear_settings()
        => Assert.Null(Read("""{"teams":["ENG"]}""").Value.Linear);

    [Fact, Trait("Lane", "unit")]
    public void A_write_token_with_no_token_and_no_url_is_still_refused()
        => Assert.Equal("SND-CONTEXT-002", Read("""{"teams":["ENG"],"writeToken":"keychain:linear/w"}""").Error.Code);
}
