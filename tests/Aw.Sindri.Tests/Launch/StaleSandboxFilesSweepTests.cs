using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The start-up sweep removes what a dead launch left of its process group file and its macOS temp folder, and nothing of a live one's (SB-9c).</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class StaleSandboxFilesSweepTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-stalesweep-").FullName;
    private readonly string _tmp = Directory.CreateTempSubdirectory("aw-staletmp-").FullName;

    private static LaunchId Dead => LaunchId.From("LCH-dead0000000000000000000000000000");

    private static LaunchId Alive => LaunchId.From("LCH-a11e0000000000000000000000000000");

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        Directory.Delete(_tmp, recursive: true);
    }

    private string Groups => ProcessGroupFiles.Directory(new LauncherOptions(_home).LaunchDirectory);

    private LaunchFileStore Store(bool mac = true, Func<string, bool>? owned = null) =>
        new(new LauncherOptions(_home), new StaleSweepSeams(mac, _tmp, owned ?? (static _ => true), static pgid => pgid == 500));

    private string TempOf(LaunchId launch) => Path.Combine(_tmp, Path.GetFileName(SandboxPaths.SeatbeltTemp(launch)));

    private string MakeTemp(LaunchId launch)
    {
        var folder = TempOf(launch);
        _ = Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "scratch"), "x");
        return folder;
    }

    private void Seed(LaunchId launch)
    {
        _ = Directory.CreateDirectory(new LauncherOptions(_home).LaunchesDirectory);
        File.WriteAllText(Path.Combine(new LauncherOptions(_home).LaunchesDirectory, LaunchFiles.CaptureName(launch)), "x\n");
    }

    private static int Sweep(LaunchFileStore store, params LaunchId[] live) =>
        store.SweepStale(KnownInstants.Epoch.AddHours(1), TimeSpan.FromSeconds(60), live.Contains);

    [Fact]
    public void A_dead_groups_file_goes_and_a_live_groups_stays_on_any_os()
    {
        ProcessGroupFiles.WritePgid(Groups, Dead.Value, 600);
        ProcessGroupFiles.WritePgid(Groups, Alive.Value, 500);

        var removed = Sweep(Store(mac: false));

        Assert.Equal(1, removed);
        Assert.Null(ProcessGroupFiles.ReadPgid(Groups, Dead.Value));
        Assert.Equal(500, ProcessGroupFiles.ReadPgid(Groups, Alive.Value));
    }

    [Fact]
    public void A_dead_groups_file_of_a_running_launch_stays()
    {
        ProcessGroupFiles.WritePgid(Groups, Alive.Value, 600);

        _ = Sweep(Store(), Alive);

        Assert.Equal(600, ProcessGroupFiles.ReadPgid(Groups, Alive.Value));
    }

    [Fact]
    public void The_real_current_process_group_reads_as_alive_and_is_kept()
    {
        _ = Directory.CreateDirectory(Groups);
        var file = Path.Combine(Groups, Alive.Value + ".pgid");
        File.WriteAllText(file, Infrastructure.Locks.Libc.OwnGroup().ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");

        _ = Sweep(new LaunchFileStore(new LauncherOptions(_home), new StaleSweepSeams(Mac: false)));

        Assert.True(File.Exists(file));
    }

    [Fact]
    public void A_stale_temp_folder_goes_and_a_live_launchs_stays()
    {
        Seed(Alive);
        var stale = MakeTemp(Dead);
        var kept = MakeTemp(Alive);

        var removed = Sweep(Store(), Alive);

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(kept));
    }

    [Fact]
    public void A_live_launch_with_only_a_session_home_keeps_its_temp_folder()
    {
        var sindriHome = Path.Combine(_home, "sindri");
        _ = Directory.CreateDirectory(Path.Combine(SessionHomePaths.Sessions(sindriHome), Alive.Value));
        var kept = MakeTemp(Alive);
        var stale = MakeTemp(Dead);
        var store = new LaunchFileStore(new LauncherOptions(_home) { SindriHome = sindriHome }, new StaleSweepSeams(Mac: true, TempRoot: _tmp, Owned: static _ => true, Alive: static _ => false));

        _ = Sweep(store, Alive);

        Assert.True(Directory.Exists(kept));
        Assert.False(Directory.Exists(stale));
    }

    [Fact]
    public void Other_names_symlinks_and_folders_of_another_user_are_left_alone()
    {
        var other = Path.Combine(_tmp, "aw-notes");
        var longer = Path.Combine(_tmp, "aw-0123456789");
        var upper = Path.Combine(_tmp, "aw-0123ABCD");
        var target = Path.Combine(_tmp, "elsewhere");
        var link = Path.Combine(_tmp, "aw-0a1b2c3d");
        foreach (var folder in new[] { other, longer, upper, target })
        {
            _ = Directory.CreateDirectory(folder);
        }

        File.WriteAllText(Path.Combine(target, "precious"), "x");
        _ = Directory.CreateSymbolicLink(link, target);
        var foreign = MakeTemp(Dead);

        var removed = Sweep(Store(owned: path => !string.Equals(path, foreign, StringComparison.Ordinal)));

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(other) && Directory.Exists(longer) && Directory.Exists(upper) && Directory.Exists(foreign));
        Assert.True(File.Exists(Path.Combine(target, "precious")));
        Assert.NotNull(new DirectoryInfo(link).LinkTarget);
    }

    [Fact]
    public void Temp_folders_are_not_touched_off_macOS()
    {
        var folder = MakeTemp(Dead);

        _ = Sweep(Store(mac: false));

        Assert.True(Directory.Exists(folder));
    }
}
