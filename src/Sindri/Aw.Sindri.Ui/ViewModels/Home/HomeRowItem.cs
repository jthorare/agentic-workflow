using System.ComponentModel;
using System.Text.RegularExpressions;
using Aw.Sindri.Ui.Api.Home;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>A Home row as the row list draws it (home spec §7.1 "Row"): a plain map of the API's row. A fresh read makes a new object with the same <see cref="Key"/>.</summary>
internal sealed partial class HomeRowItem : IRowItem
{
    /// <summary>The fact column keys, in the order the row shows them.</summary>
    internal const string Why = "why";

    /// <inheritdoc cref="Why" />
    internal const string Object = "object";

    /// <inheritdoc cref="Why" />
    internal const string From = "from";

    private HomeRowItem(HomeRowView row)
    {
        Row = row;
    }

    // A row never changes in place: a read swaps in a new object with the same key.
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    /// <summary>The API's row.</summary>
    internal HomeRowView Row { get; }

    /// <inheritdoc />
    public string Key => Row.Id;

    /// <inheritdoc />
    public string Id { get; private init; } = string.Empty;

    /// <inheritdoc />
    public string Title => Row.Title;

    /// <inheritdoc />
    public string? SubLine => null;

    /// <inheritdoc />
    public string Kind { get; private init; } = string.Empty;

    /// <inheritdoc />
    public RowStateClass StateClass { get; private init; }

    /// <inheritdoc />
    public string? StateKey { get; private init; }

    /// <inheritdoc />
    public bool Unread { get; private init; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, RowFact> Facts { get; private init; } = new Dictionary<string, RowFact>(StringComparer.Ordinal);

    /// <inheritdoc />
    public string Age { get; private init; } = string.Empty;

    /// <inheritdoc />
    public RowWaitsOn? WaitsOn { get; private init; }

    /// <inheritdoc />
    public IReadOnlyList<RowAction> Actions => [];

    /// <summary>Maps an API row.</summary>
    /// <param name="row">The row as served.</param>
    /// <param name="now">The time the age counts to.</param>
    /// <returns>The row to draw.</returns>
    internal static HomeRowItem Of(HomeRowView row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        var needsYou = string.Equals(row.Group, "needs-you", StringComparison.Ordinal);
        var subject = Fact(row, Object);
        var id = subject is not null && DisplayId.IsMatch(subject) ? subject : string.Empty;
        var facts = new Dictionary<string, RowFact>(StringComparer.Ordinal);
        Put(facts, Why, RowFactKind.Chip, Fact(row, Why));
        Put(facts, Object, RowFactKind.Text, id.Length > 0 ? row.Repo : subject);   // why: the id already leads the row, so the object column names the place (spec §7.1 "kind and place only")
        Put(facts, From, RowFactKind.Person, Fact(row, From));
        return new HomeRowItem(row)
        {
            Id = id,
            Kind = HomeSplits.GrammarKind(row.Kind),
            StateClass = StateOf(row, needsYou),
            StateKey = string.Equals(row.Kind, "mention", StringComparison.Ordinal) ? "mention" : null,
            Unread = string.Equals(row.State, "open", StringComparison.Ordinal),
            Facts = facts,
            Age = Labels.Age(now - row.Since),
            WaitsOn = needsYou ? null : Waits(row.WaitsOn),   // why: the Needs you header already says who (spec §7.1); only Moving's rows vary
        };
    }

    // #3120, ENG-412, r-0412: the ids the row shows in mono before its title.
    [GeneratedRegex(@"^(#\d+|[A-Z][A-Z0-9]*-\d+|r-\d+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DisplayId { get; }

    private static string? Fact(HomeRowView row, string kind) => row.Facts.FirstOrDefault(f => string.Equals(f.Kind, kind, StringComparison.Ordinal))?.Text;

    private static void Put(Dictionary<string, RowFact> facts, string key, RowFactKind kind, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            facts[key] = new RowFact(kind, text);
        }
    }

    // The glyph follows the class: a failed run is the x, a ready PR the open ring, a review the ring with a dot, the rest of Needs you the diamond.
    private static RowStateClass StateOf(HomeRowView row, bool needsYou) => row.Kind switch
    {
        "run-failed" => RowStateClass.Failed,
        "pr-ready-merge" => RowStateClass.Open,
        _ => needsYou ? RowStateClass.Waiting : RowStateClass.Active,
    };

    private static RowWaitsOn Waits(string who) => who switch
    {
        "you" => new RowWaitsOn(RowWho.Me, who),
        "reviewer" => new RowWaitsOn(RowWho.Reviewer, who),
        "CI" => new RowWaitsOn(RowWho.Ci, who),
        "agent" => new RowWaitsOn(RowWho.Agent, who),
        _ => new RowWaitsOn(RowWho.Person, who),
    };
}
