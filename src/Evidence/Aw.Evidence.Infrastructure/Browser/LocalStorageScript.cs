using System.Text;
using System.Text.Json;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>The init script that writes a script's localStorage entries, verbatim, into the host origin before any page script runs.</summary>
internal static class LocalStorageScript
{
    /// <summary>Builds the script; the origin and every entry are JSON-escaped string literals.</summary>
    /// <param name="origin">The vetted host's origin; the script does nothing on any other.</param>
    /// <param name="entries">The keys and values to write.</param>
    /// <returns>JavaScript for Playwright's init-script hook.</returns>
    internal static string For(string origin, IReadOnlyDictionary<string, string> entries)
    {
        var pairs = string.Join(',', entries.Select(static e => $"[{Literal(e.Key)},{Literal(e.Value)}]"));
        return new StringBuilder("(() => { if (location.origin !== ")
            .Append(Literal(origin))
            .Append(") return; for (const [key, value] of [")
            .Append(pairs)
            .Append("]) localStorage.setItem(key, value); })();")
            .ToString();
    }

    // A JSON string is a JavaScript string literal.
    private static string Literal(string text) => $"\"{JsonEncodedText.Encode(text)}\"";
}
