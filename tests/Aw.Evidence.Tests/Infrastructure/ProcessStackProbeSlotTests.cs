using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Aw.Evidence.Infrastructure.Stack;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real probe's worktree-slot checks: the slot database through a stub docker, and the listener's directory through the real lsof.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessStackProbeSlotTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private int _count;

    private ProcessStackProbe Probe(string dockerBody, string? container = "db") =>
        new(StubCommand.Write(_root, "docker-" + _count++.ToString(CultureInfo.InvariantCulture), dockerBody), new DatabaseSettings(container, User: "postgres", SeedTable: null));

    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task A_database_the_server_lists_exists_and_one_it_does_not_list_is_a_probe_failure()
    {
        using var present = Probe("echo 1");
        using var absent = Probe("exit 0");

        Assert.True((await present.DatabaseExistsAsync("app_2", TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await absent.DatabaseExistsAsync("app_2", TestContext.Current.CancellationToken)).IsError);
    }

    [Fact]
    public async Task No_container_means_nothing_to_probe_and_a_name_with_more_than_letters_digits_and_underscores_is_refused_before_docker_runs()
    {
        using var none = Probe("exit 1", container: null);
        using var docker = Probe("echo 1");

        Assert.True((await none.DatabaseExistsAsync("app_2", TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await docker.DatabaseExistsAsync("app_2'; select 1; --", TestContext.Current.CancellationToken)).IsError);
    }

    [Fact]
    public async Task A_listener_is_found_by_the_directory_its_process_runs_from_and_a_free_port_has_none()
    {
        Assert.SkipUnless(File.Exists("/usr/bin/lsof") || File.Exists("/usr/sbin/lsof"), "lsof is not installed");
        using var probe = Probe("exit 0");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var found = await probe.ListenerDirectoryAsync(port, TestContext.Current.CancellationToken);
        var free = await probe.ListenerDirectoryAsync(LocalHttpServer.FreePort(), TestContext.Current.CancellationToken);

        Assert.Equal(Directory.GetCurrentDirectory(), found.Value);
        Assert.Null(free.Value);
    }
}
