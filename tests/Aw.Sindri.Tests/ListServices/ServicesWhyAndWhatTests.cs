using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ListServices;

/// <summary>GET /sindri/services says why a service failed or is paused and what each one does (B-15): optional fields, read as raw JSON.</summary>
public sealed class ServicesWhyAndWhatTests
{
    private static readonly string[] _profiled = ["heartbeat", "stuck", "phantom"];

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_failed_service_carries_the_failure_detail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await JobHost.Start(ct, pump: false);
        await ViewScenarios.Approve(host, ct);
        await Record(host, "heartbeat", SindriErrors.ServiceNotFound.With("the fixture failed on purpose"), ct);
        await ViewScenarios.CatchUp(host, ct);

        var row = Row(await ViewScenarios.Read(host, "/sindri/services"), "heartbeat");

        var detail = row.GetProperty("lastRun").TryGetProperty("detail", out var d) ? d.GetString() : null;
        Assert.Contains("failed on purpose", detail, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_paused_service_carries_who_and_when()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await JobHost.Start(ct, pump: false);
        await ViewScenarios.Approve(host, ct);
        _ = await host.Http.Ask(Post("/sindri/services/heartbeat/pause", json: null, JobHost.Human(host)));
        await ViewScenarios.CatchUp(host, ct);

        var row = Row(await ViewScenarios.Read(host, "/sindri/services"), "heartbeat");

        Assert.True(row.GetProperty("paused").GetBoolean());
        Assert.True(row.TryGetProperty("pausedBy", out var by) && !string.IsNullOrEmpty(by.GetString()), "no pausedBy");
        Assert.True(row.TryGetProperty("pausedAt", out var at) && at.ValueKind == JsonValueKind.String, "no pausedAt");
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Every_service_has_a_description()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await JobHost.Start(ct, pump: false);
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);

        using var doc = JsonDocument.Parse(await ViewScenarios.Read(host, "/sindri/services"));
        var missing = doc.RootElement.GetProperty("services").EnumerateArray()
            .Where(r => IsBuiltInOrHeartbeat(r.GetProperty("name").GetString()) && (!r.TryGetProperty("description", out var d) || string.IsNullOrWhiteSpace(d.GetString())))
            .Select(r => r.GetProperty("name").GetString())
            .ToList();

        Assert.Empty(missing);
    }

    private static async Task Record(GoldenHost host, string service, Error error, CancellationToken ct)
    {
        await using var scope = ViewScenarios.Service(host, out var grant);
        var recorder = scope.ServiceProvider.GetRequiredService<IServiceRunRecorder>();
        _ = (await recorder.Record(ServiceName.From(service), new ScheduledRun(grant, host.Now()), error, job: null, ct)).Value;
    }

    private static JsonElement Row(string body, string name)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("services").EnumerateArray().Single(r => r.GetProperty("name").GetString() == name).Clone();
    }

    private static bool IsBuiltInOrHeartbeat(string? name) =>
        string.Equals(name, "heartbeat", StringComparison.Ordinal) || !_profiled.Contains(name, StringComparer.Ordinal);
}
