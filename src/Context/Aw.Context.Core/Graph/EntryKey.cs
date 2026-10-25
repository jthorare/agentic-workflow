using System.Globalization;
using System.Text;

namespace Aw.Context.Core.Graph;

/// <summary>
/// The versioned key function (spec §6.1): every key a map file or a build writes passes through it. Version 1: Unicode
/// NFC, trailing white space dropped, and feature, action, thing, flow and concept names in lower case (surface, code, record and
/// decision names keep their case: 'surface:setting/paneResizeStep'). A change to it is a new version with a
/// <see cref="IKeyMigration"/>; the golden snapshot test pins it.
/// </summary>
public static class EntryKey
{
    /// <summary>The current key version.</summary>
    public const int Version = 1;

    /// <summary>The version-1 key for <paramref name="key"/>.</summary>
    /// <param name="key">A key as written.</param>
    /// <returns>The normalized key.</returns>
    public static NodeKey For(NodeKey key)
    {
        var name = key.Name.Normalize(NormalizationForm.FormC).TrimEnd();
        if (key.Kind is NodeKind.Feature or NodeKind.Action or NodeKind.Thing or NodeKind.Flow or NodeKind.Concept)
        {
            name = name.ToLower(CultureInfo.InvariantCulture);
        }

        var scheme = key.Value[..key.Value.IndexOf(':', StringComparison.Ordinal)];
        return NodeKey.From($"{scheme}:{name}");
    }
}
