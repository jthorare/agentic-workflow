namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>One open PR of a project, as the project's truth carries it.</summary>
/// <param name="Url">The PR's link on the forge.</param>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Title">The title; text from the forge, served fenced.</param>
/// <param name="State">What it waits on: "draft", "ready", "ciRed", "unknown" or "waiting"; a state this build doesn't know is kept as served.</param>
/// <param name="DecidedAt">For "ciRed", when Sindri decided the failing head's CI roll-up; else null.</param>
/// <param name="WaitingOn">The reviewers it waits on, comma separated; null unless "waiting".</param>
/// <param name="WaitsOnYou">True when the owner is among the reviewers it waits on (issue 3797); null from a server that doesn't serve it yet.</param>
internal sealed record ProjectTruthPrView(string Url, string Repo, int Number, string Title, string State, DateTimeOffset? DecidedAt, string? WaitingOn, bool? WaitsOnYou = null);
