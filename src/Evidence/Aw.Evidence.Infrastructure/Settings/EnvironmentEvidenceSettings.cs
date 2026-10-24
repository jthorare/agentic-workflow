using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;

namespace Aw.Evidence.Infrastructure.Settings;

/// <summary>
/// The deployment settings from the environment Composition passes (blank counts as unset): UI_EVIDENCE_USER_AGENT,
/// AW_VISUAL_MODEL (default 'judge-default'), UI_EVIDENCE_PREVIEW_HOST_PATTERN, UI_EVIDENCE_API_PORT (1-65535; anything
/// else is unset), UI_EVIDENCE_SEED_DOMAINS and UI_EVIDENCE_SEED_EMAIL_PATTERN, and
/// the UI_EVIDENCE_SLOT_* worktree slot convention (all of FILE, KEY, COUNT and PORT_STEP, else none), and
/// UI_EVIDENCE_APP_TITLE, else that name in the local NAME=value env file UI_EVIDENCE_CONFIG names.
/// </summary>
internal sealed partial class EnvironmentEvidenceSettings(Func<string, string?> environment) : IEvidenceSettings
{
    internal const string UserAgentVariable = "UI_EVIDENCE_USER_AGENT";
    internal const string ModelVariable = "AW_VISUAL_MODEL";
    internal const string DefaultModel = "judge-default";
    internal const string PreviewHostVariable = "UI_EVIDENCE_PREVIEW_HOST_PATTERN";
    internal const string ApiPortVariable = "UI_EVIDENCE_API_PORT";
    internal const string DomainsVariable = "UI_EVIDENCE_SEED_DOMAINS";
    internal const string PatternVariable = "UI_EVIDENCE_SEED_EMAIL_PATTERN";
    internal const string TitleVariable = "UI_EVIDENCE_APP_TITLE";
    internal const string ConfigVariable = "UI_EVIDENCE_CONFIG";
    internal const string SlotFileVariable = "UI_EVIDENCE_SLOT_FILE";
    internal const string SlotKeyVariable = "UI_EVIDENCE_SLOT_KEY";
    internal const string SlotCountVariable = "UI_EVIDENCE_SLOT_COUNT";
    internal const string SlotStepVariable = "UI_EVIDENCE_SLOT_PORT_STEP";
    internal const string SlotDatabaseVariable = "UI_EVIDENCE_SLOT_DATABASE";

    /// <inheritdoc/>
    public string? UserAgent => Set(UserAgentVariable);

    /// <inheritdoc/>
    public string VisualModel => Set(ModelVariable) ?? DefaultModel;

    /// <inheritdoc/>
    public string? PreviewHostPattern => Set(PreviewHostVariable)?.Trim();

    /// <inheritdoc/>
    public int? ApiPort =>
        Set(ApiPortVariable) is { } text && int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535
            ? port
            : null;

    /// <inheritdoc/>
    public SlotSettings? Slots =>
        Set(SlotFileVariable) is { } file
            && Set(SlotKeyVariable) is { } key && SlotKey.IsMatch(key)
            && PositiveInt(SlotCountVariable) is { } count && PositiveInt(SlotStepVariable) is { } step
            ? new SlotSettings(file, key, count, step, Set(SlotDatabaseVariable))
            : null;   // unset, or incomplete: doctor says so when a worktree is asked for

    /// <inheritdoc/>
    public SeedPolicy SeedPolicy => SeedPolicy.From(Set(DomainsVariable), Set(PatternVariable));

    /// <inheritdoc/>
    public string AppTitleSource => $"{TitleVariable} in the environment, or in the env file {ConfigVariable} names";

    /// <inheritdoc/>
    public async ValueTask<string?> AppTitleAsync(CancellationToken cancellationToken)
    {
        if (Set(TitleVariable) is { } title)
        {
            return title;
        }

        if (Set(ConfigVariable) is not { } path)
        {
            return null;
        }

        try
        {
            return LocalEnvFile.Find(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false), TitleVariable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;   // an unreadable env file is an unset title: doctor reports it
        }
    }

    private int? PositiveInt(string name) =>
        Set(name) is { } text && int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1 ? value : null;

    private string? Set(string name) => environment(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SlotKey { get; }
}
