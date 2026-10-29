#pragma warning disable MA0182, MA0041, CA1822 // red commit: the stub is registered in the commit that implements it
using Aw.Context.Core.Engine;

namespace Aw.Sindri.Host.Models;

/// <summary>The progress line of a job paused for a model harness (model routing spec §5.3 step 1 and §8.3).</summary>
internal static class ModelPauseText
{
    /// <summary>"Paused: claude-code at its usage limit; codex failed; cursor not installed. Resume when one is back."</summary>
    /// <param name="harnesses">Each harness the call could have used, in its order, with why it didn't answer.</param>
    /// <returns>The line.</returns>
    internal static string Line(IReadOnlyList<(ModelHarness Harness, HarnessCause Cause)> harnesses) => throw new NotSupportedException();
}
