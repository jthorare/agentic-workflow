namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>PUT /sindri/reviews/settings's body (Ph9, proposed): writes the project's review.projects row and the named entry in the live profile.</summary>
/// <param name="Project">The project's id.</param>
/// <param name="Repos">The project's repos.</param>
/// <param name="Settings">The entry's name.</param>
/// <param name="Entry">The entry: only the keys the owner set or changed.</param>
/// <param name="Approve">True to approve the profile change in the same click (it takes effect only when nothing else waits).</param>
internal sealed record ReviewSettingsSave(string Project, IReadOnlyList<string> Repos, string Settings, ReviewSettingsEntry Entry, bool Approve);
