using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.claim.ref' (engine spec §5.7, §12; engine P6 unit (d)): claims the item in 'workflow_claims' (item, owner
/// instance, attempt) and creates the marker ref 'sindri/{user}/{item}' ('refs/sindri/…') at the default branch's head
/// of the repo 'fact:item' names, the path and the user taken from the approved profile. It replies 'claim.created'
/// with 'created', or 'claimExists' while another live instance holds the item. An ended holder's claim is taken over
/// (the marker is reused) and reads 'created'. The claim is its existence check: a retry or a re-emission by the holder
/// changes nothing and reports <see cref="EffectDone.Existed"/>.
/// </summary>
/// <param name="claims">The 'workflow_claims' table.</param>
/// <param name="liveness">Whether a holder is still live.</param>
/// <param name="refs">Creates the marker ref.</param>
/// <param name="facts">Reads 'fact:item' back.</param>
/// <param name="profile">The approved profile: the user and the repo's path.</param>
internal sealed class ClaimRefEffect(IWorkflowClaims claims, IInstanceLiveness liveness, IClaimRefs refs, IFactContents facts, IApprovedProfile profile)
    : IEffectKind, IDescribedBlock
{
    private static readonly FactKey _item = FactKey.From("fact:item");
    private static readonly OutcomeName _created = OutcomeName.From("created");
    private static readonly OutcomeName _claimExists = OutcomeName.From("claimExists");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.claim.ref"), BlockKind.Effect, Version: 1, Inputs: [FactKey.From("fact:item")], Outputs: [], Outcomes: [],
        Capability.Effects | Capability.WriteScope, Parameters: [], Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var target = await Target(context, cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return target.Error;
        }

        var mine = new WorkflowClaim(ItemKey.From(context.Item), InstanceAddress.From(context.Instance), context.Attempt);
        var holder = await Take(mine, cancellationToken).ConfigureAwait(false);
        if (holder.IsError || holder.Value.Claim.Owner != mine.Owner)
        {
            return holder.IsError ? holder.Error : new EffectDone(_claimExists, Existed: false);
        }

        var marked = await refs.Mark(target.Value.Repository, $"refs/sindri/{target.Value.User}/{mine.Item.Value}", cancellationToken).ConfigureAwait(false);
        return marked.IsError ? marked.Error : new EffectDone(_created, Existed: holder.Value.Before && !marked.Value);
    }

    /// <summary>Takes <paramref name="mine"/>, over a holder that has ended; the claim that holds afterwards, and whether it was already mine.</summary>
    private async ValueTask<Result<(WorkflowClaim Claim, bool Before)>> Take(WorkflowClaim mine, CancellationToken cancellationToken)
    {
        var found = await claims.Find(mine.Item, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        InstanceAddress? ended = null;
        if (found.Value is { } held && held.Owner != mine.Owner)
        {
            var live = await liveness.IsLive(held.Owner, cancellationToken).ConfigureAwait(false);
            if (live.IsError || live.Value)
            {
                return live.IsError ? live.Error : (held, false);
            }

            ended = held.Owner;
        }

        var taken = await claims.Take(mine, ended, cancellationToken).ConfigureAwait(false);
        return taken.IsError ? taken.Error : (taken.Value, found.Value?.Owner == mine.Owner);
    }

    /// <summary>Where the marker goes: the approved profile's user and the path of the repo 'fact:item' names.</summary>
    private async ValueTask<Result<(string User, string Repository)>> Target(EffectContext context, CancellationToken cancellationToken)
    {
        var repo = await ItemRepo(context, cancellationToken).ConfigureAwait(false);
        var approved = repo.IsError ? repo.Error : await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (approved.Value is not { } snapshot)
        {
            return WorkflowErrors.NoRuntime.With("no profile is approved, so the claim has no user or repo (fix: sindri profile approve)");
        }

        return snapshot.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal)) is { } entry
            ? (snapshot.Profile.User, entry.Path)
            : WorkflowErrors.NoRuntime.With(
                $"fact:item names the repo '{repo.Value}', which the approved profile's repos don't list (fix: add it to repos and approve the profile)");
    }

    /// <summary>The repo key 'fact:item' ('{ id, repo, spec }', spec §12) names.</summary>
    private async ValueTask<Result<string>> ItemRepo(EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.Facts.TryGetValue(_item, out var stored) || Stored(stored) is not { } fact)
        {
            return WorkflowErrors.NoRuntime.With($"{context.Instance} has no stored fact:item for effect.claim.ref (fix: start it with --fact fact:item=<file>)");
        }

        var content = await facts.Read(fact, cancellationToken).ConfigureAwait(false);
        if (content.IsError)
        {
            return content.Error;
        }

        return Repo(content.Value) is { Length: > 0 } repo
            ? repo
            : WorkflowErrors.NoRuntime.With($"fact:item of {context.Instance} names no repo (fix: give '{{ \"id\": …, \"repo\": <a repos key> }}'; \"spec\" is optional)");
    }

    /// <summary>The stored fact a fact element holds ('ref' and 'sha256', as the engine stores it).</summary>
    private static FactValue? Stored(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("ref", out var reference) && reference.ValueKind == JsonValueKind.String
        && element.TryGetProperty("sha256", out var sha) && sha.ValueKind == JsonValueKind.String
            ? new FactValue(reference.GetString()!, sha.GetString()!, HeadSha: null)   // why: both checked to be strings just above
            : null;

    private static string? Repo(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var json = JsonDocument.Parse(content);
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("repo", out var repo) && repo.ValueKind == JsonValueKind.String
                ? repo.GetString()
                : null;
        }
        catch (JsonException)   // fact:item that isn't JSON names no repo; the start checked it already (SND-WF-014)
        {
            return null;
        }
    }
}
