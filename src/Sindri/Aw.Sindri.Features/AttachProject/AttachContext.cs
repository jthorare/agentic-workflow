using Aw.Sindri.Core.Projects;

namespace Aw.Sindri.Features.AttachProject;

/// <summary>What <see cref="AttachProjectLookup"/> found for an attach.</summary>
/// <param name="Key">The stored Linear project's key; null for a hand-made project.</param>
/// <param name="Led">Whether the owner leads that project, so it is on the page already.</param>
/// <param name="Attached">The projects attached now.</param>
internal sealed record AttachContext(string? Key, bool Led, IReadOnlyList<ProjectAttachedRow> Attached);
