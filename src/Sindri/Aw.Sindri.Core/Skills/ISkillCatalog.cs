namespace Aw.Sindri.Core.Skills;

/// <summary>The skill blocks serve loaded at start and the folder they came from (skill flows spec §3.2); the Host's routes, verbs and doctor rows read through it.</summary>
public interface ISkillCatalog
{
    /// <summary>The skills folder the loaded set came from.</summary>
    public string Folder { get; }

    /// <summary>The blocks that loaded and the ones that did not.</summary>
    public SkillBlocks Blocks { get; }

    /// <summary>Reads another folder for one trial ('skill run --skills'); the loaded set is unchanged.</summary>
    /// <param name="directory">The folder.</param>
    /// <returns>Its blocks.</returns>
    public SkillBlocks LoadFolder(string directory);
}
