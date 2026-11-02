using System.Runtime.Versioning;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The launch's session home (sandbox spec §3.1, rev 7): made 0700 before the sandbox, removed at close and in the crash sweep.</summary>
[Lane(TestLane.Integration)]
[SupportedOSPlatform("linux")]
public sealed class SessionHomeTests : IDisposable
{
    private static readonly ControlEndpoint _endpoint = new(new Uri("http://127.0.0.1:7480"), Pid: 4242, Epoch: 7);

    private static readonly LaunchId _one = LaunchId.From("LCH-" + new string('0', 31) + "1");
    private static readonly LaunchId _live = LaunchId.From("LCH-" + new string('0', 31) + "2");
    private static readonly LaunchId _dead = LaunchId.From("LCH-" + new string('0', 31) + "3");

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "sandbox-fixtures", "home-" + RealTime.NewGuid().ToString("N")[..8]);
    private readonly string _real;

    public SessionHomeTests()
    {
        _real = _root + "/sindri";
        _ = Directory.CreateDirectory(_real + "/launches");
        _ = Directory.CreateDirectory(_real + "/approved");
        File.WriteAllText(_real + "/launches/marker", "real");
        File.WriteAllText(_real + "/control.json", "{}");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void The_home_sits_beside_the_real_home_and_is_0700_with_a_0600_secret_and_an_empty_gh_config()
    {
        var home = SessionHome.Create(_real, _one, "s3cret", _endpoint);

        Assert.Equal(_real + "-sessions/" + _one.Value, home);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(home));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(SessionHome.SecretFile(home)));
        Assert.Equal("s3cret", File.ReadAllText(SessionHome.SecretFile(home)));
        Assert.Empty(Directory.GetFileSystemEntries(home + "/gh-config"));
    }

    [Fact]
    public void Launches_and_approved_are_links_to_the_real_folders()
    {
        var home = SessionHome.Create(_real, _one, "s", _endpoint);

        Assert.Equal(_real + "/launches", new DirectoryInfo(home + "/launches").LinkTarget);
        Assert.Equal(_real + "/approved", new DirectoryInfo(home + "/approved").LinkTarget);
        Assert.True(File.Exists(home + "/launches/marker"));
    }

    [Fact]
    public void Under_the_sandbox_control_json_is_a_file_with_the_endpoint_record_the_host_reads_back()
    {
        var home = SessionHome.Create(_real, _one, "s", _endpoint);

        Assert.Null(new FileInfo(home + "/control.json").LinkTarget);
        var read = AwJson.Deserialize<ControlEndpoint>(File.ReadAllBytes(home + "/control.json"), AwJson.CreateOptions(SindriJsonContext.Default));
        Assert.Equal(_endpoint, read.Value with { Temporal = null });
    }

    [Fact]
    public void Outside_the_sandbox_control_json_links_to_the_real_file()
    {
        var home = SessionHome.Create(_real, _one, "s", control: null);

        Assert.Equal(_real + "/control.json", new FileInfo(home + "/control.json").LinkTarget);
    }

    [Fact]
    public void Writing_control_json_again_replaces_it_for_a_restarted_serve()
    {
        var home = SessionHome.Create(_real, _one, "s", _endpoint);

        SessionHome.WriteControl(home, _endpoint with { Epoch = 8 });

        Assert.Contains("\"epoch\":8", File.ReadAllText(home + "/control.json").Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Directory.GetFiles(home), f => f.Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public void Removing_the_home_leaves_what_the_links_point_at()
    {
        var home = SessionHome.Create(_real, _one, "s", _endpoint);

        SessionHome.Remove(home);

        Assert.False(Directory.Exists(home));
        Assert.True(File.Exists(_real + "/launches/marker"));
        Assert.True(Directory.Exists(_real + "/approved"));
        SessionHome.Remove(home);   // a missing home is fine
    }

    [Fact]
    public void The_crash_sweep_removes_the_homes_of_dead_launches_and_keeps_live_ones()
    {
        var live = SessionHome.Create(_real, _live, "s", _endpoint);
        var dead = SessionHome.Create(_real, _dead, "s", _endpoint);

        var removed = SessionHome.Sweep(_real, id => id == _live.Value);

        Assert.Equal([_dead.Value], removed);
        Assert.True(Directory.Exists(live));
        Assert.False(Directory.Exists(dead));
        Assert.True(File.Exists(_real + "/launches/marker"));
    }

    [Fact]
    public void Sweeping_with_no_sessions_folder_removes_nothing() => Assert.Empty(SessionHome.Sweep(_real, _ => false));

    [Fact]
    public void Making_a_home_again_for_the_same_launch_starts_clean()
    {
        var home = SessionHome.Create(_real, _one, "old", _endpoint);
        File.WriteAllText(home + "/gh-config/leftover", "x");

        _ = SessionHome.Create(_real, _one, "new", _endpoint);

        Assert.Equal("new", File.ReadAllText(SessionHome.SecretFile(home)));
        Assert.False(File.Exists(home + "/gh-config/leftover"));
    }
}
