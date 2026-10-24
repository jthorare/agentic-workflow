using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The jobs slice's own copy of the read slices' scope mapping (AW1003 keeps slices from sharing a type, and a root-namespace type would make the root and the slices a cycle):
/// the one mapping from Sindri to the code index's scope (code-index spec §7, §10): the approved profile's repos, each with the
/// settings its 'index:' sections make, and the current caller. A session's scope holds only its own repo, so another repo
/// answers the same not-found as one never built and the status lists nothing else (spec §7.3).
/// </summary>
/// <param name="approvals">The approved profiles.</param>
/// <param name="callers">The current caller.</param>
internal sealed class CodeIndexJobScopes(IApprovedProfile approvals, ICallerContextAccessor callers)
{
    /// <summary>The scope of the call running now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or SND-PROFILE-005 when no profile is approved, or SND-CODEIDX-001 for a repo whose settings are invalid.</returns>
    internal async ValueTask<Result<CodeIndexScope>> Current(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved, so no repo is indexed (fix: sindri profile approve)");
        }

        return For(loaded.Profile, callers.Current);
    }

    /// <summary>The scope a profile and a caller make.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="caller">The Sindri caller, or null for an in-process service.</param>
    /// <returns>The scope; SND-CODEIDX-001 when a repo's settings are invalid.</returns>
    internal static Result<CodeIndexScope> For(SindriProfile profile, CallerContext? caller)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var kind = Kind(caller);
        var sessionRepo = SessionRepo(kind, caller);
        var repos = new List<CodeIndexRepo>();
        foreach (var entry in profile.Repos ?? [])
        {
            if (!RepoName.TryFrom(entry.Name, out var repoName) || (kind == CallerKind.Session && sessionRepo != repoName))
            {
                continue;
            }

            var repo = Repo(profile, entry, repoName);
            if (repo.IsError)
            {
                return repo.Error;
            }

            repos.Add(repo.Value);
        }

        return new CodeIndexScope(repos, new CodeIndexCaller(kind, sessionRepo));
    }

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
