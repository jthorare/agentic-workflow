using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxPinsTests;

public sealed class EmbeddedOnyxPinsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Embedded_pins_parse_and_name_v4_9_0()
    {
        var pins = new EmbeddedOnyxPins().Pins;

        Assert.Equal("v4.9.0", pins.Version);
        Assert.NotEmpty(pins.Images);
        Assert.NotEmpty(pins.Files);
        foreach (var repository in pins.Images.Select(static i => i.Repository).Distinct(StringComparer.Ordinal))
        {
            Assert.Equal(["linux/amd64", "linux/arm64"], pins.Images.Where(i => i.Repository == repository).Select(static i => i.Platform).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        }
    }
}
