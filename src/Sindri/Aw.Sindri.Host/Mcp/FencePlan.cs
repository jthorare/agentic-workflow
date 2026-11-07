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

    /// <summary>GET /sindri/projects/linear for a session: each row's Linear name and lead's name.</summary>
    internal static FencePlan LinearProjects { get; } = new("sindri:project", [new("projects[].name"), new("projects[].lead")]);

    /// <summary>GET /sindri/reviews for a session: each PR's forge-sourced title and author, and each unreadable library folder with its reason.</summary>
    internal static FencePlan Reviews { get; } = new("sindri:review",
    [
        new("groups[].sections[].rows[].title"), new("groups[].sections[].rows[].author"),
        new("library.unreadable[].root"), new("library.unreadable[].reason"),
    ]);

    /// <summary>GET /sindri/library for a session: each broken doc's id, file and findings, and each near miss's file and text (they name the owner's folders and the docs' own words; a broken doc's id is whatever its front matter says).</summary>
    internal static FencePlan Library { get; } = new("sindri:library",
    [
        new("broken[].doc"), new("broken[].path"), new("broken[].errors[].text"),
        new("nearMisses[].path"), new("nearMisses[].text"),
    ]);

    /// <summary>GET /sindri/library/{doc} for a session: every front-matter string, the title, the file path and what its errors say (all of it text from a file somebody wrote; a broken doc keeps a field of the wrong shape as written).</summary>
    internal static FencePlan LibraryDoc { get; } = new("sindri:library",
    [
        new("doc.id"), new("doc.kind"), new("doc.status"), new("doc.project"), new("doc.repos[]"), new("doc.issues[]"),
        new("doc.title"), new("doc.path"), new("doc.errors[].text"),
    ]);

    /// <summary>GET /sindri/linear/issues for a session: the viewer's name and each row's title, then the Linear-typed names nested in the row (state, assignee, project, cycle, labels), which sit inside a row the title already marks.</summary>
    internal static FencePlan LinearIssues { get; } = new("sindri:linear",
    [
        new("viewer.name"), new("rows[].title"),
        new("rows[].state.name", Mark: false), new("rows[].assignee.name", Mark: false), new("rows[].project.name", Mark: false),
        new("rows[].cycle.name", Mark: false), new("rows[].labels[].name", Mark: false),
    ]);

    /// <summary>GET /sindri/linear/issues/{key} for a session: the list row's text, the viewer's name, the description, and each comment's body and author name. The comments and the root carry the trust member; the names nested in the row do not (the golden's form, as in the list).</summary>
    internal static FencePlan LinearIssue { get; } = new("sindri:linear",
    [
        new("viewer.name"), new("title"), new("description"),
        new("state.name", Mark: false), new("assignee.name", Mark: false), new("project.name", Mark: false), new("cycle.name", Mark: false), new("labels[].name", Mark: false),
        new("comments[].author.name"), new("comments[].body"),
    ]);
}
