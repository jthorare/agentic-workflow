namespace Aw.Judge.Infrastructure.Providers;

/// <summary>AW_JUDGE_JEV_URL: points Jev at a stand-in on this machine (the hook tests' stub). It takes a loopback URL only, so no setting can send a screenshot to another host.</summary>
internal static class JevEndpoint
{
    /// <summary>The environment variable.</summary>
    public const string Variable = "AW_JUDGE_JEV_URL";

    /// <summary>Resolves the setting.</summary>
    /// <param name="value">The variable's value, or null when unset or empty.</param>
    /// <param name="warn">Told once when the value is set but not a loopback http(s) URL.</param>
    /// <returns>The URL to use, or null for the real Jev.</returns>
    public static Uri? Resolve(string? value, Action<string> warn)
    {
        ArgumentNullException.ThrowIfNull(warn);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            && (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            return uri;
        }

        warn($"{Variable} is not a loopback URL; using the real Jev");
        return null;
    }
}
