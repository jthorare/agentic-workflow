using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Library;

/// <summary>What the library feed reads: the checked roots and the synced GitHub repos a doc's <c language="text">repos:</c> can name.</summary>
/// <param name="Roots">The roots, from <see cref="LibraryPaths.Resolve"/>.</param>
/// <param name="GithubRepos">The <c language="text">owner/name</c> entries of 'context.sources.github.repos'.</param>
public sealed record LibrarySetup(IReadOnlyList<LibraryRoot> Roots, IReadOnlyList<string> GithubRepos);
