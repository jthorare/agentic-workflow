using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Kernel;

namespace Aw.Sindri.Host.Models;

/// <summary>The errors of a call nobody answered (model routing spec §5.4, §10): 241 for no installed harness, 240 for limits, 042 for the rest. Labeling gets the 240 or 241 text under the 042 code, so the callers that branch on it keep working.</summary>
internal static class ModelCallErrors
{
    /// <summary>SND-CONTEXT-241: no harness is installed.</summary>
    /// <param name="call">The call.</param>
    /// <returns>The error.</returns>
    internal static Error NoHarness(ModelCall call) =>
        Wrap(call, ContextErrors.NoModelHarness.With($"no agent CLI (claude, codex, cursor-agent) is installed (fix: install one, or name another in context.models.{ModelKinds.Key(call.Kind)}.harnesses)"));

    /// <summary>SND-CONTEXT-240: every harness is at its limit or failed and the call could not pause, or its pause ended without a resume.</summary>
    /// <param name="call">The call.</param>
    /// <returns>The error.</returns>
    internal static Error Unavailable(ModelCall call) =>
        Wrap(call, ContextErrors.ModelHarnessesUnavailable.With($"{ModelPauseText.Clauses(Causes(call))} (fix: wait until a harness is back and run it again, or add a harness to context.models.{ModelKinds.Key(call.Kind)}.harnesses)"));

    /// <summary>SND-CONTEXT-042: nobody answered and nobody hit a limit.</summary>
    /// <returns>The error.</returns>
    internal static Error NoAnswer() => ContextErrors.LabelingUnavailable.With("no agent CLI (claude, codex, cursor-agent) answered the request");

    /// <summary>Each harness the call could have used, in its order, with why it did not answer (the pause line and 240's text).</summary>
    /// <param name="call">The call, after a walk.</param>
    /// <returns>The harnesses and their causes.</returns>
    internal static List<(ModelHarness Harness, HarnessCause Cause)> Causes(ModelCall call)
    {
        var wanted = call.Settings.Entry(call.Kind).Harnesses ?? call.Settings.Default.Harnesses ?? ModelHarnesses.DefaultOrder;
        return [.. wanted.Select(h => (h, CauseOf(call, h)))];
    }

    private static HarnessCause CauseOf(ModelCall call, ModelHarness harness)
    {
        if (!call.Installed.Any(i => i.Harness == harness))
        {
            return HarnessCause.NotInstalled;
        }

        var name = ModelHarnesses.Name(harness);
        return call.Pass.Exists(t => string.Equals(t.Harness, name, StringComparison.Ordinal) && ModelCall.IsLimit(t)) ? HarnessCause.Limit : HarnessCause.Failed;
    }

    private static Error Wrap(ModelCall call, Error error) =>
        call.Request.Purpose == ModelPurpose.Labeling ? ContextErrors.LabelingUnavailable.With(error.Message) : error;
}
