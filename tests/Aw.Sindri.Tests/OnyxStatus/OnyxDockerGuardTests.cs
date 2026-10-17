using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// "Docker is local and answering" is one guard inside OnyxFacts: nothing sends a docker or compose command at a daemon Sindri may not talk to.
public sealed class OnyxDockerGuardTests
{
    private const string Remote = "tcp://10.0.0.5:2375";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OnyxFacts Facts(OnyxUpRig rig) => new(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home));

    private static async Task<OnyxUpRig> RigPointedAtAnotherMachine()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = Remote };
        rig.Runtime.Calls.Clear();
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public void Facts_inspect_the_runtime_once_however_many_facts_are_read()
    {
        var rig = new OnyxUpRig();
        var facts = Facts(rig);

        _ = facts.Read(Token);
        _ = facts.Read(3001, Token);
        _ = facts.Containers(Token);
        _ = facts.Platform(Token);

        _ = Assert.Single(rig.Runtime.Calls, static c => c == "Inspect");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Containers_and_platform_are_the_docker_problem_and_never_reach_a_remote_runtime()
    {
        var rig = await RigPointedAtAnotherMachine();
        var facts = Facts(rig);

        var containers = facts.Containers(Token);
        var platform = facts.Platform(Token);

        Assert.Equal("SND-ONYX-010", containers.Error.Code);
        Assert.Contains(Remote, containers.Error.Message, StringComparison.Ordinal);
        Assert.Equal("SND-ONYX-010", platform.Error.Code);
        Assert.All(rig.Runtime.Calls, static c => Assert.Equal("Inspect", c));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_with_a_docker_on_another_machine_shows_its_docker_row_and_sends_it_nothing()
    {
        var rig = await RigPointedAtAnotherMachine();
        var service = new OnyxStatusService(rig.Pins, Facts(rig), rig.Health, rig.Workspace);

        var status = (await service.Handle(new OnyxStatusRequest(), Token)).Value;

        Assert.Equal("SND-ONYX-010", Assert.Single(status.Checks, static c => c.Name == "docker").Problem?.Code);
        Assert.Equal(OnyxState.Unknown, status.State);
        Assert.Null(status.Url);
        Assert.Null(status.RunningVersion);
        Assert.All(rig.Runtime.Calls, static c => Assert.Equal("Inspect", c));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_with_a_docker_on_another_machine_names_the_problem_and_sends_it_nothing()
    {
        var rig = await RigPointedAtAnotherMachine();
        var doctor = new OnyxStackDoctor(rig.Workspace, Facts(rig), rig.Pins);

        var check = await doctor.Check(Token);

        Assert.Equal(CheckState.Warn, check!.State);
        Assert.Contains("Docker points at another machine (" + Remote + ")", check.Detail, StringComparison.Ordinal);
        Assert.Contains("onyx:stack not checked", check.Detail, StringComparison.Ordinal);
        Assert.All(rig.Runtime.Calls, static c => Assert.Equal("Inspect", c));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_a_usable_docker_status_and_doctor_still_read_the_stack()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.Calls.Clear();

        var status = (await new OnyxStatusService(rig.Pins, Facts(rig), rig.Health, rig.Workspace).Handle(new OnyxStatusRequest(), Token)).Value;
        var check = await new OnyxStackDoctor(rig.Workspace, Facts(rig), rig.Pins).Check(Token);

        Assert.Equal(OnyxState.Running, status.State);
        Assert.DoesNotContain("not checked", check!.Detail, StringComparison.Ordinal);
        Assert.Contains("Ps", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.Contains("ServerPlatform", rig.Runtime.Calls, StringComparer.Ordinal);
    }
}
