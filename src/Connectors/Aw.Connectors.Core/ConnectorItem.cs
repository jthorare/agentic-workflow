namespace Aw.Connectors.Core;

/// <summary>One record a connector read, neutral on purpose: the Host maps it onto Context's record batch (owned index spec §5.2).</summary>
/// <param name="Key">The record key, such as 'github:pr/acme/widgets/12'.</param>
/// <param name="Kind">'issue', 'pr', 'message' and so on.</param>
/// <param name="Title">One line, cleaned.</param>
/// <param name="Text">The full text, cleaned (the title first).</param>
/// <param name="Author">The author's login, or null.</param>
/// <param name="Url">The permalink (https), or null when the source gave none that is allowed.</param>
/// <param name="State">'open' or 'closed' (more words as connectors learn them).</param>
/// <param name="StateRaw">The source's own word for the state.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="ClosedAt">When it was closed, or null.</param>
/// <param name="Version">The source's version of the record (its change time in round-trip form); the store replays a page by comparing it.</param>
/// <param name="Links">References to other records.</param>
/// <param name="Repo">The profile repo it belongs to, or null.</param>
public sealed record ConnectorItem(
    string Key,
    string Kind,
    string Title,
    string Text,
    string? Author,
    string? Url,
    string State,
    string StateRaw,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    string Version,
    IReadOnlyList<ConnectorLink> Links,
    string? Repo);
