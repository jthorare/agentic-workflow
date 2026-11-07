namespace Aw.Sindri.Core.Skills;

/// <summary>The result file as the reader found it (skill flows spec §3.3).</summary>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="Modified">When it was last written.</param>
public sealed record SkillResultFile(ReadOnlyMemory<byte> Bytes, DateTimeOffset Modified);
