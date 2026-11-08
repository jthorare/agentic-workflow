using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the code index's words on the file map and the code lens (code index spec §7.3, §10).</summary>
internal static partial class Labels
{
    /// <summary>The symbols column's heading.</summary>
    /// <summary>The line under the file when the code index could not be asked.</summary>
    internal const string ContextCodeUnavailable = "code lens unavailable";

    internal const string ContextSymbolsColumn = "symbols";

    /// <summary>The cell and caption when no index file exists yet.</summary>
    internal const string ContextSymbolsNotBuilt = "index not built";

    /// <summary>The cell and caption while the first build runs.</summary>
    internal const string ContextSymbolsBuilding = "index building";

    /// <summary>The cell and caption when the index file is damaged.</summary>
    internal const string ContextSymbolsDamaged = "index damaged";

    /// <summary>The cell and caption when the repo is not in the approved profile.</summary>
    internal const string ContextSymbolsNotConfigured = "not configured";

    /// <summary>The caption of the callers panel.</summary>
    internal const string ContextCallersTitle = "Callers";

    /// <summary>The caption of the callees panel.</summary>
    internal const string ContextCalleesTitle = "Callees";

    /// <summary>The panel text when the index has no edges for the symbols asked about.</summary>
    internal const string ContextCallsNone = "none found";

    /// <summary>Where the symbols come from, when the index is at the map's commit.</summary>
    /// <param name="commit">The index's commit.</param>
    /// <returns>"from 7c3e1a2".</returns>
    internal static string ContextSymbolsFrom(string commit) => "from " + Short(commit);

    /// <summary>Where the symbols come from, when the checkout moved on and the build is not done.</summary>
    /// <param name="commit">The index's commit.</param>
    /// <returns>"from 7c3e1a2, stale".</returns>
    internal static string ContextSymbolsFromStale(string commit) => ContextSymbolsFrom(commit) + ", stale";

    /// <summary>Where the symbols come from, when the map and the index are at different commits.</summary>
    /// <param name="commit">The index's commit.</param>
    /// <param name="asOf">The map's commit.</param>
    /// <returns>"from abcdef0, not 1234567".</returns>
    internal static string ContextSymbolsFromNot(string commit, string asOf) => ContextSymbolsFrom(commit) + ", not " + Short(asOf);

    /// <summary>The call sites that resolved to nothing.</summary>
    /// <param name="n">How many.</param>
    /// <returns>"3 calls not resolved".</returns>
    internal static string ContextCallsUnresolved(int n) => string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? "call" : "calls")} not resolved");

    /// <summary>A file's symbols in one cell.</summary>
    /// <param name="names">The first names.</param>
    /// <param name="more">How many more there are.</param>
    /// <returns>"setOrientation · resize · +3".</returns>
    internal static string ContextSymbolsCell(IReadOnlyList<string> names, int more) =>
        Dots([.. names, more > 0 ? string.Create(CultureInfo.InvariantCulture, $"+{more}") : null]);

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;
}
