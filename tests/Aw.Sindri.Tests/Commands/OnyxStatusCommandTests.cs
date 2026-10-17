using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri onyx status' as Program runs it, over fake Docker and host probes, with no control plane recorded in the home.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class OnyxStatusCommandTests : IDisposable
{
    private const long Gib = 1L << 30;

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public OnyxStatusCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(long freeBytes, params string[] args)
    {
        var probes = new FakeHostProbes { FreeBytes = freeBytes };
        _ = probes.Directories.Add("/sindri-home");
        return CommandTable.Run(args, Environment, TimeProvider.System, services => services
            .AddSingleton<IContainerRuntime>(new FakeContainerRuntime(FakeContainerRuntime.Healthy))
            .AddSingleton<IHostProbes>(probes)
            .AddSingleton<ISindriHome>(new FakeSindriHome("/sindri-home")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Onyx_status_prints_the_seven_rows_and_exits_1_when_a_check_fails()
    {
        var code = await Run(9 * Gib, "onyx", "status");

        Assert.Equal(1, code);
        var lines = _out.ToString().TrimEnd('\n').Split('\n');
        Assert.Equal(
            [
                "onyx      not set up (run `sindri onyx up`)",
                "pinned    v4.9.0 (f1738ed)",
                "docker    ok 29.4.0",
                "compose   ok v2.40.3",
                "memory    ok 15.7 GiB for Docker, 0.0 GiB used by other containers, needs 5.5 GiB free",
                "disk      fail 9.0 GiB free at /sindri-home, needs 10.5 GiB; Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources) (SND-ONYX-003: Not enough disk: 9.0 GiB free at /sindri-home, needs 10.5 GiB; Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources). Free space on that volume, then run this again)",
                "port      ok 3000 free",
            ],
            lines,
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Onyx_status_exits_0_when_every_check_is_ok()
        => Assert.Equal(0, await Run(100 * Gib, "onyx", "status"));

    [Fact, Trait("Lane", "integration")]
    public async Task Onyx_status_json_parses_as_the_response()
    {
        var code = await Run(9 * Gib, "onyx", "status", "--json");

        var written = _out.ToString();
        var status = AwJson.Deserialize<OnyxStatusResponse>(Encoding.UTF8.GetBytes(written), _json).Value;
        Assert.Equal(1, code);
        Assert.Contains("\"state\":\"not-set-up\"", written, StringComparison.Ordinal);
        Assert.Equal(OnyxState.NotSetUp, status.State);
        Assert.Equal(written.TrimEnd('\n'), Encoding.UTF8.GetString(AwJson.Serialize(status, _json)));
        Assert.Equal(1, status.ExitCode);
        Assert.Equal("SND-ONYX-003", status.Checks.Single(static c => c.Name == "disk").Problem?.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Onyx_status_with_an_unknown_flag_is_a_usage_error_exit_2()
    {
        var code = await Run(100 * Gib, "onyx", "status", "--bogus");

        Assert.Equal(2, code);
        Assert.Contains("SND-CLI-001", _error.ToString(), StringComparison.Ordinal);
    }

    private Task<int> RunOver(OnyxUpRig rig, params string[] args)
        => CommandTable.Run(args, Environment, TimeProvider.System, services => services
            .AddSingleton<IContainerRuntime>(rig.Runtime)
            .AddSingleton<IHostProbes>(rig.Probes)
            .AddSingleton<IOnyxHealth>(rig.Health)
            .AddSingleton<IOnyxWorkspace>(rig.Workspace)
            .AddSingleton<ISindriHome>(new FakeSindriHome(OnyxUpRig.Home)));

    [Fact, Trait("Lane", "integration")]
    public async Task Status_running_has_url_and_running_version_rows()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(TestContext.Current.CancellationToken)).Value;

        var code = await RunOver(rig, "onyx", "status");

        Assert.Equal(0, code);
        var lines = _out.ToString().TrimEnd('\n').Split('\n');
        Assert.Equal(
            ["onyx      running", "url       http://127.0.0.1:3000", "running   v4.9.0", "pinned    v4.9.0 (f1738ed)"],
            lines.Take(4),
            StringComparer.Ordinal);
        Assert.Contains("port      ok 3000 (Onyx's own)", lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Status_stopped_has_no_url_row()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(TestContext.Current.CancellationToken)).Value;
        _ = rig.Runtime.Stop(rig.Paths.Project, TestContext.Current.CancellationToken);

        _ = await RunOver(rig, "onyx", "status");

        Assert.StartsWith("onyx      stopped (run `sindri onyx up` to start it)\npinned ", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Status_is_unknown_not_stopped_when_docker_points_at_another_machine()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(TestContext.Current.CancellationToken)).Value;
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        _ = await RunOver(rig, "onyx", "status");

        var text = _out.ToString();
        Assert.StartsWith("onyx      unknown (Docker isn't usable here: docker points at another machine)\npinned ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri onyx up` to start", text, StringComparison.Ordinal);
        Assert.DoesNotContain("url ", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Status_maps_a_Result_error_to_an_error_line_and_exit_1()
    {
        var code = await CommandTable.Run(["onyx", "status"], Environment, TimeProvider.System, services => services
            .AddSingleton<IService<OnyxStatusRequest, OnyxStatusResponse>>(new FailingStatus()));

        Assert.Equal(1, code);
        Assert.Contains("SND-ONYX-011 docker said no", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _out.ToString());
    }

    private sealed class FailingStatus : IService<OnyxStatusRequest, OnyxStatusResponse>
    {
        public ValueTask<Result<OnyxStatusResponse>> Handle(OnyxStatusRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<OnyxStatusResponse>>(SindriErrors.OnyxComposeFailed.With("docker said no"));
    }
}
