namespace Aw.Sindri.Features.SetProfileKey;

/// <summary>Set one scalar key of the live profile ('sindri profile set', #1740); it checks the edited profile before writing and never approves.</summary>
/// <param name="Key">The dotted key, such as `context.models.labeling.pick`.</param>
/// <param name="Value">The value as typed.</param>
public sealed record SetProfileKeyRequest(string Key, string Value);
