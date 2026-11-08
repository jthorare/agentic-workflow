using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetEngineHistory;
using Aw.Sindri.Testing.Engine;

namespace Aw.Sindri.Tests.GetEngineHistory;

/// <summary>'GET /engine/instances/{id}/history' (API spec §3.8, E-30, P13): pages of 50 newest first, each event joined to the ledger row that mirrors it.</summary>
[Lane(TestLane.Unit)]
public sealed class GetEngineHistoryServiceTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-01JE9/item/1/step.4");
    private readonly FakeEngineReader _engine = new();
    private readonly FakeMirroredSequences _mirrored = new();

    private GetEngineHistoryService Service() => new(_engine, _mirrored);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void Events(int count) => _engine.Record(_address, "run-1", [.. Enumerable.Range(1, count).Reverse().Select(i => EngineFacts.Event(i, KnownInstants.Epoch.AddSeconds(i), activityId: i % 2 == 0 ? string.Create(CultureInfo.InvariantCulture, $"act-{i}") : null))]);

    [Fact]
    public async Task A_mirrored_event_names_its_ledger_sequence_and_an_unmirrored_one_has_none()
    {
        Events(4);
        _mirrored.Rows["act-4"] = 4231;

        var served = await Service().Handle(new GetEngineHistoryRequest(Address: _address.Value), Token);

        Assert.Equal([4, 3, 2, 1], served.Value.Events.Select(static e => e.EventId));
        Assert.Equal([4231L, null, null, null], served.Value.Events.Select(static e => e.LedgerSequence));
        Assert.Equal((_address, "run-1"), _mirrored.Asked.Single());
    }

    [Fact]
    public async Task A_page_holds_fifty_events_newest_first_with_the_total_and_the_next_page()
    {
        Events(120);

        var first = (await Service().Handle(new GetEngineHistoryRequest(Address: _address.Value), Token)).Value;
        var third = (await Service().Handle(new GetEngineHistoryRequest(Address: _address.Value, Page: "3"), Token)).Value;

        Assert.Equal((50, 1, 120L, 2), (first.Events.Count, first.Page, first.Total, first.Next));
        Assert.Equal((20, 3, null), (third.Events.Count, third.Page, third.Next));
        Assert.Equal(20L, third.Events[0].EventId);
    }

    [Fact]
    public async Task Page_two_holds_the_next_fifty_older_events_and_points_on_to_page_three()
    {
        Events(120);

        var first = (await Service().Handle(new GetEngineHistoryRequest(Address: _address.Value, Page: "1"), Token)).Value;
        var second = (await Service().Handle(new GetEngineHistoryRequest(Address: _address.Value, Page: "2"), Token)).Value;

        Assert.Equal(2, first.Next);
        Assert.Equal((50, 2, 3), (second.Events.Count, second.Page, second.Next));
        Assert.Equal((70L, 21L), (second.Events[0].EventId, second.Events[^1].EventId));
        Assert.True(first.Events[^1].EventId > second.Events[0].EventId);
    }

    [Fact]
    public async Task An_address_wins_over_a_different_id_in_the_path()
    {
        Events(2);
        var other = WorkflowInstanceId.Of(InstanceAddress.From("T-9/step/1")).Value;

        var served = await Service().Handle(new GetEngineHistoryRequest(Instance: other, Address: _address.Value), Token);

        Assert.Equal(2, served.Value.Events.Count);
    }

    [Fact]
    public async Task The_opaque_id_finds_the_instance_as_the_address_does()
    {
        Events(2);

        var served = await Service().Handle(new GetEngineHistoryRequest(Instance: WorkflowInstanceId.Of(_address).Value), Token);

        Assert.Equal(2, served.Value.Events.Count);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("two")]
    [InlineData("")]
    public async Task A_page_below_one_or_not_a_number_is_SND_API_013(string page)
    {
        Events(1);

        var served = await Service().Handle(new GetEngineHistoryRequest(Address: _address.Value, Page: page), Token);

        Assert.Equal(SindriErrors.InvalidQuery.Code, served.Error.Code);
    }

    [Fact]
    public async Task No_id_and_no_address_is_SND_API_013()
    {
        var served = await Service().Handle(new GetEngineHistoryRequest(), Token);

        Assert.Equal(SindriErrors.InvalidQuery.Code, served.Error.Code);
    }

    [Fact]
    public async Task An_address_that_never_ran_is_SND_ENGINE_404()
    {
        var served = await Service().Handle(new GetEngineHistoryRequest(Address: "T-9/step/1"), Token);

        Assert.Equal(SindriErrors.EngineNotFound.Code, served.Error.Code);
    }

    [Fact]
    public async Task A_server_that_does_not_answer_is_SND_ENGINE_001()
    {
        Events(1);
        _engine.Down = true;

        var served = await Service().Handle(new GetEngineHistoryRequest(Address: _address.Value), Token);

        Assert.Equal(SindriErrors.EngineUnavailable.Code, served.Error.Code);
    }
}
