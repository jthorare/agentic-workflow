using Aw.Kernel;

namespace Aw.Sindri.Core.Projects;

/// <summary>Reads what the owner typed for a Linear project (projects page spec §5.3): a 'linear:project/&lt;id&gt;' key, or a Linear project url ('https://linear.app/&lt;workspace&gt;/project/&lt;slug&gt;-&lt;id&gt;').</summary>
public static class LinearProjectRefs
{
    private const string KeyPrefix = "linear:project/";
    private const int SlugMin = 6;

    /// <summary>Reads <paramref name="text"/> as a Linear project key or url.</summary>
    /// <param name="text">What the owner typed.</param>
    /// <returns>The key (its shape is checked by the decision that uses it) or the url's slug id, or SND-PROJECT-004 naming the 'linear' field.</returns>
    public static Result<LinearProjectRef> Parse(string text)
    {
        var typed = text?.Trim() ?? string.Empty;
        if (typed.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return typed.Length > KeyPrefix.Length ? new LinearProjectRef(typed, Slug: null) : NotLinear(typed);
        }

        return Uri.TryCreate(typed, UriKind.Absolute, out var uri) && SlugOf(uri) is { } slug ? new LinearProjectRef(Key: null, slug) : NotLinear(typed);
    }

    private static string? SlugOf(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.Equals(uri.Host, "linear.app", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !string.Equals(parts[1], "project", StringComparison.Ordinal))
        {
            return null;
        }

        var id = parts[2][(parts[2].LastIndexOf('-') + 1)..];
        return id.Length >= SlugMin && id.All(char.IsAsciiLetterOrDigit) ? id : null;
    }

    private static Error NotLinear(string typed) =>
        SindriErrors.ProjectInvalid.With($"linear: '{(typed.Length > 80 ? typed[..80] + "…" : typed)}' isn't a Linear project key or url (expected linear:project/<id>, or https://linear.app/<workspace>/project/<name>-<id>)");
}
