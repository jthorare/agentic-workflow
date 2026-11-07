namespace Aw.Architecture.Tests.WriteKeyFixtures;

/// <summary>Holds the key in a field (an ARCH-LINEAR-001 fixture).</summary>
internal sealed class FieldReaderFixture(KeyFixture key)
{
    internal KeyFixture Key { get; } = key;
}
