using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// One icon per block kind on a workflow card (#3684, design/canvas-3684/cards.png): stroked 16 x 16 paths in the Icons.axaml style. Pointer = run by hand, clock = schedule,
/// braces = Code, fork = If, globe = HTTP, bell = Notify, hourglass = Wait, stack = Collect, chat bubble = Prompt, plus square = Create item; the kinds the frame doesn't draw
/// get their own shape, and a kind this table doesn't know gets a plain square.
/// </summary>
internal static class GraphBlockIcons
{
    /// <summary>The icon's side in pixels at 100 % text size.</summary>
    internal const double Size = 16;

    private const string Fallback = "M3,3 L13,3 L13,13 L3,13 Z";

    private static readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal)
    {
        ["manual"] = "M4,2 L4,13 L7,10 L9.5,14.5 L11,13.7 L8.6,9.3 L12.5,9 Z",
        ["schedule"] = "M8,2 A6,6 0 1 1 7.99,2 M8,4.5 L8,8 L10.5,9.5",
        ["code"] = "M6,2.5 C4,2.5 4.5,4 4.5,6 C4.5,7.5 3.5,8 2.5,8 C3.5,8 4.5,8.5 4.5,10 C4.5,12 4,13.5 6,13.5 M10,2.5 C12,2.5 11.5,4 11.5,6 C11.5,7.5 12.5,8 13.5,8 C12.5,8 11.5,8.5 11.5,10 C11.5,12 12,13.5 10,13.5",
        ["condition"] = "M5,14 L5,2 M5,10 C5,6.5 11,7.5 11,3 M9,4.5 L11,2.5 L13,4.5",
        ["http"] = "M2,8 A6,6 0 1 0 14,8 A6,6 0 1 0 2,8 M2,8 L14,8 M8,2 C5.5,5 5.5,11 8,14 M8,2 C10.5,5 10.5,11 8,14",
        ["notify"] = "M4,11 L4,7 A4,4 0 0 1 12,7 L12,11 L13,12 L3,12 Z M6.5,14 L9.5,14",
        ["wait"] = "M4,2 L12,2 M4,14 L12,14 M5,2 L5,4.5 L8,8 L11,4.5 L11,2 M5,14 L5,11.5 L8,8 L11,11.5 L11,14",
        ["collect"] = "M8,2 L14,5.5 L8,9 L2,5.5 Z M2,8.5 L8,12 L14,8.5 M2,11 L8,14.5 L14,11",
        ["prompt"] = "M2.5,3 L13.5,3 L13.5,11 L6,11 L3,14 L3,11 L2.5,11 Z",
        ["create_item"] = "M2.5,2.5 L13.5,2.5 L13.5,13.5 L2.5,13.5 Z M8,5 L8,11 M5,8 L11,8",
        ["run_agent"] = "M8,3 A2.5,2.5 0 1 1 7.99,3 M3,13.5 C3,10.5 5,9.5 8,9.5 C11,9.5 13,10.5 13,13.5",
        ["run_workflow"] = "M2,4 L7,4 L7,7 L2,7 Z M9,9 L14,9 L14,12 L9,12 Z M7,5.5 L11.5,5.5 L11.5,9",
        ["for_each"] = "M13,8 A5,5 0 1 1 11.5,4.5 M11.5,1.5 L11.5,4.5 L8.5,4.5",
        ["call_tool"] = "M3,13 L9,7 M9,7 A3,3 0 1 0 12,4 M9,7 L10,8",
        ["ask_owner"] = "M5.5,5.5 A2.5,2.5 0 1 1 8,8 L8,10.5 M8,13 L8,13.2",
        ["connection_event"] = "M9,2 L3.5,9 L8,9 L7,14 L12.5,7 L8,7 Z",
        ["only_new"] = "M3,8 L6.5,11.5 L13,4.5",
        ["memory_search"] = "M3,4 A5,1.8 0 1 0 13,4 A5,1.8 0 1 0 3,4 M3,4 L3,12 A5,1.8 0 0 0 13,12 L13,4",
        ["memory_write"] = "M3,4 A5,1.8 0 1 0 13,4 A5,1.8 0 1 0 3,4 M3,4 L3,12 A5,1.8 0 0 0 13,12 L13,4 M6,9 L10,9 M8,7 L8,11",
        ["memory_forget"] = "M3,4 A5,1.8 0 1 0 13,4 A5,1.8 0 1 0 3,4 M3,4 L3,12 A5,1.8 0 0 0 13,12 L13,4 M6,9 L10,9",
    };

    private static readonly Dictionary<string, Geometry> _parsed = new(StringComparer.Ordinal);

    /// <summary>The path data drawn for a block type.</summary>
    /// <param name="type">The block type.</param>
    /// <returns>The path data; the plain square for a type the table doesn't know.</returns>
    internal static string PathFor(string type) => _paths.TryGetValue(type, out var path) ? path : Fallback;

    /// <summary>The block types that have their own icon.</summary>
    internal static IReadOnlyCollection<string> Known => _paths.Keys;

    /// <summary>The parsed icon for a block type, made once per type.</summary>
    /// <param name="type">The block type.</param>
    /// <returns>The geometry, in a 16 x 16 box.</returns>
    internal static Geometry For(string type)
    {
        var key = _paths.ContainsKey(type) ? type : string.Empty;
        if (!_parsed.TryGetValue(key, out var geometry))
        {
            geometry = Geometry.Parse(PathFor(type));
            _parsed[key] = geometry;
        }

        return geometry;
    }
}
