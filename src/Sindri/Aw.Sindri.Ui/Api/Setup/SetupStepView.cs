namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>
/// One step of the setup list. Id and State are read as strings, so a step or state this build doesn't know renders as itself instead of failing the read. Every
/// text but <see cref="Id"/>, <see cref="State"/> and <see cref="Action"/> is the API's plain words.
/// </summary>
/// <param name="Id">repo, profile, recipes, mcp, map or onyx.</param>
/// <param name="Title">What the step is, in a few words ("Add your repo").</param>
/// <param name="State">todo, running, done, failed, needs-you or skipped.</param>
/// <param name="Why">One sentence on why the step is in this state.</param>
/// <param name="Detail">What it did or is doing, or null.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="Action">The catalog action that moves it forward, or null when it runs on its own or waits on another step.</param>
/// <param name="Progress">How far a counted step is, or null.</param>
/// <param name="Items">One line per repo for a step that counts repos; null or empty otherwise (an older Sindri sends none).</param>
/// <param name="Preview">What the profile allows in plain words (the profile step only); null otherwise, and for an older Sindri.</param>
internal sealed record SetupStepView(
    string Id,
    string Title,
    string State,
    string Why,
    string? Detail,
    SetupErrorView? Error,
    string? Action,
    SetupProgressView? Progress,
    IReadOnlyList<SetupItemView>? Items = null,
    IReadOnlyList<SetupPreviewLineView>? Preview = null);
