using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>
/// <see cref="ILensPacks"/> over the APPROVED profile and the built-in packs (LN-PK lite): the pack id is the profile's repo entry, else its default, else
/// 'design-five' (design) or 'code-review' (review). Nothing approved means the defaults; an approved profile that can't be read fails the call, and a pack id
/// that is not a built-in one is an error, never a silent fallback.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="source">The built-in packs' files.</param>
internal sealed class ProfileLensPacks(IApprovedProfile approved, ILensPackSource source) : ILensPacks
{
    /// <summary>The built-in design pack's id.</summary>
    internal const string DefaultDesign = "design-five";

    /// <summary>The built-in review pack's id.</summary>
    internal const string DefaultReview = "code-review";

    public async ValueTask<Result<LensPack>> Resolve(LensPackKind kind, RepoName? repo, CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsError ? latest.Error : Read(kind, Chosen(kind, repo, latest.Value?.Profile.Workflow?.Packs?.Lenses));
    }

    // The profile's repo entry, else its default, else the built-in pack of the loop.
    private static string Chosen(LensPackKind kind, RepoName? repo, LensPackSettings? lenses)
    {
        var choice = kind == LensPackKind.Design ? lenses?.Design : lenses?.Review;
        return RepoEntry(choice, repo) ?? choice?.Default ?? (kind == LensPackKind.Design ? DefaultDesign : DefaultReview);
    }

    private static string? RepoEntry(PackChoice? choice, RepoName? repo) =>
        repo is { } named && choice?.Repos is { } repos && repos.TryGetValue(named.Value, out var own) ? own : null;

    private Result<LensPack> Read(LensPackKind kind, string id)
    {
        if (source.Files(id) is not { } files)
        {
            return SindriErrors.InvalidInput.With($"the lens pack '{id}' is not a built-in pack (built in: {string.Join(", ", source.Ids())}; fix: set workflow.packs.lenses.{Loop(kind)} to one of them)");
        }

        if (!LensPackId.TryFrom(id, out var packId))
        {
            return SindriErrors.InvalidInput.With($"the lens pack '{id}' is not a pack id (a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters)");
        }

        var pack = LensPackReader.Read(packId, files);
        return pack.IsError || pack.Value.Applies == kind
            ? pack
            : SindriErrors.InvalidInput.With($"the lens pack '{id}' serves the {Loop(pack.Value.Applies)} loop, not the {Loop(kind)} one (fix: name a pack that applies to {Loop(kind)})");
    }

    private static string Loop(LensPackKind kind) => kind == LensPackKind.Design ? "design" : "review";
}
