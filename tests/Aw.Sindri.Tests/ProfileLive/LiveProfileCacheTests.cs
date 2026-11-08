using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.ProfileLive;

/// <summary>Latest() parses the live file once while its last write time and length stay the same, and sees the next write (#2258 PR 1, review SHOULD 4).</summary>
public sealed class LiveProfileCacheTests : IDisposable
{
    private const string Profile = "schemaVersion: 1\nuser: me\nrepos: []\n";

    private readonly TempHome _home = new();
    private readonly ProfileFiles _files;

    public LiveProfileCacheTests()
    {
        _files = new ProfileFiles(_home.Path);
        _files.Write(Profile);
    }

    public void Dispose()
    {
        _files.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task Two_reads_with_no_change_parse_once_and_a_write_is_seen_on_the_next_read()
    {
        var counting = new CountingSource(_files.Source);
        var live = new LiveApprovedProfile(counting, _files.Settings.ProfilePath);

        var first = await live.Latest(Token);
        var second = await live.Latest(Token);

        Assert.Equal(1, counting.Loads);
        Assert.Equal(first.Value!.Hash, second.Value!.Hash);

        _files.Write(Profile + "# an edit that changes the length\n");
        var third = await live.Latest(Token);

        Assert.Equal(2, counting.Loads);
        Assert.True(third.IsSuccess);
    }

    private sealed class CountingSource(IProfileSource inner) : IProfileSource
    {
        public int Loads { get; private set; }

        public ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken)
        {
            Loads++;
            return inner.Load(cancellationToken);
        }
    }
}
