namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One group of the inbox: its kind, its heading and its rows, in the order they were served.</summary>
/// <param name="Kind">The group's kind.</param>
/// <param name="Heading">The group's heading.</param>
/// <param name="Rows">The group's rows.</param>
/// <typeparam name="T">What a row is (a served entry, a row view model).</typeparam>
internal sealed record NeedsYouGroup<T>(NeedsYouGroupKind Kind, string Heading, IReadOnlyList<T> Rows);
