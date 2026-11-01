namespace Aw.Architecture.Tests.PinnedFixtures.Guarded;

/// <summary>Builds a bwrap argv with --die-with-parent (an ARCH-PROC-002 fixture read as IL, never run).</summary>
internal static class ArgvBuilderFixture
{
    internal static string[] Argv() => ["bwrap", "--die-with-parent"];
}
