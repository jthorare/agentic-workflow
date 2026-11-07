namespace Aw.Architecture.Tests.WriteKeyFixtures;

/// <summary>Builds the key inside a closure and calls it, naming it nowhere in its signatures (an ARCH-LINEAR-001 fixture).</summary>
internal static class CallerFixture
{
    internal static Func<string> Secret() => static () => new KeyFixture().Secret();
}
