using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>Builds the argv an entry launches with (launcher spec section 3, 'BuildArgv'). Pure.</summary>
public static class ArgvBuilder
{
    /// <summary>The seed: a fixed template plus the item id, never item text (UI 6.2, API C-26).</summary>
    /// <param name="item">The item.</param>
    /// <returns>The seed prompt.</returns>
    public static string Seed(ItemId item) =>
        $"Sindri item {item.Value}. Read it with the sindri_item tool before you start; you are taking it over from Sindri.";

    /// <summary>The entry's argv for <paramref name="mode"/>.</summary>
    /// <param name="entry">The catalog entry.</param>
    /// <param name="mode">Open or Resume (Attach starts no process and is refused).</param>
    /// <param name="inputs">The expansion inputs.</param>
    /// <returns>
    /// The arguments; an unattended Open (a Step) ends with the model and policy form arguments (SND-LAUNCH-013 when the entry's form can't express them, and nothing starts). SND-LAUNCH-006 for Resume on an entry without a resume template; SND-LAUNCH-015 when the entry's pattern times out on the session id (a Resume with a timed-out id is refused, not opened fresh); SND-REQ-001 for a relative repo path, Attach, or a session id the
    /// entry's pattern rejects where the template needs it. A Resume with no usable session id falls back to the open argv.
    /// </returns>
    public static Result<IReadOnlyList<string>> Build(AgentEntry entry, LaunchMode mode, ArgvInputs inputs) => Build(entry, mode, inputs, SessionIdRules.DefaultTimeout);

    internal static Result<IReadOnlyList<string>> Build(AgentEntry entry, LaunchMode mode, ArgvInputs inputs, TimeSpan patternTimeout)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(inputs);
        if (Refusal(entry, mode, inputs) is { } refused)
        {
            return refused;
        }

        var outcome = SessionIdRules.Check(entry, inputs.SessionId, patternTimeout);
        var resume = mode == LaunchMode.Resume && outcome == PatternOutcome.Matched;
        if (TimedOut(entry, mode, inputs, outcome) is { } timedOut)
        {
            return timedOut;
        }

        if (!resume && BadPreChosen(entry, inputs, outcome))
        {
            return SindriErrors.RequestInvalid.With($"the session id doesn't match {entry.Id.Value}'s sessionIdPattern (fix: pass a fresh id)");
        }

        var values = new PlaceholderValues(entry.Bin, inputs.RepoPath, inputs.SessionName, inputs.SessionName, inputs.Seed, inputs.SessionId, inputs.McpConfigPath);
        var argv = Placeholders.Expand(resume ? entry.Resume! : entry.Open, values); // why: a null Resume returned above
        return argv.IsError || mode != LaunchMode.Open || inputs.Step is not { } step ? argv : WithForms(argv.Value, entry, step, inputs.PolicyFilePath, inputs.RepoPath);
    }

    private static Result<IReadOnlyList<string>> WithForms(IReadOnlyList<string> argv, AgentEntry entry, StepArgs step, string? policyFilePath, string workDir)
    {
        var forms = StepForms.Arguments(entry, step, policyFilePath, workDir);
        return forms.IsError ? forms.Error : Results.Success<IReadOnlyList<string>>([.. argv, .. forms.Value]);
    }

    private static Error? Refusal(AgentEntry entry, LaunchMode mode, ArgvInputs inputs)
    {
        if (!Placeholders.IsAbsolute(inputs.RepoPath))
        {
            return SindriErrors.RequestInvalid.With("the repo path isn't absolute (fix: pass the repo's absolute path)");
        }

        if (mode == LaunchMode.Attach)
        {
            return SindriErrors.RequestInvalid.With("attach starts no process, so it has no argv");
        }

        return mode == LaunchMode.Resume && entry.Resume is null ? SindriErrors.LaunchNoSession.With($"{entry.Id.Value} has no resume command") : null;
    }

    private static Error? TimedOut(AgentEntry entry, LaunchMode mode, ArgvInputs inputs, PatternOutcome outcome) =>
        outcome == PatternOutcome.TimedOut && (mode == LaunchMode.Resume || BadPreChosen(entry, inputs, outcome))
            ? SindriErrors.LaunchSessionIdTimedOut.With($"{entry.Id.Value}'s sessionIdPattern timed out on the session id, so it is refused unjudged, not as a mismatch (fix: simplify the pattern, or retry when the box is less loaded)")
            : null;

    private static bool BadPreChosen(AgentEntry entry, ArgvInputs inputs, PatternOutcome outcome) =>
        entry.SessionIdMode == SessionIdMode.PreChosenUuid && inputs.SessionId is not null && outcome != PatternOutcome.Matched;
}
