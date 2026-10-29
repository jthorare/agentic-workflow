namespace Aw.Sindri.Testing.Prepare;

/// <summary>The stored 'fact:lineContext' text a line's starter passes (recipes/sindri/workflow.line.yaml): one builder for the seed Step's tests and the prepare's, so renaming a key breaks both.</summary>
public static class LineContexts
{
    /// <summary>A line context for a phase.</summary>
    /// <param name="baseSha">The line's base commit.</param>
    /// <param name="line">The line name.</param>
    /// <param name="phase">The phase name.</param>
    /// <returns>The JSON text.</returns>
    public static string Of(string baseSha, string line = "L-1", string phase = "p2") =>
        $$$"""{"line":"{{{line}}}","phase":"{{{phase}}}","attempt":1,"base":"{{{baseSha}}}","runArea":{}}""";
}
