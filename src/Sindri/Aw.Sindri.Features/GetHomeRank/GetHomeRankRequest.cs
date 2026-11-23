using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetHomeRank;

/// <summary>Why a Home row sits where it does (GET /sindri/home/rank/{id}, 'sindri home rank --explain'; home spec §4, §8): request class.</summary>
/// <param name="Id">The row.</param>
public sealed record GetHomeRankRequest(HomeRowId Id) : IQuery<GetHomeRankResponse>;
