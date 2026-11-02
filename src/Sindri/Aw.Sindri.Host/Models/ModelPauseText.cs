using Aw.Context.Core.Engine;

namespace Aw.Sindri.Host.Models;

/// <summary>The progress line of a job paused for a model harness (model routing spec §5.3 step 1 and §8.3), and the same causes as an error's text.</summary>
internal static class ModelPauseText
{
    /// <summary>"Paused: claude-code at its usage limit; codex failed; cursor not installed. Resume when one is back."</summary>
    /// <param name="harnesses">Each harness the call could have used, in its order, with why it didn't answer.</param>
    /// <returns>The line.</returns>
    internal static string Line(IReadOnlyList<(ModelHarness Harness, HarnessCause Cause)> harnesses) => "Paused: " + Clauses(harnesses) + ". Resume when one is back.";

    /// <summary>"claude-code at its usage limit; codex failed; cursor not installed".</summary>
    /// <param name="harnesses">Each harness the call could have used, in its order, with why it didn't answer.</param>
    /// <returns>One clause per harness.</returns>
    internal static string Clauses(IReadOnlyList<(ModelHarness Harness, HarnessCause Cause)> harnesses) =>
        string.Join("; ", harnesses.Select(static h => ModelHarnesses.Name(h.Harness) + " " + Words(h.Cause)));

    private static string Words(HarnessCause cause) => cause switch
    {
        HarnessCause.Limit => "at its usage limit",
        HarnessCause.Failed => "failed",
        HarnessCause.NotInstalled => "not installed",
        _ => throw new ArgumentOutOfRangeException(nameof(cause)),
    };
}
