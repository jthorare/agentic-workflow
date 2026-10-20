using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Testing.Line;

/// <summary>
/// The line fixture's gold facts as the design and plan checks read them (LN-K): a packet's files by name, the scope packet's under 'scope/', the
/// design packet's under 'design/' for the plan, a style or lens pack under 'pack/', and the repo files a contract edit quotes under 'repo/'.
/// Embedded from tests/fixtures/line, so no test reads a file.
/// </summary>
public sealed class LineFixture : Dictionary<string, string>
{
    private const string Gold = "line-gold/";

    /// <summary>An empty fact set with ordinal keys.</summary>
    public LineFixture()
        : base(StringComparer.Ordinal)
    {
    }

    /// <summary>The gold design packet with the scope packet beside it, the plain style pack, the design-five lens pack and the decision a contract edit quotes.</summary>
    /// <returns>A fresh, editable copy.</returns>
    public static LineFixture Design()
    {
        var facts = new LineFixture();
        facts.Load("design/expiry/", string.Empty);
        facts.Load("scope/expiry/", "scope/");
        facts["pack/style.json"] = Read("line-packs/style/plain/pack.json");
        facts["pack/lenses.json"] = Read("line-packs/lenses/design-five/pack.json");
        facts["repo/docs/decisions/0001-links-never-expire.md"] = Read("line-repo/docs/decisions/0001-links-never-expire.md");
        return facts;
    }

    /// <summary>The gold plan packet with the design packet under 'design/' and a passing aw-packets validation.</summary>
    /// <returns>A fresh, editable copy.</returns>
    public static LineFixture Plan()
    {
        var facts = new LineFixture();
        facts.Load("plan/expiry/", string.Empty);
        facts.Load("design/expiry/", "design/");
        facts["validation.json"] = "{\"passed\":true,\"failures\":[]}";
        return facts;
    }

    /// <summary>One of schemas/line/examples' design examples (design.minimal, design.decision …) as the JSON text of fact:design.</summary>
    /// <param name="name">The example's name without '.json'.</param>
    /// <returns>The JSON text.</returns>
    public static string DesignExample(string name) => Read("line-schemas/" + name + ".json");

    /// <summary>The gold design's spec rendered in the formal style.</summary>
    /// <returns>The markdown.</returns>
    public static string FormalSpec() => Read(Gold + "design/expiry.formal.spec.md");

    /// <summary>The formal style pack's pack.json.</summary>
    /// <returns>The JSON text.</returns>
    public static string FormalPack() => Read("line-packs/style/formal/pack.json");

    /// <summary>Edits one JSON fact in place.</summary>
    /// <param name="key">The fact.</param>
    /// <param name="edit">Receives the parsed JSON; change it in place.</param>
    /// <returns>The same facts, for chaining.</returns>
    public LineFixture Edit(string key, Action<JsonNode> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var node = JsonNode.Parse(this[key])!;
        edit(node);
        this[key] = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return this;
    }

    /// <summary>Rewrites one fact's text (a markdown or jsonl fact, or a JSON one as text).</summary>
    /// <param name="key">The fact.</param>
    /// <param name="rewrite">Gets the text, returns the new text.</param>
    /// <returns>The same facts, for chaining.</returns>
    public LineFixture EditText(string key, Func<string, string> rewrite)
    {
        ArgumentNullException.ThrowIfNull(rewrite);
        this[key] = rewrite(this[key]);
        return this;
    }

    /// <summary>Removes one fact, as a directory that does not hold it.</summary>
    /// <param name="key">The fact.</param>
    /// <returns>The same facts, for chaining.</returns>
    public LineFixture Without(string key)
    {
        _ = Remove(key);
        return this;
    }

    private void Load(string directory, string prefix)
    {
        var root = Gold + directory;
        foreach (var name in typeof(LineFixture).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(root, StringComparison.Ordinal)))
        {
            this[prefix + name[root.Length..]] = Read(name);
        }
    }

    private static string Read(string resource)
    {
        using var stream = typeof(LineFixture).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
