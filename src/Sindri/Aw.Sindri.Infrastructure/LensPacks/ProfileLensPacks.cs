using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>
/// <see cref="ILensPacks"/> over the APPROVED profile and the built-in packs (LN-PK lite): the pack id is the profile's repo entry, else its default, else
/// 'design-five' (design) or 'code-review' (review). Nothing approved means the defaults; an approved profile that can't be read fails the call, and a pack id
/// that resolves nowhere is an error, never a silent fallback. A pack is looked for in the repo ('&lt;repo&gt;/.agentic-workflow/packs'), then on the machine, then built in
/// (line spec §5.1); one that is not built in needs a pin in 'workflow.packs.pins' ('lenses/&lt;id&gt;') equal to its hash, or it is SND-PACK-002.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="source">The built-in packs' files.</param>
/// <param name="outside">The files of the packs a repo or the machine supplies; null for built-in packs only.</param>
/// <param name="machineRoot">The machine's packs root ('$AW_STATE_DIR/packs'); null for none.</param>
internal sealed class ProfileLensPacks(IApprovedProfile approved, ILensPackSource source, IOutsidePackFiles? outside = null, string? machineRoot = null) : ILensPacks
{
    /// <summary>The built-in design pack's id.</summary>
    internal const string DefaultDesign = "design-five";

    /// <summary>The built-in review pack's id.</summary>
    internal const string DefaultReview = "code-review";

    public async ValueTask<Result<LensPack>> Resolve(LensPackKind kind, RepoName? repo, CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var profile = latest.Value?.Profile;
        return Read(kind, Chosen(kind, repo, profile?.Workflow?.Packs?.Lenses), new Lookup(RepoPath(profile, repo), profile?.Workflow?.Packs?.Pins));
    }

    // The profile's repo entry, else its default, else the built-in pack of the loop.
    private static string Chosen(LensPackKind kind, RepoName? repo, LensPackSettings? lenses)
    {
        var choice = kind == LensPackKind.Design ? lenses?.Design : lenses?.Review;
        return RepoEntry(choice, repo) ?? choice?.Default ?? (kind == LensPackKind.Design ? DefaultDesign : DefaultReview);
    }

    private static string? RepoEntry(PackChoice? choice, RepoName? repo) =>
        repo is { } named && choice?.Repos is { } repos && repos.TryGetValue(named.Value, out var own) ? own : null;

    private static string? RepoPath(SindriProfile? profile, RepoName? repo) =>
        repo is { } named ? profile?.Repos?.FirstOrDefault(r => string.Equals(r.Name, named.Value, StringComparison.Ordinal))?.Path : null;

    private Result<LensPack> Read(LensPackKind kind, string id, Lookup lookup)
    {
        var (files, outsideRoot) = Find(id, lookup);
        if (files is null)
        {
            return PackErrors.NotFound.With($"the lens pack '{id}' is not a repo, machine or built-in pack (built in: {string.Join(", ", source.Ids())}; fix: set workflow.packs.lenses.{Loop(kind)} to a pack that exists)");
        }

        if (!LensPackId.TryFrom(id, out var packId))
        {
            return PackErrors.Invalid.With($"the lens pack '{id}' is not a pack id (a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters)");
        }

        var pack = LensPackReader.Read(packId, files);
        if (pack.IsError)
        {
            return pack;
        }

        if (outsideRoot is not null && PinProblem(id, pack.Value.Hash, lookup.Pins) is { } unpinned)
        {
            return unpinned;
        }

        return pack.Value.Applies == kind
            ? pack
            : PackErrors.WrongKind.With($"the lens pack '{id}' serves the {Loop(pack.Value.Applies)} loop, not the {Loop(kind)} one (fix: name a pack that applies to {Loop(kind)})");
    }

    // The files of the first place the pack is in, and the packs root they came from (null for a built-in pack).
    private (IReadOnlyDictionary<string, string>? Files, string? Root) Find(string id, Lookup lookup)
    {
        if (outside is not null)
        {
            string?[] roots = [lookup.RepoPath is { } repo ? Path.Combine(repo, ".agentic-workflow", "packs") : null, machineRoot];
            foreach (var root in roots.OfType<string>())
            {
                if (outside.Files(root, id) is { } found)
                {
                    return (found, root);
                }
            }
        }

        return (source.Files(id), null);
    }

    private static Error? PinProblem(string id, string hash, IReadOnlyDictionary<string, string>? pins)
    {
        var key = "lenses/" + id;
        return pins is not null && pins.TryGetValue(key, out var pinned) && string.Equals(pinned, hash, StringComparison.Ordinal)
            ? null
            : PackErrors.PinMismatch.With($"the lens pack '{id}' is not pinned to its hash (fix: approve workflow.packs.pins: {key}: {hash})");
    }

    private static string Loop(LensPackKind kind) => kind == LensPackKind.Design ? "design" : "review";

    private sealed record Lookup(string? RepoPath, IReadOnlyDictionary<string, string>? Pins);
}
