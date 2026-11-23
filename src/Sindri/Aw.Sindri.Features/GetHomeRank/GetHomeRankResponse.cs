using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetHomeRank;

/// <summary>A row's stored rank record and the objects behind its blocks count: the explain data (home spec §4.2, §9).</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Id">The row.</param>
/// <param name="Kind">Its kind, which sets the reason weight.</param>
/// <param name="Since">When it started to need the owner: the age the order uses after blocks and weight.</param>
/// <param name="Rank">The rank record: blocks, weight, age rank and position.</param>
/// <param name="BlocksList">The objects behind the blocks count, each with the edge that reaches it; empty for a session reading a row of no repo.</param>
public sealed record GetHomeRankResponse(AsOf AsOf, HomeRowId Id, HomeKind Kind, DateTimeOffset Since, HomeRank Rank, IReadOnlyList<HomeBlocked> BlocksList);
