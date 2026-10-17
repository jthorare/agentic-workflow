using Aw.Kernel.Json;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// What only the file adapter can get wrong: permissions and partial files.
public sealed class FileOnyxWorkspaceTests : IDisposable
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "aw-onyx-ws-" + Guid.NewGuid().ToString("N"));
    private readonly FileOnyxWorkspace _workspace;
    private readonly OnyxPaths _paths;

    public FileOnyxWorkspaceTests()
    {
        _paths = OnyxPaths.For(_home);
        _workspace = new FileOnyxWorkspace(new FakeSindriHome(_home), AwJson.CreateOptions(SindriJsonContext.Default));
    }

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_file_that_exists_but_cannot_be_read_is_SND_ONYX_012_not_an_exception_or_a_missing_file()
    {
        if (Environment.IsPrivilegedProcess)
        {
            return;   // root reads a mode-000 file
        }

        _ = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);
        File.SetUnixFileMode(_paths.Env, UnixFileMode.None);
        try
        {
            var read = _workspace.Read(_paths.Env);

            Assert.True(read.IsError, "read of an unreadable file succeeded with " + (read.IsSuccess ? read.Value ?? "null" : ""));
            Assert.Equal("SND-ONYX-012", read.Error.Code);
            Assert.Contains(_paths.Env, read.Error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(_paths.Env, OwnerFile);
        }
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_protected_file_is_0600_and_its_directory_0700()
    {
        _ = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);

        Assert.Equal(OwnerFile, File.GetUnixFileMode(_paths.Env));
        Assert.Equal(OwnerDirectory, File.GetUnixFileMode(Path.GetDirectoryName(_paths.Env)!));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_protected_file_that_is_wider_than_0600_is_narrowed_even_when_its_content_is_unchanged()
    {
        _ = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);
        File.SetUnixFileMode(_paths.Env, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var wrote = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);

        Assert.False(wrote);
        Assert.Equal(OwnerFile, File.GetUnixFileMode(_paths.Env));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_file_that_is_not_protected_keeps_the_mode_it_has()
    {
        _ = _workspace.WriteIfChanged(_paths.Override, "a\n", secret: false);
        File.SetUnixFileMode(_paths.Override, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        _ = _workspace.WriteIfChanged(_paths.Override, "a\n", secret: false);

        Assert.NotEqual(OwnerFile, File.GetUnixFileMode(_paths.Override));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_lock_the_user_may_not_create_is_a_clear_error_not_a_crash_or_a_busy_answer()
    {
        _ = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);
        File.SetUnixFileMode(_paths.Root, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Assert.SkipWhen(Environment.UserName == "root", "root ignores directory permissions");

            var result = _workspace.TryLock();

            Assert.True(result.IsError);
            Assert.Equal("SND-ONYX-012", result.Error.Code);
            Assert.Contains(_paths.Root, result.Error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(_paths.Root, OwnerDirectory);
        }
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_changed_protected_file_stays_0600()
    {
        _ = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);

        _ = _workspace.WriteIfChanged(_paths.Env, "A=2\n", secret: true);

        Assert.Equal(OwnerFile, File.GetUnixFileMode(_paths.Env));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void The_state_file_is_0600()
    {
        _workspace.WriteState(new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Equal(OwnerFile, File.GetUnixFileMode(_paths.State));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_state_file_carries_its_schema_version()
    {
        _workspace.WriteState(new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Contains("\"schemaVersion\":1", File.ReadAllText(_paths.State), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_write_never_leaves_a_partial_file_for_a_reader()
    {
        var big = new string('x', 2_000_000);
        _ = _workspace.WriteIfChanged(_paths.Override, big + "a", secret: false);
        using var stop = new CancellationTokenSource();
        var lengths = new List<int>();
        var reader = Task.Run(
            () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    lengths.Add(_workspace.Read(_paths.Override).Value?.Length ?? -1);
                }
            },
            CancellationToken.None);

        for (var i = 0; i < 20; i++)
        {
            _ = _workspace.WriteIfChanged(_paths.Override, big + (i % 2 == 0 ? "b" : "c"), secret: false);
        }

        await stop.CancelAsync();
        await reader.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.All(lengths, static length => Assert.Equal(2_000_001, length));
    }

    [Fact, Trait("Lane", "integration")]
    public void No_temporary_file_is_left_behind()
    {
        _ = _workspace.WriteIfChanged(_paths.Override, "a\n", secret: false);
        _workspace.WriteState(new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Empty(Directory.EnumerateFiles(_home, "*.tmp*", SearchOption.AllDirectories));
    }
}
