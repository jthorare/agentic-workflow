using System.Net;
using Aw.Kernel;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests;

[Collection(ProcessEnvironment.Name)]
public sealed class LoopbackBindingTests
{
    /// <summary>Spec §4d's override vectors, as "NAME=value" pairs separated by ';' (PREFERHOSTINGURLS needs a URL to prefer).</summary>
    public static TheoryData<string> Vectors =>
    [
        "ASPNETCORE_URLS=http://0.0.0.0:0",
        "DOTNET_URLS=http://0.0.0.0:0",
        "ASPNETCORE_HTTP_PORTS=0",
        "ASPNETCORE_PREFERHOSTINGURLS=true;ASPNETCORE_URLS=http://0.0.0.0:0",
        "Kestrel__Endpoints__Any__Url=http://0.0.0.0:0",
        "ASPNETCORE_Kestrel__Endpoints__Any__Url=http://0.0.0.0:0",
    ];

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(Vectors))]
    public async Task The_only_listener_is_loopback_whatever_the_environment_says(string vector)
    {
        var pairs = vector.Split(';').Select(p => p.Split('=', 2)).ToList();
        var before = pairs.ToDictionary(p => p[0], p => Environment.GetEnvironmentVariable(p[0]), StringComparer.Ordinal);
        pairs.ForEach(p => Environment.SetEnvironmentVariable(p[0], p[1]));
        try
        {
            var addresses = await ListenerAddresses();

            var only = Assert.Single(addresses);
            Assert.StartsWith("http://127.0.0.1:", only, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var (name, value) in before)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--urls", "http://0.0.0.0:0")]
    [InlineData("--Kestrel:Endpoints:Any:Url", "http://0.0.0.0:0")]
    public void Command_line_arguments_are_refused(string option, string value)
    {
        var refused = Assert.Throws<ArgumentException>(
            () => LoopbackBinding.Build([option, value], _ => null, new FakeTimeProvider()));

        Assert.Equal("args", refused.ParamName);
    }

    // WebApplicationBuilder registers its own ILoggerFactory before Compose runs, so the http host's guarantee is the
    // resolved one: Kernel's (registered later), with ASP.NET's providers cleared.
    [Fact, Trait("Lane", "integration")]
    public async Task Kernel_telemetry_is_the_only_logger()
    {
        var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        await using (app)
        {
            var runtime = app.Services.GetRequiredService<TelemetryRuntime>();

            Assert.Same(runtime.LoggerFactory, app.Services.GetRequiredService<ILoggerFactory>());
            Assert.Empty(app.Services.GetServices<ILoggerProvider>());
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_non_loopback_host_header_is_refused_with_400()
    {
        await using var app = await Started();
        using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "example.com";

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_error_result_is_problem_details_with_its_code()
    {
        var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        await using (app)
        {
            _ = Pipeline.Configure(app);
            _ = app.MapGet("/probe-error", static () => ((Result<Unit>)KernelErrors.Invalid.With("probe")).ToHttpResult());
            await app.StartAsync(TestContext.Current.CancellationToken);
            using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };

            using var response = await client.GetAsync(new Uri("/probe-error", UriKind.Relative), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains(KernelErrors.Invalid.Code, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_non_loopback_browser_origin_is_refused_with_403()
    {
        await using var app = await Started();
        using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "https://example.com");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("ASPNETCORE_ENVIRONMENT")]
    [InlineData("DOTNET_ENVIRONMENT")]
    public async Task An_inherited_development_environment_still_answers_a_bad_body_with_400_SND_CLI_003(string variable)
    {
        var before = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "Development");
        try
        {
            using var home = new TempHome();
            var app = LoopbackBinding.Build([], n => n == "AW_SINDRI_HOME" ? home.Path : null, new FakeTimeProvider());
            await using (app)
            {
                _ = Pipeline.Configure(app);
                await app.StartAsync(TestContext.Current.CancellationToken);
                using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };
                using var body = new StringContent("""{"hash":""", System.Text.Encoding.UTF8, "application/json");

                using var response = await client.PostAsync(new Uri("/sindri/profile/approve", UriKind.Relative), body, TestContext.Current.CancellationToken);
                var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal("SND-CLI-003", System.Text.Json.JsonDocument.Parse(text).RootElement.GetProperty("title").GetString());
                Assert.Equal(Environments.Production, app.Environment.EnvironmentName);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, before);
        }
    }

    private static async Task<WebApplication> Started()
    {
        var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        _ = Pipeline.Configure(app);   // the pipeline Program.cs runs
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static string Address(WebApplication app) => Addresses(app).Single();

    private static IReadOnlyList<string> Addresses(WebApplication app)
        => [.. app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? []];

    private static async Task<IReadOnlyList<string>> ListenerAddresses()
    {
        await using var app = await Started();
        return Addresses(app);
    }
}
