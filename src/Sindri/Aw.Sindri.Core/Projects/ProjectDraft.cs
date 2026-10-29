using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Projects;

/// <summary>What the owner typed to attach a Linear project or add one by hand (projects page spec §5.3).</summary>
/// <param name="Linear">The Linear project ('linear:project/&lt;id&gt;'), or null for a hand-made project.</param>
/// <param name="Name">The name; required when <paramref name="Linear"/> is null.</param>
/// <param name="State">The owner's state, or null.</param>
/// <param name="Links">The links, or null for none.</param>
/// <param name="Note">The note, or null.</param>
/// <param name="Target">The target date, or null.</param>
public sealed record ProjectDraft(string? Linear = null, string? Name = null, ProjectState? State = null, IReadOnlyList<string>? Links = null, string? Note = null, DateOnly? Target = null);
