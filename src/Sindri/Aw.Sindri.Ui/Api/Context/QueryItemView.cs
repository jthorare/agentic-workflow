namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One item of an answer section: for a code item, Node is code:&lt;path&gt;.</summary>
/// <param name="Kind">The item kind (code, record, thing, feature).</param>
/// <param name="Node">The node key, or null.</param>
/// <param name="Title">The title, or null.</param>
/// <param name="Text">The engine's one-line text, or null.</param>
/// <param name="Why">Why the engine named it.</param>
internal sealed record QueryItemView(string Kind, string? Node, string? Title, string? Text, IReadOnlyList<string>? Why);
