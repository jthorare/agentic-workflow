using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.SuggestLinks;

/// <summary>The links Sindri suggests (Linear hub spec 2026-11-04 §6.1, TP-8; query): an issue key a stored pull request names, for an issue no open item holds. Nothing is applied until the owner links it.</summary>
/// <param name="Repo">Only pull requests of this repo ('owner/name'), or null for all.</param>
internal sealed record SuggestLinksRequest(string? Repo = null) : IQuery<SuggestLinksResponse>;
