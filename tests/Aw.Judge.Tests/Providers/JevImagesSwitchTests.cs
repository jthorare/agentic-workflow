using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>
/// Image sending to Jev is on unless config.json's providers.jevImages is false; AW_JUDGE_JEV_IMAGES=0|1 in the environment
/// wins over it (a repo whose screenshots show customer data turns it off in its own shell). Read through the provider catalog, the
/// one place the config and the environment meet.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JevImagesSwitchTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(null, "0", false)]
    [InlineData(true, "0", false)]
    [InlineData(false, "1", true)]
    public async Task Images_go_to_jev_unless_config_or_environment_turns_them_off(bool? configured, string? environment, bool sendsImages)
    {
        var handler = RecordingJevHandler.Choosing("batch");
        using var jev = JevImages.Jev(handler);
        var catalog = new ProviderCatalog(
            name => name == "AW_JUDGE_JEV_IMAGES" ? environment : null,
            new FakeExecutableLocator([AgentCliNames.Binary(AgentCliName.ClaudeCli)]),
            [jev, new ClaudeCliProvider(new FakeProcessRunner())]);
        var lineup = catalog.Lineup(new ProvidersConfig { JevImages = configured }, allowed: null, new ScriptedProvider(ProviderName.Rules, [.. ContentClasses.All]));
        var inLineup = lineup.Providers.Single(p => p.Name == ProviderName.Jev);
        var question = Questions.Text with { Images = [JevImages.Attachment("/ev/home.png", 1512, 982)] };

        var result = await inLineup.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        if (sendsImages)
        {
            _ = Assert.IsType<ProviderDecided>(result);
            Assert.Equal("true", Assert.Single(handler.Requests).ImagesHeader);
        }
        else
        {
            Assert.Equal(new ProviderUnavailable("images-off"), result);
            Assert.Empty(handler.Requests);
        }
    }
}
