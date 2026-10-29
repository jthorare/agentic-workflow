namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>Why a step failed: the catalogue code (for a bug report) and one plain sentence.</summary>
/// <param name="Code">The error code, such as SND-SETUP-004.</param>
/// <param name="Sentence">What went wrong in plain words.</param>
internal sealed record SetupErrorView(string Code, string Sentence);
