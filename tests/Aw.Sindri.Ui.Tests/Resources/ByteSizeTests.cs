using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Resources;

namespace Aw.Sindri.Ui.Tests.Resources;

/// <summary>The size words: B, KB, MB or GB, one decimal, no trailing ".0", rounded before the unit is picked.</summary>
[Lane(TestLane.Unit)]
public sealed class ByteSizeTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(2048L, "2 KB")]
    [InlineData(1_258_291L, "1.2 MB")]
    [InlineData(1_048_575L, "1 MB")]
    [InlineData(5_368_709_120L, "5 GB")]
    public void A_size_reads_with_one_decimal_and_no_trailing_zero(long bytes, string words) =>
        Assert.Equal(words, ByteSize.Format(bytes));
}
