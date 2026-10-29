namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>One parked Step a resume would start (wire contract item 4), in the order and cap of the server's readyToResume.</summary>
/// <param name="Instance">The workflow instance address; what the answer-resume command takes.</param>
/// <param name="Item">The item it belongs to, or null when the server can't read it.</param>
/// <param name="Title">The item's title, or null when the items view is down, rebuilding or has no row for it.</param>
/// <param name="Since">When it began waiting.</param>
internal sealed record SandboxResumeRowView(string Instance, string? Item, string? Title, DateTimeOffset Since);
