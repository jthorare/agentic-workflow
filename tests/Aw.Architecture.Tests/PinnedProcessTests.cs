using Aw.Architecture.Tests.PinnedFixtures.Guarded;
using Aw.Architecture.Tests.PinnedFixtures.Other;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-PROC-002 (#1680): next to a bwrap --die-with-parent argv builder, processes start through PinnedProcess.</summary>
[Trait("Lane", "integration")]
public sealed class PinnedProcessTests
{
    private const string Sandbox = "Aw.Sindri.Infrastructure.Sandbox";

    private static readonly Dictionary<string, string> _exemptions = new(StringComparer.Ordinal)
    {
        [$"{Sandbox}.SandboxLoginRunner"] = "no bwrap: the host sign-in runs unsandboxed",
        [$"{Sandbox}.SandboxProcesses"] = "short-lived: every run is bounded at seconds, well under the 20 s thread retire",
    };

    [Fact]
    public void No_production_type_next_to_a_die_with_parent_argv_starts_a_process_itself() =>
        Verify.None(PinnedProcessRules.Check(Production.Assemblies.SelectMany(static a => a.GetTypes()), _exemptions));

    [Fact]
    public void A_stray_Process_Start_in_an_async_method_is_reported_and_pinned_or_unrelated_types_are_not() =>
        Verify.Exactly(PinnedProcessRules.Check(
            [typeof(ArgvBuilderFixture), typeof(StraySpawnerFixture), typeof(PinnedSpawnerFixture), typeof(OtherNamespaceFixture)], new Dictionary<string, string>(StringComparer.Ordinal)),
            PinnedProcessRules.Id, "StraySpawnerFixture calls Process.Start");
}
