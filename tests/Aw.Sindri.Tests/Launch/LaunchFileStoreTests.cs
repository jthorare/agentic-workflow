using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The launcher-owned per-launch files on disk (API spec §8.1, launcher L-9): the secret's hash, the capture rule and the reported id, and their cleanup.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LaunchFileStoreTests : LaunchFilesContract, IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launchfiles-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private LauncherOptions Options => new(_home);

    private string Launches => Options.LaunchesDirectory;

    private static LaunchId Second => LaunchId.From("LCH-00000000000000000000000000000002");

    protected override ILaunchFiles Create() => new LaunchFileStore(Options);

    protected override void Open(ILaunchFiles files, LaunchId launch, string proof) => LaunchSecrets.Write(Options.SecretsDirectory, launch, proof);

    private string Seed(string name, DateTimeOffset modified)
    {
        _ = Directory.CreateDirectory(Launches);
        var path = Path.Combine(Launches, name);
        File.WriteAllText(path, "x\n");
        File.SetLastWriteTimeUtc(path, modified.UtcDateTime);
        return path;
    }

    [Fact]
    public void Forget_deletes_the_hash_the_capture_rule_and_the_reported_id()
    {
        var files = Create();
        Open(files, A.FirstLaunchId, "p-1");
        var capture = Seed(LaunchFiles.CaptureName(A.FirstLaunchId), KnownInstants.Epoch);
        var id = Seed(LaunchFiles.SessionIdName(A.FirstLaunchId), KnownInstants.Epoch);
        var other = Seed(LaunchFiles.CaptureName(Second), KnownInstants.Epoch);

        files.Forget(A.FirstLaunchId);

        Expect.Equal((false, false, false, true), (File.Exists(LaunchSecrets.PathFor(Options.SecretsDirectory, A.FirstLaunchId)), File.Exists(capture), File.Exists(id), File.Exists(other)));
    }

    [Fact]
    public void Startup_sweep_removes_stale_per_launch_files()
    {
        var now = KnownInstants.Epoch.AddHours(1);
        var files = Create();
        var stale = Seed(LaunchFiles.CaptureName(A.FirstLaunchId), now.AddSeconds(-61));
        var staleId = Seed(LaunchFiles.SessionIdName(A.FirstLaunchId), now.AddSeconds(-61));
        var fresh = Seed(LaunchFiles.CaptureName(Second), now.AddSeconds(-59));
        var stranger = Seed("notes.txt", now.AddHours(-5));

        var removed = files.SweepStale(now, TimeSpan.FromSeconds(60), static _ => false);

        Expect.Equal((2, false, false, true, true), (removed, File.Exists(stale), File.Exists(staleId), File.Exists(fresh), File.Exists(stranger)));
    }

    [Fact]
    public void Startup_sweep_removes_a_secret_file_no_wrap_ever_read_but_not_a_live_launchs()
    {
        var now = KnownInstants.Epoch.AddHours(1);
        var files = Create();
        LaunchSecrets.Write(Options.SecretsDirectory, A.FirstLaunchId, "never-read");
        LaunchSecrets.Write(Options.SecretsDirectory, Second, "still-needed");
        File.SetLastWriteTimeUtc(LaunchSecrets.SecretPathFor(Options.SecretsDirectory, A.FirstLaunchId), now.AddMinutes(-5).UtcDateTime);
        File.SetLastWriteTimeUtc(LaunchSecrets.SecretPathFor(Options.SecretsDirectory, Second), now.AddMinutes(-5).UtcDateTime);

        var removed = files.SweepStale(now, TimeSpan.FromSeconds(60), static launch => launch.Equals(Second));

        Expect.Equal((1, false, true), (removed, File.Exists(LaunchSecrets.SecretPathFor(Options.SecretsDirectory, A.FirstLaunchId)), File.Exists(LaunchSecrets.SecretPathFor(Options.SecretsDirectory, Second))));
    }

    [Fact]
    public void Startup_sweep_keeps_the_files_of_a_launch_with_a_live_session()
    {
        var now = KnownInstants.Epoch.AddHours(1);
        var files = Create();
        var kept = Seed(LaunchFiles.CaptureName(A.FirstLaunchId), now.AddMinutes(-10));

        var removed = files.SweepStale(now, TimeSpan.FromSeconds(60), static launch => launch.Equals(A.FirstLaunchId));

        Expect.Equal((0, true), (removed, File.Exists(kept)));
    }

    [Fact]
    public void The_secret_hash_directory_is_not_under_launches() => Expect.True(!Options.SecretsDirectory.StartsWith(Launches, StringComparison.Ordinal));
}
