using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>
/// <see cref="IStylePacks"/> over the APPROVED profile and the built-in packs (line spec §5.1): the pack id is the profile's repo entry, else its default, else 'plain'. Nothing approved means
/// the default; an approved profile that can't be read fails the call, and a pack id that is not a built-in one is an error (SND-PACK-001), never a silent fallback.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="source">The built-in packs' files.</param>
internal sealed class ProfileStylePacks(IApprovedProfile approved, IStylePackSource source) : IStylePacks
{
    /// <summary>The built-in style pack's id.</summary>
    internal const string Default = "plain";

    public async ValueTask<Result<StylePack>> Resolve(RepoName? repo, CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var chosen = Chosen(repo, latest.Value?.Profile.Workflow?.Packs?.Style);
        return LensPackId.TryFrom(chosen, out var id)
            ? Find(id)
            : SindriErrors.InvalidInput.With($"the style pack '{chosen}' is not a built-in pack (built in: {string.Join(", ", source.Ids())}; fix: set workflow.packs.style to one of them)");
    }

    public Result<StylePack> Find(LensPackId id) =>
        source.Files(id.Value) is { } files
            ? StylePackReader.Read(id.Value, files)
            : SindriErrors.InvalidInput.With($"the style pack '{id.Value}' is not a built-in pack (built in: {string.Join(", ", source.Ids())}; fix: set workflow.packs.style to one of them)");

    private static string Chosen(RepoName? repo, PackChoice? choice) =>
        (repo is { } named && choice?.Repos is { } repos && repos.TryGetValue(named.Value, out var own) ? own : null) ?? choice?.Default ?? Default;
}
