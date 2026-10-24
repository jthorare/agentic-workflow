using System.Net;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Infrastructure.Desktop;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class LoopbackStaticSiteTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly HttpClient _http = new();

    public void Dispose()
    {
        _http.Dispose();
        _root.Dispose();
    }

    private async Task<IServedSite> ServeAsync()
    {
        _ = Directory.CreateDirectory(_root.Combine("mockups/sub"));
        await File.WriteAllTextAsync(_root.Combine("mockups/home.html"), "<p>home</p>", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(_root.Combine("mockups/sub/a.css"), "p{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(_root.Combine("secret.txt"), "outside", TestContext.Current.CancellationToken);
        var served = await new LoopbackStaticSite().ServeAsync(_root.Combine("mockups"), TestContext.Current.CancellationToken);
        return served.Value;
    }

    [Fact]
    public async Task A_file_below_the_folder_is_served_with_its_media_type()
    {
        await using var site = await ServeAsync();

        using var html = await _http.GetAsync(new Uri(site.Origin + "/home.html"), TestContext.Current.CancellationToken);
        using var css = await _http.GetAsync(new Uri(site.Origin + "/sub/a.css"), TestContext.Current.CancellationToken);

        Assert.Equal(("<p>home</p>", "text/html", "text/css"), (await html.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), html.Content.Headers.ContentType?.MediaType, css.Content.Headers.ContentType?.MediaType));
    }

    [Fact]
    public async Task A_missing_file_a_path_out_of_the_folder_and_a_write_are_refused()
    {
        await using var site = await ServeAsync();

        using var missing = await _http.GetAsync(new Uri(site.Origin + "/nope.html"), TestContext.Current.CancellationToken);
        using var outside = await _http.GetAsync(new Uri(site.Origin + "/%2e%2e/secret.txt"), TestContext.Current.CancellationToken);
        using var body = new StringContent("x");
        using var post = await _http.PostAsync(new Uri(site.Origin + "/home.html"), body, TestContext.Current.CancellationToken);

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed], [missing.StatusCode, outside.StatusCode, post.StatusCode]);
    }
}
