using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>
/// The rule catalog on disk (issue 3787): one listing per '&lt;kind&gt;/&lt;language&gt;/*.yaml' record under the rules folder (for example 'engineering/csharp/x.yaml'),
/// with its id, kind, section, text, status and checks. A record that fails to parse or has no id or text is counted as skipped, never an error.
/// </summary>
/// <param name="rulesFolder">The rules folder (<see cref="RuleFolders.Resolve"/>).</param>
internal sealed class FileRuleCatalog(string rulesFolder) : IRuleCatalogPort
{
    /// <inheritdoc />
    public ValueTask<Result<RuleCatalogListing>> List(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(rulesFolder))
        {
            return ValueTask.FromResult(Results.Success(new RuleCatalogListing([], Skipped: 0)));
        }

        try
        {
            var rules = new List<RuleListing>();
            var skipped = 0;
            foreach (var file in Files())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryRead(file) is { } rule)
                {
                    rules.Add(rule);
                }
                else
                {
                    skipped++;
                }
            }

            IReadOnlyList<RuleListing> sorted = [.. rules.OrderBy(static r => r.Id, StringComparer.Ordinal)];
            return ValueTask.FromResult(Results.Success(new RuleCatalogListing(sorted, skipped)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: a folder that can't be read
        {
            return ValueTask.FromResult(Results.Failure<RuleCatalogListing>(SindriErrors.GraphBlockFailed.With($"the rules folder {rulesFolder} can't be read: {e.Message}")));
        }
    }

    // why: a record sits two folders down ('engineering/csharp/x.yaml'); 'engineering/csharp.inventory.yaml' beside the language folders is not a record
    private IEnumerable<string> Files() =>
        Directory.EnumerateDirectories(rulesFolder)
            .SelectMany(static kind => Directory.EnumerateDirectories(kind))
            .SelectMany(static language => Directory.EnumerateFiles(language, "*.yaml"));

    private static RuleListing? TryRead(string file)
    {
        try
        {
            var stream = new YamlStream();
            using (var reader = File.OpenText(file))
            {
                stream.Load(reader);
            }

            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode map)
            {
                return null;
            }

            var id = Scalar(map, "id");
            var text = Scalar(map, "text");
            return id is null || text is null
                ? null
                : new RuleListing(id, Scalar(map, "kind") ?? string.Empty, Scalar(map, "section") ?? string.Empty, text, Scalar(map, "status") ?? string.Empty, Checks(map));
        }
        catch (Exception e) when (e is YamlException or IOException or UnauthorizedAccessException)   // boundary: one bad record is skipped, not a failed step
        {
            return null;
        }
    }

    private static string? Scalar(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode { Value: { Length: > 0 } value } ? value : null;

    private static List<RuleCheckListing> Checks(YamlMappingNode map)
    {
        var checks = new List<RuleCheckListing>();
        if (map.Children.TryGetValue(new YamlScalarNode("checks"), out var node) && node is YamlSequenceNode list)
        {
            foreach (var item in list.Children.OfType<YamlMappingNode>())
            {
                checks.Add(new RuleCheckListing(Scalar(item, "type") ?? string.Empty, Scalar(item, "id") ?? string.Empty));
            }
        }

        return checks;
    }
}
