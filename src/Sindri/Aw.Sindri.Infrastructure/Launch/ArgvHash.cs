using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The argv hash of a merged catalog entry (launcher spec section 2, L-11): SHA-256 over the RFC 8785 (JCS) text of the fields that decide what runs. The model and
/// policy form names are inputs, their per-run values never are. The goldens in tests/fixtures/launcher/argv-hash/ pin it, and scripts/check-launcher.mjs computes the same.
/// </summary>
internal static class ArgvHash
{
    /// <summary>The canonical text of the hash input; keys are sorted by UTF-16 code unit, which for these fixed names is alphabetical.</summary>
    /// <param name="input">The merged entry's hashed fields.</param>
    /// <returns>The canonical JSON text.</returns>
    internal static string Canonical(HashInput input)
    {
        var (bin, open, resume, sessionId, mcp, model, policy) = input;
        var text = new StringBuilder("{\"bin\":");
        Quote(text, BaseName(bin));
        _ = text.Append(",\"mcp\":");
        Quote(text, mcp);
        _ = text.Append(",\"model\":");
        Quote(text, model);
        _ = text.Append(",\"open\":");
        List(text, open);
        _ = text.Append(",\"policy\":");
        Quote(text, policy);
        _ = text.Append(",\"resume\":");
        if (resume is null)
        {
            _ = text.Append("null");
        }
        else
        {
            List(text, resume);
        }

        _ = text.Append(",\"sessionId\":");
        Quote(text, sessionId);
        return text.Append('}').ToString();
    }

    /// <summary>'sha256:' and the lower-case hex digest of <see cref="Canonical"/>.</summary>
    /// <param name="canonical">The canonical text.</param>
    /// <returns>The hash.</returns>
    internal static string Of(string canonical) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

    private static string BaseName(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed[(trimmed.LastIndexOf('/') + 1)..];
    }

    private static void List(StringBuilder text, IReadOnlyList<string> items)
    {
        _ = text.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            _ = text.Append(i == 0 ? "" : ",");
            Quote(text, items[i]);
        }

        _ = text.Append(']');
    }

    private static void Quote(StringBuilder text, string value)
    {
        _ = text.Append('"');
        foreach (var c in value)
        {
            _ = c switch
            {
                '"' => text.Append("\\\""),
                '\\' => text.Append("\\\\"),
                '\b' => text.Append("\\b"),
                '\t' => text.Append("\\t"),
                '\n' => text.Append("\\n"),
                '\f' => text.Append("\\f"),
                '\r' => text.Append("\\r"),
                < ' ' => text.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture)),
                _ => text.Append(c),
            };
        }

        _ = text.Append('"');
    }
}
