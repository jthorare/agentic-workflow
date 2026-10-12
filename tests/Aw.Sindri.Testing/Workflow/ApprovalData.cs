using System.Text.RegularExpressions;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>W8: approvals are stored keyed by (id, version, hash), never in a recipe file. Finds an approval key in recipe text.</summary>
public static partial class ApprovalData
{
    /// <summary>Whether <paramref name="text"/> has a key that would carry approval data ('approved', 'approval', 'approvedBy', 'pins', …).</summary>
    /// <param name="text">A recipe file's text.</param>
    /// <returns>True when it has one.</returns>
    public static bool In(string text) => Key.IsMatch(text);

    [GeneratedRegex(@"(^|[\s{,])(approv[A-Za-z]*|pins)\s*:", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Key { get; }
}
