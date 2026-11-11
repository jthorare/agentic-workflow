using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>
/// The one place that spells a toolkit tool's name for clients: <c lang="text">&lt;toolkit&gt;__&lt;tool&gt;</c>. A dot breaks the tool-name rules of
/// many clients and of the model API (letters, digits, '_' and '-' only), so the separator is two underscores. A toolkit name never holds
/// '_' and a tool name starts with a letter, so the first "__" always splits the two.
/// </summary>
public static partial class ToolNames
{
    /// <summary>The separator between the toolkit name and the tool name.</summary>
    public const string Separator = "__";

    /// <summary>The longest name a client accepts.</summary>
    public const int MaxLength = 64;

    /// <summary>The client-facing name of a toolkit's tool.</summary>
    /// <param name="toolkit">The toolkit name: lowercase letters, digits and '-'.</param>
    /// <param name="tool">The tool name: lowercase letters, digits and '_', starting with a letter.</param>
    /// <returns>The name, or SND-TK-010 when a part is malformed or the whole is over <see cref="MaxLength"/> characters.</returns>
    public static Result<string> For(string toolkit, string tool)
    {
        if (toolkit is null || tool is null || !ToolkitName.IsMatch(toolkit) || !ToolName.IsMatch(tool))
        {
            return SindriErrors.ToolkitNameInvalid.With("A toolkit is lowercase letters, digits and '-'; a tool is lowercase letters, digits and '_', starting with a letter.");
        }

        var name = toolkit + Separator + tool;
        return name.Length <= MaxLength
            ? name
            : SindriErrors.ToolkitNameInvalid.With($"'{name}' is {name.Length} characters; clients allow {MaxLength}. Shorten the toolkit or the tool name.");
    }

    /// <summary>Whether <paramref name="name"/> is a toolkit name: lowercase letters, digits and '-', at most 64 characters, starting with a letter or digit.</summary>
    /// <param name="name">A folder or toolkit name.</param>
    /// <returns>True when it is one.</returns>
    public static bool IsToolkitName(string? name) => name is not null && ToolkitName.IsMatch(name);

    /// <summary>
    /// The PascalCase name the toolkit generator (scripts/scaffold/new-toolkit.mjs) takes for a new toolkit, or null when the generator would refuse it or turn it into a
    /// different folder name. The generator wants <c lang="text">^[A-Z][A-Za-z0-9]{2,30}$</c>, not one of its reserved names, and writes <c>Toolkit.Named(kebab(pascal))</c>;
    /// its kebab splits only before an upper-case letter that follows a lower-case letter or a digit, so 'x-ray' ("XRay") comes back as "xray" and is refused here.
    /// </summary>
    /// <param name="name">A new toolkit's name: lowercase words joined by '-', each starting with a letter.</param>
    /// <returns>The generator's name, or null.</returns>
    public static string? GeneratorName(string? name)
    {
        if (name is null || !Words.IsMatch(name))
        {
            return null;
        }

        var pascal = string.Concat(name.Split('-').Select(static w => char.ToUpperInvariant(w[0]) + w[1..]));
        return GeneratorPascal.IsMatch(pascal) && !_reserved.Contains(pascal, StringComparer.Ordinal) && string.Equals(Kebab(pascal), name, StringComparison.Ordinal) ? pascal : null;
    }

    private static readonly string[] _reserved = ["Common", "Shared", "Utils", "Helpers"];   // new-toolkit.mjs RESERVED

#pragma warning disable CA1308 // the generator's kebab lower-cases; the name is ASCII by the Words rule
    private static string Kebab(string pascal) => KebabSplit.Replace(pascal, "$1-$2").ToLowerInvariant();
#pragma warning restore CA1308

    /// <summary>Splits a client-facing name back into the toolkit and the tool.</summary>
    /// <param name="name">The name as a client sent it.</param>
    /// <param name="toolkit">The toolkit name when this returns true.</param>
    /// <param name="tool">The tool name when this returns true.</param>
    /// <returns>True when the name has the toolkit form.</returns>
    public static bool TryParse(string? name, out string toolkit, out string tool)
    {
        toolkit = tool = string.Empty;
        var at = name?.IndexOf(Separator, StringComparison.Ordinal) ?? -1;
        if (name is null || at < 1)
        {
            return false;
        }

        var left = name[..at];
        var right = name[(at + Separator.Length)..];
        if (!ToolkitName.IsMatch(left) || !ToolName.IsMatch(right))
        {
            return false;
        }

        toolkit = left;
        tool = right;
        return true;
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z][a-z0-9]*)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Words { get; }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]{2,30}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GeneratorPascal { get; }

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KebabSplit { get; }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ToolkitName { get; }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ToolName { get; }
}
