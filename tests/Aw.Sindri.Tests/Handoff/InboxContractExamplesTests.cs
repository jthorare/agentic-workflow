using Aw.Kernel.Testing;
using Aw.Sindri.Core.Handoff;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CreateHandoffItem;
using YamlDotNet.RepresentationModel;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>Every example offer in schemas/handoff/inbox.openapi.yaml, mapped to a handoff request the way the inbox poller maps an offer, passes the handoff validator (one set of limits).</summary>
[Lane(TestLane.Unit)]
public sealed class InboxContractExamplesTests
{
    [Fact]
    public void Every_example_offer_in_the_contract_passes_CreateHandoffItemValidator()
    {
        using var reader = new StringReader(File.ReadAllText(Path.Combine(RepoRoot(), "schemas", "handoff", "inbox.openapi.yaml")));
        var yaml = new YamlStream();
        yaml.Load(reader);

        var offers = new List<YamlMappingNode>();
        Collect(yaml.Documents[0].RootNode, offers);

        Assert.True(offers.Count >= 3, "the contract's example offers were not found");
        var validator = new CreateHandoffItemValidator();
        foreach (var node in offers)
        {
            var offer = ToOffer(node);
            var result = validator.Validate(ToRequest(offer));
            Assert.True(result.IsSuccess, offer.Key + ": " + (result.IsError ? result.Error.Message : string.Empty));
        }
    }

    // An offer is a mapping with id, title and repo (the examples nest them under offers[] and offer).
    private static void Collect(YamlNode node, List<YamlMappingNode> found)
    {
        switch (node)
        {
            case YamlMappingNode map:
                var keys = map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
                if (keys.Contains("id") && keys.Contains("title") && keys.Contains("repo"))
                {
                    found.Add(map);
                }

                foreach (var child in map.Children.Values)
                {
                    Collect(child, found);
                }

                break;
            case YamlSequenceNode sequence:
                foreach (var child in sequence.Children)
                {
                    Collect(child, found);
                }

                break;
            default:
                break;
        }
    }

    private static string? Text(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode { Value: not (null or "null") } scalar ? scalar.Value : null;

    private static Offer ToOffer(YamlMappingNode map)
    {
        var links = map.Children.TryGetValue(new YamlScalarNode("links"), out var l) && l is YamlSequenceNode ls
            ? ls.Children.OfType<YamlMappingNode>().Select(x => new HandoffLink(Text(x, "label")!, Text(x, "url")!)).ToList()
            : [];
        var labels = map.Children.TryGetValue(new YamlScalarNode("labels"), out var b) && b is YamlSequenceNode bs
            ? bs.Children.OfType<YamlScalarNode>().Select(x => x.Value!).ToList()
            : [];
        return new Offer(Text(map, "id")!, Text(map, "title")!, Text(map, "repo")!, Text(map, "branch"), Text(map, "prompt"), links, labels);
    }

    private static CreateHandoffItemRequest ToRequest(Offer offer) =>
        new(offer.Title, "inbox", offer.Key, RepoName.From(offer.Repo), offer.Branch, offer.Prompt, offer.Links, offer.Labels);

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test binaries");
    }
}
