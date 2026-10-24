namespace Aw.Context.Core.Map;

/// <summary>What the purge removes (spec §6.2, Phase 3): exports after 14 days, retained versions beyond the newest 100.</summary>
public static class MapRetention
{
    /// <summary>How long an export is kept.</summary>
    public static TimeSpan ExportLifetime { get; } = TimeSpan.FromDays(14);

    /// <summary>How many retained versions are kept.</summary>
    public const int RetainedVersions = 100;

    /// <summary>The exports past their lifetime.</summary>
    /// <param name="files">The exports folder's files.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The expired files.</returns>
    public static IReadOnlyList<ExportedFile> ExpiredExports(IReadOnlyList<ExportedFile> files, DateTimeOffset now) =>
        [.. files.Where(f => now - f.Written > ExportLifetime)];

    /// <summary>How far back a backtest can read a map version: the eval cache and report lifetime.</summary>
    public static TimeSpan EvalWindow { get; } = TimeSpan.FromDays(90);

    /// <summary>
    /// The retained versions that nothing needs: beyond the newest <see cref="RetainedVersions"/> and not read by an as-of lookup
    /// inside <see cref="EvalWindow"/>. A version is read for the dates from its own creation until its successor's, so one
    /// stays while its successor was created within the window.
    /// </summary>
    /// <param name="versions">Every version.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The versions to delete.</returns>
    public static IReadOnlyList<int> ExcessVersions(IReadOnlyList<MapVersion> versions, DateTimeOffset now)
    {
        var readable = versions.Where(v => v.Status is MapVersionStatus.Current or MapVersionStatus.Retained).OrderBy(v => v.Number).ToList();
        var windowStart = now - EvalWindow;
        var surplus = readable.Where(v => v.Status == MapVersionStatus.Retained).OrderByDescending(v => v.Number).Skip(RetainedVersions).Select(v => v.Number).ToHashSet();
        return
        [
            .. readable.Select((v, i) => (Version: v, Next: i + 1 < readable.Count ? readable[i + 1] : null))
                .Where(x => surplus.Contains(x.Version.Number) && x.Next is not null && x.Next.Created < windowStart)
                .Select(x => x.Version.Number),
        ];
    }
}
