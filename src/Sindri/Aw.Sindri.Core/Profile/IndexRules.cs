using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The code index keys of 'index:' (code index spec §13), the profile's and each repo's: limits in range, utility globs like
/// deny globs, an embedding URL that is a loopback IP over http and a model that is not a cloud one. Each problem names its key path.
/// </summary>
internal static class IndexRules
{
    private const int MaxMegabytes = 102_400;
    private const int MaxGraphMegabytes = 4_096;
    private const string ConceptOther = "other";
    private const string FeaturePrefix = "feature:";

    internal static Error? Check(SindriProfile profile) =>
        RepoPageRepos(profile) ?? Section("index", profile.Index, inherited: [.. (profile.Repos ?? []).SelectMany(r => Ids(r.Index?.Concepts))], top: [])
        ?? (profile.Repos ?? []).Select(r => Repo(profile, r)).FirstOrDefault(static e => e is not null);

    private static Error? Repo(SindriProfile profile, RepoEntry r) =>
        Section($"repos.{r.Name}.index", r.Index, Ids(profile.Index?.Concepts), profile.Index?.Concepts ?? [])
        ?? SharedIds($"repos.{r.Name}.index.concepts", profile.Index?.Concepts, r.Index?.Concepts);

    /// <summary>A page under a repo's own 'index.pages' belongs to that repo: its 'repo:' must be absent or equal it (pages spec §3.1). Nothing else about pages is refused here: the write rules run at write and a read never refuses.</summary>
    private static Error? RepoPageRepos(SindriProfile profile) =>
        (profile.Repos ?? []).SelectMany(r => (r.Index?.Pages ?? []).Select((page, position) => (Repo: r, Page: page, Position: position)))
            .Where(static x => x.Page.Repo is { } named && !string.Equals(named, x.Repo.Name, StringComparison.Ordinal))
            .Select(static x => Invalid(string.Create(CultureInfo.InvariantCulture, $"repos.{x.Repo.Name}.index.pages[{x.Position}].repo"), $"must be absent or '{x.Repo.Name}': a page under a repo's own index belongs to that repo"))
            .FirstOrDefault();

    private static Error? Section(string path, IndexSettings? index, HashSet<string> inherited, IReadOnlyList<ConceptSettings> top)
    {
        if (index is null)
        {
            return null;
        }

        return Range($"{path}.maxFileKB", index.MaxFileKB, MaxMegabytes)
            ?? Range($"{path}.maxTotalMB", index.MaxTotalMB, MaxMegabytes)
            ?? Range($"{path}.graph.maxMB", index.Graph?.MaxMB, MaxGraphMegabytes)
            ?? Embeddings($"{path}.embeddings", index.Embeddings)
            ?? Globs($"{path}.utilityGlobs", index.UtilityGlobs)
            ?? Concepts($"{path}.concepts", index.Concepts, inherited, top);
    }

    private const int MaxName = 200;
    private const int MaxSummary = 280;

    private static HashSet<string> Ids(IReadOnlyList<ConceptSettings>? concepts) =>
        [.. (concepts ?? []).Select(c => c.Id?.Value).OfType<string>()];

    private static Error? Concepts(string path, IReadOnlyList<ConceptSettings>? concepts, HashSet<string> inherited, IReadOnlyList<ConceptSettings> top)
    {
        var known = Ids(concepts);
        known.UnionWith(inherited);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < (concepts?.Count ?? 0); i++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]");
            // why: the loop bound is zero when concepts is null
            if (Pin(at, concepts![i], known) is { } problem)
            {
                return problem;
            }

