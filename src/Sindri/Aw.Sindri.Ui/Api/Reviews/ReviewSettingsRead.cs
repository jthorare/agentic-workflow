namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>
/// GET /sindri/reviews/settings?project= (review page spec §7, Ph9; PROPOSED contract, api may rename): one project's review settings as written, and as they resolve.
/// All the Ph9 mirror records are the files named <c language="text">ReviewSettings*</c> here, so a rename on the API side touches only them and the fake's goldens.
/// </summary>
/// <param name="Project">The project's id (prj_… or linear:project/…).</param>
/// <param name="Repos">The repos (owner/name) whose PRs the project's settings apply to.</param>
/// <param name="Settings">The settings entry's name; "default" while the project names none.</param>
/// <param name="SharedWith">The other projects that use the same entry.</param>
/// <param name="Entry">The entry as written in the profile (only the keys the owner set).</param>
/// <param name="Resolved">What a PR gets: every key with <c language="text">extends</c> and the built-in default applied.</param>
/// <param name="AllPanels">Every panel name there is, for the order and visibility editor.</param>
internal sealed record ReviewSettingsRead(
    string Project,
    IReadOnlyList<string> Repos,
    string Settings,
    IReadOnlyList<string> SharedWith,
    ReviewSettingsEntry Entry,
    ReviewSettingsResolved Resolved,
    IReadOnlyList<string> AllPanels);
