using System.Net;
using System.Text;
using Aw.UiDriver.Cli;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>The server and the command line against a real window: the secret, the routes, the files, and a verb end to end.</summary>
public sealed class DriverServerTests : IAsyncLifetime
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-uidriver-");
    private DriverServer? _server;
    private DriverFiles? _files;
    private HttpClient? _http;

    public async ValueTask InitializeAsync()
    {
        var screen = await TestScreen.OpenAsync();
        _files = DriverFiles.Create(_directory.FullName);
        _server = DriverServer.Start(new DriverHost(() => screen.Window, TimeProvider.System), _files.Secret);
        Assert.NotNull(_server);
        _files.PublishEndpoint(_server.Origin);
        _http = new HttpClient { BaseAddress = new Uri(_server.Origin) };
    }

    public async ValueTask DisposeAsync()
    {
        _http?.Dispose();
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        _files?.Remove();
        _directory.Delete(recursive: true);
    }

    private async Task<HttpResponseMessage> Post(string path, string body, string? secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (secret is not null)
        {
            request.Headers.Add(DriverEndpoint.TokenHeader, secret);
        }

        return await _http!.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_call_without_the_secret_or_with_a_wrong_one_is_refused()
    {
        using var none = await Post("/v1/tree", "{}", secret: null);
        using var wrong = await Post("/v1/tree", "{}", "not-the-secret");
        using var right = await Post("/v1/tree", "{}", _files!.Secret);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.OK], [none.StatusCode, wrong.StatusCode, right.StatusCode]);
        Assert.Contains("AW-UIDRIVER-005", await none.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task The_routes_answer_with_the_status_of_their_error()
    {
        using var unknown = await Post("/v1/dance", "{}", _files!.Secret);
        using var badBody = await Post("/v1/click", "{}", _files.Secret);
        using var noMatch = await Post("/v1/click", """{"selector":{"automationId":"nope"}}""", _files.Secret);
        using var notAPost = await _http!.GetAsync(new Uri("/v1/tree", UriKind.Relative), TestContext.Current.CancellationToken);
        using var ping = await PingAsync();

        Assert.Equal(
            [HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.OK],
            [unknown.StatusCode, badBody.StatusCode, noMatch.StatusCode, notAPost.StatusCode, ping.StatusCode]);
    }

    private async Task<HttpResponseMessage> PingAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ping");
        request.Headers.Add(DriverEndpoint.TokenHeader, _files!.Secret);
        return await _http!.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_screenshot_answers_a_png()
    {
        using var response = await Post("/v1/screenshot", "{}", _files!.Secret);

        Assert.Equal(("image/png", (byte)0x89), (response.Content.Headers.ContentType?.MediaType, (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))[0]));
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task The_command_line_drives_the_window_through_the_files_the_driver_wrote()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var run = new CliRun(output, error, name => name == "AW_UI_DRIVER_DIR" ? _directory.FullName : null);

        var ready = await run.RunAsync(["ready", "--wait-ms", "2000"], TestContext.Current.CancellationToken);
        var search = await run.RunAsync(["search", "--automation-id", "save"], TestContext.Current.CancellationToken);
        var shot = Path.Combine(_directory.FullName, "window.png");
        var picture = await run.RunAsync(["screenshot", "--out", shot], TestContext.Current.CancellationToken);

        Assert.Equal((0, 0, 0, ""), (ready, search, picture, error.ToString()));
        Assert.Contains("\"automationId\":\"save\"", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(0x89, (await File.ReadAllBytesAsync(shot, TestContext.Current.CancellationToken))[0]);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_secret_and_the_driver_directory_are_for_the_owner_alone_and_the_files_go_when_removed()
    {
        var token = Path.Combine(_directory.FullName, DriverEndpoint.TokenFile);
        var endpoint = Path.Combine(_directory.FullName, DriverEndpoint.EndpointFile);

        Assert.True(File.Exists(token) && File.Exists(endpoint));
        Assert.Equal(64, File.ReadAllText(token).Length);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(token));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(endpoint));
        }

        _files!.Remove();

        Assert.False(File.Exists(token) || File.Exists(endpoint));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_verbs_are_the_ones_the_command_line_sends() =>
        Assert.Equal(["tree", "search", "props", "click", "type", "key", "scroll", "wait-for", "screenshot", "window", "action"], Verbs.Names);

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_verb_that_throws_answers_500_and_the_next_call_is_still_served()
    {
        var screen = await TestScreen.OpenAsync();
        var calls = 0;
        var files = DriverFiles.Create(Directory.CreateTempSubdirectory("aw-uidriver-throw-").FullName);
        var server = DriverServer.Start(
            new DriverHost(() => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("boom") : screen.Window, TimeProvider.System),
            files.Secret);
        Assert.NotNull(server);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(server.Origin) };
            using var first = await PostAs(http, files.Secret);
            using var second = await PostAs(http, files.Secret);

            Assert.Equal([HttpStatusCode.InternalServerError, HttpStatusCode.OK], [first.StatusCode, second.StatusCode]);
            Assert.Contains("AW-UIDRIVER-007", await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        finally
        {
            await server.DisposeAsync();
            files.Remove();
        }
    }

    private static async Task<HttpResponseMessage> PostAs(HttpClient http, string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tree") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Add(DriverEndpoint.TokenHeader, secret);
        return await http.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
