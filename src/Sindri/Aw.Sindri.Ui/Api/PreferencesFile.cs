using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Api;

/// <summary>
/// preferences.json as written (UI spec §12.3): <see cref="UiPreferences"/> with every field but the version optional, so
/// a file from an older build of the same version, or one edited by hand, keeps what it has and takes defaults for the rest.
/// </summary>
/// <param name="Version">The format.</param>
/// <param name="Window">The window's bounds.</param>
/// <param name="SheetWidth">The sheet's width.</param>
/// <param name="LastPage">The last page.</param>
/// <param name="SeenIds">Seen needs-you row ids.</param>
/// <param name="PaletteHistory">Palette entries run.</param>
/// <param name="TextScale">The text scale.</param>
/// <param name="ReduceMotion">The in-app Reduce motion switch.</param>
/// <param name="SeenPages">Seen suggested page keys per concept.</param>
internal sealed record PreferencesFile(
    int Version,
    WindowBounds? Window = null,
    double? SheetWidth = null,
    string? LastPage = null,
    IReadOnlyList<string>? SeenIds = null,
    IReadOnlyList<string>? PaletteHistory = null,
    double? TextScale = null,
    bool? ReduceMotion = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? SeenPages = null);
