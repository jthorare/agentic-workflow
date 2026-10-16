using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// The command catalog's rules (API spec §3.3, UI spec E-8, E-16, §8), each a pure check over rows that names its
/// violations, so a fixture can prove it red. The route rule needs the endpoint table: see the Cqrs integration tests.
/// </summary>
internal static class CatalogRules
{
    /// <summary>The phases already shipped: a 'pending:&lt;phase&gt;' row names none of them. Each phase appends itself when it ships.</summary>
    public static readonly IReadOnlyList<string> ShippedPhases = ["S1", "P0", "P1", "P2", "P3", "P4"];

    /// <summary>UI spec §8's fixed keys: a row's shortcut never uses one.</summary>
    public static readonly IReadOnlySet<string> ReservedKeys = new HashSet<string>(["G", "J", "K", "O", "A", "T", "B", "Y", "C", "R", "/", "?"], StringComparer.Ordinal);

    private const string Pending = "pending:";
    private const string CliOnly = "cli-only:";

    /// <summary>Rows whose CLI form doesn't read back through CliForm.Parse or isn't plain ASCII.</summary>
    public static IEnumerable<string> UncheckedCli(IEnumerable<CommandRow> rows) =>
        rows.Where(row => !CliForm.Parse(row.Cli.Text).IsSuccess || row.Cli.Text.Any(c => c > '\x7E'))
            .Select(row => $"{row.Action.Value}: its CLI form isn't built from checked parts (E-16)");

    /// <summary>Rows whose shortcut uses a reserved key (with or without a modifier).</summary>
    public static IEnumerable<string> ReservedShortcuts(IEnumerable<CommandRow> rows) =>
        rows.Where(row => row.Shortcut is { } key && ReservedKeys.Contains(key.Split('+')[^1].ToUpperInvariant()))
            .Select(row => $"{row.Action.Value}: shortcut '{row.Shortcut}' uses a reserved key (UI spec §8); pick another");

    /// <summary>'cli-only' rows that aren't terminal-bound; local rows are exempt (G16).</summary>
    public static IEnumerable<string> LooseCliOnly(IEnumerable<CommandRow> rows) =>
        rows.Where(row => row.Ui.StartsWith(CliOnly, StringComparison.Ordinal) && !row.NeedsTerminal && row.Class != ActionClass.Local)
            .Select(row => $"{row.Action.Value}: '{row.Ui}' is only for terminal-bound rows (API spec §3.3)");

    /// <summary>'pending:&lt;phase&gt;' rows whose phase has shipped.</summary>
    public static IEnumerable<string> ShippedPending(IEnumerable<CommandRow> rows) =>
        rows.Where(row => row.Ui.StartsWith(Pending, StringComparison.Ordinal) && ShippedPhases.Contains(row.Ui[Pending.Length..], StringComparer.Ordinal))
            .Select(row => $"{row.Action.Value}: '{row.Ui}' names a shipped phase; flip it to 'ready' and bind its command type");

    /// <summary>Rows whose UI status isn't 'ready', 'pending:&lt;phase&gt;' or 'cli-only:&lt;reason&gt;'.</summary>
    public static IEnumerable<string> UnknownUi(IEnumerable<CommandRow> rows) =>
        rows.Where(row => !IsUi(row.Ui)).Select(row => $"{row.Action.Value}: UI status '{row.Ui}' isn't ready, pending:<phase> or cli-only:<reason>");

    private static bool IsUi(string ui) =>
        string.Equals(ui, "ready", StringComparison.Ordinal)
        || (ui.StartsWith(Pending, StringComparison.Ordinal) && ui.Length > Pending.Length && char.IsAsciiLetterUpper(ui[Pending.Length]))
        || (ui.StartsWith(CliOnly, StringComparison.Ordinal) && ui.Length > CliOnly.Length && ui[CliOnly.Length..].All(c => char.IsAsciiLetterLower(c) || c == '-'));

    /// <summary>Formats violations one per line for an assertion message.</summary>
    public static string Lines(IEnumerable<string> violations) => string.Join('\n', violations.Select(v => "  " + v));
}
