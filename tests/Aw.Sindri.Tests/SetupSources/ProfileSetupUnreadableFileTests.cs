using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SetupSources;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Profile;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>#3833: the setup list over a real profile file the process can't read, and over a directory at the file's path: the profile step fails, never "no profile".</summary>
[Lane(TestLane.Integration)]
public sealed class ProfileSetupUnreadableFileTests : IDisposable
{
    private readonly ProfileFiles _files = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProfileSetupReader Reader() =>
        new(_files.Source, new RepoGitCache(new FakeRepoInspector(), _clock), new SettingsProfileFile(_files.Settings), new InMemoryWorkflowStore(_clock), new InMemoryWorkflowRunStore(_clock));

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    public async Task A_profile_file_with_no_read_permission_is_the_presence_steps_failure()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "root ignores file permissions");
        _files.Write("schemaVersion: 1\nuser: thor\n");
        File.SetUnixFileMode(_files.Settings.ProfilePath, UnixFileMode.None);
        try
        {
            var presence = await Reader().Presence(Token);

            Assert.Equal((SindriErrors.ProfileInvalid.Code, SindriErrors.ProfileUnreadableFix), (presence.Error.Code, presence.Error.Message));
        }
        finally
        {
            File.SetUnixFileMode(_files.Settings.ProfilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task A_directory_at_the_profile_path_is_the_repos_steps_failure_not_zero_repos()
    {
        _ = Directory.CreateDirectory(_files.Settings.ProfilePath);

        var repos = await Reader().Repos(Token);

        Assert.Equal(SindriErrors.ProfileInvalid.Code, repos.Error.Code);
    }
}
