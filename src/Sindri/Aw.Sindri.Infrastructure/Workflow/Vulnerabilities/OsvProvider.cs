#pragma warning disable MA0182 // why: the graph block registry is not registered in DI yet (EmptyGraphBlockCatalog); remove this when it is
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Vulnerabilities;

/// <summary>OSV.dev's batch API (no key): one batch query, then one read per advisory found.</summary>
/// <param name="http">Sends the requests (built with redirects off).</param>
/// <param name="allowedHosts">The hosts it may call.</param>
internal sealed class OsvProvider(HttpClient http, IReadOnlySet<string> allowedHosts) : IVulnerabilityProvider
{
    private const string Origin = "https://api.osv.dev/v1/";

    /// <inheritdoc />
    public string Name => "osv";

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<Vulnerability>>> Query(IReadOnlyList<VulnPackage> packages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        var batch = await SafeHttp.Send(http, allowedHosts, new SafeCall(HttpMethod.Post, new Uri(Origin + "querybatch"), BatchBody(packages)), cancellationToken).ConfigureAwait(false);
        if (batch.IsError)
        {
            return batch.Error;
        }

        var hits = Hits(batch.Value, packages);
        var details = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in hits.Select(static h => h.Id).Distinct(StringComparer.Ordinal))
        {
            var detail = await SafeHttp.Send(http, allowedHosts, new SafeCall(HttpMethod.Get, new Uri(Origin + "vulns/" + Uri.EscapeDataString(id))), cancellationToken).ConfigureAwait(false);
            if (detail.IsError)
            {
                return detail.Error;
            }

            details[id] = detail.Value;
        }

        return hits.Select(h => Map(h.Package, h.Id, details[h.Id])).ToList();
    }

    private static List<(VulnPackage Package, string Id)> Hits(string batch, IReadOnlyList<VulnPackage> packages)
    {
        var hits = new List<(VulnPackage Package, string Id)>();
        using var doc = JsonDocument.Parse(batch);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return hits;
        }

        var index = 0;
        foreach (var result in results.EnumerateArray().Take(packages.Count))
        {
            if (result.TryGetProperty("vulns", out var vulns) && vulns.ValueKind == JsonValueKind.Array)
            {
                var package = packages[index];
                hits.AddRange(vulns.EnumerateArray().Select(static v => Text(v, "id")).OfType<string>().Select(id => (package, id)));
            }

            index++;
        }

        return hits;
    }

    private static string BatchBody(IReadOnlyList<VulnPackage> packages)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteStartArray("queries");
            foreach (var p in packages)
            {
                w.WriteStartObject();
                w.WriteStartObject("package");
                w.WriteString("name", p.Name);
                w.WriteString("ecosystem", p.Ecosystem);
                w.WriteEndObject();
                w.WriteString("version", p.Version);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Vulnerability Map(VulnPackage package, string id, string detail)
    {
        using var doc = JsonDocument.Parse(detail);
        var advisory = doc.RootElement;
        var aliases = advisory.TryGetProperty("aliases", out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(static x => x.GetString()).OfType<string>().ToList() : [];
        var severity = advisory.TryGetProperty("database_specific", out var db) ? Text(db, "severity") : null;
        return new Vulnerability(
            Advisory: id,
            Aliases: aliases,
            Summary: Text(advisory, "summary") ?? Text(advisory, "details") ?? id,
            Severity: severity,
            Package: package.Name,
            Version: package.Version,
            FixedIn: FixedIn(advisory, package),
            Url: "https://osv.dev/vulnerability/" + id);
    }

    // The first 'fixed' event of a range that names this package.
    private static string? FixedIn(JsonElement advisory, VulnPackage package)
    {
        if (!advisory.TryGetProperty("affected", out var affected) || affected.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return affected.EnumerateArray()
            .Where(entry => entry.TryGetProperty("package", out var p) && string.Equals(Text(p, "name"), package.Name, StringComparison.OrdinalIgnoreCase) && entry.TryGetProperty("ranges", out _))
            .SelectMany(static entry => entry.GetProperty("ranges").EnumerateArray())
            .Where(static range => range.TryGetProperty("events", out _))
            .SelectMany(static range => range.GetProperty("events").EnumerateArray())
            .Select(static e => Text(e, "fixed"))
            .FirstOrDefault(static f => f is not null);
    }

    private static string? Text(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
