using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Sizes, percentages and durations of the profile's sandbox section.</summary>
public sealed class SandboxUnitsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("1KiB", 1024L)]
    [InlineData("256MiB", 268_435_456L)]
    [InlineData("12GiB", 12_884_901_888L)]
    [InlineData("2TiB", 2_199_023_255_552L)]
    public void Sizes_parse_to_bytes(string text, long bytes) => Assert.Equal(bytes, SandboxUnits.ParseSize(text));

    [Theory, Trait("Lane", "unit")]
    [InlineData(12_884_901_888L, "12GiB")]
    [InlineData(1_610_612_736L, "1536MiB")]
    [InlineData(268_435_456L, "256MiB")]
    public void Sizes_format_as_whole_GiB_else_whole_MiB(long bytes, string text) => Assert.Equal(text, SandboxUnits.FormatSize(bytes));

    [Fact, Trait("Lane", "unit")]
    public void A_percent_parses_to_its_number() => Assert.Equal(50, SandboxUnits.ParsePercent("50%"));

    [Theory, Trait("Lane", "unit")]
    [InlineData(70, "70m")]
    [InlineData(240, "4h")]
    [InlineData(250, "250m")]
    public void Durations_format_as_whole_hours_else_minutes(int minutes, string text) => Assert.Equal(text, SandboxUnits.FormatDuration(TimeSpan.FromMinutes(minutes)));
}
