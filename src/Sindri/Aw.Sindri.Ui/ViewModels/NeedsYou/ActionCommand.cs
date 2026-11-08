using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The command one action of a row shows: its own catalog row's command with the placeholders the row's subject can fill, filled.
/// The row's primary action and an action the catalog lacks keep the row's served command. The app never invents a command or
/// fills a placeholder it cannot resolve: that one stays as served and the action is marked "fill in".
/// </summary>
internal static partial class ActionCommand
{
    /// <summary>The command to show for an action.</summary>
    /// <param name="catalogCli">The action's command in the catalog; empty when the catalog has no row for it.</param>
    /// <param name="isPrimary">True for the row's primary action, whose command the server already wrote for this row.</param>
    /// <param name="rowCli">The row's served command.</param>
    /// <param name="subject">The row's subject, or null.</param>
    /// <param name="itemId">The row's item, or null.</param>
    /// <returns>The command, and the hint naming the required placeholders still open (empty when none).</returns>
    internal static (string Cli, string Hint) For(string catalogCli, bool isPrimary, string rowCli, NeedsYouSubject? subject, string? itemId)
    {
        if (isPrimary || string.IsNullOrEmpty(catalogCli))
        {
            return (rowCli, "");
        }

        // An optional [group] is filled whole or shown as served (never half-filled) and never asks for a fill-in: Token reads a
        // group as one match, so only placeholders outside a group are collected as open.
        var filled = Token.Replace(catalogCli, m => m.Groups["body"].Success ? FillGroup(m, subject, itemId) : Value(m.Groups["word"].Value, subject, itemId) ?? m.Value);
        var open = Token.Matches(filled).Where(m => m.Groups["word"].Success).Select(m => m.Groups["word"].Value).Distinct(StringComparer.Ordinal).ToList();
        return (filled, open.Count == 0 ? "" : Labels.FillInHint(open));
    }

    // A group whose placeholders all resolve loses its brackets; any other group is left exactly as served.
    private static string FillGroup(Match group, NeedsYouSubject? subject, string? itemId)
    {
        var open = false;
        var filled = Placeholder.Replace(group.Groups["body"].Value, m =>
        {
            var value = Value(m.Groups["word"].Value, subject, itemId);
            open |= value is null;
            return value ?? m.Value;
        });
        return open || !Placeholder.IsMatch(group.Groups["body"].Value) ? group.Value : filled;
    }

    // <name> is the service's name, <itemId> the row's item, <decisionId> a decision's id. The catalog's
    // <id> means a context-map review operation and <item> is not a catalog word, so neither is the row's item.
    private static string? Value(string placeholder, NeedsYouSubject? subject, string? itemId) => placeholder switch
    {
        "name" when subject?.Kind == NeedsYouKinds.Service => subject.Id,
        "itemId" when !string.IsNullOrEmpty(itemId) => itemId,
        "decisionId" when subject?.DecisionId is { } decision => decision,
        _ => null,
    };

    [GeneratedRegex("<(?<word>[A-Za-z]+)>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder { get; }

    [GeneratedRegex(@"\[(?<body>[^\]]*)\]|<(?<word>[A-Za-z]+)>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }
}
