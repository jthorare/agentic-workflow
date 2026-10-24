using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Line;

/// <summary>
/// The checks of 'sindri line doctor' (line spec Appendix D, LN-WF9): the approved profile's line keys for one repo and what the checkout and the environment hold. Pure: the facts are read by the caller.
/// The parallel-versus-cap check is the profile's own cross-key rule (<c language="text">LineProfileRules</c>), repeated here because the cap can drop after the profile was approved.
/// </summary>
public static class LineDoctorRules
{
    private const string DefaultCredentialVariable = "AW_FORGE_TOKEN";
    private static readonly string[] _builtIns = ["plain", "design-five", "code-review"];

    /// <summary>The variable the forge credential comes from: AW_FORGE_TOKEN by default, the NAME of 'env:NAME', or null for a 'command:' credential.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The variable, or null.</returns>
    public static string? CredentialVariable(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var credential = profile.Workflow?.Forge?.Credential;
        return string.IsNullOrEmpty(credential) ? DefaultCredentialVariable
            : credential.StartsWith("env:", StringComparison.Ordinal) ? credential["env:".Length..]
            : null;
    }

    /// <summary>Checks one repo.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">The repo's name in the profile.</param>
    /// <param name="box">The box's limits now.</param>
    /// <param name="facts">What the checkout and the environment hold.</param>
    /// <returns>One row per check, in order.</returns>
    public static IReadOnlyList<DoctorCheck> Check(SindriProfile profile, string repo, BoxLimits box, LineSetupFacts facts)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(facts);
        var workflow = profile.Workflow ?? new WorkflowSettings();
        return [.. Settings(workflow, box), .. Checkout(facts), .. Packs(workflow, repo), .. Forge(workflow.Forge, facts)];
    }

    /// <summary>SND-LINE-004 naming every row that is not ok, or null when the setup is complete.</summary>
    /// <param name="checks">The rows.</param>
    /// <returns>The error, or null.</returns>
    public static Error? Missing(IReadOnlyList<DoctorCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        var missing = checks.Where(static c => c.State == CheckState.Fail).ToList();
        return missing.Count == 0
            ? null
            : LineErrors.SetupMissing.With(string.Join("; ", missing.Select(static c => $"{c.Name}: {c.Detail}" + (c.Fix is null ? string.Empty : $" (fix: {c.Fix})"))));
    }

    private static DoctorCheck[] Settings(WorkflowSettings workflow, BoxLimits box) => [Build(workflow.Build), Parallel(workflow.Line, box)];

    private static DoctorCheck[] Checkout(LineSetupFacts facts) => [TestPaths(facts), Evidence(facts)];

    private static DoctorCheck[] Forge(ForgeSettings? forge, LineSetupFacts facts) => [Credential(forge, facts), Babysitter(forge)];

    private static DoctorCheck[] Packs(WorkflowSettings workflow, string repo)
    {
        var packs = workflow.Packs ?? new PackSettings();
        var lenses = packs.Lenses ?? new LensPackSettings();
        return [Pack("style", repo, packs.Style, packs.Pins), Pack("lenses.design", repo, lenses.Design, packs.Pins), Pack("lenses.review", repo, lenses.Review, packs.Pins)];
    }

    private static DoctorCheck Build(BuildSettings? build) => build?.Commands is { Count: > 0 } commands
        ? new("workflow.build.commands", CheckState.Ok, string.Create(CultureInfo.InvariantCulture, $"{commands.Count} command(s)"), Fix: null)
        : new("workflow.build.commands", CheckState.Fail, "no commands check a phase's change", "add workflow.build.commands to the profile (for example the build and the test command), then sindri profile approve");

    private static DoctorCheck Parallel(LineSettings? line, BoxLimits box) => LineProfileRules.ParallelCap(line?.Parallel, box) is { } error
        ? new("workflow.line.parallel", CheckState.Fail, error.Message, "lower workflow.line.parallel and approve the profile, or raise $AW_STATE_DIR/heavy-slots-cap")
        : new("workflow.line.parallel", CheckState.Ok, string.Create(CultureInfo.InvariantCulture, $"{line?.Parallel?.ToString(CultureInfo.InvariantCulture) ?? "default"} within the box's {box.HeavySlots} heavy slot(s)"), Fix: null);

    private static DoctorCheck TestPaths(LineSetupFacts facts) => facts.TestFiles
        ? new("test paths", CheckState.Ok, "the checkout has files the test-path convention names", Fix: null)
        : new("test paths", CheckState.Warn, "no file in the checkout follows the test-path convention (tests/, *.test.*, *Tests.cs …), so new tests would not be allowed by the scope gate", "put tests under a tests/ directory or name them *.test.* / *Tests.*");

    private static DoctorCheck Evidence(LineSetupFacts facts) => facts.EvidenceConfig
        ? new("evidence route", CheckState.Ok, ".agentic-workflow/evidence.json is present", Fix: null)
        : new("evidence route", CheckState.Warn, "no .agentic-workflow/evidence.json: the evidence route has no targets to check", "add .agentic-workflow/evidence.json to the repo");

    // A pack that is not built in must have a pin line in the profile (SND-PACK-002), under '<kind>/<id>' where the kind is style or lenses.
    private static DoctorCheck Pack(string kind, string repo, PackChoice? choice, IReadOnlyDictionary<string, string>? pins)
    {
        var name = "workflow.packs." + kind;
        var id = PackId(choice, repo);
        if (id is null || Array.IndexOf(_builtIns, id) >= 0)
        {
            return new(name, CheckState.Ok, id is null ? "built-in default" : $"{id} (built in)", Fix: null);
        }

        var pin = (kind == "style" ? "style/" : "lenses/") + id;
        return pins is not null && pins.ContainsKey(pin)
            ? new(name, CheckState.Ok, $"{id}, pinned", Fix: null)
            : new(name, CheckState.Fail, $"{id} has no pin in workflow.packs.pins", $"run sindri pack check on the pack, then add the pin line it prints under workflow.packs.pins ({pin})");
    }

    private static string? PackId(PackChoice? choice, string repo) => choice?.Repos is { } repos && repos.TryGetValue(repo, out var own) ? own : choice?.Default;

    private static DoctorCheck Credential(ForgeSettings? forge, LineSetupFacts facts) => (forge?.Adapter, facts.CredentialSet) switch
    {
        (null, _) => new("forge credential", CheckState.Fail, "workflow.forge.adapter is not set, so effects have no forge", "set workflow.forge.adapter (github) and approve the profile"),
        (_, false) => new("forge credential", CheckState.Fail, "the credential's environment variable is not set", "export the variable named by workflow.forge.credential (default AW_FORGE_TOKEN) in the shell that starts sindri serve"),
        (_, null) => new("forge credential", CheckState.Ok, "a command credential, run per call", Fix: null),
        _ => new("forge credential", CheckState.Ok, "the credential's variable is set", Fix: null),
    };

    private static DoctorCheck Babysitter(ForgeSettings? forge) => forge?.Adapter is null
        ? new("babysitter CI read", CheckState.Fail, "the PR babysitter reads CI through the forge adapter, and none is set", "set workflow.forge.adapter and approve the profile")
        : new("babysitter CI read", CheckState.Ok, $"CI is read through the {forge.Adapter} forge adapter", Fix: null);
}
