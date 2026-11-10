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

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ToolkitName { get; }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ToolName { get; }
}
