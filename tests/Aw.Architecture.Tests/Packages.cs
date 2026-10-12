namespace Aw.Architecture.Tests;

/// <summary>
/// The registry of every Aw.* production project the architecture rules cover (spec §4b.1). One line per project (its
/// project name; a Host that renames its binary with &lt;AssemblyName&gt; is still listed as Aw.&lt;Pkg&gt;.Host),
/// plus one ProjectReference per project in Aw.Architecture.Tests.csproj. /new-package appends both for each project
/// it generates (Core, Features, Infrastructure, Host, or Core and App in the small shape). RegistryTests fails when a
/// production project in AgenticWorkflow.slnx is missing here (ARCH-REG-001) or a line here doesn't load (ARCH-REG-002).
/// </summary>
internal static class Packages
{
    /// <summary>Project names, one per production project.</summary>
    public static IReadOnlyList<string> Production { get; } =
    [
        "Aw.Kernel",
        "Aw.Kernel.Data",
        "Aw.Kernel.Telemetry",
        "Aw.Packets.Core",
        "Aw.Packets.App",
        "Aw.Readiness.Core",
        "Aw.Readiness.App",
        "Aw.Redaction",
        "Aw.Judge.Core",
        "Aw.Judge.Features",
        "Aw.Judge.Infrastructure",
        "Aw.Judge.Host",
        "Aw.Evidence.Core",
        "Aw.Evidence.Features",
        "Aw.Evidence.Infrastructure",
        "Aw.Evidence.Host",
        "Aw.BugFix.Core",
        "Aw.BugFix.Features",
        "Aw.BugFix.Infrastructure",
        "Aw.BugFix.Host",
        "Aw.Sindri.Core",
        "Aw.Sindri.Features",
        "Aw.Sindri.Infrastructure",
        "Aw.Sindri.Host",
    ];
}
