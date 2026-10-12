using System.Runtime.Versioning;
using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The approval files when the disk refuses: an error from the catalogue, never an exception.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class FileApprovalsFailureTests : IDisposable
{
    private readonly ProfileFiles _files = new();

    public void Dispose()
    {
        File.SetUnixFileMode(_files.Settings.ApprovalsDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _files.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task An_unreadable_approval_is_SND_PROFILE_006()
    {
        SkipWhenPrivileged();
        _ = await _files.Approvals.Save(SliceHarness.Profile("5s"), Token);
        File.SetUnixFileMode(Path.Combine(_files.Settings.ApprovalsDirectory, "latest"), UnixFileMode.None);

        var latest = await _files.Approvals.Latest(Token);

        Assert.Equal(SindriErrors.ApprovalsUnavailable.Code, latest.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_save_into_a_read_only_directory_is_SND_PROFILE_006()
    {
        SkipWhenPrivileged();
        _ = Directory.CreateDirectory(_files.Settings.ApprovalsDirectory);
        File.SetUnixFileMode(_files.Settings.ApprovalsDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var saved = await _files.Approvals.Save(SliceHarness.Profile("5s"), Token);

        Assert.Equal(SindriErrors.ApprovalsUnavailable.Code, saved.Error.Code);
    }

    /// <summary>root reads and writes through any mode bits, so a chmod can't make the disk refuse.</summary>
    private static void SkipWhenPrivileged() =>
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "a privileged process ignores the permissions these tests take away");
}
