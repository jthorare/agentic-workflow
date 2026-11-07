namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>Where serve reads skill manifests from (skill flows spec §3.2), resolved once at start: AW_SINDRI_SKILLS_DIR, else the checkout's 'skills/' when serve runs inside an agentic-workflow checkout, else the installed toolkit's.</summary>
internal static class SkillFolders
{
    /// <summary>The folder.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="currentDirectory">Serve's working directory.</param>
    /// <returns>The folder; it may not exist (the loader then reports 'no skills folder').</returns>
    internal static string Resolve(Func<string, string?> environment, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment("AW_SINDRI_SKILLS_DIR") is { Length: > 0 } explicitDir)
        {
            return explicitDir;
        }

        var checkout = Path.Combine(currentDirectory, "skills");
        if (Directory.Exists(Path.Combine(checkout, "_shared")))
        {
            return checkout;   // why: skills/_shared is how an agentic-workflow checkout is told apart from any other repo with a skills folder
        }

        var toolkit = environment("AW_TOOLKIT") is { Length: > 0 } set ? set : Path.Combine(environment("HOME") ?? string.Empty, ".agentic-workflow", "toolkit");
        return Path.Combine(toolkit, "skills");
    }
}
