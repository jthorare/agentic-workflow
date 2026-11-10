using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// Finds the catalog row a command attempt runs as (API spec §2.1, UI spec E-12), fail-closed: a plain command takes its
/// one row; a classified command takes '&lt;Command&gt;:&lt;Variant&gt;'; protected fields make the row a desk row; an unknown variant
/// or a command with no row of its own is a local row, which no caller may run through the API.
/// </summary>
public static class ActionClassifier
{
    private const string Unknown = "UnknownCommand";

    /// <summary>The row <paramref name="command"/> runs as.</summary>
    /// <param name="catalog">The command catalog.</param>
    /// <param name="command">The request record.</param>
    /// <returns>Its row, or a synthetic protected row when it has no row to match.</returns>
    public static CommandRow Classify(ICommandCatalog catalog, object command)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(command);
        var rows = catalog.For(command.GetType());
        if (command is not IClassifiedCommand classified)
        {
            return rows is [var only] && only.Variant is null ? only : Unlisted(command.GetType(), rows, variant: null);
        }

        var row = rows.FirstOrDefault(r => string.Equals(r.Variant, classified.Variant, StringComparison.Ordinal));
        return row is null ? Unlisted(command.GetType(), rows, classified.Variant)
            : classified.CarriesProtectedFields && row.Class != ActionClass.Desk ? row with { Class = ActionClass.Desk }
            : row;
    }

    /// <summary>A local row (nobody runs it through the API) for an attempt the catalog has no row for, named '&lt;Command&gt;[:&lt;variant&gt;]' when that is an action id.</summary>
    private static CommandRow Unlisted(Type type, IReadOnlyList<CommandRow> rows, string? variant)
    {
        var known = rows.Count > 0 ? rows[0] : null;
        var name = known is null ? Name(type) : known.Action.Value.Split(':')[0];
        var action = ActionId.TryFrom(variant is null ? name : name + ":" + variant, out var full) ? full
            : ActionId.TryFrom(name, out var plain) ? plain
            : ActionId.From(Unknown);
        return new CommandRow(action, type, variant, ActionClass.Local, ServiceGrants: [], NeedsTerminal: false, SelfAudited: false,
            Destructive: false, ReadsViews: false, Shortcut: null, known?.Method, known?.Route, known?.Cli ?? CliForm.Parse("sindri").Value,
            "cli-only:unclassified");
    }

    private static string Name(Type type) =>
        type.Name.EndsWith("Request", StringComparison.Ordinal) ? type.Name[..^"Request".Length] : type.Name;
}
