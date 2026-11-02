namespace Aw.Sindri.Core.Doctor;

/// <summary>The one rule behind 'stale build' (#1032): the running control plane is stale when it and the install both name a build and the names differ.</summary>
public static class StaleBuild
{
    /// <summary>The text of the fix, for status and doctor alike.</summary>
    public const string Fix = "sindri stop && sindri";

    /// <summary>Whether the running build is not the installed one.</summary>
    /// <param name="running">The stamp this process runs from, or null when it isn't an installed build.</param>
    /// <param name="installed">The stamp 'bin/cs/current' names, or null when there is none.</param>
    /// <returns>True only when both are known and differ; an unknown stamp is no finding.</returns>
    public static bool Is(string? running, string? installed) =>
        running is not null && installed is not null && !string.Equals(running, installed, StringComparison.Ordinal);

    /// <summary>The sentence for a stale build.</summary>
    /// <param name="running">The running stamp.</param>
    /// <param name="installed">The installed stamp.</param>
    /// <returns>"stale build: serving A, installed B; fix: sindri stop &amp;&amp; sindri".</returns>
    public static string Sentence(string running, string installed) => $"stale build: serving {running}, installed {installed}; fix: {Fix}";
}
