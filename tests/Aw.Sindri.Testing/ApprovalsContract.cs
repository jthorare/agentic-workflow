using Aw.Sindri.Core.Profile;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IApprovals"/> honours: nothing approved at first, then the latest save wins.</summary>
public abstract class ApprovalsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>An empty approval store.</summary>
    /// <returns>The store.</returns>
    protected abstract IApprovals Create();

    /// <summary>Two checked profiles with different hashes.</summary>
    /// <returns>The profiles.</returns>
    protected abstract (LoadedProfile First, LoadedProfile Second) Profiles();

    /// <summary>A fresh store has no approval; after two saves the second is the latest, field for field.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_latest_save_is_the_approval()
    {
        var approvals = Create();
        Expect.True((await approvals.Latest(Token)).Value is null);
        var (first, second) = Profiles();

        _ = await approvals.Save(first, Token);
        _ = await approvals.Save(second, Token);
        var latest = (await approvals.Latest(Token)).Value;

        Expect.Equal(second.Hash, latest?.Hash);
        Expect.Equal(second.Profile.User, latest?.Profile.User);
    }
}
