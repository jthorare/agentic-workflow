namespace Aw.Sindri.Host.Commands;

/// <summary>A repo 'setup --apply' couldn't add.</summary>
/// <param name="Path">The repo's path.</param>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error's message.</param>
internal sealed record SetupApplyFailure(string Path, string Code, string Message);
