using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.GetLibraryDoc;

/// <summary>One design doc and what is wrong with it.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Doc">The doc.</param>
public sealed record GetLibraryDocResponse(AsOf AsOf, LibraryDocView Doc);
