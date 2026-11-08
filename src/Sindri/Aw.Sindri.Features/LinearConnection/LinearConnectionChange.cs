namespace Aw.Sindri.Features.LinearConnection;

/// <summary>The profile keys one Linear connection click wrote, so the approval can name exactly them: nothing else that waits in the live file rides along.</summary>
/// <param name="Token">The read key's pointer, when this click wrote it.</param>
/// <param name="Teams">The ticked teams, when this click wrote them (with the read key).</param>
/// <param name="WriteToken">The write key's pointer, when this click wrote it.</param>
/// <param name="WriteTokenRemoved">True when this click removed the write key's pointer.</param>
internal sealed record LinearConnectionChange(string? Token = null, IReadOnlyList<string>? Teams = null, string? WriteToken = null, bool WriteTokenRemoved = false)
{
    /// <summary>The read key save: its pointer and the ticked teams.</summary>
    /// <param name="pointer">The read key's pointer.</param>
    /// <param name="teams">The ticked teams.</param>
    /// <returns>The change.</returns>
    public static LinearConnectionChange ReadSaved(string pointer, IReadOnlyList<string> teams) => new(Token: pointer, Teams: teams);

    /// <summary>The write key save: its pointer.</summary>
    /// <param name="pointer">The write key's pointer.</param>
    /// <returns>The change.</returns>
    public static LinearConnectionChange WriteSaved(string pointer) => new(WriteToken: pointer);

    /// <summary>The write key removal.</summary>
    /// <returns>The change.</returns>
    public static LinearConnectionChange WriteRemoved() => new(WriteTokenRemoved: true);
}
