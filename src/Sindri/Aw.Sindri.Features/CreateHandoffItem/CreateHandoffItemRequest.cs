using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CreateHandoffItem;

/// <summary>Make an item from a handoff (POST /sindri/handoff, 'sindri handoff send', the sindri_handoff tool; #2258 PR 12): desk class, appends 'item.created' then 'item.handoff'.</summary>
/// <param name="Title">The item's title: 1 to 200 characters after trimming.</param>
/// <param name="Source">Who sends it: 1 to 32 characters of a-z, 0-9, '.', '_' and '-'. The envelope's actor is 'handoff:' and this.</param>
/// <param name="Offer">The sender's id for the offer, or null: the item id then stands in.</param>
/// <param name="Repo">The repo the work is for; null takes the caller's X-Sindri-Repo.</param>
/// <param name="Branch">The branch it names, or null.</param>
/// <param name="Prompt">The full prompt, or null: kept whole in 'item.handoff'; the item body keeps its first 4,000 characters.</param>
/// <param name="Links">Links, or null: at most 50, each at most 2,048 characters.</param>
/// <param name="Labels">Labels, or null: at most 20, each at most 64 characters.</param>
public sealed record CreateHandoffItemRequest(
    string Title,
    string Source,
    string? Offer = null,
    RepoName? Repo = null,
    string? Branch = null,
    string? Prompt = null,
    IReadOnlyList<string>? Links = null,
    IReadOnlyList<string>? Labels = null) : ICommand<HandoffItemCreatedResponse>;
