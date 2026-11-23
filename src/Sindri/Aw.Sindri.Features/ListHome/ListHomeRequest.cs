using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Home;

namespace Aw.Sindri.Features.ListHome;

/// <summary>A page of Home (GET /sindri/home?split=&amp;after=&amp;limit=&amp;snoozed=, 'sindri home'; home spec §8): request class.</summary>
/// <param name="Split">One group (needs-you or moving), or null for both.</param>
/// <param name="Snoozed">True reads the snoozed rows instead of the open ones.</param>
/// <param name="After">The cursor of the previous page, or null for the first.</param>
/// <param name="Limit">The most rows to return; 0 or less is the default page of 60.</param>
public sealed record ListHomeRequest(HomeGroup? Split = null, bool Snoozed = false, string? After = null, int Limit = 0) : IQuery<ListHomeResponse>;
