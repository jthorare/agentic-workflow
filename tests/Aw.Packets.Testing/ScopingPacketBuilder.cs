using System.Text;
using Aw.Packets.Core.Kinds;

namespace Aw.Packets.Testing;

/// <summary>
/// Builds the files of a scope packet. The default passes every scoping check: the fourteen files, a lenses.json with
/// one found node, one found item and one edge in the code lens and nothing in the others.
/// </summary>
internal sealed class ScopingPacketBuilder
{
    /// <summary>The default lenses.json.</summary>
    public const string GoldLenses =
        "{\n" +
        "  \"schemaVersion\": 1,\n" +
        "  \"code\": {\n" +
        "    \"anchors\": [\"code:src/a.cs\"], \"depth\": 2, \"hops\": [], \"mapVersion\": \"v13\", \"foundCount\": 2,\n" +
        "    \"nodes\": [{ \"key\": \"code:src/b.cs\", \"kind\": \"file\", \"label\": \"b.cs\", \"band\": \"code\", \"lensItem\": \"L-code-1\", \"trust\": \"untrusted\" }],\n" +
        "    \"edges\": [{ \"from\": \"code:src/a.cs\", \"to\": \"code:src/b.cs\", \"type\": \"imports\", \"provenance\": \"static\", \"confirmed\": false, \"trust\": \"untrusted\" }],\n" +
        "    \"items\": [{ \"lensItem\": \"L-code-2\", \"kind\": \"commit\", \"label\": \"fix b\", \"citations\": [\"git:abc123\"], \"trust\": \"untrusted\" }],\n" +
        "    \"truncated\": []\n" +
        "  },\n" +
        "  \"product\": { \"anchors\": [], \"depth\": 2, \"hops\": [\"state\"], \"mapVersion\": \"v13\", \"foundCount\": 0, \"nodes\": [], \"edges\": [], \"items\": [], \"truncated\": [] },\n" +
        "  \"related\": { \"anchors\": [], \"depth\": 2, \"hops\": [\"state\", \"dependency\", \"flow\"], \"mapVersion\": \"v13\", \"foundCount\": 0, \"nodes\": [], \"edges\": [], \"items\": [], \"truncated\": [] },\n" +
        "  \"history\": { \"anchors\": [], \"depth\": 0, \"hops\": [], \"mapVersion\": \"v13\", \"foundCount\": 0, \"nodes\": [], \"edges\": [], \"items\": [], \"truncated\": [] }\n" +
        "}\n";

    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>Creates the builder with the gold files.</summary>
    public ScopingPacketBuilder()
    {
        foreach (var spec in PacketKinds.Scoping.Files)
        {
            _files[spec.Name] = Encoding.UTF8.GetBytes(spec.Format switch
            {
                PacketFileFormat.Json => "{ \"schemaVersion\": 1 }\n",
                PacketFileFormat.JsonLines => "{ \"round\": 1 }\n{ \"round\": 2 }\n",
                PacketFileFormat.Text or _ => "# Brief\n",
            });
        }

        _files["lenses.json"] = Encoding.UTF8.GetBytes(GoldLenses);
    }

    /// <summary>Replaces (or adds) a file.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="text">Its text.</param>
    /// <returns>This builder.</returns>
    public ScopingPacketBuilder With(string name, string text)
    {
        _files[name] = Encoding.UTF8.GetBytes(text);
        return this;
    }

    /// <summary>Replaces (or adds) a file.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <returns>This builder.</returns>
    public ScopingPacketBuilder With(string name, byte[] bytes)
    {
        _files[name] = bytes;
        return this;
    }

    /// <summary>Replaces the first occurrence of some text inside the current lenses.json.</summary>
    /// <param name="from">The text to replace (must occur).</param>
    /// <param name="to">The replacement.</param>
    /// <returns>This builder.</returns>
    public ScopingPacketBuilder EditLenses(string from, string to)
    {
        var current = Encoding.UTF8.GetString(_files["lenses.json"]);
        if (!current.Contains(from, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{from}' does not occur in lenses.json.");
        }

        var text = string.Concat(current.AsSpan(0, current.IndexOf(from, StringComparison.Ordinal)), to, current.AsSpan(current.IndexOf(from, StringComparison.Ordinal) + from.Length));

        return With("lenses.json", text);
    }

    /// <summary>Removes a file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>This builder.</returns>
    public ScopingPacketBuilder Without(string name)
    {
        _ = _files.Remove(name);
        return this;
    }

    /// <summary>The files, by name: a fresh map a test may change.</summary>
    /// <returns>The files.</returns>
    public Dictionary<string, byte[]> Build() => new(_files, StringComparer.Ordinal);
}
