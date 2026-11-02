using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>GET /sindri/launch/picks as the app reads it (model-pick spec §6.1): the path with its filters, and the fake's answer read as the app's records.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchPicksApiTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    [Theory]
    [InlineData(null, null, "/sindri/launch/picks")]
    [InlineData("ENG-142", null, "/sindri/launch/picks?item=ENG-142")]
    [InlineData(null, "T-1/step/1", "/sindri/launch/picks?instance=T-1%2Fstep%2F1")]
    [InlineData("ENG-142", "T-1/step/1", "/sindri/launch/picks?item=ENG-142&instance=T-1%2Fstep%2F1")]
    public void The_path_carries_only_the_filters_that_are_set_escaped(string? item, string? instance, string path) =>
        Assert.Equal(path, SindriApiClient.LaunchPicksRoute(item, instance));

    [Fact]
    public async Task The_fake_answers_its_golden_and_records_what_it_was_asked()
    {
        var fake = new FakeSindriApi();

        var read = await fake.LaunchPicks(_api, "ENG-142", "T-1/step/1", TestContext.Current.CancellationToken);

        Assert.Empty(read.Value.Picks);
        Assert.Equal(("ENG-142", "T-1/step/1"), fake.LastPicksRead);
    }
}
