using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Engine;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.GetEngine;

/// <summary>'GET /engine', '/engine/instances' and '/engine/instances/{id}/history' on the real host over a fake engine (API spec §3.8, E-30, P13), and GET /status's health word.</summary>
[Lane(TestLane.Integration)]
public sealed class EngineRoutesTests : IAsyncLifetime
{
    private const string Address = "ITM-01JE9/item/1/step.4";
    private readonly FakeEngineReader _engine = new();
    private readonly FakeMirroredSequences _mirrored = new();
    private GoldenHost _host = null!;
    private (string Name, string Value)[] _human = [];

    public async ValueTask InitializeAsync()
    {
        _engine.Running.Add(EngineFacts.Instance(Address, EngineFacts.Wait(EngineWaitKinds.Signal, KnownInstants.Epoch.AddMinutes(-6)), state: "checks"));
        _engine.Record(AddressOf(), "run-1", EngineFacts.Event(412, KnownInstants.Epoch, activityId: "7"), EngineFacts.Event(411, KnownInstants.Epoch.AddSeconds(-1)));
        _mirrored.Rows["7"] = 4231;
        _host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IEngineReader>(_engine));
            _ = services.Replace(ServiceDescriptor.Singleton<IMirroredSequences>(_mirrored));
        });
        _human = Human(_host.Token.Value);
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private static InstanceAddress AddressOf() => InstanceAddress.From(Address);

    private async Task<JsonElement> Read(string path)
    {
        var served = await _host.Http.Ask(Get(path, _human));
        Assert.True(served.Status == HttpStatusCode.OK, served.Body);
        return JsonDocument.Parse(served.Body).RootElement;
    }

    [Fact]
    public async Task Engine_serves_the_view_shape_of_the_spec()
    {
        var view = await Read("/sindri/engine");

        Assert.Equal(("temporal-dev", "sindri", "http://127.0.0.1:8233"), (view.GetProperty("server").GetProperty("kind").GetString(), view.GetProperty("server").GetProperty("namespace").GetString(), view.GetProperty("server").GetProperty("uiUrl").GetString()));
        Assert.Equal(("sindri", true), (view.GetProperty("worker").GetProperty("taskQueue").GetString(), view.GetProperty("worker").GetProperty("polling").GetBoolean()));
        Assert.Equal(0, view.GetProperty("mirror").GetProperty("lagEvents").GetInt32());
        Assert.Equal((0, 1, 1, 0), (view.GetProperty("pending").GetProperty("updates").GetInt32(), view.GetProperty("pending").GetProperty("signals").GetInt32(), view.GetProperty("pending").GetProperty("stuck").GetInt32(), view.GetProperty("pending").GetProperty("continueAsNew").GetInt32()));
        Assert.True(view.TryGetProperty("asOf", out _));
    }

    [Fact]
    public async Task Instances_list_the_waiting_instance_with_its_wait_and_stuck_flag()
    {
        var list = await Read("/sindri/engine/instances");

        var instance = Assert.Single(list.GetProperty("instances").EnumerateArray().ToList());
        Assert.Equal((Address, "checks", "signal", true), (instance.GetProperty("address").GetString(), instance.GetProperty("state").GetString(), instance.GetProperty("waitingOn").GetProperty("kind").GetString(), instance.GetProperty("stuck").GetBoolean()));
        Assert.Equal(360, instance.GetProperty("waitingOn").GetProperty("ageSeconds").GetInt32());
        Assert.StartsWith("WFI-", instance.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal(("workflow.step", 88), (instance.GetProperty("recipe").GetProperty("id").GetString(), instance.GetProperty("historyLength").GetInt32()));
        Assert.Equal(1, list.GetProperty("pending").GetProperty("stuck").GetInt32());
    }

    [Fact]
    public async Task The_stuck_filter_narrows_the_list()
    {
        _ = Assert.Single((await Read("/sindri/engine/instances?stuck=true")).GetProperty("instances").EnumerateArray().ToList());
        Assert.Empty((await Read("/sindri/engine/instances?stuck=false")).GetProperty("instances").EnumerateArray().ToList());
    }

    [Fact]
    public async Task History_by_address_names_the_ledger_sequence_of_a_mirrored_event()
    {
        var history = await Read("/sindri/engine/instances/-/history?address=" + Uri.EscapeDataString(Address));

        var events = history.GetProperty("events").EnumerateArray().ToList();
        Assert.Equal([412, 411], events.Select(static e => e.GetProperty("eventId").GetInt32()));
        Assert.Equal(4231, events[0].GetProperty("ledgerSequence").GetInt32());
        Assert.Equal(JsonValueKind.Null, events[1].GetProperty("ledgerSequence").ValueKind);
        Assert.Equal((1, 2), (history.GetProperty("page").GetInt32(), history.GetProperty("total").GetInt32()));
        Assert.Equal(JsonValueKind.Null, history.GetProperty("next").ValueKind);
    }

    [Fact]
    public async Task History_by_the_opaque_id_finds_the_same_instance()
    {
        var id = WorkflowInstanceId.Of(AddressOf()).Value;

        Assert.Equal(2, (await Read($"/sindri/engine/instances/{id}/history?page=1")).GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task The_address_query_wins_over_a_different_id_in_the_path()
    {
        var other = WorkflowInstanceId.Of(InstanceAddress.From("T-9/step/1")).Value;

        var history = await Read($"/sindri/engine/instances/{other}/history?address=" + Uri.EscapeDataString(Address));

        Assert.Equal(2, history.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task An_address_that_never_ran_is_404_SND_ENGINE_404()
    {
        var served = await _host.Http.Ask(Get("/sindri/engine/instances/-/history?address=T-9%2Fstep%2F1", _human));

        Assert.Equal((HttpStatusCode.NotFound, "SND-ENGINE-404"), (served.Status, served.Code));
    }

    [Fact]
    public async Task A_page_below_one_is_400_SND_API_013()
    {
        var served = await _host.Http.Ask(Get("/sindri/engine/instances/-/history?address=" + Uri.EscapeDataString(Address) + "&page=0", _human));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-API-013"), (served.Status, served.Code));
    }

    [Theory]
    [InlineData("/sindri/engine")]
    [InlineData("/sindri/engine/instances")]
    [InlineData("/sindri/engine/instances/-/history?address=ITM-01JE9%2Fitem%2F1%2Fstep.4")]
    public async Task A_server_that_is_down_is_503_SND_ENGINE_001(string path)
    {
        _engine.Down = true;

        var served = await _host.Http.Ask(Get(path, _human));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "SND-ENGINE-001"), (served.Status, served.Code));
    }

    [Fact]
    public async Task A_stuck_instance_makes_status_degraded_and_a_down_server_does_not_fail_it()
    {
        var degraded = await Read("/sindri/status");
        Assert.Equal("degraded", degraded.GetProperty("health").GetProperty("word").GetString());
        Assert.Contains(degraded.GetProperty("health").GetProperty("problems").EnumerateArray(), p => p.GetProperty("code").GetString() == "SND-ENGINE-002");

        _engine.Down = true;
        ((FakeTimeProvider)_host.App.Services.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromSeconds(11));   // past the count's ten seconds
        var served = await _host.Http.Ask(Get("/sindri/status", _human));

        Assert.Equal(HttpStatusCode.OK, served.Status);
        Assert.DoesNotContain("SND-ENGINE-002", served.Body, StringComparison.Ordinal);
    }
}
