namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>Where 'rule_catalog' reads rule records from, resolved once at start: AW_SINDRI_RULES_DIR, else the checkout's 'rules/' when serve runs inside an agentic-workflow checkout, else the installed toolkit's.</summary>
internal static class RuleFolders
{
    /// <summary>The folder.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="currentDirectory">Serve's working directory.</param>
    /// <returns>The folder; it may not exist (the catalog is then empty).</returns>
    internal static string Resolve(Func<string, string?> environment, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment("AW_SINDRI_RULES_DIR") is { Length: > 0 } explicitDir)
        {
            return explicitDir;
        }

        // why: skills/_shared is how an agentic-workflow checkout is told apart from any other repo (the same test SkillFolders makes)
        if (Directory.Exists(Path.Combine(currentDirectory, "skills", "_shared")))
        {
            return Path.Combine(currentDirectory, "rules");
        }

        var toolkit = environment("AW_TOOLKIT") is { Length: > 0 } set ? set : Path.Combine(environment("HOME") ?? string.Empty, ".agentic-workflow", "toolkit");
        return Path.Combine(toolkit, "rules");
    }
}
