using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.RecordEval;

/// <summary>
/// 'recipe eval --record' (spec §6.3, §9.2; engine P7, owner answer Q6): reads the instance's 'workflow.*' events from the ledger and
/// the stored content of the facts they name, and writes them back as an eval case (<see cref="EvalLedgerRecorder"/>) against the
/// recipe the address names, as it is approved now, with its pinned children. An address the ledger never saw is SND-WF-001.
/// </summary>
internal sealed class RecordEvalService(ILedgerReader ledger, IApprovedRecipes recipes, RecipeRegistryReader registry, IFactContents facts, JsonSerializerOptions options)
    : IService<RecordEvalRequest, RecordEvalResponse>
{
    private const int Page = 500;

    public async ValueTask<Result<RecordEvalResponse>> Handle(RecordEvalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!InstanceAddress.TryFrom(request.Address, out var address))
        {
            return WorkflowErrors.InstanceNotFound.With($"{request.Address} isn't an instance address (fix: sindri workflow list)");
        }

        var rows = await Rows(address, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        if (rows.Value.Count == 0)
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} has no events in the ledger (fix: sindri workflow list)");
        }

        var approved = await recipes.Latest(RecipeId.From("workflow." + RecipeNameOf(address)), cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var children = await registry.Children(cancellationToken).ConfigureAwait(false);
        if (children.IsError)
        {
            return children.Error;
        }

        var contents = await Contents(rows.Value, cancellationToken).ConfigureAwait(false);
        if (contents.IsError)
        {
            return contents.Error;
        }

        var recorded = EvalLedgerRecorder.Record("recorded", approved.Value.Recipe, children.Value, rows.Value, contents.Value);
        return recorded.IsError ? recorded.Error : new RecordEvalResponse(EvalLines.Write(recorded.Value, options));
    }

    /// <summary>The recipe name an address ends in: a root's second part, a child's last part before its entry.</summary>
    private static string RecipeNameOf(InstanceAddress address)
    {
        var parts = address.Value.Split('/');
        return parts.Length > 3 ? parts[^1].Split('.')[0] : parts[1];
    }

    private async ValueTask<Result<List<WorkflowEventPayload>>> Rows(InstanceAddress address, CancellationToken cancellationToken)
    {
        var item = address.Value.Split('/')[0];
        var found = new List<WorkflowEventPayload>();
        for (long after = 0; ;)
        {
            var page = await ledger.ReadAfter(after, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var row in page.Value.Where(r => string.Equals(r.Item, item, StringComparison.Ordinal) && r.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)
                && !string.Equals(r.Envelope.Type, EventTypes.WorkflowInjection, StringComparison.Ordinal))) // why: the injection row is the engine's own mirror of a run's inputs, not a trigger an eval line names
            {
                var payload = AwJson.Deserialize<WorkflowEventPayload>(Encoding.UTF8.GetBytes(row.Envelope.Payload), options);
                if (payload.IsSuccess && string.Equals(payload.Value.Instance, address.Value, StringComparison.Ordinal))
                {
                    found.Add(payload.Value);
                }
            }

            if (page.Value.Count < Page)
            {
                return found;
            }

            after = page.Value[^1].Sequence;
        }
    }

    private async ValueTask<Result<Dictionary<string, JsonElement>>> Contents(List<WorkflowEventPayload> rows, CancellationToken cancellationToken)
    {
        var contents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var sha in rows.SelectMany(static r => r.Detail?.Delta.Facts?.Values ?? []).Distinct(StringComparer.Ordinal))
        {
            var read = await facts.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            contents[sha] = EvalLedgerRecorder.ContentOf(read.Value.Span);
        }

        return contents;
    }
}
