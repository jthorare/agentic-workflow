using System.Globalization;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetLibraryDoc;

/// <summary>
/// One design doc over Context (PR review page spec §6.1). The audience rule is the Reviews list's: a human reads the docs of every repo of the profile, a session its own repo's. Two scopes can
/// share a doc id; the doc at the lowest path answers, which is the one that keeps the id (spec §4.2).
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade.</param>
/// <param name="home">The home folder, written <c language="text">~</c> in paths.</param>
internal sealed class ContextLibraryDocFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, string home) : ILibraryDocFacts
{
    public async ValueTask<Result<LibraryDocView?>> Find(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a design doc needs a caller");
        }

        var audience = await AudienceOf(callers.Current, cancellationToken).ConfigureAwait(false);
        if (audience.IsError)
        {
            return audience.Error;
        }

        var docs = await engine.ListDocs(new LibraryDocFilter(), audience.Value, cancellationToken).ConfigureAwait(false);
        if (docs.IsError)
        {
            return docs.Error;
        }

        var doc = docs.Value.Where(d => string.Equals(d.Slug, id, StringComparison.Ordinal)).OrderBy(static d => d.Key.Value, StringComparer.Ordinal).FirstOrDefault();
        return Results.Success(doc is null ? null : View(doc));
    }

    /// <summary>The path with the home folder written <c language="text">~</c>.</summary>
    /// <param name="path">A path.</param>
    /// <param name="home">The home folder.</param>
    /// <returns>The path as shown.</returns>
    internal static string Fold(string path, string home)
    {
        var root = home.TrimEnd('/');
        return root.Length > 0 && path.StartsWith(root + "/", StringComparison.Ordinal) ? "~" + path[root.Length..] : path;
    }

    /// <summary>One error as a sentence: its message, with the file and line written <c language="text">&lt;path&gt; line &lt;n&gt;:</c>, then the fix.</summary>
    /// <param name="error">The error.</param>
    /// <param name="key">The doc's key, which the message starts with.</param>
    /// <param name="path">The path as shown.</param>
    /// <returns>The text.</returns>
    internal static string Text(DesignDocError error, string key, string path)
    {
        var message = error.Line is { } line && error.Message.StartsWith(key + ":" + line.ToString(CultureInfo.InvariantCulture) + ":", StringComparison.Ordinal)
            ? $"{path} line {line.ToString(CultureInfo.InvariantCulture)}:" + error.Message[(key.Length + line.ToString(CultureInfo.InvariantCulture).Length + 2)..]
            : error.Message.StartsWith(key, StringComparison.Ordinal) ? path + error.Message[key.Length..] : error.Message;
        return error.Fix.Length == 0 ? message : $"{message} {error.Fix}";
    }

    private LibraryDocView View(LibraryDoc doc)
    {
        var path = Fold(doc.Key.Value, home);
        return new LibraryDocView(
            doc.Slug ?? string.Empty, doc.Root, doc.Scope, doc.Kind, doc.Status, doc.Project, doc.Repos, doc.Issues, doc.Title, path,
            [.. doc.Errors.Select(e => new LibraryDocErrorView(e.Code, Text(e, doc.Key.Value, path)))]);
    }

    private async ValueTask<Result<AudienceScope>> AudienceOf(CallerContext caller, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        // why: the audience rule needs the repos' names, not each repo's index globs, so the entries carry name and path only
        ProfileRepoEntry[] entries = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, entries);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var names = ScopeRules.Usable(entries).Select(static r => r.Name).ToList();
        return await Allowed(new ProfileAudiencePolicy(settings.Value, names), Asked(caller, names), cancellationToken).ConfigureAwait(false);
    }

    private static List<ContextRepoName> Asked(CallerContext caller, List<ContextRepoName> all) =>
        caller.Class != CallerClass.Session ? all : caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var own) ? [own] : [];

    private static async ValueTask<Result<AudienceScope>> Allowed(ProfileAudiencePolicy policy, IEnumerable<ContextRepoName> asked, CancellationToken cancellationToken)
    {
        var allowed = new HashSet<AudienceKey>();
        foreach (var name in asked)
        {
            var one = await policy.For(name, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            allowed.UnionWith(one.Value.Allowed);
        }

        return new AudienceScope(allowed);
    }
}
