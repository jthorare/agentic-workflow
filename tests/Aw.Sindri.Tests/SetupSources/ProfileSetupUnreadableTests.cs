using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SetupSources;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>#3341: a profile file that is on disk but can't be read is not "no profile" on the setup list; it is that step's failure, so the list never offers `profile init` over a file that is there.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileSetupUnreadableTests
{
    private readonly InMemoryProfiles _profiles = new();   // answers SND-PROFILE-001 by default, as the source does for an absent and for an unreadable file
    private readonly FakeRepoInspector _inspector = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProfileSetupReader Reader(IProfileFile file) => new(_profiles, new RepoGitCache(_inspector, _clock), file);

    [Fact]
    public async Task An_unreadable_file_is_the_presence_steps_failure_not_missing()
    {
        var presence = await Reader(new OnDisk(exists: true)).Presence(Token);

        Assert.True(presence.IsError);
        Assert.Equal(SindriErrors.ProfileInvalid.Code, presence.Error.Code);
    }

    [Fact]
    public async Task An_unreadable_file_is_the_repos_steps_failure_not_zero_repos()
    {
        var repos = await Reader(new OnDisk(exists: true)).Repos(Token);

        Assert.True(repos.IsError);
        Assert.Equal(SindriErrors.ProfileInvalid.Code, repos.Error.Code);
    }

    [Fact]
    public async Task An_absent_file_stays_missing_with_no_repos()
    {
        var reader = Reader(new OnDisk(exists: false));

        Assert.Equal(ProfilePresence.Missing, (await reader.Presence(Token)).Value.Presence);
        Assert.Equal(0, (await reader.Repos(Token)).Value.Count);
    }

    private sealed class OnDisk(bool exists) : IProfileFile
    {
        public bool Exists() => exists;
    }
}
