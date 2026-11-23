using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// The one mapping from Sindri to the code index's scope (code-index spec §7, §10): the profile's repos, each with the
/// settings its 'index:' sections make, and the current caller. A session reads every repo of the profile, as a human does
/// (owner q93, 2026-10-23; spec §7.3 narrowed it to the session's own repo).
/// </summary>
/// <param name="approvals">The profile.</param>
/// <param name="live">The live profile file: its repos the profile doesn't name are indexed too (owner q102, CIX-L59).</param>
/// <param name="callers">The current caller.</param>
internal sealed class CodeIndexScopes(IApprovedProfile approvals, IProfileSource live, ICallerContextAccessor callers)
{
    /// <summary>The scope of the call running now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or SND-PROFILE-001 when the profile file is missing, SND-PROFILE-004 when it can't be read, or SND-CODEIDX-001 for a repo whose settings are invalid.</returns>
    internal async ValueTask<Result<CodeIndexScope>> Current(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileMissing.With("no profile yet, so no repo is indexed (fix: sindri repo add <path>)");
        }

        var file = await live.Load(cancellationToken).ConfigureAwait(false);
        return For(loaded.Profile, file.IsError ? null : file.Value.Profile, callers.Current);   // a live file that is missing or invalid adds nothing: the approved repos stay indexed
    }

    /// <summary>The scope a profile, the live profile and a caller make (owner q102, CIX-L59): the approved repos, then every live repo the profile doesn't name.</summary>
    /// <param name="approved">The profile: the only source of the top-level 'index:' section (deny globs, embeddings address).</param>
    /// <param name="live">The live profile, or null when there is none.</param>
    /// <param name="caller">The Sindri caller, or null for an in-process service.</param>
    /// <returns>The scope; SND-CODEIDX-001 when a repo's settings are invalid.</returns>
    internal static Result<CodeIndexScope> For(SindriProfile approved, SindriProfile? live, CallerContext? caller)
    {
        ArgumentNullException.ThrowIfNull(approved);
        var kind = Kind(caller);
        var sessionRepo = SessionRepo(kind, caller);
        var repos = new List<CodeIndexRepo>();
        foreach (var (entry, isLive) in Entries(approved, live))
        {
            if (!RepoName.TryFrom(entry.Name, out var repoName))
            {
                continue;
            }

            var repo = Repo(approved, isLive ? LiveOnly(entry) : entry, repoName);
            if (repo.IsError)
            {
                return repo.Error;
            }

            repos.Add(repo.Value);
        }

        return new CodeIndexScope(repos, new CodeIndexCaller(kind, sessionRepo));
    }

    /// <summary>The scope a profile and a caller make.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="caller">The Sindri caller, or null for an in-process service.</param>
    /// <returns>The scope; SND-CODEIDX-001 when a repo's settings are invalid.</returns>
    internal static Result<CodeIndexScope> For(SindriProfile profile, CallerContext? caller) => For(profile, live: null, caller);

    /// <summary>The approved repos, then each live repo the profile names neither by name nor by folder.</summary>
    private static IEnumerable<(RepoEntry Entry, bool IsLive)> Entries(SindriProfile approved, SindriProfile? live)
    {
        var named = (approved.Repos ?? []).ToList();
        foreach (var entry in named)
        {
            yield return (entry, false);
        }

        foreach (var entry in live?.Repos ?? [])
        {
            if (!named.Exists(n => string.Equals(n.Name, entry.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(n.Path.TrimEnd('/'), entry.Path.TrimEnd('/'), StringComparison.Ordinal)))
            {
                yield return (entry, true);
            }
        }
    }

    /// <summary>A live-only repo's entry as the scope may trust it: its deny globs only add to the approved ones, and it never names an embeddings address or model (it may only turn the layer off).</summary>
    private static RepoEntry LiveOnly(RepoEntry entry) => entry.Index is { Embeddings: { } embeddings } own
        ? entry with { Index = own with { Embeddings = embeddings with { Url = null, Model = null } } }
        : entry;

    private static RepoName? SessionRepo(CallerKind kind, CallerContext? caller)
        => kind == CallerKind.Session && caller?.Repo is { } repo && RepoName.TryFrom(repo.Value, out var name) ? name : null;

    private static Result<CodeIndexRepo> Repo(SindriProfile profile, RepoEntry entry, RepoName name)
    {
        var settings = CodeIndexSettings.Create(Input(profile, entry));
        return settings.IsError ? settings.Error : new CodeIndexRepo(name, entry.Path, settings.Value);
    }

    /// <summary>The whole 'index:' section a repo sees: the profile's list keys joined with the repo's, each scalar the repo names replacing the profile's (spec §13).</summary>
    private static CodeIndexSettingsInput Input(SindriProfile profile, RepoEntry entry)
    {
        var top = profile.Index ?? new IndexSettings();
        var own = entry.Index ?? new IndexSettings();
        var input = new CodeIndexSettingsInput(
            DenyPaths: RepoDenyGlobs.For(profile, entry),
            UtilityGlobs: Join(top.UtilityGlobs, own.UtilityGlobs),
            MaxFileKB: own.MaxFileKB ?? top.MaxFileKB,
            MaxTotalMB: own.MaxTotalMB ?? top.MaxTotalMB);
        return WithGraph(WithEmbeddings(input, top.Embeddings, own.Embeddings), top.Graph, own.Graph);
    }

    private static CodeIndexSettingsInput WithEmbeddings(CodeIndexSettingsInput input, EmbeddingSettings? top, EmbeddingSettings? own)
        => input with
        {
            EmbeddingsEnabled = own?.Enabled ?? top?.Enabled,
            EmbeddingsUrl = own?.Url ?? top?.Url,
            EmbeddingsModel = own?.Model ?? top?.Model,
        };

    private static CodeIndexSettingsInput WithGraph(CodeIndexSettingsInput input, GraphSettings? top, GraphSettings? own)
        => input with { GraphEnabled = own?.Enabled ?? top?.Enabled, GraphMaxMB = own?.MaxMB ?? top?.MaxMB };

    private static string[]? Join(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
    {
        string[] all = [.. first ?? [], .. second ?? []];
        return all.Length == 0 ? null : all;
    }

    private static CallerKind Kind(CallerContext? caller) => caller?.Class switch
    {
        CallerClass.Human => CallerKind.Human,
        CallerClass.Session => CallerKind.Session,
        CallerClass.Service => CallerKind.Service,
        null => CallerKind.Service,
        _ => CallerKind.Service,
    };
}
