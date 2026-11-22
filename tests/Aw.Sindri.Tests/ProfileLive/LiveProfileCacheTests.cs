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

    [Fact, Trait("Lane", "integration")]
    public async Task A_write_by_sindri_is_seen_even_when_the_last_write_time_and_length_do_not_change()
    {
        var path = _files.Settings.ProfilePath;
        _files.Write("schemaVersion: 1\nuser: me\nrepos: []\ncontext:\n  models:\n    labeling:\n      pick: off\n");
        var live = new LiveApprovedProfile(_files.Source, path);
        var before = (await live.Latest(Token)).Value!.Hash;
        var stamp = File.GetLastWriteTimeUtc(path);
        var length = new FileInfo(path).Length;

        var writer = new YamlProfileKeyWriter(_files.Settings, live);
        var edit = writer.Plan("context.models.labeling.pick", "jev").Value;   // same length as "off"
        Assert.True(writer.Save(edit).IsSuccess);
        File.SetLastWriteTimeUtc(path, stamp);   // one clock tick: nothing but the invalidate tells the cache

        Assert.Equal(length, new FileInfo(path).Length);
        var after = (await live.Latest(Token)).Value!.Hash;

        Assert.NotEqual(before, after);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_chmod_is_seen_on_the_next_read()
    {
        if (OperatingSystem.IsWindows())
        {
            return;   // no Unix file mode to change
        }

        var path = _files.Settings.ProfilePath;
        var counting = new CountingSource(_files.Source);
        var live = new LiveApprovedProfile(counting, path);
        var mode = File.GetUnixFileMode(path);
        _ = await live.Latest(Token);

        File.SetUnixFileMode(path, UnixFileMode.None);   // #3429: chmod 000 changes neither the last write time nor the length
        try
        {
            _ = await live.Latest(Token);
        }
        finally
        {
            File.SetUnixFileMode(path, mode);
        }

        Assert.Equal(2, counting.Loads);
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
