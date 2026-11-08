using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'vulnerabilities' block: asks an <see cref="IVulnerabilityProvider"/> about packages, or about a repo's lockfiles.</summary>
public sealed class VulnerabilitiesBlock(IEnumerable<IVulnerabilityProvider> providers, ILockfileSource lockfiles) : IGraphBlock
{
    private const string DefaultProvider = "osv";

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["vulns"] = "array", ["count"] = "number" });

    private readonly List<IVulnerabilityProvider> _providers = [.. providers];

    /// <inheritdoc />
    public string Type => "vulnerabilities";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new(
    [
        new("provider", BlockFieldType.Text, Required: false),
        new("packages", BlockFieldType.MapList, Required: false),
        new("repo", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"provider":"osv","packages":[{"ecosystem":"npm","name":"lodash","version":"4.17.20"}]}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        var hasPackages = config["packages"] is not null;
        var hasRepo = config["repo"] is not null;
        if (hasPackages == hasRepo)
        {
            problems.Add((hasPackages ? "repo" : "packages", "set exactly one of 'packages' and 'repo'; remove one, or add the one that is missing"));
        }

        if (config["packages"] is JsonArray list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (Package(list[i]) is null)
                {
                    problems.Add(("packages", $"item {(i + 1).ToString(CultureInfo.InvariantCulture)} needs text 'ecosystem', 'name' and 'version'; fix it"));
                }
            }
        }

        return problems;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var name = BlockConfig.Text(context.Config["provider"]) ?? DefaultProvider;
        if (_providers.Find(p => string.Equals(p.Name, name, StringComparison.Ordinal)) is not { } provider)
        {
            return SindriErrors.GraphBlockFailed.With($"vulnerabilities has no provider '{name}'; use one of: {string.Join(", ", _providers.Select(static p => p.Name))}");
        }

        var packages = await Packages(context.Config, cancellationToken).ConfigureAwait(false);
        if (packages.IsError)
        {
            return packages.Error;
        }

        var found = packages.Value.Count == 0
            ? Results.Success<IReadOnlyList<Vulnerability>>([])
            : await provider.Query(packages.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var vulns = new JsonArray([.. found.Value.Select(Item)]);
        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["vulns"] = vulns, ["count"] = vulns.Count });
    }

    private async ValueTask<Result<IReadOnlyList<VulnPackage>>> Packages(JsonObject config, CancellationToken cancellationToken)
    {
        var repo = BlockConfig.Text(config["repo"]);
        if (repo is not null && config["packages"] is null)
        {
            return await lockfiles.Read(repo, cancellationToken).ConfigureAwait(false);
        }

        if (repo is not null || config["packages"] is not JsonArray list)
        {
            return SindriErrors.GraphBlockFailed.With("vulnerabilities needs exactly one of 'packages' and 'repo'");
        }

        var packages = new List<VulnPackage>();
        foreach (var item in list)
        {
            if (Package(item) is not { } package)
            {
                return SindriErrors.GraphBlockFailed.With("each package needs text 'ecosystem', 'name' and 'version'");
            }

            packages.Add(package);
        }

        return Results.Success<IReadOnlyList<VulnPackage>>(packages);
    }

    private static VulnPackage? Package(JsonNode? node) =>
        node is JsonObject o && BlockConfig.Text(o["ecosystem"]) is { } ecosystem && BlockConfig.Text(o["name"]) is { } name && BlockConfig.Text(o["version"]) is { } version
            ? new VulnPackage(ecosystem, name, version)
            : null;

    private static JsonNode Item(Vulnerability v)
    {
        var json = new JsonObject
        {
            ["id"] = v.Advisory,
            ["aliases"] = new JsonArray([.. v.Aliases.Select(static a => (JsonNode?)JsonValue.Create(a))]),
            ["summary"] = v.Summary,
            ["package"] = v.Package,
            ["version"] = v.Version,
            ["url"] = v.Url,
        };
        if (v.Severity is not null)
        {
            json["severity"] = v.Severity;
        }

        if (v.FixedIn is not null)
        {
            json["fixedIn"] = v.FixedIn;
        }

        return json;
    }
}
