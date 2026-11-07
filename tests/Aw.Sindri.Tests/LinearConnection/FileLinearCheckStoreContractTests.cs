using Aw.Kernel.Testing;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.LinearConnection;

[Lane(TestLane.Integration)]
public sealed class FileLinearCheckStoreContractTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private FileLinearCheckStore Open() => new(new SettingsSindriHome(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"))));

    [Fact]
    public Task Read_before_any_record_is_two_nulls() => LinearCheckStoreContract.ReadBeforeAnyRecordIsTwoNulls(Open());

    [Fact]
    public Task Each_side_keeps_its_own_check() => LinearCheckStoreContract.EachSideKeepsItsOwnCheck(Open());

    [Fact]
    public Task A_later_record_replaces_the_earlier() => LinearCheckStoreContract.ALaterRecordReplacesTheEarlier(Open());

    [Fact]
    public Task Clearing_the_write_check_leaves_the_read_check() => LinearCheckStoreContract.ClearingTheWriteCheckLeavesTheReadCheck(Open());

    [Fact]
    public async Task A_check_survives_a_restart_which_is_a_new_store_over_the_same_home()
    {
        var check = new LinearStoredCheck("keychain:sindri-index/linear-write", "resolves", DateTimeOffset.UnixEpoch.AddDays(1));
        _ = await Open().RecordWrite(check, Token);

        Assert.Equal(check, (await Open().Read(Token)).Value.Write);
    }

    [Fact]
    public async Task A_file_that_does_not_parse_reads_as_never_checked()
    {
        _ = await Open().RecordRead(new LinearStoredCheck("keychain:a/b", "resolves", DateTimeOffset.UnixEpoch), Token);
        foreach (var file in Directory.EnumerateFiles(_home.Path, "linear-checks.json", SearchOption.AllDirectories))
        {
            await File.WriteAllTextAsync(file, "{ not json", Token);
        }

        Assert.Equal(new LinearStoredChecks(Read: null, Write: null), (await Open().Read(Token)).Value);
    }
}