            if (concepts[i].Id is { } id && !seen.Add(id.Value))
            {
                return Invalid($"{at}.id", $"'{id.Value}' is already used by an earlier entry");
            }
        }

        return AliasRules.Clashes(path, concepts, top);
    }

    private static Error? Pin(string at, ConceptSettings pin, HashSet<string> known) =>
        Name($"{at}.name", pin.Name) ?? Summary($"{at}.summary", pin.Summary) ?? Reserved($"{at}.id", pin.Id) ?? Features($"{at}.features", pin.Features) ?? Globs($"{at}.paths", pin.Paths)
        ?? DependsOn($"{at}.dependsOn", pin.DependsOn, known) ?? Owns($"{at}.owns", pin.Owns) ?? AliasRules.Aliases($"{at}.aliases", pin.Aliases);

    private static Error? DependsOn(string path, IReadOnlyList<string>? ids, HashSet<string> known)
    {
        for (var i = 0; i < (ids?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when ids is null
            if (!known.Contains(ids![i]))
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), $"names no pin: '{ids[i]}' is not the id of an entry in index.concepts (the profile's or this repo's)");
            }
        }

        return null;
    }

    // A store name the data-store extractor can key: at least one letter or digit, no control characters.
    private static Error? Owns(string path, IReadOnlyList<string>? names)
    {
        for (var i = 0; i < (names?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when names is null
            if (!names![i].Any(char.IsLetterOrDigit) || names[i].Any(char.IsControl) || names[i].Length > MaxName)
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), "must be a data store name with a letter or digit in it, like 'invoices' or 'payments_t'");
            }
        }

        return null;
    }

    private static Error? Name(string path, string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Length > MaxName || name.Any(Invisible)
            ? Invalid(path, string.Create(CultureInfo.InvariantCulture, $"must not be empty, over {MaxName} characters or hold control, format or line-separator characters"))
            : null;

    private static Error? Summary(string path, string? summary) =>
        summary is not null && (summary.Length > MaxSummary || summary.Any(Invisible))
            ? Invalid(path, string.Create(CultureInfo.InvariantCulture, $"must be up to {MaxSummary} characters with no control, format or line-separator characters"))
            : null;

    // Control (Cc), format (Cf) and line or paragraph separator (Zl, Zp) characters: they hide text or break a line.
    internal static bool Invisible(char c) =>
        char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    private static Error? Reserved(string path, ConceptPinId? id) =>
        id switch
        {
            null => null,
            { Value: ConceptOther } => Invalid(path, "'other' is reserved for the concept that holds what no other takes"),
            { } pinned when !IsSlug(pinned.Value) => Invalid(path, "must be a slug of lowercase letters, digits, '-' and '.' (like 'scheduling' or 'tabs.tab-manager')"),
            _ => null,
        };

    // Lowercase words joined by single '-' or '.', the shape derived concept ids have, so a pin can name one.
    private static bool IsSlug(string id) =>
        id.Split('-', '.').All(static word => word.Length > 0 && word.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')));

    private static Error? Features(string path, IReadOnlyList<string>? features)
    {
        for (var i = 0; i < (features?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when features is null
            if (!IsFeatureKey(features![i]))
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), "must be a feature: key or glob, like 'feature:shifts' or 'feature:shifts.*'");
            }
        }

        return null;
    }

    private static bool IsFeatureKey(string key) =>
        key.Length > FeaturePrefix.Length && key.StartsWith(FeaturePrefix, StringComparison.Ordinal) && !key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));

    /// <summary>A repo's pins may not reuse an id the profile's own already pin (a repo's list is added to the profile's).</summary>
    private static Error? SharedIds(string path, IReadOnlyList<ConceptSettings>? top, IReadOnlyList<ConceptSettings>? own)
    {
        var shared = (top ?? []).Select(c => c.Id?.Value).OfType<string>().ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < (own?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when own is null
            if (own![i].Id is { } id && shared.Contains(id.Value))
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{i}].id"), $"'{id.Value}' is already used by the profile's index.concepts");
            }
        }

        return null;
    }

    private static Error? Range(string path, int? value, int max) =>
        value is < 1 || value > max ? Invalid(path, string.Create(CultureInfo.InvariantCulture, $"must be 1 to {max}")) : null;

    private static Error? Globs(string path, IReadOnlyList<string>? globs)
    {
        for (var i = 0; i < (globs?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when globs is null
            if (ProfileRules.GlobProblem(globs![i]) is { } problem)
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), problem);
            }
        }

        return null;
    }

    private static Error? Embeddings(string path, EmbeddingSettings? embeddings)
    {
        if (embeddings?.Url is { } url && !IsLoopbackUrl(url))
        {
            return Invalid($"{path}.url", $"'{Redacted(url)}' is not a loopback address: use http://127.0.0.1:11434 or http://[::1]:11434 (an IP, never a name, and no credentials)");
        }

        return embeddings?.Model is { } model && (string.IsNullOrWhiteSpace(model) || model.Contains("cloud", StringComparison.OrdinalIgnoreCase))
            ? Invalid($"{path}.model", "must be a local model name (not empty, and not a cloud model)")
            : null;
    }

    /// <summary>An http URL on 127.0.0.1 or [::1] with no credentials, path, query or fragment ('localhost' can resolve elsewhere).</summary>
    private static bool IsLoopbackUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && uri.UserInfo.Length == 0
        && (string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal) || string.Equals(uri.Host, "[::1]", StringComparison.Ordinal))
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;

    /// <summary>The URL without credentials, so a bad one is never echoed with its password.</summary>
    private static string Redacted(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.UserInfo.Length > 0 ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped) : url;

    internal static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
