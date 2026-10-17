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
        "Aw.Sindri.Ui",
        "Aw.Sindri.Ui.Desktop",
        "Aw.Context.Core",
        "Aw.Context.Features",
        "Aw.Context.Infrastructure",
        "Aw.Sindri.Workflows",
        "Aw.Meta.Core",
        "Aw.Meta.Features",
        "Aw.Meta.Infrastructure",
        "Aw.Meta.Host",
        "Aw.Forge.Core",
        "Aw.Forge.Infrastructure",
        "Aw.Forge.Host",
    ];

    /// <summary>
    /// The registered projects that are deliberately unshaped (no Core/Features/Infrastructure/Host layers): the Sindri
    /// UI (spec §4a "UI") and its platform heads. Only the global rules cover them. Declared, not inferred, so a misnamed
    /// project inside a shaped root (Aw.Sindri.Engine) still fails ARCH-PLACE-001; ARCH-REG-003 fails a registered
    /// project that is neither tier 0, a layer project nor listed here.
    /// </summary>
    public static IReadOnlyList<string> Unshaped { get; } = ["Aw.Sindri.Ui", "Aw.Sindri.Ui.Desktop"];
}
