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
        Section("index", profile.Index)
        ?? (profile.Repos ?? []).Select(r => Section($"repos.{r.Name}.index", r.Index) ?? SharedIds($"repos.{r.Name}.index.concepts", profile.Index?.Concepts, r.Index?.Concepts)).FirstOrDefault(static e => e is not null);

    private static Error? Section(string path, IndexSettings? index)
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
            ?? Concepts($"{path}.concepts", index.Concepts);
    }

    private const int MaxName = 200;

    private static Error? Concepts(string path, IReadOnlyList<ConceptSettings>? concepts)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < (concepts?.Count ?? 0); i++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]");
            // why: the loop bound is zero when concepts is null
            if (Pin(at, concepts![i]) is { } problem)
            {
                return problem;
            }

            if (concepts[i].Id is { } id && !seen.Add(id.Value))
            {
                return Invalid($"{at}.id", $"'{id.Value}' is already used by an earlier entry");
            }
        }

        return null;
    }

    private static Error? Pin(string at, ConceptSettings pin) =>
        Name($"{at}.name", pin.Name) ?? Reserved($"{at}.id", pin.Id) ?? Features($"{at}.features", pin.Features) ?? Globs($"{at}.paths", pin.Paths);

    private static Error? Name(string path, string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Length > MaxName || name.Any(char.IsControl)
            ? Invalid(path, string.Create(CultureInfo.InvariantCulture, $"must not be empty, over {MaxName} characters or hold control characters"))
            : null;

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

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
