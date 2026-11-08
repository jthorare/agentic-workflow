namespace Aw.Sindri.Features.ListTodos;

/// <summary>One link on a todo, as the list serves it.</summary>
/// <param name="Kind">"item", "pr", "linear", "workflow" or "link".</param>
/// <param name="Ref">What a chip shows: "acme/api#12", "ENG-12", "ITM-…".</param>
/// <param name="Href">The deep link the chip opens.</param>
public sealed record TodoLinkItem(string Kind, string Ref, string Href);
