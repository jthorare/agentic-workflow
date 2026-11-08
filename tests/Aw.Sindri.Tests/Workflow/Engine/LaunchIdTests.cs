using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>A launch id is 'LCH-' and 32 lower-case hex digits, as the ids the launches view (UI spec E-1) hands out.</summary>
public sealed class LaunchIdTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_launch_id_is_LCH_and_32_lower_case_hex_digits() =>
        Assert.Equal("LCH-0123456789abcdef0123456789abcdef", LaunchId.For(new Guid(0x01234567, 0x89ab, 0xcdef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef)).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("LCH-1")]
    [InlineData("WFI-0123456789abcdef0123456789abcdef")]
    [InlineData("LCH-0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("LCH-0123456789abcdef0123456789abcdef0")]
    public void Anything_else_is_not_a_launch_id(string text) => Assert.False(LaunchId.TryFrom(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void A_launch_id_round_trips_through_JSON_as_a_plain_string()
    {
        var id = LaunchId.From("LCH-0123456789abcdef0123456789abcdef");

        Assert.Equal("\"LCH-0123456789abcdef0123456789abcdef\"", System.Text.Json.JsonSerializer.Serialize(id));
        Assert.Equal(id, System.Text.Json.JsonSerializer.Deserialize<LaunchId>("\"LCH-0123456789abcdef0123456789abcdef\""));
    }
}
