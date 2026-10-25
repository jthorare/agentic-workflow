using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'RecoverAgentStep' does when an agent's session ended without a report (issue #1000): reads the run directory's output folder and lets <see cref="AgentRecovery"/> decide. A
/// folder that proves one outcome has its files stored as the report's outputs, as 'workflow complete' stores them; one that holds files but proves nothing parks the run with the folder's path.
/// </summary>
/// <param name="catalog">The blocks' descriptors.</param>
/// <param name="outputs">The output folder.</param>
/// <param name="facts">Where the outputs are stored.</param>
internal sealed class AgentRecovering(BlockCatalog catalog, IStepOutputs outputs, IFactStore facts)
{
    /// <summary>Decides the run from its output folder.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report to complete with, the reason to park with, or nothing when the folder is empty.</returns>
    public async Task<AgentRecovered> Recover(AgentRecoverRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var names = await outputs.Names(run.Worktree, run.Since, cancellationToken).ConfigureAwait(false);
        if (names.IsError)
        {
            throw new InvalidOperationException($"{names.Error.Code}: {names.Error.Message}");   // the activity retries: the folder could not be read this time
        }

        if (names.Value.Count == 0)
        {
            return AgentRecovered.Nothing;
        }

        var folder = Path.Combine(run.Worktree, AgentRecovery.OutputFolder);
        var decision = AgentRecovery.Decide(catalog.Find(BlockId.From(run.Block)), names.Value, folder);
        if (decision.Outcome is not { } outcome)
        {
            return AgentRecovered.Nothing with { Reason = decision.Reason };
        }

        return await Stored(run, outcome.Value, decision.Files, folder, cancellationToken).ConfigureAwait(false);
    }

    // Stores each file as the report's output and digests the report; a file that can't be read parks the run, a store that can't take one retries the activity.
    private async Task<AgentRecovered> Stored(AgentRecoverRun run, string outcome, IReadOnlyDictionary<FactKey, string> files, string folder, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<FactKey, FactValue>();
        var report = new StringBuilder().Append(CultureInfo.InvariantCulture, $"recovered run {run.Run}\noutcome {outcome}\n");
        foreach (var (fact, name) in files.OrderBy(static f => f.Key.Value, StringComparer.Ordinal))
        {
            var content = await outputs.Read(run.Worktree, name, cancellationToken).ConfigureAwait(false);
            if (content.IsError)
            {
                return AgentRecovered.Nothing with { Reason = $"the agent finished but its result was not recorded: {folder} ({content.Error.Message})" };
            }

            var put = await facts.Put(content.Value, cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                throw new InvalidOperationException($"{put.Error.Code}: {put.Error.Message}");   // the activity retries: the store could not take it this time
            }

            stored[fact] = put.Value;
            _ = report.Append(CultureInfo.InvariantCulture, $"output {fact.Value} {Convert.ToHexStringLower(SHA256.HashData(content.Value.Span))}\n");
        }

        return new AgentRecovered(outcome, stored, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(report.ToString()))), Reason: null);
    }
}
