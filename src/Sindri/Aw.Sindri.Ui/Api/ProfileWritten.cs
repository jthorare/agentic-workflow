namespace Aw.Sindri.Ui.Api;

/// <summary>What <see cref="ProfileWriteFlow.Run"/> did.</summary>
/// <param name="State">How it ended.</param>
/// <param name="Hash">The profile hash the write job returned, or null when none was written or read.</param>
/// <param name="Code">The error id when refused or failed, else null.</param>
/// <param name="Message">The server's sentence when refused or failed, else null.</param>
internal sealed record ProfileWritten(ProfileWriteState State, string? Hash = null, string? Code = null, string? Message = null);
