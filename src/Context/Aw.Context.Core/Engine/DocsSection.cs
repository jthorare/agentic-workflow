using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.docs': the repo-docs globs and the file names that count as docs in any directory.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DocsSection(IReadOnlyList<string>? Globs = null, IReadOnlyList<string?>? DirectoryNames = null);
