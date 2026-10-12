using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vogen;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// The API's own failures, through the pipeline Program runs and the CLI's client: each carries a catalogue code, so the
/// CLI never reads a broken answer as 'not running'.
/// </summary>
public sealed class ApiProblemTests : IDisposable
{
    private const int Deadline = 30_000;
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task A_handler_that_throws_is_SND_API_005_exit_2_never_not_running()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Running(static services => services.AddScoped<IService<GetStatusRequest, GetStatusResponse>, ThrowingStatus>(), ct);
        var (verbs, stderr) = Verbs(app);

        var code = await verbs.Status(json: false, ct);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-API-005 ", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", stderr.ToString(), StringComparison.Ordinal);
        await app.StopAsync(ct);
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task An_answer_without_an_error_code_is_SND_API_004_never_not_running()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Running(configure: null, ct);
        using var client = new ControlPlaneClient(app.Services.GetRequiredService<IControlPlaneProbe>(), SliceHarness.Json);

        var answer = await client.Get<GetStatusResponse>("/sindri/no-such-route", ct);   // an unknown route's 404 has no body

        Assert.Equal(("SND-API-004", ErrorKind.Unavailable), (answer.Error.Code, answer.Error.Kind));
        Assert.Contains("HTTP 404", answer.Error.Message, StringComparison.Ordinal);
        await app.StopAsync(ct);
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task A_value_object_refusal_is_a_400_with_fixed_text_and_the_framework_message_only_in_the_log()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new ListLoggerFactory();
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System,
            services => services.AddSingleton<ILoggerFactory>(logs));
        _ = Pipeline.Configure(app);
        _ = app.MapGet("/probe-id", static string () => throw new ValueObjectValidationException("framework detail: Use of uninitialized Value Object"));
        await app.StartAsync(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        var answer = await http.GetAsync(new Uri("/probe-id", UriKind.Relative), ct);
        var problem = System.Text.Json.JsonDocument.Parse(await answer.Content.ReadAsStringAsync(ct)).RootElement;

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.Equal(("SND-CLI-003", "GET /probe-id: a value in the request isn't valid for this route"),
            (problem.GetProperty("title").GetString(), problem.GetProperty("detail").GetString()));
        Assert.Contains(logs.Logger.Lines, l => l.Contains("framework detail: Use of uninitialized Value Object", StringComparison.Ordinal));
        await app.StopAsync(ct);
    }

    /// <summary>A started control plane on a free loopback port, holding the lease, with its endpoint recorded.</summary>
    private async Task<WebApplication> Running(Action<IServiceCollection>? configure, CancellationToken ct)
    {
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, configure);
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
            await app.StartAsync(ct);
            _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        }

        return app;
    }

    private static (Verbs Verbs, StringWriter Stderr) Verbs(WebApplication app)
    {
        var probe = app.Services.GetRequiredService<IControlPlaneProbe>();
        var client = new ControlPlaneClient(probe, SliceHarness.Json);
        var stderr = new StringWriter();
        return (new Verbs(client, probe, new Output(new StringWriter(), stderr, SliceHarness.Json), app.Services), stderr);
    }

    /// <summary>A status handler with a bug: it throws.</summary>
    private sealed class ThrowingStatus : IService<GetStatusRequest, GetStatusResponse>
    {
        public ValueTask<Result<GetStatusResponse>> Handle(GetStatusRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("status is broken");
    }
}
