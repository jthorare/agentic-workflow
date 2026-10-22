using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>Reads a lens pack from its files (line spec §5.2): <c language="text">pack.json</c> and one prompt file a lens, checking the shape the loops rely on.</summary>
public static class LensPackReader
{
    /// <summary>The pack the files hold.</summary>
    /// <param name="id">The id the pack was asked for; the file must say the same.</param>
    /// <param name="files">Each file's path inside the pack and its text.</param>
    /// <returns>The pack; an error naming what is wrong with the file.</returns>
    public static Result<LensPack> Read(LensPackId id, IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var header = Header(id, files);
        if (header.IsError)
        {
            return header.Error;
        }

        var (kind, entries) = header.Value;
        var lenses = new List<PackLens>();
        foreach (var entry in entries)
        {
            var lens = Lens(id.Value, entry as JsonObject, files);
            if (lens.IsError)
            {
                return lens.Error;
            }

            lenses.Add(lens.Value);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return lenses.Exists(l => !seen.Add(l.Id.Value)) ? Bad(id.Value, "names a lens id twice") : new LensPack(id, Hash(files), kind, lenses);
    }

    /// <summary>The hash of a pack's files: each path and the SHA-256 of its UTF-8 bytes as 'path NUL hex LF', in ordinal path order, hashed again.</summary>
    /// <param name="files">The files.</param>
    /// <returns>'sha256:' and 64 lower-case hex digits.</returns>
    public static string Hash(IReadOnlyDictionary<string, string> files)
    {
        var manifest = new StringBuilder();
        foreach (var path in files.Keys.Order(StringComparer.Ordinal))
        {
            _ = manifest.Append(path).Append('\0').Append(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(files[path])))).Append('\n');
        }

        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    // pack.json's own fields: kind, id, applies, and between 1 and the most lenses a pack may hold.
    private static Result<(LensPackKind Kind, JsonArray Entries)> Header(LensPackId id, IReadOnlyDictionary<string, string> files)
    {
        if (!files.TryGetValue("pack.json", out var text))
        {
            return Bad(id.Value, "has no pack.json");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException e)
        {
            return Bad(id.Value, $"has a pack.json that is not JSON: {e.Message}");
        }

        if (root is not JsonObject pack || Text(pack, "kind") != "lenses" || Text(pack, "id") != id.Value)
        {
            return Bad(id.Value, $"has a pack.json that is not a lens pack with the id '{id.Value}' (a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters)");
        }

        var applies = Text(pack, "applies") switch { "design" => (LensPackKind?)LensPackKind.Design, "review" => LensPackKind.Review, _ => null };
        if (applies is not { } kind)
        {
            return Bad(id.Value, "says 'applies' something other than design or review");
        }

        return pack["lenses"] switch
        {
            not JsonArray { Count: > 0 } => Bad(id.Value, "has no lens"),
            JsonArray { Count: > LensPack.MaxLenses } many => Bad(id.Value, $"has {many.Count} lenses, more than the {LensPack.MaxLenses} a pack may hold (fix: split the pack)"),
            JsonArray entries => (kind, entries),
        };
    }

    private static Result<PackLens> Lens(string id, JsonObject? entry, IReadOnlyDictionary<string, string> files)
    {
        var lensId = Text(entry, "id");
        var title = Text(entry, "title");
        var promptPath = Text(entry, "prompt");
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(promptPath))
        {
            return Bad(id, "has a lens without a title or a prompt");
        }

        if (lensId is null || !LensId.TryFrom(lensId, out var typedId))
        {
            return Bad(id, $"has a lens id '{lensId}' that is not a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters");
        }

        if (!files.TryGetValue(promptPath, out var prompt))
        {
            return Bad(id, $"names the prompt '{promptPath}' of lens '{lensId}', which is not in the pack");
        }

        return When(entry?["when"]) is { } when
            ? new PackLens(typedId, title, prompt, when)
            : Bad(id, $"has a 'when' on lens '{lensId}' that is not \"always\", a surfaceKinds list or a paths list");
    }

    private static LensWhen? When(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var word))
        {
            return string.Equals(word, "always", StringComparison.Ordinal) ? LensWhen.Always : null;
        }

        if (node is not JsonObject condition)
        {
            return null;
        }

        var kinds = Strings(condition["surfaceKinds"]);
        var paths = Strings(condition["paths"]);
        return kinds is null || paths is null || (kinds.Count == 0 && paths.Count == 0) ? null : new LensWhen(kinds, paths);
    }

    private static List<string>? Strings(JsonNode? node) => node switch
    {
        null => [],
        JsonArray array when array.All(static n => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0) => [.. array.Select(static n => n!.GetValue<string>())],   // why: the guard checked every element is a string
        _ => null,
    };

    private static string? Text(JsonObject? obj, string name) => obj?[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static Error Bad(string id, string problem) => SindriErrors.InvalidInput.With($"the lens pack '{id}' {problem}");
}
