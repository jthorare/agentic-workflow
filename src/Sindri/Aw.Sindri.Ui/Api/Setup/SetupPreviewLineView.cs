namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>One plain-words line of what the profile allows.</summary>
/// <param name="Kind">repo, service, allows or limit.</param>
/// <param name="Text">The sentence.</param>
internal sealed record SetupPreviewLineView(string Kind, string Text);
