using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// The one reader of a phase packet's signals.json for the prove Steps and rules (line spec §3.2 row 4): the file is either keyed by item,
/// <c language="text">{signals:{item:[{id, kind, statement, runsAt, script?}]}}</c>, or the flat list the plan station copies from the design,
/// <c language="text">{signals:[{item, kind, statement, runs, script?}]}</c>. Both read as the first form: <c language="text">runs</c> becomes
/// <c language="text">runsAt</c>, and a signal with no id gets <c language="text">{item}-s{n}</c> (n counting from 1 among the item's signals).
/// </summary>
public static class PhaseSignals
{
    /// <summary>The signals of one item, normalised; empty when the file holds none.</summary>
    /// <param name="signals">The parsed signals.json, or null.</param>
    /// <param name="item">The acceptance item's id.</param>
    /// <returns>Copies of the item's signals, in file order.</returns>
    public static IReadOnlyList<JsonObject> Of(JsonNode? signals, string item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return signals?["signals"] switch
        {
            JsonObject byItem => [.. (byItem[item] as JsonArray ?? []).OfType<JsonObject>().Select(static s => (JsonObject)s.DeepClone())],
            JsonArray flat => Flat(flat, item),
            _ => [],
        };
    }

    private static List<JsonObject> Flat(JsonArray flat, string item)
    {
        var own = new List<JsonObject>();
        foreach (var signal in flat.OfType<JsonObject>().Where(s => string.Equals((string?)s["item"], item, StringComparison.Ordinal)))
        {
            var copy = (JsonObject)signal.DeepClone();
            if (copy["runsAt"] is null && copy["runs"] is { } runs)
            {
                copy["runsAt"] = runs.DeepClone();
            }

            copy["id"] ??= string.Create(CultureInfo.InvariantCulture, $"{item}-s{own.Count + 1}");
            own.Add(copy);
        }

        return own;
    }
}
