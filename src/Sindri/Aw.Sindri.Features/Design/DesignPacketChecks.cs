using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>
/// The design station's checks (line spec §4.3): one pure function over the packet's files and the scope packet's, run by 'design check' (draft),
/// the evidence step, stop 2's read model and 'step.plan.freeze'. A rule whose files are absent is reported not-run with their names and does not fail;
/// at the evidence stage a not-run rule is never a pass. aw-packets' PKT-DSN-* rules check the files' shape; these check what they mean.
/// </summary>
internal static partial class DesignPacketChecks
{
    /// <summary>Runs every rule of the stage.</summary>
    /// <param name="facts">The files.</param>
    /// <param name="stage">Draft runs the draft rules; evidence runs all.</param>
    internal static LineCheckResponse Run(LineFacts facts, LineStage stage) => LineChecks.Run("design", DesignRules.All, facts, stage);
}
