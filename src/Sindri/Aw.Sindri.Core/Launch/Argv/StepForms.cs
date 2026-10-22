using System.Globalization;
using System.Text;
using Aw.Kernel;

namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>
/// The arguments an unattended Step adds after the expanded open template (launcher L-17): the model id through the entry's <see cref="ModelForm"/> and the
/// policy through its <see cref="PolicyForm"/>. Pure; the 'claude-settings-file' file is written by the launcher, and this only names its path.
/// </summary>
public static class StepForms
{
    /// <summary>The form arguments for <paramref name="step"/>, in order: the model's, then the policy's.</summary>
    /// <param name="entry">The catalog entry.</param>
    /// <param name="step">What the Step asks for.</param>
    /// <param name="policyFilePath">Where the launcher writes the settings file, for the 'claude-settings-file' form; absolute.</param>
    /// <returns>The arguments (none when the Step asks for neither), SND-LAUNCH-013 when the entry's form can't express what the Step asks for and nothing may start, SND-REQ-001 for an unusable value.</returns>
    public static Result<IReadOnlyList<string>> Arguments(AgentEntry entry, StepArgs step, string? policyFilePath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(step);
        var model = step.Model is null ? Results.Success<IReadOnlyList<string>>([]) : Model(entry, step.Model);
        if (model.IsError)
        {
            return model.Error;
        }

        var policy = step.Policy is null ? Results.Success<IReadOnlyList<string>>([]) : Policy(entry, step.Policy, policyFilePath);
        return policy.IsError ? policy.Error : Results.Success<IReadOnlyList<string>>([.. model.Value, .. policy.Value]);
    }

    private static Result<IReadOnlyList<string>> Model(AgentEntry entry, string model)
    {
        if (model.Length == 0 || model.StartsWith('-'))
        {
            return SindriErrors.RequestInvalid.With("the Step's model id is empty or starts with '-' (fix: pass the model's id)");
        }

        return entry.ModelForm switch
        {
            ModelForm.ClaudeFlag => Results.Success<IReadOnlyList<string>>(["--model", model]),
            ModelForm.CodexFlag => Results.Success<IReadOnlyList<string>>(["-m", model]),
            ModelForm.CursorFlag => Unsupported(entry, "model", "cursor-flag is reserved until it is verified"),
            ModelForm.None => Unsupported(entry, "model", "its model form is none"),
            _ => Unsupported(entry, "model", "its model form is unknown"),
        };
    }

    private static Result<IReadOnlyList<string>> Policy(AgentEntry entry, StepPolicy policy, string? settingsPath)
    {
        if (policy.WritablePath is { } writable && !Placeholders.IsAbsolute(writable))
        {
            return SindriErrors.RequestInvalid.With("the Step's writable path isn't absolute (fix: pass the Step directory's absolute path)");
        }

        if (entry.PolicyForm != PolicyForm.ClaudeSettingsFile && (policy.ReadOnlyPaths is { Count: > 0 } || policy.DeniedPaths is { Count: > 0 }))
        {
            return Unsupported(entry, "policy", "its policy form can't deny a read or keep a path read-only (only claude-settings-file can)");
        }

        return entry.PolicyForm switch
        {
            PolicyForm.ClaudeSettingsFile => settingsPath is { } path && Placeholders.IsAbsolute(path)
                ? Results.Success<IReadOnlyList<string>>(["--settings", path])
                : SindriErrors.RequestInvalid.With("a claude-settings-file policy needs the settings file's absolute path"),
            PolicyForm.CodexOverrides => policy.DeniedPaths is { Count: > 0 }
                ? Unsupported(entry, "policy", "codex-overrides can't deny reads of paths")
                : Results.Success(CodexOverrides(policy)),
            PolicyForm.CursorConfig => Unsupported(entry, "policy", "cursor-config is reserved until it is verified"),
            PolicyForm.None => Unsupported(entry, "policy", "its policy form is none"),
            _ => Unsupported(entry, "policy", "its policy form is unknown"),
        };
    }

    /// <summary>The typed '-c key=value' overrides for codex. ReadOnly allows writes to the one path only; Plan allows none; Default leaves codex's own mode.</summary>
    private static IReadOnlyList<string> CodexOverrides(StepPolicy policy) => policy.Mode switch
    {
        StepPermissionMode.Plan => ["-c", "sandbox_mode=\"read-only\""],
        StepPermissionMode.ReadOnly when policy.WritablePath is { } path =>
            ["-c", "sandbox_mode=\"workspace-write\"", "-c", $"sandbox_workspace_write.writable_roots=[{Toml(path)}]"],
        StepPermissionMode.ReadOnly => ["-c", "sandbox_mode=\"read-only\""],
        StepPermissionMode.Default => [],
        _ => [],
    };

    private static string Toml(string value)
    {
        var text = new StringBuilder("\"");
        foreach (var c in value)
        {
            _ = c switch
            {
                '"' => text.Append("\\\""),
                '\\' => text.Append("\\\\"),
                < ' ' => text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)),
                _ => text.Append(c),
            };
        }

        return text.Append('"').ToString();
    }

    private static Error Unsupported(AgentEntry entry, string what, string why) =>
        SindriErrors.LaunchFormUnsupported.With($"{entry.Id.Value} can't take the Step's {what}: {why} (fix: choose an entry whose {what} form is supported)");
}
