using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The code index keys of 'index:' (code index spec §13), the profile's and each repo's: limits in range, utility globs like
/// deny globs, an embedding URL that is a loopback IP over http and a model that is not a cloud one. Each problem names its key path.
/// </summary>
internal static class IndexRules
{
    private const int MaxMegabytes = 102_400;
    private const int MaxGraphMegabytes = 4_096;

    internal static Error? Check(SindriProfile profile) =>
        Section("index", profile.Index) ?? (profile.Repos ?? []).Select(r => Section($"repos.{r.Name}.index", r.Index)).FirstOrDefault(static e => e is not null);

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
            ?? Globs($"{path}.utilityGlobs", index.UtilityGlobs);
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
