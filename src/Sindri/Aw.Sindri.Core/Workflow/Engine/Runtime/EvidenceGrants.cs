namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The 'ui-evidence' verbs each evidence agent Step may run (cutover spec §3.1): one grant per verb, never 'Bash(ui-evidence:*)', so a Step gets only the verbs its brief names
/// and 'gh-publish' reaches the publish Step alone. A prefix grant allows any flag of its verb: '--allow-writes' and a non-loopback '--host' are held by the binary's own host guard.
/// </summary>
internal static class EvidenceGrants
{
    /// <summary>The publish Step's block id: the only Step that may push evidence to a pull request.</summary>
    internal const string PublishStep = "step.evidence.publish";

    private static readonly Dictionary<string, string[]> _verbs = new(StringComparer.Ordinal)
    {
        ["step.evidence.ui-plan"] = ["doctor", "provenance"],
        ["step.evidence.api-plan"] = ["doctor"],
        ["step.evidence.db-plan"] = ["doctor"],
        ["step.evidence.ui-run"] = ["doctor", "run", "parity", "provenance"],
        ["step.evidence.api-run"] = ["doctor", "api-run"],
        ["step.evidence.db-run"] = ["doctor", "db-run"],
        [PublishStep] = ["publish-plan", "parity-plan", "parity-comment", "parity-put", "parity-stale", "gh-publish"],
    };

    /// <summary>The verbs a Step may run; none for a Step that is not an evidence agent Step.</summary>
    /// <param name="block">The Step's block id.</param>
    /// <returns>The verbs, in grant order.</returns>
    internal static IReadOnlyList<string> VerbsFor(string block) => _verbs.TryGetValue(block, out var verbs) ? verbs : [];

    /// <summary>The permission grants for a Step: 'Bash(ui-evidence &lt;verb&gt;:*)' per verb.</summary>
    /// <param name="block">The Step's block id.</param>
    /// <returns>The grants; none for a Step that is not an evidence agent Step.</returns>
    internal static IReadOnlyList<string> For(string block) => [.. VerbsFor(block).Select(static v => $"Bash(ui-evidence {v}:*)")];
}
