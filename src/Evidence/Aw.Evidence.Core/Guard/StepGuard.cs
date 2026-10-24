using System.Globalization;
using Aw.Evidence.Core.Scripts;
using Aw.Kernel;

namespace Aw.Evidence.Core.Guard;

/// <summary>
/// Refuses steps before a browser starts: a goto that leaves the host (always), and click or goto steps whose words
/// read like a write (save, delete, …) unless writes are allowed. A run guards writes only on a preview host (a local
/// stack is the developer's own); a parity run is read-only on every host.
/// </summary>
public static class StepGuard
{
    /// <summary>Checks a run script's steps.</summary>
    /// <param name="steps">The steps.</param>
    /// <param name="host">The resolved host.</param>
    /// <param name="allowWrites">Whether --allow-writes was given.</param>
    /// <returns>Success, or AW-EVIDENCE-005 naming each refused step.</returns>
    public static Result<Unit> ForRun(IReadOnlyList<ScriptStep> steps, ResolvedHost host, bool allowWrites) =>
        Check(steps, refuseWrites: RunRefusesWrites(host, allowWrites));

    /// <summary>Whether a run refuses writes: on a preview host, unless --allow-writes was given.</summary>
    /// <param name="host">The resolved host.</param>
    /// <param name="allowWrites">Whether --allow-writes was given.</param>
    /// <returns>Whether write-looking steps (and repair candidates) are refused.</returns>
    public static bool RunRefusesWrites(ResolvedHost host, bool allowWrites) => host.Kind == HostKind.Preview && !allowWrites;

    /// <summary>Whether a control's label (a test id, accessible name or text) reads like a write, as a scripted click target would.</summary>
    /// <param name="label">The label, or null.</param>
    /// <returns>Whether it reads like a write.</returns>
    public static bool LabelLooksLikeWrite(string? label) => WriteWords.LabelLooksLikeWrite(label);

    /// <summary>Checks a parity manifest's frame steps (read-only on every host).</summary>
    /// <param name="steps">Every frame's steps, in order.</param>
    /// <param name="allowWrites">Whether --allow-writes was given.</param>
    /// <returns>Success, or AW-EVIDENCE-005 naming each refused step.</returns>
    public static Result<Unit> ForParity(IReadOnlyList<ScriptStep> steps, bool allowWrites) => Check(steps, refuseWrites: !allowWrites);

    /// <summary>A goto target is a path on the vetted host: one leading '/', no scheme-relative '//' and no backslash.</summary>
    /// <param name="target">The goto target.</param>
    /// <returns>Whether it stays on the host.</returns>
    public static bool IsSameOriginPath(string target) =>
        target.StartsWith('/') && !target.StartsWith("//", StringComparison.Ordinal) && !target.Contains('\\', StringComparison.Ordinal);

    private static Result<Unit> Check(IReadOnlyList<ScriptStep> steps, bool refuseWrites)
    {
        var offOrigin = Numbered(steps, static s => s.Action == StepAction.Goto && !IsSameOriginPath(s.Target));
        if (offOrigin.Count > 0)
        {
            return EvidenceErrors.StepsRefused.With($"goto targets must be paths on the host (start with a single \"/\"): {string.Join(", ", offOrigin)}");
        }

        var writes = refuseWrites ? Numbered(steps, WriteWords.LooksLikeWrite) : [];
        return writes.Count == 0
            ? Results.Ok()
            : EvidenceErrors.StepsRefused.With($"refusing write-looking steps without --allow-writes: {string.Join(", ", writes)}");
    }

    private static List<string> Numbered(IReadOnlyList<ScriptStep> steps, Func<ScriptStep, bool> refused) =>
        [.. steps.Select(static (s, i) => (Step: s, Number: i + 1))
            .Where(x => refused(x.Step))
            .Select(static x => string.Create(CultureInfo.InvariantCulture, $"#{x.Number} {x.Step.ActionName} {x.Step.Subject}"))];
}
