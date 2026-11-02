using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The launch request built with no pick, the files the pick question shows, and how to build the same request on a picked model.</summary>
/// <param name="Request">The request with the model the Step names, or none.</param>
/// <param name="Files">The pack's files and the written facts.</param>
/// <param name="WithModel">The request again, on the given model.</param>
internal sealed record PreparedLaunch(LaunchRequest Request, IReadOnlyList<PickFileText> Files, Func<string?, LaunchRequest> WithModel);
