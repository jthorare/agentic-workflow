using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetHomeRow;

/// <summary>One Home row with its blocks list.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Row">The row.</param>
public sealed record GetHomeRowResponse(AsOf AsOf, HomeRow Row);
