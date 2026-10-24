using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>A callers or callees walk goes 1 to 8 levels deep (raised from 3 per owner q93, 2026-10-23; each level is still cut at the per-level cap).</summary>
public sealed class CodeIndexInputsDepthTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void A_depth_from_1_to_8_is_taken(int depth) => Assert.Equal(depth, (int)CodeIndexInputs.Depth(depth).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(9)]
    public void A_depth_outside_1_to_8_is_refused_with_the_range(int depth)
        => Assert.Contains("1 to 8", CodeIndexInputs.Depth(depth).Error.Message, StringComparison.Ordinal);
}
