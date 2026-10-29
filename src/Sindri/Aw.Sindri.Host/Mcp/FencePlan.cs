namespace Aw.Sindri.Host.Mcp;

/// <summary>The free-text fields of one tool's result and where the text comes from (context spec §7, API spec §8).</summary>
/// <param name="Source">The fence's source id.</param>
/// <param name="Fields">The fields to fence.</param>
internal sealed record FencePlan(string Source, IReadOnlyList<FenceField> Fields)
{
    /// <summary>sindri_queue: the item titles.</summary>
    internal static FencePlan Queue { get; } = new("sindri:item", [new("groups[].items[].title")]);

    /// <summary>sindri_item: the title and body, by whoever created the item.</summary>
    internal static FencePlan Item { get; } = new("sindri:item", [new("title", "createdBy"), new("body", "createdBy")]);

    /// <summary>sindri_item_timeline: each line, by the entry's actor.</summary>
    internal static FencePlan Timeline { get; } = new("sindri:timeline", [new("entries[].line", "actor")]);

    /// <summary>sindri_pending_decisions: the question (and what a request carried), the note, the notice and the cleared text.</summary>
    internal static FencePlan Decisions { get; } = new("sindri:decision",
    [
        new("decisions[].question", "filedBy"), new("decisions[].note", "decidedBy"),
        new("decisions[].notice"), new("decisions[].cleared"),
    ]);

    /// <summary>sindri_job: the summary, each progress line and every string inside the result (a service's result can carry text an agent wrote).</summary>
    internal static FencePlan Job { get; } = new("sindri:job", [new("summary", "startedBy"), new("progress[].line"), new("result", "startedBy", EveryString: true)]);

    /// <summary>sindri_projects: the Linear-sourced text of every card (name, description, lead's name) in the three lists, and the viewer's name; the owner's notes (free text a session reads); the owner's links stay plain.</summary>
    internal static FencePlan Projects { get; } = new("sindri:project",
    [
        new("viewer"),
        new("projects[].name"), new("projects[].description"), new("projects[].lead.name"), new("projects[].notes"),
        new("finished[].name"), new("finished[].description"), new("finished[].lead.name"), new("finished[].notes"),
        new("hidden[].name"), new("hidden[].description"), new("hidden[].lead.name"), new("hidden[].notes"),
    ]);

    /// <summary>sindri_projects for one project: the card's Linear-sourced text, the owner's notes and the viewer's name.</summary>
    internal static FencePlan OneProject { get; } = new("sindri:project", [new("viewer"), new("project.name"), new("project.description"), new("project.lead.name"), new("project.notes")]);
}
