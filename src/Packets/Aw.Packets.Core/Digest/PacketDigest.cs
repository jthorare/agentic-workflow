using System.Buffers;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Digest;

/// <summary>
/// The packet's canonical hash (spec §4c Digests, version 1): SHA-256 over the canonical JSON of the object
/// '{"digestVersion":1,"files":{&lt;path&gt;: &lt;entry&gt;}}'. A JSON packet file's entry is the file itself without its
/// '$schema' editor hint (and packet.json without its 'status' and 'hash', which change when a packet is frozen); any other linked file's entry is
/// 'sha256:&lt;hex of its bytes&gt;'. Canonical JSON: object keys sorted by ordinal, no whitespace, numbers as written in
/// the file, and strings as System.Text.Json writes them with the Kernel profile's encoder (UnsafeRelaxedJsonEscaping):
/// '"' and '\' escaped, control characters in their short or \u00XX form, U+2028 and U+2029 as \u escapes, and
/// characters outside the Basic Multilingual Plane as \uXXXX surrogate pairs; everything else as UTF-8.
/// CanonicalJsonTests pins examples and one packet's hash; changing these bytes needs a new <see cref="Version"/>.
/// </summary>
public static class PacketDigest
{
    /// <summary>The canonical form's version, recorded with every hash.</summary>
    public const int Version = 1;

    private const string ManifestFile = "packet.json";
    private static readonly JsonDocumentOptions _strict = new() { AllowDuplicateProperties = false };
    private static readonly JsonWriterOptions _writer = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false };

    /// <summary>Computes the hash of a loaded packet.</summary>
    /// <param name="loaded">The packet and its files.</param>
    /// <returns>The hash, or AW-PACKETS-003 when a JSON file can't be put in canonical form (a duplicate key).</returns>
    public static Result<PacketHash> Compute(LoadedPacket loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var links = loaded.Packet.Manifest.Links;
        var json = new HashSet<string>(StringComparer.Ordinal) { ManifestFile, links.Decisions, links.OutOfScope, links.Tasks };
        return Compute(loaded.Files.Select(f => (f.Key, f.Value, json.Contains(f.Key))), kind: null);
    }

    /// <summary>
    /// Computes the hash of a scope packet: the same canonical form, over the scoping kind's fourteen files, with
    /// '"kind":"scoping"' in the hashed document so no build packet can collide with it. A JSON file's entry is the file
    /// without its '$schema'; a JSON-lines or text file's entry is 'sha256:&lt;hex of its bytes&gt;'.
    /// </summary>
    /// <param name="files">The packet directory as read.</param>
    /// <returns>The hash; AW-PACKETS-003 when a listed file is missing or a JSON file has no canonical form.</returns>
    public static Result<PacketHash> Compute(ScopingFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var kind = PacketKinds.Scoping;
        if (kind.Files.FirstOrDefault(f => !files.Present.ContainsKey(f.Name)) is { } missing)
        {
            return PacketsErrors.Malformed.With($"{missing.Name} is missing; a scope packet is hashed with all {kind.Files.Count} files.");
        }

        return Compute(kind.Files.Select(f => (f.Name, files.Present[f.Name], f.Format == PacketFileFormat.Json)), kind.Name);
    }

    /// <summary>
    /// Computes the hash of a line packet (design, plan, phase, proof, review, handoff): the same canonical form over the
    /// kind's files (packet.json and index.json among them) and the files under its linked directories, with
    /// '"kind":"&lt;kind&gt;"' in the hashed document so no packet of another kind can collide with it. A JSON file's entry
    /// is the file without its '$schema' hint (packet.json also without 'status' and 'hash'); any other file's entry is
    /// 'sha256:&lt;hex of its bytes&gt;'.
    /// </summary>
    /// <param name="kind">A line kind.</param>
    /// <param name="files">The packet directory as read.</param>
    /// <returns>The hash; AW-PACKETS-003 when a listed file is missing or a JSON file has no canonical form.</returns>
    public static Result<PacketHash> Compute(PacketKind kind, LinePacketFiles files)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(files);
        if (kind.Files.FirstOrDefault(f => !files.Present.ContainsKey(f.Name)) is { } missing)
        {
            return PacketsErrors.Malformed.With($"{missing.Name} is missing; a {kind.Name} packet is hashed with all {kind.Files.Count} files.");
        }

        var json = kind.Files.Where(static f => f.Format == PacketFileFormat.Json).Select(static f => f.Name).ToHashSet(StringComparer.Ordinal);
        var listed = kind.Files.Select(static f => f.Name).ToHashSet(StringComparer.Ordinal);
        var covered = files.Present.Where(f => listed.Contains(f.Key) || kind.Directories.Any(d => f.Key.StartsWith(d.Prefix, StringComparison.Ordinal)));
        return Compute(covered.Select(f => (f.Key, f.Value, json.Contains(f.Key))), kind.Name);
    }

    private static Result<PacketHash> Compute(IEnumerable<(string Path, ReadOnlyMemory<byte> Bytes, bool IsJson)> entries, string? kind)
    {
        var files = new JsonObject();
        foreach (var (path, bytes, isJson) in entries.OrderBy(static f => f.Path, StringComparer.Ordinal))
        {
            var entry = isJson ? Parse(path, bytes) : Results.Success<JsonNode?>(Hex(bytes.Span));
            if (entry.IsError)
            {
                return entry.Error;
            }

            files[path] = entry.Value;
        }

        var document = new JsonObject { ["digestVersion"] = Version, ["files"] = files };
        if (kind is not null)
        {
            document["kind"] = kind;
        }

        return PacketHash.From(Hex(Canonical(document)));
    }

    /// <summary>The canonical bytes of a JSON node: keys sorted by ordinal, no whitespace.</summary>
    /// <param name="node">The node.</param>
    /// <returns>UTF-8 bytes.</returns>
    public static byte[] Canonical(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, _writer))
        {
            Write(writer, node);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static Result<JsonNode?> Parse(string path, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            var node = JsonNode.Parse(bytes.Span, nodeOptions: null, _strict);
            if (node is JsonObject file)
            {
                _ = file.Remove("$schema");   // an editor hint, like formatting: not content
                if (path == ManifestFile)
                {
                    _ = file.Remove("status");
                    _ = file.Remove("hash");
                }
            }

            return Results.Success(node);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            return PacketsErrors.Malformed.With($"{path} has no canonical form: {ex.Message}");
        }
    }

    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var (key, value) in obj.OrderBy(static p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    Write(writer, value);
                }

                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                node.WriteTo(writer);
                break;
        }
    }

    private static string Hex(ReadOnlySpan<byte> bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
}
