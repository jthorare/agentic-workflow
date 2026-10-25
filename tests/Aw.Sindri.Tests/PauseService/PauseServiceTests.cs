using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PauseService;

/// <summary>Pauseing a service (API P6 unit D): a human's desk command that appends 'service.paused' for a service of the approved profile.</summary>
[Lane(TestLane.Unit)]
public sealed class PauseServiceTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private PauseServiceService Service() => new(_h.Profiles, _h.Store, _h.Held, _h.Events, _h.Callers, [new BuiltInService(ServiceName.From("setup"), new Cadence(TimeSpan.FromMinutes(1)))]);

    private async Task Approve()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _ = await _h.HoldLease();
    }

    private IDisposable Human() => _h.Callers.Enter(CallerContext.Http(new HttpCaller(CallerClass.Human, Terminal: false, ClientLabel.None, Repo: null), _h.Ids));   // a fresh command id each time, as the chain gives

    [Fact]
    public async Task PauseService_appends_service_paused_for_the_named_service()
    {
        await Approve();
        using var entered = Human();

        var result = await Service().Handle(A.PauseServiceRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Store.Events);
        Expect.Equal("service.paused", recorded.Envelope.Type);
        var payload = _h.Events.Payload<ServicePaused>(recorded).Value;
        Expect.Equal(ServiceName.From("heartbeat"), payload.Service);
        Expect.Equal($"service.paused:heartbeat:{payload.CommandId.Value}", recorded.IdempotencyKey);
        Expect.Equal((payload.CommandId, recorded.Sequence), (result.Value.CommandId, result.Value.Sequence));
    }

    [Fact]
    public async Task Pause_unknown_service_is_SND_SVC_404_and_records_nothing()
    {
        await Approve();
        using var entered = Human();

        var result = await Service().Handle(A.PauseServiceRequest().ForService(ServiceName.From("ghost")).Build(), Token);

        Expect.Equal(SindriErrors.ServiceNotFound.Code, result.Error.Code);
        Assert.Empty(_h.Store.Events);
    }

    [Fact]
    public async Task Pause_with_no_approved_profile_is_SND_SVC_404()
    {
        _ = await _h.HoldLease();
        using var entered = Human();

        var result = await Service().Handle(A.PauseServiceRequest().Build(), Token);

        Expect.Equal(SindriErrors.ServiceNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Pause_twice_is_accepted_and_appended_twice()
    {
        await Approve();
        for (var n = 0; n < 2; n++)
        {
            using var entered = Human();   // each command has its own id, so each is its own record
            Assert.True((await Service().Handle(A.PauseServiceRequest().Build(), Token)).IsSuccess);
        }

        Expect.Equal(2, _h.Store.Events.Count(e => string.Equals(e.Envelope.Type, "service.paused", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task The_setup_built_in_cannot_be_paused_because_the_profile_has_no_entry_for_it()
    {
        await Approve();
        using var entered = Human();

        var result = await Service().Handle(A.PauseServiceRequest().ForService(ServiceName.From("setup")).Build(), Token);

        Expect.Equal(SindriErrors.ServiceNotFound.Code, result.Error.Code);
        Expect.Equal("setup is a built-in service and can't be paused or resumed.", result.Error.Message);
        Assert.Empty(_h.Store.Events);
    }

    [Fact]
    public async Task A_profile_service_named_like_a_built_in_can_be_paused()
    {
        await Approve();
        using var entered = Human();
        var service = new PauseServiceService(_h.Profiles, _h.Store, _h.Held, _h.Events, _h.Callers, [new BuiltInService(ServiceName.From("heartbeat"), new Cadence(TimeSpan.FromMinutes(1)))]);

        var result = await service.Handle(A.PauseServiceRequest().Build(), Token);   // heartbeat is in the approved profile

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        _ = Assert.Single(_h.Store.Events);
    }

    [Fact]
    public async Task A_name_that_is_neither_keeps_the_profile_wording()
    {
        await Approve();
        using var entered = Human();

        var result = await Service().Handle(A.PauseServiceRequest().ForService(ServiceName.From("ghost")).Build(), Token);

        Expect.Equal("The approved profile has no service 'ghost' (fix: sindri services)", result.Error.Message);
    }
}
