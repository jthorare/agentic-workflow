using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Resources;

/// <summary>
/// Who owns a resource: <c lang="text">workflow:&lt;slug&gt;</c>, <c lang="text">node:&lt;slug&gt;/&lt;id&gt;</c> or <c lang="text">toolkit:&lt;name&gt;</c>. Every part is
/// lowercase letters, digits and '-', so the owner's folder can never leave the resources root.
/// </summary>
public sealed partial record ResourceOwner
{
    private ResourceOwner(ResourceOwnerKind kind, string key)
    {
        Kind = kind;
        Key = key;
    }

    /// <summary>Gets the kind.</summary>
    public ResourceOwnerKind Kind { get; }

    /// <summary>Gets the key after the colon: a slug, <c lang="text">slug/id</c> for a node, or a toolkit name.</summary>
    public string Key { get; }

    /// <summary>Gets the owner's folder relative to the resources root, with '/' separators: <c lang="text">workflow/triage</c>.</summary>
    public string Folder => $"{Kind.ToString().ToLowerInvariant()}/{Key}";

    /// <summary>Parses <c lang="text">kind:key</c>.</summary>
    /// <param name="text">The owner as typed.</param>
    /// <returns>The owner, or SND-RES-006.</returns>
    public static Result<ResourceOwner> Parse(string text)
    {
        if (text is null || (text.IndexOf(':', StringComparison.Ordinal) is var colon && colon < 1))
        {
            return Bad();
        }

        var key = text[(colon + 1)..];
        var kind = text[..colon] switch
        {
            "workflow" => ResourceOwnerKind.Workflow,
            "node" => ResourceOwnerKind.Node,
            "toolkit" => ResourceOwnerKind.Toolkit,
            _ => (ResourceOwnerKind?)null,
        };
        var parts = key.Split('/');
        var shaped = kind switch
        {
            ResourceOwnerKind.Node => parts.Length == 2,
            ResourceOwnerKind.Workflow or ResourceOwnerKind.Toolkit => parts.Length == 1,
            _ => false,
        };
        return kind is { } k && shaped && parts.All(static p => Segment.IsMatch(p)) ? new ResourceOwner(k, key) : Bad();
    }

    /// <summary>The owner as typed: <c lang="text">workflow:triage</c>.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{Key}";

    private static Error Bad() => SindriErrors.ResourceInvalid.With("An owner is workflow:<slug>, node:<slug>/<id> or toolkit:<name>, each part lowercase letters, digits and '-'.");

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Segment { get; }
}
