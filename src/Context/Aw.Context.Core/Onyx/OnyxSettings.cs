using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Onyx;

/// <summary>
/// The profile's 'context.sources.onyx' entry, checked (spec §8.8, §14 S-L2): Onyx's base URL on a loopback address,
/// the secret pointer to its API key, and the pinned release tag. Only <see cref="Create"/> makes one, so every
/// instance holds to the rules. No message repeats the token, or a user name or password written into the URL.
/// </summary>
public sealed partial record OnyxSettings
{
    /// <summary>
    /// The email of the dedicated Onyx user that `sindri onyx up` makes for Sindri (an admin, so the search route works, on its own account rather than a person's). Onyx Community Edition has no service-account keys
    /// (their route is Business tier), so Sindri's token is a personal access token of this user, and doctor tells it from a person's account by this email. A manual setup names its dedicated user the same.
    /// It is an example.com address on purpose: RFC 2606 reserves the name, its null MX (RFC 7505) means no mail to it is ever delivered, and nobody can own the mailbox (so Onyx's forgot-password or invite mail can't reach anyone).
    /// Onyx accepts it: its pinned email-validator 2.2.0 leaves example.com out of its special-use list (`email_validator/__init__.py:66`), whereas `.invalid`, `.test`, `.local` and `.localhost` are rejected.
    /// </summary>
    public const string DedicatedAccountEmail = "sindri@example.com";

    private const string UrlKey = "context.sources.onyx.url";
    private const string TokenKey = "context.sources.onyx.token";
    private const string VersionKey = "context.sources.onyx.version";
    private const int MaxShown = 64;
    private static readonly HashSet<string> _schemes = new(StringComparer.Ordinal) { "env", "file", "keychain", "op" };

    private OnyxSettings(Uri url, string token, string version, bool managed)
    {
        Url = url;
        Token = token;
        Version = version;
        Managed = managed;
    }

    /// <summary>Gets a value indicating whether Sindri manages this Onyx ('context.sources.onyx.managed'): it stands the stack up, starts it with 'serve' and stops it with 'stop'. Only a managed one has its url on 127.0.0.1.</summary>
    public bool Managed { get; }

    /// <summary>Onyx's base URL (http or https on a loopback host), ending in '/'.</summary>
    public Uri Url { get; }

    /// <summary>The secret pointer to the API key ('env:…', 'file:…', 'keychain:…', 'op:…'), never the key itself.</summary>
    public string Token { get; }

    /// <summary>The pinned Onyx release tag as the profile writes it, e.g. 'v4.9.0'.</summary>
    public string Version { get; }

    /// <summary>Onyx's host and port, as messages name it.</summary>
    public string Address => Url.Authority;

    /// <summary>Checks the profile's values.</summary>
    /// <param name="url">'context.sources.onyx.url'.</param>
    /// <param name="token">'context.sources.onyx.token', a secret pointer.</param>
    /// <param name="version">'context.sources.onyx.version', a release tag.</param>
    /// <param name="managed">'context.sources.onyx.managed': Sindri runs this Onyx, so its url must be http://127.0.0.1:&lt;port&gt;.</param>
    /// <returns>The settings, SND-CONTEXT-055 for a URL or version that breaks the rules, or SND-CONTEXT-002 for no token or one that isn't a secret pointer (never repeated).</returns>
    public static Result<OnyxSettings> Create(string? url, string? token, string? version, bool managed = false)
    {
        var baseUrl = CheckUrl(url);
        if (baseUrl.IsError)
        {
            return baseUrl.Error;
        }

        if (managed && (baseUrl.Value.Scheme != Uri.UriSchemeHttp || baseUrl.Value.Host != "127.0.0.1"))
        {
            return NotLoopback("a managed Onyx (managed: true) is reached at http://127.0.0.1:<port> only");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return ContextErrors.SourceNotConfigured.With(
                $"{TokenKey}: required: a secret pointer to the Onyx API key (env:NAME, file:/path, keychain:service/account or op:vault/item/field).");
        }

        if (!IsPointer(token))
        {
            return ContextErrors.SourceNotConfigured.With(
                $"{TokenKey}: isn't a secret pointer, so it isn't used or shown here. Keep the Onyx API key outside the profile and point to it: "
                + "env:NAME, file:/path (mode 0600), keychain:service/account or op:vault/item/field (spec §4); rotate a key that was written into the profile.");
        }

        var pinned = CheckPinned(version);
        return pinned.IsError ? pinned.Error : new OnyxSettings(baseUrl.Value, token, pinned.Value, managed);
    }

