using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>GET /sindri/skills as the app reads it (skill flows spec §8.1): the path, and the server's golden read as the app's records.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillsApiTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Skills_client_reads_the_blocks()
    {
        var canned = new Canned(GoldenFiles.Api("skills", "fixtures-loaded"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var read = await client.Skills(_api, Token);

        var first = read.Value.Blocks[0];
        Assert.Equal("/sindri/skills", canned.Path);
        Assert.Equal(("office-hours", "step.skill.office-hours", "Shapes an idea into a plan folder with requirements and tasks.", ["fact:officeHoursFeatureDir"], false),
            (first.Id, first.Descriptor, first.Sentence, first.Outputs, first.Pushes));
        Assert.Equal(("idea", "text", true), (first.Inputs[0].Key, first.Inputs[0].Type, first.Inputs[0].Required));
    }

    [Fact]
    public async Task The_fake_answers_the_same_golden()
    {
        var read = await new FakeSindriApi().Skills(_api, Token);

        Assert.Contains(read.Value.Blocks, b => b.Descriptor == "step.skill.review");
    }

    private sealed class Canned(string body) : HttpMessageHandler
    {
        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath; // why: HttpClient always sets the URI
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
