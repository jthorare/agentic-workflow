using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetHomeRow;

/// <summary>One Home row (GET /sindri/home/rows/{id}; home spec §8): request class.</summary>
/// <param name="Id">The row.</param>
public sealed record GetHomeRowRequest(HomeRowId Id) : IQuery<GetHomeRowResponse>;
