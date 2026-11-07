namespace Aw.Architecture.Tests.WriteKeyFixtures;

/// <summary>Stands in for the write key type (an ARCH-LINEAR-001 fixture read as metadata and IL, never run).</summary>
internal sealed class KeyFixture
{
    private readonly string _secret = "secret";

    internal string Secret() => _secret;
}
