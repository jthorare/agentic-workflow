using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The API goldens (UI spec E-17, API spec §12): each route's response body per scenario, at
/// 'Golden/Api/&lt;route-slug&gt;/&lt;scenario&gt;.json' in this project. The body is re-indented as served (same keys,
/// order and values; only whitespace changes) and normalized only where a field can't be fixed: ProblemDetails'
/// 'traceId', which AddProblemDetails draws at random (plan G18), the temp home's path (status's 'profile.source'),
/// written '&lt;home&gt;', and the hash an apply-file challenge draws from that path. Everything else is fixed by the GoldenHost's clock, ids and pid. AW_GOLDEN_UPDATE=1 rewrites
/// a file from the current answer (then review the diff).
/// </summary>
internal static class ApiGolden
{
    private static readonly NormalizationRule _traceId = new("traceId", "\"traceId\": \"[^\"]*\"", "\"traceId\": \"<trace>\"");

    // The apply-file challenge binds its six characters to the review file's path, which sits in the temp home.
    private static readonly NormalizationRule _applyFile = new("apply-file", "review apply-file [0-9a-f]{6}", "review apply-file <hash6>");

    private static readonly JsonWriterOptions _indented = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // keep the served text readable: no ' for a quote
    };

    /// <summary>The golden file of <paramref name="slug"/> and <paramref name="scenario"/>, relative to the test project.</summary>
    public static string PathOf(string slug, string scenario) => $"Golden/Api/{slug}/{scenario}.json";

    /// <summary>
    /// The route slug (E-17): the path after '/sindri/', '/' → '-', each '{…}' segment → 'id' whatever its name
    /// ('/sindri/items/{itemId}' → 'items-id', UI spec §18.1; '/sindri/profile/approve' → 'profile-approve').
    /// </summary>
    public static string Slug(string route)
    {
        const string Prefix = "/sindri/";
        if (!route.StartsWith(Prefix, StringComparison.Ordinal) || route.Length == Prefix.Length)
        {
            throw new ArgumentException($"'{route}' isn't a route under {Prefix}", nameof(route));
        }

        var parts = route[Prefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(static p => p.StartsWith('{') && p.EndsWith('}') ? "id" : p);
        return string.Join('-', parts);
    }

    /// <summary>Checks <paramref name="body"/> (JSON as served by the host in <paramref name="home"/>) against its golden file.</summary>
    public static void Match(string body, string home, string slug, string scenario) => Match(body, home, slug, scenario, []);

    /// <summary>
    /// Checks <paramref name="body"/> as <see cref="Match(string, string, string, string)"/> does, after <paramref name="masks"/>
    /// masks the values a served answer draws at run time (an instance's id, its timestamps).
    /// </summary>
    public static void Match(string body, string home, string slug, string scenario, IReadOnlyList<NormalizationRule> masks) =>
        Golden.Match(
            Reindent(body),
            typeof(ApiGolden).Assembly,
            PathOf(slug, scenario),
            new Normalization([_traceId, _applyFile, .. HomeRules(home), .. masks]));

    // The server builds paths from the home's real path, which on macOS ('/private/var/...') is longer than the temp path
    // ('/var/...') the test holds, so the real one goes first: the short one would leave '/private' in front of '<home>'.
    private static IEnumerable<NormalizationRule> HomeRules(string home)
    {
        var canonical = CanonicalPath.Of(home);
        if (!string.Equals(canonical, home, StringComparison.Ordinal))
        {
            yield return new NormalizationRule("home-real", System.Text.RegularExpressions.Regex.Escape(canonical), "<home>");
        }

        yield return new NormalizationRule("home", System.Text.RegularExpressions.Regex.Escape(home), "<home>");
    }

    /// <summary>The JSON re-indented with two spaces and a final newline; keys, order and values as served.</summary>
    public static string Reindent(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, _indented))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }
}
