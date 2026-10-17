using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IComposeServices over YamlDotNet's representation model, read only: the text is the pinned, hash-checked compose file.</summary>
internal sealed class YamlComposeServices : IComposeServices
{
    /// <inheritdoc />
    public Result<IReadOnlyList<ComposeService>> Read(string composeText)
    {
        ArgumentNullException.ThrowIfNull(composeText);
        var stream = new YamlStream();
        try
        {
            using var reader = new StringReader(composeText);
            stream.Load(reader);
        }
        catch (YamlException exception)
        {
            return SindriErrors.OnyxPinsMalformed.With($"the compose file isn't valid YAML ({exception.Message}); the pinned release's files changed, so update context/onyx.lock and Sindri together");
        }

        var services = stream.Documents.Count > 0
            && stream.Documents[0].RootNode is YamlMappingNode root
            && root.Children.TryGetValue(new YamlScalarNode("services"), out var node)
            && node is YamlMappingNode mapping
                ? mapping
                : null;
        return services is null
            ? SindriErrors.OnyxPinsMalformed.With("the compose file has no `services:` mapping; the pinned release's layout changed, so update context/onyx.lock and Sindri together")
            : Results.Success<IReadOnlyList<ComposeService>>([.. services.Children.Select(static c => Service(c.Key, c.Value))]);
    }

    private static ComposeService Service(YamlNode key, YamlNode value)
    {
        var name = (key as YamlScalarNode)?.Value ?? "";
        if (value is not YamlMappingNode body)
        {
            return new ComposeService(name, Image: null, Profiles: []);
        }

        var image = body.Children.TryGetValue(new YamlScalarNode("image"), out var imageNode) ? (imageNode as YamlScalarNode)?.Value : null;
        IReadOnlyList<string> profiles = body.Children.TryGetValue(new YamlScalarNode("profiles"), out var profileNode) && profileNode is YamlSequenceNode sequence
            ? [.. sequence.Children.OfType<YamlScalarNode>().Select(static n => n.Value ?? "")]
            : [];
        return new ComposeService(name, image, profiles);
    }
}
