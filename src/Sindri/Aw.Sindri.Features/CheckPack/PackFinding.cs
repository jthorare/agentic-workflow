namespace Aw.Sindri.Features.CheckPack;

/// <summary>One problem in a pack.</summary>
/// <param name="Code">The error code (SND-PACK-003).</param>
/// <param name="Message">What is wrong and how to fix it.</param>
public sealed record PackFinding(string Code, string Message);
