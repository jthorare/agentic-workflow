using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Line;

/// <summary>
/// The files a line station's check reads, by name (a packet's files, a parent packet's under a prefix, a pack, a repo file): text, parsed once.
/// A '.json' file parses as one document, a '.jsonl' file as one document a line; a file that does not parse is present and unparseable,
/// which the rule that needs it reports.
/// </summary>
public sealed class LineFacts
{
    private readonly Dictionary<string, string> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonNode?> _json = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _unparseable = new(StringComparer.Ordinal);

    /// <summary>Holds the files and parses the JSON ones.</summary>
    /// <param name="files">Each file's text by name.</param>
    public LineFacts(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        foreach (var (name, body) in files)
        {
            _text[name] = body;
            if (name.EndsWith(".json", StringComparison.Ordinal))
            {
                Parse(name, body, lines: false);
            }
            else if (name.EndsWith(".jsonl", StringComparison.Ordinal))
            {
                Parse(name, body, lines: true);
            }
        }
    }

    /// <summary>The names of the files held.</summary>
    public IEnumerable<string> Names => _text.Keys;

    /// <summary>Whether the file is present (parseable or not).</summary>
    /// <param name="name">The file name.</param>
    /// <returns>True when present.</returns>
    public bool Has(string name) => _text.ContainsKey(name);

    /// <summary>The file's text, or an empty string when it is absent.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The text.</returns>
    public string Text(string name) => _text.GetValueOrDefault(name, string.Empty);

    /// <summary>The parsed JSON file (a '.jsonl' file is a JSON array of its lines), or null when absent or unparseable.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The tree or null.</returns>
    public JsonNode? Json(string name) => _json.GetValueOrDefault(name);

    /// <summary>The parser's reason when the file is present but does not parse; otherwise null.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The reason or null.</returns>
    public string? Unparseable(string name) => _unparseable.GetValueOrDefault(name);

    /// <summary>The names among <paramref name="names"/> the facts do not hold, in the order given.</summary>
    /// <param name="names">The names.</param>
    /// <returns>The absent ones.</returns>
    public IReadOnlyList<string> Missing(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return [.. names.Where(n => !Has(n))];
    }

    private void Parse(string name, string body, bool lines)
    {
        try
        {
            if (!lines)
            {
                _json[name] = JsonNode.Parse(body);
                return;
            }

            var all = new JsonArray();
            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                all.Add(JsonNode.Parse(line));
            }

            _json[name] = all;
        }
        catch (JsonException e)
        {
            _unparseable[name] = e.Message;   // boundary: a file is text a model or a person wrote
        }
    }
}
