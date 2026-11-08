using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where an agent Step runs (engine P8 D8): the agent is the APPROVED profile's 'workflow.agent.target', and the item and its repo checkout come from the instance's
/// stored 'fact:item' and the profile's 'repos' entry of that name. A child instance starts with its declared inputs only, so it has no 'fact:item':
/// its item (the first part of its address) names the repo through the items port instead. Read when the launch activity runs, so a retry sees the current approval.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="contents">Reads the stored 'fact:item'.</param>
/// <param name="repos">The repo an item was created in, for an instance (a child) that has no 'fact:item'.</param>
internal sealed class AgentTargetResolver(IApprovedProfile approved, IFactContents contents, IItemRepos repos)
{
    private static readonly System.Text.Json.JsonSerializerOptions _options = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>The target, or why there is none.</summary>
    /// <param name="item">The instance's stored 'fact:item'; null when it has none.</param>
    /// <param name="address">The instance's address, whose first part is its item.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The target; SND-LAUNCH-003 when the profile names no agent, SND-LAUNCH-004 when the item or its repo can't be found.</returns>
    public async ValueTask<Result<AgentStepTarget>> Resolve(FactValue? item, InstanceAddress address, CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        if (latest.Value?.Profile.Workflow?.Agent?.Target is not { } agent)
        {
            return SindriErrors.LaunchAgentMissing.With("the approved profile has no workflow.agent.target (fix: set it and approve the profile)");
        }

        var read = await ReadItem(item, address, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return RepoOf(latest.Value.Profile, read.Value, agent);
    }

    private static Result<AgentStepTarget> RepoOf(SindriProfile profile, ItemFact item, AgentId agent)
    {
        if (!ItemId.TryFrom(item.Id, out var id))
        {
            return SindriErrors.InvalidInput.With("the instance's fact:item has an id that isn't an item id (an item id is 'ITM-' and 32 lowercase hex digits)");
        }

        var path = profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, item.Repo, StringComparison.Ordinal))?.Path;
        return path is null
            ? SindriErrors.LaunchRepoMissing.With($"the item's repo '{item.Repo}' is not in the approved profile's repos (fix: add it and approve the profile)")
            : new AgentStepTarget(id, agent, path, RepoName.TryFrom(item.Repo, out var repo) ? repo : null);
    }

    private async ValueTask<Result<ItemFact>> ReadItem(FactValue? item, InstanceAddress address, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return await ItemOfAddress(address, cancellationToken).ConfigureAwait(false);
        }

        var bytes = await contents.Read(item, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        var parsed = AwJson.Deserialize<ItemFact>(bytes.Value.Span, _options);
        if (parsed.IsError)
        {
            return SindriErrors.InvalidInput.With($"the instance's fact:item doesn't read: {parsed.Error.Message} (fix: give '{{ \"id\": …, \"repo\": <a repos key> }}'; \"spec\" is optional)");
        }

        return parsed.Value.Repo is not { Length: > 0 }
            ? SindriErrors.LaunchRepoMissing.With("the instance's fact:item names no repo")
            : parsed.Value;
    }

    // A child has no fact:item: the address starts with its item, and the ledger knows the repo that item was created in.
    private async ValueTask<Result<ItemFact>> ItemOfAddress(InstanceAddress address, CancellationToken cancellationToken)
    {
        if (!ItemId.TryFrom(address.Value.Split('/')[0], out var id))
        {
            return SindriErrors.LaunchRepoMissing.With("the instance has no fact:item, so there is no repo to run in");
        }

        var repo = await repos.RepoOf(id, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        return repo.Value is { } name
            ? new ItemFact(id.Value, name.Value, Spec: null)
            : SindriErrors.LaunchRepoMissing.With($"the instance has no fact:item and the ledger knows no repo for {id.Value}, so there is no repo to run in");
    }
}
