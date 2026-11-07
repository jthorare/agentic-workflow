namespace Aw.Architecture.Tests.WriteKeyFixtures;

/// <summary>Never touches the key (an ARCH-LINEAR-001 fixture).</summary>
internal static class UnrelatedFixture
{
    internal static int Count(IEnumerable<string> names) => names.Count();
}
