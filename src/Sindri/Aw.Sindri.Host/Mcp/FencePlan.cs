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

    /// <summary>GET /sindri/reviews/{owner}/{repo}/{number} for a session: the PR's forge text (title, author, branch, patches, thread bodies, run names) and the docs' own words (item titles, claim errors, test rows, evidence labels).</summary>
    internal static FencePlan ReviewPage { get; } = new("sindri:review",
    [
        new("pr.title"), new("pr.author"), new("pr.head.branch"),
        new("panels[].data.checks[].title"), new("panels[].data.checks[].detail"),
        new("panels[].data.claims[].error"), new("panels[].data.doc.title"), new("panels[].data.doc.path"), new("panels[].data.doc.items[].title"),
        new("panels[].data.projectDocs[].title"), new("panels[].data.projectDocs[].path"),
        new("panels[].data.rows[].text"), new("panels[].data.runs[].name"),
        new("panels[].data.items[].label"), new("panels[].data.hits[].path"), new("panels[].data.hits[].why"),
        new("panels[].data.threads[].path"), new("panels[].data.threads[].comments[].author"), new("panels[].data.threads[].comments[].body"),
        new("panels[].text"), new("diff.text"), new("diff.groups[].name"), new("diff.groups[].files[].path"), new("diff.groups[].files[].previousPath"),
        new("diff.groups[].files[].patch"), new("diff.groups[].files[].note"),
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
}
