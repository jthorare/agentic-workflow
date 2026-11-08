using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearConnection;

[Lane(TestLane.Unit)]
public sealed class FakeLinearCheckStoreContractTests
{
    [Fact]
    public Task Read_before_any_record_is_two_nulls() => LinearCheckStoreContract.ReadBeforeAnyRecordIsTwoNulls(new FakeLinearCheckStore());

    [Fact]
    public Task Each_side_keeps_its_own_check() => LinearCheckStoreContract.EachSideKeepsItsOwnCheck(new FakeLinearCheckStore());

    [Fact]
    public Task A_later_record_replaces_the_earlier() => LinearCheckStoreContract.ALaterRecordReplacesTheEarlier(new FakeLinearCheckStore());

    [Fact]
    public Task Clearing_the_write_check_leaves_the_read_check() => LinearCheckStoreContract.ClearingTheWriteCheckLeavesTheReadCheck(new FakeLinearCheckStore());
}
