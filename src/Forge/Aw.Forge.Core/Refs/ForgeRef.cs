using Aw.Kernel;

namespace Aw.Forge.Core.Refs;

/// <summary>A pull request on a forge: which adapter, the host, the repository and the number.</summary>
/// <param name="Forge">The adapter kind.</param>
/// <param name="Host">The host, lower case.</param>
/// <param name="Owner">The owner or organisation.</param>
/// <param name="Repo">The repository name.</param>
/// <param name="Number">The pull request number.</param>
public sealed record ForgeRef(ForgeKind Forge, string Host, string Owner, string Repo, int Number)
{
    /// <summary>The repository this pull request is in.</summary>
    public ForgeRepo Repository => new(Host, Owner, Repo);

    /// <summary>The canonical link, https://host/owner/repo/pull/number.</summary>
    public string Link => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://{Host}/{Owner}/{Repo}/pull/{Number}");

    /// <summary>
    /// Parses a pull request link. Refuses, naming the part that failed, a link that isn't https, carries credentials or
    /// a port, whose host isn't allowed (checked before the path, so no request is ever needed to refuse it), or whose
    /// path isn't owner/repo/pull/number (further segments such as /files are ignored).
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="forge">The adapter the ref belongs to.</param>
    /// <param name="hosts">The allowed hosts.</param>
    /// <returns>The ref, or the refusal.</returns>
    public static Result<ForgeRef> Parse(string? link, ForgeKind forge, ForgeHosts hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri))
        {
            return ForgeErrors.Invalid.With("link: not an absolute URL");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return ForgeErrors.Invalid.With("scheme: must be https");
        }

        if (uri.UserInfo.Length > 0)
        {
            return ForgeErrors.Invalid.With("host: credentials in a link are refused");
        }

        if (!uri.IsDefaultPort)
        {
            return ForgeErrors.Invalid.With("host: a port is not accepted");
        }

        var host = uri.IdnHost.ToLowerInvariant();
        if (!hosts.Allows(host))
        {
            return ForgeErrors.HostNotAllowed.With($"host '{host}' is not in the forge hosts");
        }

        return FromPath(uri.AbsolutePath, forge, host);
    }

    private static Result<ForgeRef> FromPath(string path, ForgeKind forge, string host)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || !string.Equals(parts[2], "pull", StringComparison.OrdinalIgnoreCase))
        {
            return ForgeErrors.Invalid.With("path: expected owner/repo/pull/number");
        }

        if (!IsName(parts[0]))
        {
            return ForgeErrors.Invalid.With("owner: letters, digits, '.', '_' and '-' only");
        }

        if (!IsName(parts[1]))
        {
            return ForgeErrors.Invalid.With("repo: letters, digits, '.', '_' and '-' only");
        }

        if (!int.TryParse(parts[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            return ForgeErrors.Invalid.With("number: a positive integer");
        }

        return new ForgeRef(forge, host, parts[0], parts[1], number);
    }

    private static bool IsName(string s) =>
        s.Length > 0 && s is not ("." or "..") && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
