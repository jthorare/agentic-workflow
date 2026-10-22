using System.Text.Json.Nodes;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Prove;

/// <summary>What the hold-out log needs from its context.</summary>
/// <param name="Line">The line instance's slug, the root of the instance's address.</param>
/// <param name="Repo">The instance's repo.</param>
/// <param name="Dir">The phase packet's directory.</param>
/// <param name="Head">The built head.</param>
/// <param name="Report">fact:holdoutResult, the evaluator's report.</param>
/// <param name="ReadFacts">The text of the facts the Step reads, which the canary scan covers.</param>
internal sealed record HoldoutLogInputs(string Line, RepoName Repo, string Dir, string Head, JsonObject Report, IReadOnlyList<string> ReadFacts)
{
    /// <summary>Reads them; null when the context lacks one.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>The inputs.</returns>
    internal static HoldoutLogInputs? Of(StepContext context)
    {
        var report = StepFacts.Read(context, "fact:holdoutResult") as JsonObject;
        var packet = StepFacts.Read(context, "fact:phasePacket");
        var built = StepFacts.Read(context, "fact:built");
        return context.Address?.Split('/')[0] is { Length: > 0 } line && context.Repo is { } repo && report is not null && (string?)packet?["dir"] is { } dir && StepFacts.Head(built) is { } head
            ? new HoldoutLogInputs(line, repo, dir, head, report, [report.ToJsonString(), packet!.ToJsonString(), built!.ToJsonString()]) // why: both are non-null once dir and head were read
            : null;
    }
}