    /// <summary>Compares the version a running Onyx reports with the pinned one.</summary>
    /// <param name="reported">What 'GET /api/version' returned as 'backend_version'.</param>
    /// <returns>Success when they name the same release (a leading 'v' on either side is ignored), else SND-CONTEXT-055 naming both.</returns>
    public Result<Unit> CheckVersion(string reported)
    {
        ArgumentNullException.ThrowIfNull(reported);
        return string.Equals(Bare(Version), Bare(reported), StringComparison.Ordinal)
            ? Unit.Value
            : ContextErrors.OnyxNotPinnedOrLoopback.With(
                $"{VersionKey}: Onyx at {Address} reports version '{Shown(reported)}', but the profile pins '{Version}'. Run the pinned "
                + $"release (image tag {Version}), or check the running release (spec §8.8), pin it and approve the profile again.");
    }

    /// <summary>The settings without the token pointer.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"OnyxSettings {{ Url = {Url}, Version = {Version}, Token = (withheld) }}";

    private static Result<Uri> CheckUrl(string? text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return NotLoopback("it isn't an absolute http or https URL");
        }

        if (uri.UserInfo.Length > 0)
        {
            return NotLoopback($"it carries a user name or password; Onyx's API key goes behind {TokenKey}");
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return NotLoopback("it carries a query or fragment; give Onyx's base URL only");
        }

        if (!IsLoopback(uri))
        {
            return NotLoopback($"its host is '{Shown(uri.Host)}'");
        }

        var path = uri.GetLeftPart(UriPartial.Path);
        return new Uri(path.EndsWith('/') ? path : path + "/");
    }

    private static bool IsLoopback(Uri uri) =>
        (uri.HostNameType == UriHostNameType.Dns && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        || (uri.HostNameType == UriHostNameType.IPv4 && uri.Host.StartsWith("127.", StringComparison.Ordinal))
        || (uri.HostNameType == UriHostNameType.IPv6 && string.Equals(uri.Host, "[::1]", StringComparison.Ordinal));

    private static Result<string> CheckPinned(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return NotPinned("required: the Onyx release tag that runs, e.g. v4.9.0");
        }

        if (string.Equals(version.Trim(), "latest", StringComparison.OrdinalIgnoreCase))
        {
            return NotPinned("'latest' isn't a pin; set the release tag that runs, e.g. v4.9.0, and run that image tag");
        }

        return ReleaseTag.IsMatch(version) ? version : NotPinned($"'{Shown(version)}' isn't a release tag such as v4.9.0");
    }

    private static Error NotLoopback(string why) => ContextErrors.OnyxNotPinnedOrLoopback.With(
        $"{UrlKey}: Onyx must be on a loopback address (127.0.0.0/8, [::1] or localhost) over http or https, but {why}. "
        + "Bind Onyx's published ports to 127.0.0.1 and point the url there, e.g. http://127.0.0.1:3189 (spec §8.8).");

    private static Error NotPinned(string why) => ContextErrors.OnyxNotPinnedOrLoopback.With($"{VersionKey}: {why} (spec §8.8).");

    /// <summary>
    /// A known scheme ('env', 'file', 'keychain', 'op') and a non-empty target: the least that tells a pointer from a
    /// key written into the profile. Sindri's own parser decides the rest when it resolves the pointer (doctor names
    /// the exact SND-SECRET reason), so every pointer it accepts passes here too.
    /// </summary>
    private static bool IsPointer(string token)
    {
        var colon = token.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && colon < token.Length - 1 && _schemes.Contains(token[..colon]);
    }

    private static string Bare(string version) => version.StartsWith('v') ? version[1..] : version;

    /// <summary>Text from outside (a host, a reported version) as a message shows it: one line, at most 64 characters.</summary>
    private static string Shown(string text)
    {
        var line = new string([.. text.Select(c => char.IsControl(c) ? ' ' : c)]);
        return line.Length <= MaxShown ? line : line[..MaxShown] + "…";
    }

    [GeneratedRegex(@"^v?[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReleaseTag { get; }
}
