using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>What an approval checks and shows beyond the diff: the context section, and the argv behind each step hash (launcher L-16).</summary>
/// <param name="context">Checks the context section.</param>
/// <param name="catalog">The entries on this box.</param>
/// <param name="models">Checks the model keys against the catalog and the recipes.</param>
internal sealed class ProfileChecks(IContextEngine context, IAgentCatalog catalog, ModelKeyChecks? models = null)
{
    /// <summary>The model keys' verdict against this box (SND-MODEL-001); null when the checks aren't wired (a test without a catalog of recipes).</summary>
    /// <param name="profile">The live profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The error, or null.</returns>
    public ValueTask<Error?> Models(SindriProfile profile, CancellationToken cancellationToken) =>
        models is null ? ValueTask.FromResult<Error?>(null) : models.Models(profile, cancellationToken);

    /// <summary>The context section's verdict: an invalid section is never approved (SND-CONTEXT-001).</summary>
    /// <param name="profile">The live profile.</param>
    /// <returns>The error, or null.</returns>
    public Error? Context(SindriProfile profile)
    {
        var checkedContext = context.CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))]);
        return checkedContext.IsError ? checkedContext.Error : null;
    }

    /// <summary>For each stepArgvHash key in <paramref name="changes"/>, each listed hash with the entry that has it.</summary>
    /// <param name="profile">The live profile.</param>
    /// <param name="changes">The changed key paths.</param>
    /// <returns>The lines, in key order.</returns>
    public IReadOnlyList<StepArgvLine> StepArgv(SindriProfile profile, IReadOnlyList<string> changes)
    {
        var lines = new List<StepArgvLine>();
        foreach (var (id, limits) in profile.Agents?.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal))
        {
            if (!changes.Contains($"agents.{id}.stepArgvHash", StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var hash in limits.StepArgvHash ?? [])
            {
                var found = catalog.All.FirstOrDefault(e => string.Equals(e.ArgvHash, hash, StringComparison.Ordinal));
                lines.Add(new StepArgvLine(id, hash, found is null ? null : new StepArgvEntry(found.Bin, found.Open, found.Resume, found.Source)));
            }
        }

        return lines;
    }
}
