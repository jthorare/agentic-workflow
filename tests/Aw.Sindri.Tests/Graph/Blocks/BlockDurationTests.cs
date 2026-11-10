using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class BlockDurationTests
{
    [Theory]
    [InlineData("999999d")]
    [InlineData("999999d999999d999999d999999d999999d999999d999999d999999d999999d999999d999999d999999d999999d")]
    public void A_duration_too_long_to_add_to_a_date_is_an_error_not_an_exception(string text)
    {
        var parsed = BlockDuration.Parse(text);

        Assert.True(parsed.IsError);
    }

    [Fact]
    public void A_run_of_parts_adds_up() =>
        Assert.Equal(TimeSpan.FromMinutes(90), BlockDuration.Parse("1h30m").Value);
}
