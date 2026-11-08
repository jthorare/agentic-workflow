using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How a skill block's run ends (skill flows spec §3.3): its watch read a finished result, or a block-author bug in it, from the result file.</summary>
internal sealed partial class RecipeWorkflow
{
    // The watch returned a skill's result. Closing is as for a report: usage, diff signal, stop the session. A finished result completes the run as 'agent:result' (outputs to facts, the verdict to the outcome,
    // the transition carries the result file's sha256 as 'resultSha256'; the report digest also covers each output path's sha256); an unmapped verdict or a missing or bad output fails the Step with its SND-SKILL code.
    private async Task<WorkflowInstance> SkillEnded(WorkflowInstance instance, AgentRunning running, StepUse step, AgentSessionState ended, Interpreting context)
    {
        var open = running.Open;
        var closed = await Close(running, measure: true).ConfigureAwait(true);
        if (ended.Skill is { } finished)
        {
            var completed = new StepCompleted("agent:result", open.Token, OutcomeName.From(finished.Outcome), finished.Outputs, closed.Tokens, StepRuntime.Agent, finished.ReportSha256, EmptyDiff: closed.EmptyDiff)
            {
                Agent = running.Launched?.Agent,
                Model = running.Launched?.Model,
                ResultSha256 = finished.ResultFileSha256,
            };
            return await Decided(instance, open, completed, reportId: null, context).ConfigureAwait(true);
        }

        var failure = ended.SkillFailure ?? string.Empty;
        var split = failure.IndexOf(": ", StringComparison.Ordinal);
        var failed = AgentFailed(open, step, split < 0 ? failure : failure[(split + 2)..], closed.Tokens) with { Code = split < 0 ? null : failure[..split] };
        return await Decided(instance, open, failed, reportId: null, context).ConfigureAwait(true);
    }
}
