namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'exec-template' viewer's settings from launcher.json's 'viewer' section.</summary>
/// <param name="Name">The label the UI shows ("Sent to kitty").</param>
/// <param name="Argv">The template: closed placeholders, '{cmd...}' spliced as the attach argv.</param>
internal sealed record ExecTemplateOptions(string Name, IReadOnlyList<string> Argv)
{
    /// <summary>Variables added to the viewer process (tests point it at a private tmux directory); TMUX is always removed.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Reads an environment variable of this process (the GUI-session check); the real environment when null.</summary>
    public Func<string, string?>? GetEnv { get; init; }
}
