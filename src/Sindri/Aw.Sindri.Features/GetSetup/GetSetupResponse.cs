using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>The setup list.</summary>
/// <param name="Steps">The six steps in their fixed order: repo, profile, recipes, mcp, map, sources.</param>
/// <param name="AsOf">How fresh the views are (API spec §2.2).</param>
public sealed record GetSetupResponse(IReadOnlyList<SetupStep> Steps, AsOf AsOf);
