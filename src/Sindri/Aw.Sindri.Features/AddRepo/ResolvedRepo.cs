using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.AddRepo;

/// <summary>What the admission worked out for a start: the repo's name, its main checkout and what git said of it.</summary>
/// <param name="Name">The name the repo gets in the profile.</param>
/// <param name="Inspected">The checkout and its git facts.</param>
internal sealed record ResolvedRepo(RepoName Name, InspectedRepo Inspected);
