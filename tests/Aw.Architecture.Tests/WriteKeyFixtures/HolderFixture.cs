namespace Aw.Architecture.Tests.WriteKeyFixtures;

/// <summary>The one type allowed to hold the key (an ARCH-LINEAR-001 fixture).</summary>
internal sealed class HolderFixture
{
    private readonly KeyFixture _key = new();

    internal string Use() => _key.Secret();
}
