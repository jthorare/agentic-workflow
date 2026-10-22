using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>The 'recipe approve-all' plan and result as text (setup-once spec §5.3, §7.2). Pure: ApproveAllCommand writes it.</summary>
internal static class ApproveAllText
{
    /// <summary>The plan a hold approves, in the order approval happens (children first), with each recipe's capabilities and what waits for its own approval.</summary>
    /// <param name="plan">The plan the challenge is bound to.</param>
    /// <returns>The text.</returns>
    internal static string Plan(ShippedRecipePlan plan)
    {
        var text = new StringBuilder().Append(CultureInfo.InvariantCulture, $"approving {plan.Recipes.Count} recipes, children first (digest {plan.Digest}):\n");
        foreach (var row in plan.Recipes)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"  {row.Id.Value} version {row.Version} ({row.ChangeWord})\n");
            foreach (var line in row.Capabilities)
            {
                _ = text.Append("      ").Append(line.Sentence).Append('\n');
            }
        }

        if (plan.OwnApproval.Count > 0)
        {
            _ = text.Append("not covered, approve each one at a time with 'sindri recipe approve <recipe>':\n");
            foreach (var own in plan.OwnApproval)
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"  {own.Id.Value}: {own.Sentence}\n");
            }
        }

        return text.ToString();
    }

    /// <summary>The line after the job started: its id and how to follow it.</summary>
    /// <param name="started">The started job.</param>
    /// <returns>The text.</returns>
    internal static string Started(JobStartedResponse started) =>
        $"approve-all started as job {started.JobId.Value}; follow it with 'sindri jobs show {started.JobId.Value}'\n";
}
