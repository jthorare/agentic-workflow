using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IProfileRepoWriter"/> honours, over an empty profile: the first repo creates it, another is appended, the same again changes nothing, and a name or folder another entry has is SND-SETUP-002.</summary>
public abstract class ProfileRepoWriterContract
{
    private static readonly string[] _effects = ["effect.claim.ref"];

    /// <summary>A writer over a box with no profile.</summary>
    /// <returns>The writer.</returns>
    protected abstract IProfileRepoWriter Create();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The first repo creates the profile, the second joins it.</summary>
    [Fact]
    public async Task The_first_repo_creates_the_profile_and_the_next_is_appended()
    {
        var writer = Create();

        Expect.Equal(RepoAppended.Created, (await writer.Append(new RepoEntry("demo", "/work/demo"), _effects, Token)).Value);
        Expect.Equal(RepoAppended.Appended, (await writer.Append(new RepoEntry("other", "/work/other"), _effects, Token)).Value);
    }

    /// <summary>The same entry twice changes nothing the second time.</summary>
    [Fact]
    public async Task The_same_entry_twice_is_already_there()
    {
        var writer = Create();
        _ = await writer.Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Expect.Equal(RepoAppended.AlreadyThere, (await writer.Append(new RepoEntry("demo", "/work/demo"), _effects, Token)).Value);
    }

    /// <summary>A name or a folder another entry has is SND-SETUP-002.</summary>
    [Fact]
    public async Task A_taken_name_or_folder_is_refused()
    {
        var writer = Create();
        _ = await writer.Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Expect.Equal("SND-SETUP-002", (await writer.Append(new RepoEntry("demo", "/work/else"), _effects, Token)).Error.Code);
        Expect.Equal("SND-SETUP-002", (await writer.Append(new RepoEntry("else", "/work/demo"), _effects, Token)).Error.Code);
    }
}
