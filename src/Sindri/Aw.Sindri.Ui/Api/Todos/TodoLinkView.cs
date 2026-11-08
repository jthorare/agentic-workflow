namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>One link on a todo (todo spec §4.3): a PR, a Linear issue or an item.</summary>
/// <param name="Kind">"pr", "linear" or "item".</param>
/// <param name="Ref">What the owner typed or Sindri read: "acme/api#12", "ENG-12", "ITM-…".</param>
/// <param name="Href">The deep link the chip opens.</param>
internal sealed record TodoLinkView(string Kind, string Ref, string Href);
