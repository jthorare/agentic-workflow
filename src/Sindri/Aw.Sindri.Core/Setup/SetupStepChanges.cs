namespace Aw.Sindri.Core.Setup;

/// <summary>When a step's derived state is a change worth an event (setup-once spec §7.5): when it differs from the last event of that step, or the step has none yet.</summary>
public static class SetupStepChanges
{
    /// <summary>The changes to record now, in step order.</summary>
    /// <param name="steps">The derived steps.</param>
    /// <param name="recorded">The last recorded change of each step that has one.</param>
    /// <returns>Zero to six changes.</returns>
    public static IReadOnlyList<StepChange> Plan(IReadOnlyList<SetupStep> steps, IReadOnlyDictionary<SetupStepId, RecordedStep> recorded)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(recorded);
        List<StepChange> changes = [];
        foreach (var step in steps)
        {
            var code = step.Error?.Code;
            if (!recorded.TryGetValue(step.Id, out var last))
            {
                changes.Add(new StepChange(step.Id, step.State, code, 0));
            }
            else if (last.State != step.State || !string.Equals(last.Code, code, StringComparison.Ordinal))
            {
                changes.Add(new StepChange(step.Id, step.State, code, last.Count));
            }
        }

        return changes;
    }

    /// <summary>The idempotency key: 'setup.step.changed:&lt;id&gt;:&lt;state&gt;:&lt;n&gt;'.</summary>
    /// <param name="id">The step.</param>
    /// <param name="state">The state it moved to.</param>
    /// <param name="count">The step's earlier change events.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(SetupStepId id, SetupState state, int count) => $"setup.step.changed:{Word(id)}:{Word(state)}:{count.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>A state in the event's fixed words.</summary>
    /// <param name="state">The state.</param>
    /// <returns>'todo', 'running', 'needs-you', 'done', 'failed' or 'skipped'.</returns>
    public static string Word(SetupState state) => state switch
    {
        SetupState.Todo => "todo",
        SetupState.Running => "running",
        SetupState.NeedsYou => "needs-you",
        SetupState.Done => "done",
        SetupState.Failed => "failed",
        SetupState.Skipped => "skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    /// <summary>A step in the event's fixed words.</summary>
    /// <param name="id">The step.</param>
    /// <returns>'repo', 'profile', 'recipes', 'mcp', 'map' or 'onyx'.</returns>
    public static string Word(SetupStepId id) => id switch
    {
        SetupStepId.Repo => "repo",
        SetupStepId.Profile => "profile",
        SetupStepId.Recipes => "recipes",
        SetupStepId.Mcp => "mcp",
        SetupStepId.Map => "map",
        SetupStepId.Onyx => "onyx",
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    /// <summary>Reads a state's fixed word back.</summary>
    /// <param name="word">The word.</param>
    /// <param name="state">The state, when the word is one.</param>
    /// <returns>Whether it is.</returns>
    public static bool TryState(string word, out SetupState state)
    {
        foreach (var candidate in Enum.GetValues<SetupState>())
        {
            if (string.Equals(Word(candidate), word, StringComparison.Ordinal))
            {
                state = candidate;
                return true;
            }
        }

        state = default;
        return false;
    }

    /// <summary>Reads a step's fixed word back.</summary>
    /// <param name="word">The word.</param>
    /// <param name="id">The step, when the word is one.</param>
    /// <returns>Whether it is.</returns>
    public static bool TryStep(string word, out SetupStepId id)
    {
        foreach (var candidate in Enum.GetValues<SetupStepId>())
        {
            if (string.Equals(Word(candidate), word, StringComparison.Ordinal))
            {
                id = candidate;
                return true;
            }
        }

        id = default;
        return false;
    }
}
