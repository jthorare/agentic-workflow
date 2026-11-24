using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListHomeDone;

/// <summary>The Done list (GET /sindri/home/done, 'sindri home done-list'; home spec §5.4, §8): request class. It names nothing; the list is the last keepDays of done rows.</summary>
public sealed record ListHomeDoneRequest : IQuery<ListHomeDoneResponse>;
