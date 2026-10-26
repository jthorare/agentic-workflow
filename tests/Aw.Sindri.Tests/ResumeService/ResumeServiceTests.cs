using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ResumeService;

/// <summary>Resumeing a service (API P6 unit D): a human's desk command that appends 'service.resumed' for a service of the approved profile.</summary>
[Lane(TestLane.Unit)]
public sealed class ResumeServiceTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ResumeServiceService Service() => new(_h.Profiles, _h.Store, _h.Held, _h.Events, _h.Callers, [new BuiltInService(ServiceName.From("setup"), new Cadence(TimeSpan.FromMinutes(1)))]);

    private async Task Approve()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _ = await _h.HoldLease();
    }

    private IDisposable Human() => _h.Callers.Enter(CallerContext.Http(new HttpCaller(CallerClass.Human, Terminal: false, ClientLabel.None, Repo: null), _h.Ids));   // a fresh command id each time, as the chain gives

    [Fact]
    public async Task ResumeService_appends_service_resumed_for_the_named_service()
    {
        await Approve();
        using var entered = Human();

        var result = await Service().Handle(A.ResumeServiceRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Store.Events);
        Expect.Equal("service.resumed", recorded.Envelope.Type);
        var payload = _h.Events.Payload<ServiceResumed>(recorded).Value;
        Expect.Equal(ServiceName.From("heartbeat"), payload.Service);
        Expect.Equal($"service.resumed:heartbeat:{payload.CommandId.Value}", recorded.IdempotencyKey);
        Expect.Equal((payload.CommandId, recorded.Sequence), (result.Value.CommandId, result.Value.Sequence));
    }

    [Fact]
    public async Task Resume_unknown_service_is_SND_SVC_404_and_records_nothing()
    {
        await Approve();
        using var entered = Human();

        var result = await Service().Handle(A.ResumeServiceRequest().ForService(ServiceName.From("ghost")).Build(), Token);

        Expect.Equal(SindriErrors.ServiceNotFound.Code, result.Error.Code);
        Assert.Empty(_h.Store.Events);
    }

    [Fact]
    public async Task Resume_with_no_approved_profile_is_SND_SVC_404()
    {
        _ = await _h.HoldLease();
        using var entered = Human();

        var result = await Service().Handle(A.ResumeServiceRequest().Build(), Token);

        Expect.Equal(SindriErrors.ServiceNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Resume_twice_is_accepted_and_appended_twice()
    {
        await Approve();
        for (var n = 0; n < 2; n++)
        {
            using var entered = Human();   // each command has its own id, so each is its own record
            Assert.True((await Service().Handle(A.ResumeServiceRequest().Build(), Token)).IsSuccess);
        }

        Expect.Equal(2, _h.Store.Events.Count(e => string.Equals(e.Envelope.Type, "service.resumed", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("setup", "setup is a built-in service and can't be paused or resumed.")]
    [InlineData("ghost", "The approved profile has no service 'ghost' (fix: sindri services)")]
    public async Task Resume_refuses_with_the_wording_of_what_the_name_is(string name, string message)
    {
        await Approve();
        using var entered = Human();

        var result = await Service().Handle(A.ResumeServiceRequest().ForService(ServiceName.From(name)).Build(), Token);

        Expect.Equal(SindriErrors.ServiceNotFound.Code, result.Error.Code);
        Expect.Equal(message, result.Error.Message);
        Assert.Empty(_h.Store.Events);
    }
}
