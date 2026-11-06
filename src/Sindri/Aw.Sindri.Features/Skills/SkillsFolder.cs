namespace Aw.Sindri.Features.Skills;

/// <summary>The skills folder serve loads blocks from, resolved once at start (skill flows spec §3.2).</summary>
/// <param name="Path">The folder that holds '&lt;skill&gt;/block.yaml'.</param>
internal sealed record SkillsFolder(string Path)
{
    /// <summary>AW_SINDRI_SKILLS_DIR, not AW_SKILLS_DIR, which setup.sh already uses for '~/.claude/skills'.</summary>
    internal const string Variable = "AW_SINDRI_SKILLS_DIR";

    /// <summary>The folder, in order: <paramref name="explicitDirectory"/> (the in-process verbs' --skills flag), AW_SINDRI_SKILLS_DIR, the 'skills/' of an agentic-workflow checkout at or above <paramref name="currentDirectory"/>, else the installed toolkit's.</summary>
    /// <param name="explicitDirectory">The folder an in-process verb was given, or null.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="currentDirectory">The working directory.</param>
    /// <returns>The folder; it may not exist (the loader then reports 'no skills folder').</returns>
    internal static SkillsFolder Resolve(string? explicitDirectory, Func<string, string?> environment, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!string.IsNullOrWhiteSpace(explicitDirectory))
        {
            return new SkillsFolder(System.IO.Path.GetFullPath(explicitDirectory, currentDirectory));
        }

        var fromEnvironment = environment(Variable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new SkillsFolder(System.IO.Path.GetFullPath(fromEnvironment, currentDirectory));
        }

        for (var directory = new DirectoryInfo(currentDirectory); directory is not null; directory = directory.Parent)
        {
            // why: the shared capability map is the file only this toolkit's skills folder has
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "skills", "_shared", "capabilities.md")))
            {
                return new SkillsFolder(System.IO.Path.Combine(directory.FullName, "skills"));
            }
        }

        var toolkit = environment("AW_TOOLKIT");
        if (string.IsNullOrWhiteSpace(toolkit))
        {
            toolkit = System.IO.Path.Combine(environment("HOME") ?? string.Empty, ".agentic-workflow", "toolkit");
        }

        return new SkillsFolder(System.IO.Path.Combine(toolkit, "skills"));
    }
}
