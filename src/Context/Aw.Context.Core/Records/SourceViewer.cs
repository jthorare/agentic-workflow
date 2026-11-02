using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>The user a source's credential belongs to.</summary>
/// <param name="Id">The user's id in the source, such as Linear's UUID.</param>
/// <param name="Name">The user's display name.</param>
public sealed record SourceViewer(ExternalId Id, string Name);
