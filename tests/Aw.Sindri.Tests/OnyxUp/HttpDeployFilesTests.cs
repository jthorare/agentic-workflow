using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// What only the real HTTP adapter can get wrong: redirects, size, scheme and time.
[Lane(TestLane.Integration)]
public sealed class HttpDeployFilesTests : IDisposable
{
    private static readonly byte[] _good = "services: {}\n"u8.ToArray();
    private static readonly string _path = "/" + new string('a', 40) + "/deployment/docker_compose/docker-compose.yml";

    private readonly LoopbackFileServer _server = new(new Dictionary<string, byte[]>(StringComparer.Ordinal) { [_path] = _good });
    private readonly HttpClient _client = new(new SocketsHttpHandler { AllowAutoRedirect = false });
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-deploy-" + Guid.NewGuid().ToString("N"));

    private static OnyxPins Pins(byte[] content) => new(
        "v4.9.0", new string('a', 40), [new OnyxFilePin("docker_compose/docker-compose.yml", OnyxFilePin.Sha256Of(content))], [], DiskBytes: 1, DockerMemoryBytes: 1);

    private async Task<Kernel.Result<DeployFetch>> Fetch(HttpDeployFiles files, byte[]? pinned = null)
        => await files.Fetch(Pins(pinned ?? _good), _dir, static _ => { }, TestContext.Current.CancellationToken);

    private bool Written => File.Exists(Path.Combine(_dir, "docker_compose/docker-compose.yml"));

    public void Dispose()
    {
        _server.Dispose();
        _client.Dispose();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("same")]
    [InlineData("http://example.invalid/x")]
    public async Task A_redirect_is_refused(string target)
    {
        _server.Redirects[_path] = target == "same" ? _server.BaseUri + _path.TrimStart('/') + "?again" : target;

        var error = (await Fetch(new HttpDeployFiles(_client, _server.BaseUri))).Error;

        Assert.Equal("SND-ONYX-004", error.Code);
        Assert.Contains("redirect", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Written);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_body_over_1_MiB_is_refused()
    {
        var big = new byte[(1 << 20) + 1];
        _server.Files[_path] = big;

        var error = (await Fetch(new HttpDeployFiles(_client, _server.BaseUri), pinned: big)).Error;

        Assert.Equal("SND-ONYX-004", error.Code);
        Assert.Contains("1 MiB", error.Message, StringComparison.Ordinal);
        Assert.False(Written);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_non_https_non_loopback_base_is_refused_at_construction()
    {
        _ = Assert.Throws<ArgumentException>(() => new HttpDeployFiles(_client, new Uri("http://example.invalid/")));
        _ = Assert.Throws<ArgumentException>(() => new HttpDeployFiles(_client, new Uri("ftp://example.invalid/")));
        _ = new HttpDeployFiles(_client, new Uri("https://raw.githubusercontent.com/onyx-dot-app/onyx/"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_slow_server_times_out_per_file()
    {
        _server.Delay = TimeSpan.FromSeconds(3);

        var error = (await Fetch(new HttpDeployFiles(_client, _server.BaseUri, TimeSpan.FromMilliseconds(200)))).Error;

        Assert.Equal("SND-ONYX-004", error.Code);
        Assert.Contains("timed out", error.Message, StringComparison.Ordinal);
        Assert.False(Written);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_cancelled_fetch_throws_and_writes_nothing()
    {
        using var source = new CancellationTokenSource();
        _server.Delay = TimeSpan.FromSeconds(3);
        source.CancelAfter(TimeSpan.FromMilliseconds(200));
        var files = new HttpDeployFiles(_client, _server.BaseUri);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await files.Fetch(Pins(_good), _dir, static _ => { }, source.Token));

        Assert.False(Written);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_temporary_file_is_left_behind_after_a_mismatch()
    {
        _ = await Fetch(new HttpDeployFiles(_client, _server.BaseUri), pinned: "other\n"u8.ToArray());

        Assert.False(Directory.Exists(_dir) && Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories).Any());
    }
}
