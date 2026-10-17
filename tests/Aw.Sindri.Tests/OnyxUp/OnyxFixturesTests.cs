using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxFixturesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_fixture_matches_its_lock_row()
    {
        var pins = new EmbeddedOnyxPins().Pins;

        Assert.All(pins.Files, pin => Assert.True(pin.Matches(OnyxFixtures.ReadBytes(pin.Path)), $"{pin.Path} differs from its lock row"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_set_of_fixture_files_is_the_locks_file_set()
    {
        var pins = new EmbeddedOnyxPins().Pins;

        var onDisk = Directory.EnumerateFiles(OnyxFixtures.Root, "*", SearchOption.AllDirectories)
            .Select(static f => Path.GetRelativePath(OnyxFixtures.Root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

        Assert.Equal(pins.Files.Select(static f => f.Path).Order(StringComparer.Ordinal), onDisk);
    }
}
