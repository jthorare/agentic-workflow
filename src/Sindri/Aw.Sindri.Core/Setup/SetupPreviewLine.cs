namespace Aw.Sindri.Core.Setup;

/// <summary>One plain-words line of what a profile allows, for the profile step's preview.</summary>
/// <param name="Kind">repo, service, allows or limit.</param>
/// <param name="Text">The sentence.</param>
public sealed record SetupPreviewLine(string Kind, string Text);
